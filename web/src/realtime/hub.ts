import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { StatusChange } from '../api/types'

export type ConnectionStatus = 'offline' | 'connecting' | 'live' | 'reconnecting'

export interface OrderHub {
  start: () => Promise<void>
  stop: () => Promise<void>
  onChange: (listener: (change: StatusChange) => void) => () => void
  onStatus: (listener: (status: ConnectionStatus) => void) => () => void
}

const RETRY_DELAYS_MS = [0, 2000, 5000, 10000, 20000]

/**
 * One SignalR connection that receives status pushes for the signed-in customer's orders.
 * The server decides which orders you may hear about (the group comes from your token).
 */
export function createOrderHub(hubUrl: string, getToken: () => string | null): OrderHub {
  const changeListeners = new Set<(change: StatusChange) => void>()
  const statusListeners = new Set<(status: ConnectionStatus) => void>()
  let status: ConnectionStatus = 'offline'

  const setStatus = (next: ConnectionStatus) => {
    status = next
    statusListeners.forEach((listener) => listener(next))
  }

  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => getToken() ?? '' })
    .withAutomaticReconnect(RETRY_DELAYS_MS)
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('orderStatusChanged', (change: StatusChange) => changeListeners.forEach((listener) => listener(change)))

  // After a reconnect the server has forgotten our groups, so join again.
  const join = () => connection.invoke('JoinMyOrders')
  connection.onreconnecting(() => setStatus('reconnecting'))
  connection.onreconnected(async () => {
    await join()
    setStatus('live')
  })
  connection.onclose(() => setStatus('offline'))

  let stopped = false

  async function start() {
    if (connection.state !== HubConnectionState.Disconnected) return
    stopped = false
    setStatus('connecting')
    try {
      await connection.start()
      await join()
      setStatus('live')
    } catch {
      setStatus('offline')
      // The pages also poll while an order is pending, so a failed start is not fatal. Try again shortly.
      if (!stopped) window.setTimeout(() => void start(), 5000)
    }
  }

  async function stop() {
    stopped = true
    await connection.stop()
    setStatus('offline')
  }

  return {
    start,
    stop,
    onChange(listener) {
      changeListeners.add(listener)
      return () => changeListeners.delete(listener)
    },
    onStatus(listener) {
      statusListeners.add(listener)
      listener(status)
      return () => statusListeners.delete(listener)
    },
  }
}
