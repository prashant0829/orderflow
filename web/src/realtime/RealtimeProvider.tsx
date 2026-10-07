import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { API_URL } from '../api/client'
import { getAccessToken } from '../api/tokens'
import type { StatusChange } from '../api/types'
import { useAuth } from '../auth/AuthProvider'
import { shortId } from '../lib/format'
import { isFinal } from '../lib/orderProgress'
import { createOrderHub, type ConnectionStatus, type OrderHub } from './hub'

const RealtimeContext = createContext<ConnectionStatus>('offline')

export const useConnectionStatus = () => useContext(RealtimeContext)

/** Which queries are stale when an order moves. Kept in one place so pages never have to remember it. */
export function invalidateForOrder(queryClient: ReturnType<typeof useQueryClient>, orderId: string) {
  void queryClient.invalidateQueries({ queryKey: ['order', orderId] })
  void queryClient.invalidateQueries({ queryKey: ['progress', orderId] })
  void queryClient.invalidateQueries({ queryKey: ['timeline', orderId] })
  void queryClient.invalidateQueries({ queryKey: ['orders'] })
  void queryClient.invalidateQueries({ queryKey: ['notifications'] })
  void queryClient.invalidateQueries({ queryKey: ['catalog'] })
}

export function toastForChange(change: StatusChange) {
  const label = shortId(change.orderId)
  if (change.state === 'Confirmed') toast.success(`Order ${label} confirmed`, { description: change.message })
  else if (change.state === 'Failed') toast.error(`Order ${label} failed`, { description: change.message })
  else if (change.state === 'Cancelled') toast.info(`Order ${label} cancelled`)
}

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { status: authStatus } = useAuth()
  const queryClient = useQueryClient()
  const [connection, setConnection] = useState<ConnectionStatus>('offline')

  useEffect(() => {
    if (authStatus !== 'authenticated') return

    const hub: OrderHub = createOrderHub(`${API_URL}/hubs/orders`, getAccessToken)
    const stopStatus = hub.onStatus(setConnection)
    const stopChange = hub.onChange((change) => {
      invalidateForOrder(queryClient, change.orderId)
      if (isFinal(change.state)) toastForChange(change)
    })

    void hub.start()
    return () => {
      stopChange()
      stopStatus()
      void hub.stop()
      setConnection('offline')
    }
  }, [authStatus, queryClient])

  return <RealtimeContext.Provider value={connection}>{children}</RealtimeContext.Provider>
}
