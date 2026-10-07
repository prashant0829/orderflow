import { cn } from '../../lib/cn'
import { useConnectionStatus } from '../../realtime/RealtimeProvider'

const labels = { live: 'Live', connecting: 'Connecting', reconnecting: 'Reconnecting', offline: 'Offline' } as const

/** Shows whether live order updates are flowing. */
export function ConnectionIndicator({ className }: { className?: string }) {
  const status = useConnectionStatus()
  return (
    <span className={cn('inline-flex items-center gap-1.5 text-xs text-muted', className)} title="Live order updates">
      <span
        className={cn(
          'size-2 rounded-full',
          status === 'live' && 'bg-emerald-500',
          (status === 'connecting' || status === 'reconnecting') && 'animate-pulse bg-amber-500',
          status === 'offline' && 'bg-slate-400',
        )}
      />
      {labels[status]}
    </span>
  )
}
