import type { TimelineEntry } from '../../api/types'
import { cn } from '../../lib/cn'
import { formatTime } from '../../lib/format'

const dot: Record<string, string> = {
  Confirmed: 'bg-emerald-500',
  Failed: 'bg-rose-500',
  Cancelled: 'bg-slate-400',
}

export function OrderTimeline({ entries }: { entries: TimelineEntry[] }) {
  if (entries.length === 0) {
    return <p className="text-sm text-muted">Waiting for the first update...</p>
  }

  return (
    <ol className="space-y-5" aria-label="Order history">
      {entries.map((entry, index) => (
        <li key={`${entry.state}-${entry.occurredAtUtc}`} className="animate-slide-in relative flex gap-3 pl-1">
          {index < entries.length - 1 && <span aria-hidden className="absolute left-[7px] top-4 h-[calc(100%+0.75rem)] w-px bg-line" />}
          <span className={cn('relative mt-1.5 size-3.5 shrink-0 rounded-full ring-4 ring-surface', dot[entry.state] ?? 'bg-brand-500')} />
          <div>
            <p className="text-sm font-medium">{entry.message}</p>
            <p className="text-xs text-muted">{formatTime(entry.occurredAtUtc)}</p>
          </div>
        </li>
      ))}
    </ol>
  )
}
