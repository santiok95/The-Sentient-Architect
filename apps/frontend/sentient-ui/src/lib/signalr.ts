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

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

// When the API is on plain HTTP, WebSocket and SSE upgrades get redirected to
// HTTPS by ASP.NET's UseHttpsRedirection middleware and fail silently.
// Skip the noisy fallback attempts and go straight to LongPolling.
const transport = BASE_URL.startsWith('http://')
  ? HttpTransportType.LongPolling
  : undefined // auto-negotiate (WS preferred) on HTTPS

export type HubName = 'conversation' | 'ingestion' | 'analysis'

const HUB_PATHS: Record<HubName, string> = {
  conversation: '/hubs/conversation',
  ingestion: '/hubs/ingestion',
  analysis: '/hubs/analysis',
}

// In-memory singleton registry — one connection per hub
const connections = new Map<HubName, HubConnection>()

/**
 * Returns the singleton HubConnection for `hubName`.
 * Creates it on first call. Never recreates it.
 */
export function getHubConnection(hubName: HubName): HubConnection {
  if (connections.has(hubName)) {
    return connections.get(hubName)!
  }

  const connection = new HubConnectionBuilder()
    .withUrl(`${BASE_URL}${HUB_PATHS[hubName]}`, {
      accessTokenFactory: () => getToken() ?? '',
      transport,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // Exponential backoff capped at 30s
        const delays = [1000, 2000, 5000, 10000, 30000]
        return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)]
      },
    })
    .configureLogging(
      // On HTTP dev (local), transport negotiation attempts produce expected
      // WS/SSE failures that are noise — silence them. On HTTPS (prod) log warnings.
      BASE_URL.startsWith('http://') ? LogLevel.None : LogLevel.Warning,
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
