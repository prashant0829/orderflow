import { CheckCircle2, CircleDashed, Clock, XCircle } from 'lucide-react'
import type { OrderStatus, ProgressState } from '../../api/types'
import { Badge } from '../ui/primitives'

const config = {
  Pending: { tone: 'info', label: 'Processing', icon: Clock },
  Confirmed: { tone: 'success', label: 'Confirmed', icon: CheckCircle2 },
  Failed: { tone: 'danger', label: 'Failed', icon: XCircle },
  Cancelled: { tone: 'neutral', label: 'Cancelled', icon: CircleDashed },
} as const

export function StatusBadge({ status }: { status: OrderStatus | ProgressState }) {
  const key = (['Confirmed', 'Failed', 'Cancelled'].includes(status) ? status : 'Pending') as keyof typeof config
  const { tone, label, icon: Icon } = config[key]
  return (
    <Badge tone={tone}>
      <Icon className="size-3.5" aria-hidden />
      {label}
    </Badge>
  )
}
