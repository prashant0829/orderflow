import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, ShoppingBag } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ordersApi } from '../api/endpoints'
import { StatusBadge } from '../components/orders/StatusBadge'
import { Button } from '../components/ui/Button'
import { Card, EmptyState, ErrorState, PageHeader, Skeleton } from '../components/ui/primitives'
import { formatDateTime, formatMoney, shortId } from '../lib/format'

const PAGE_SIZE = 8

export function OrdersPage() {
  const [page, setPage] = useState(1)
  const { data, isPending, isError, error, refetch, isPlaceholderData } = useQuery({
    queryKey: ['orders', page],
    queryFn: () => ordersApi.list(page, PAGE_SIZE),
    placeholderData: (previous) => previous,
  })

  const totalPages = data ? Math.max(Math.ceil(data.totalCount / PAGE_SIZE), 1) : 1

  return (
    <>
      <PageHeader title="My orders" description="Everything you have ordered, newest first." />

      {isPending && (
        <div className="space-y-3">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-20" />
          ))}
        </div>
      )}
      {isError && <ErrorState message={error instanceof Error ? error.message : 'Could not load your orders.'} onRetry={() => void refetch()} />}
      {data && data.items.length === 0 && (
        <EmptyState
          icon={<ShoppingBag className="size-6" />}
          title="No orders yet"
          description="When you place an order it shows up here, and you can follow it live."
          action={
            <Link to="/">
              <Button>Start shopping</Button>
            </Link>
          }
        />
      )}

      {data && data.items.length > 0 && (
        <>
          <ul className={`space-y-3 transition-opacity ${isPlaceholderData ? 'opacity-60' : ''}`}>
            {data.items.map((order) => (
              <li key={order.id}>
                <Link to={`/orders/${order.id}`} className="block rounded-2xl focus-visible:outline-offset-4">
                  <Card className="flex flex-wrap items-center gap-x-6 gap-y-2 p-4 transition-colors hover:border-brand-500/50">
                    <div className="min-w-32">
                      <p className="font-semibold">{shortId(order.id)}</p>
                      <p className="text-xs text-muted">{formatDateTime(order.createdAtUtc)}</p>
                    </div>
                    <p className="text-sm text-muted">
                      {order.lines.reduce((sum, line) => sum + line.quantity, 0)} item(s)
                    </p>
                    <p className="ml-auto font-semibold tabular-nums">{formatMoney(order.totalAmount)}</p>
                    <StatusBadge status={order.status} />
                  </Card>
                </Link>
              </li>
            ))}
          </ul>

          {totalPages > 1 && (
            <nav className="mt-6 flex items-center justify-between" aria-label="Pagination">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)} icon={<ChevronLeft className="size-4" />}>
                Newer
              </Button>
              <span className="text-sm text-muted">
                Page {page} of {totalPages}
              </span>
              <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>
                Older <ChevronRight className="size-4" />
              </Button>
            </nav>
          )}
        </>
      )}
    </>
  )
}
