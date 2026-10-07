import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { adminApi } from '../../api/endpoints'
import type { OrderStatus } from '../../api/types'
import { StatusBadge } from '../../components/orders/StatusBadge'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorState, Skeleton } from '../../components/ui/primitives'
import { cn } from '../../lib/cn'
import { formatDateTime, formatMoney, shortId } from '../../lib/format'

const FILTERS: (OrderStatus | 'All')[] = ['All', 'Pending', 'Confirmed', 'Failed', 'Cancelled']
const PAGE_SIZE = 10

export function AdminOrders() {
  const [params, setParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const requested = params.get('status') as OrderStatus | null
  const filter: OrderStatus | 'All' = requested && FILTERS.includes(requested) ? requested : 'All'

  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['admin', 'orders', filter, page],
    queryFn: () => adminApi.orders(filter === 'All' ? undefined : filter, page, PAGE_SIZE),
    placeholderData: (previous) => previous,
    refetchInterval: 10_000,
  })
  const totalPages = data ? Math.max(Math.ceil(data.totalCount / PAGE_SIZE), 1) : 1

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by status">
        {FILTERS.map((option) => (
          <button
            key={option}
            type="button"
            aria-pressed={filter === option}
            onClick={() => {
              setPage(1)
              setParams(option === 'All' ? {} : { status: option })
            }}
            className={cn(
              'rounded-full border px-3.5 py-1.5 text-sm transition-colors',
              filter === option ? 'border-brand-500 bg-brand-500/10 text-brand-600 dark:text-brand-300' : 'border-line text-muted hover:bg-surface-2',
            )}
          >
            {option === 'Pending' ? 'Processing' : option}
          </button>
        ))}
      </div>

      {isPending && <Skeleton className="h-72" />}
      {isError && <ErrorState message="Could not load orders." onRetry={() => void refetch()} />}
      {data && data.items.length === 0 && <EmptyState icon={<span className="text-lg">∅</span>} title="No orders match" />}

      {data && data.items.length > 0 && (
        <Card className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-line text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3 font-medium">Order</th>
                <th className="px-4 py-3 font-medium">Customer</th>
                <th className="px-4 py-3 font-medium">Placed</th>
                <th className="px-4 py-3 text-right font-medium">Total</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Reason</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {data.items.map((order) => (
                <tr key={order.id} className="hover:bg-surface-2/60">
                  <td className="px-4 py-3 font-medium">
                    <Link to={`/orders/${order.id}`} className="text-brand-600 hover:underline dark:text-brand-300">
                      {shortId(order.id)}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-muted">{shortId(order.customerId)}</td>
                  <td className="px-4 py-3 text-muted">{formatDateTime(order.createdAtUtc)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatMoney(order.totalAmount)}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={order.status} />
                  </td>
                  <td className="max-w-56 truncate px-4 py-3 text-muted" title={order.failureReason ?? ''}>
                    {order.failureReason ?? '–'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)} icon={<ChevronLeft className="size-4" />}>
            Newer
          </Button>
          <span className="text-sm text-muted">
            Page {page} of {totalPages} · {data?.totalCount} orders
          </span>
          <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>
            Older <ChevronRight className="size-4" />
          </Button>
        </div>
      )}
    </div>
  )
}
