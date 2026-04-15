/**
 * lib/signalr.ts
 * SignalR connection factory — returns managed singleton connections per hub.
 *
 * ⚠️  SINGLETON RULE: Connections are created ONCE per hub URL.
 * Never call HubConnectionBuilder inside a React component.
 * Use the exported `getHubConnection(hubName)` function from a Zustand store or
 * a module-level initialization hook only.
 */
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'
import { getToken } from './auth'
import { getApiBaseUrl, config } from './config'

export type HubName = 'conversation' | 'ingestion' | 'analysis'

// In-memory singleton registry — one connection per hub
const connections = new Map<HubName, HubConnection>()

/**
 * Returns the singleton HubConnection for `hubName`.
 * Creates it on first call. Never recreates it.
 *
 * ⚠️  Called only client-side (inside useEffect via useHub).
 * getApiBaseUrl() is safe here because window.__API_BASE_URL is already set
 * by the time any React effect runs.
 */
export function getHubConnection(hubName: HubName): HubConnection {
  if (connections.has(hubName)) {
    return connections.get(hubName)!
  }

  // Resolve URL at connection-creation time (always client-side, post-hydration)
  const baseUrl = getApiBaseUrl()
  const hubPath = config.signalrHubs[hubName]

  // When the API is on plain HTTP, WebSocket and SSE upgrades get redirected to
  // HTTPS by ASP.NET's UseHttpsRedirection middleware and fail silently.
  // Skip the noisy fallback attempts and go straight to LongPolling.
  const transport = baseUrl.startsWith('http://')
    ? HttpTransportType.LongPolling
    : undefined // auto-negotiate (WS preferred) on HTTPS

  const connection = new HubConnectionBuilder()
    .withUrl(`${baseUrl}${hubPath}`, {
      // accessTokenFactory is called on every request — always reads the latest token.
      // Return '' when no token: SignalR omits the Authorization header for empty strings
      // in @microsoft/signalr ≥ 8.x (unlike null which breaks the type).
      accessTokenFactory: () => getToken() ?? '',
      transport,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        const delays = [1000, 2000, 5000, 10000, 30000]
        return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)]
      },
    })
    .configureLogging(
      baseUrl.startsWith('http://') ? LogLevel.None : LogLevel.Warning,
    )
    .build()

  connections.set(hubName, connection)
  return connection
}

/**
 * Starts the hub connection if it is not already connected or connecting.
 * Safe to call multiple times.
 */
export async function startHub(hubName: HubName): Promise<void> {
  if (process.env.NEXT_PUBLIC_API_MOCK === 'true') return

  const connection = getHubConnection(hubName)
  if (
    connection.state === HubConnectionState.Disconnected ||
    connection.state === HubConnectionState.Disconnecting
  ) {
    await connection.start()
  }
}

/**
 * Stops and removes a hub connection from the singleton registry.
 * Use during logout or unmount of the root layout.
 */
export async function stopHub(hubName: HubName): Promise<void> {
  const connection = connections.get(hubName)
  if (connection) {
    await connection.stop()
    connections.delete(hubName)
  }
}

/**
 * Returns the current connection state for a hub (useful for UI indicators).
 */
export function getHubState(hubName: HubName): HubConnectionState | null {
  return connections.get(hubName)?.state ?? null
}
