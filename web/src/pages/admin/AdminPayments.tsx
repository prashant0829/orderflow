import { useQuery } from '@tanstack/react-query'
import { adminApi } from '../../api/endpoints'
import type { Payment } from '../../api/types'
import { Badge, Card, EmptyState, ErrorState, Skeleton } from '../../components/ui/primitives'
import { formatDateTime, formatMoney, shortId } from '../../lib/format'

const tone = { Completed: 'success', Failed: 'danger', Refunded: 'warning', AwaitingCustomer: 'info', Cancelled: 'neutral' } as const

export function paymentSummary(payments: Payment[]) {
  const completed = payments.filter((payment) => payment.status === 'Completed')
  return {
    count: payments.length,
    failed: payments.filter((payment) => payment.status === 'Failed').length,
    refunded: payments.filter((payment) => payment.status === 'Refunded').length,
    collected: completed.reduce((sum, payment) => sum + payment.amount, 0),
  }
}

export function AdminPayments() {
  const { data, isPending, isError, refetch } = useQuery({ queryKey: ['admin', 'payments'], queryFn: () => adminApi.payments(), refetchInterval: 10_000 })

  if (isPending) return <Skeleton className="h-72" />
  if (isError) return <ErrorState message="Could not load payments." onRetry={() => void refetch()} />
  if (data.length === 0) return <EmptyState icon={<span className="text-lg">$</span>} title="No payments yet" />

  const summary = paymentSummary(data)

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[
          ['Recent payments', summary.count],
          ['Collected', formatMoney(summary.collected)],
          ['Failed', summary.failed],
          ['Refunded', summary.refunded],
        ].map(([label, value]) => (
          <Card key={label as string} className="p-4">
            <p className="text-sm text-muted">{label}</p>
            <p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p>
          </Card>
        ))}
      </div>

      <Card className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-left text-sm">
          <thead className="border-b border-line text-xs uppercase tracking-wide text-muted">
            <tr>
              <th className="px-4 py-3 font-medium">Order</th>
              <th className="px-4 py-3 font-medium">When</th>
              <th className="px-4 py-3 text-right font-medium">Amount</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Reference / reason</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {data.map((payment) => (
              <tr key={payment.id} className="hover:bg-surface-2/60">
                <td className="px-4 py-3 font-medium">{shortId(payment.orderId)}</td>
                <td className="px-4 py-3 text-muted">{formatDateTime(payment.createdAtUtc)}</td>
                <td className="px-4 py-3 text-right tabular-nums">{formatMoney(payment.amount)}</td>
                <td className="px-4 py-3">
                  <Badge tone={tone[payment.status]}>{payment.status}</Badge>
                </td>
                <td className="max-w-64 truncate px-4 py-3 text-muted" title={payment.transactionId ?? payment.providerPaymentId ?? payment.failureReason ?? ''}>
                  {payment.transactionId ?? payment.providerPaymentId ?? payment.failureReason ?? '–'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  )
}
