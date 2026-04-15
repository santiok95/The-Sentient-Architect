/**
 * Runtime configuration
 * These values are resolved at runtime, not build-time, so they work with Docker env injection
 *
 * SINGLE SOURCE OF TRUTH for all API/WebSocket URLs
 */

export const getApiBaseUrl = (): string => {
  // Client-side: read from window if available (injected in layout.tsx)
  if (typeof window !== 'undefined') {
    const url = (window as any).__API_BASE_URL
    if (url) return url
  }

  // Fallback: use NEXT_PUBLIC_API_URL (set at build-time)
  if (typeof process !== 'undefined' && process.env.NEXT_PUBLIC_API_URL) {
    return process.env.NEXT_PUBLIC_API_URL
  }

  // Last resort: localhost
  return 'http://localhost:5291'
}

/**
 * Get WebSocket URL from API base URL
 * Converts http:// → ws:// and https:// → wss://
 */
export const getWsBaseUrl = (): string => {
  const baseUrl = getApiBaseUrl()
  if (baseUrl.startsWith('https://')) return baseUrl.replace('https://', 'wss://')
  return baseUrl.replace('http://', 'ws://')
}

export const config = {
  /**
   * Base URL for REST API calls
   * Used by: apiClient, actions, mocks
   */
  get apiBaseUrl(): string {
    return getApiBaseUrl()
  },

  /**
   * Base URL for WebSocket connections
   * Used by: SignalR, real-time features
   */
  get wsBaseUrl(): string {
    return getWsBaseUrl()
  },

  /**
   * SignalR hub URLs - relative paths, BASE_URL is prepended
   */
  signalrHubs: {
    conversation: '/hubs/conversation',
    ingestion: '/hubs/ingestion',
    analysis: '/hubs/analysis',
  },
}
