import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Receipt, XCircle } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { catalogApi, notificationsApi, ordersApi } from '../api/endpoints'
import { ApiError } from '../api/client'
import { ConnectionIndicator } from '../components/layout/ConnectionIndicator'
import { OrderStepper } from '../components/orders/OrderStepper'
import { OrderTimeline } from '../components/orders/OrderTimeline'
import { StatusBadge } from '../components/orders/StatusBadge'
import { Button } from '../components/ui/Button'
import { ConfirmDialog } from '../components/ui/ConfirmDialog'
import { Card, ErrorState, Skeleton } from '../components/ui/primitives'
import { formatDateTime, formatMoney, shortId } from '../lib/format'
import { effectiveState, isFinal } from '../lib/orderProgress'

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const queryClient = useQueryClient()
  const [confirmCancel, setConfirmCancel] = useState(false)

  const order = useQuery({
    queryKey: ['order', id],
    queryFn: () => ordersApi.get(id),
    // Live updates arrive over SignalR; polling while pending is the safety net if the connection drops.
    refetchInterval: (query) => (query.state.data?.status === 'Pending' ? 5000 : false),
  })
  const progress = useQuery({
    queryKey: ['progress', id],
    queryFn: () => ordersApi.progress(id),
    enabled: order.isSuccess,
    refetchInterval: (query) => (query.state.data && isFinal(query.state.data.state) ? false : 4000),
  })
  const timeline = useQuery({
    queryKey: ['timeline', id],
    queryFn: () => notificationsApi.timeline(id),
    enabled: order.isSuccess,
    refetchInterval: () => (order.data?.status === 'Pending' ? 6000 : false),
  })
  const catalog = useQuery({ queryKey: ['catalog'], queryFn: catalogApi.list })

  const cancel = useMutation({
    mutationFn: () => ordersApi.cancel(id, 'Cancelled by customer'),
    onSuccess: () => {
      setConfirmCancel(false)
      toast.info('Cancelling your order...')
      void queryClient.invalidateQueries({ queryKey: ['order', id] })
      void queryClient.invalidateQueries({ queryKey: ['progress', id] })
    },
    onError: (error) => {
      setConfirmCancel(false)
      toast.error(error instanceof ApiError ? error.message : 'Could not cancel the order.')
    },
  })

  if (order.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-40" />
        <Skeleton className="h-64" />
      </div>
    )
  }

  if (order.isError) {
    const notFound = order.error instanceof ApiError && order.error.status === 404
    return <ErrorState message={notFound ? 'We could not find that order.' : 'Could not load this order.'} onRetry={notFound ? undefined : () => void order.refetch()} />
  }

  const data = order.data
  const state = effectiveState(progress.data?.state, data.status)
  const entries = timeline.data ?? []
  const nameOf = (productId: string) => catalog.data?.find((product) => product.productId === productId)?.name ?? `Product ${productId.slice(0, 8)}`

  return (
    <div className="space-y-6">
      <Link to="/orders" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft className="size-4" /> All orders
      </Link>

      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight">Order {shortId(data.id)}</h1>
            <StatusBadge status={state} />
          </div>
          <p className="mt-1 text-sm text-muted">Placed {formatDateTime(data.createdAtUtc)}</p>
        </div>
        <div className="flex items-center gap-4">
          <ConnectionIndicator />
          {/* The saga's live state can be ahead of the order row for a moment, so check both. */}
          {data.status === 'Pending' && !isFinal(state) && (
            <Button variant="secondary" icon={<XCircle className="size-4" />} onClick={() => setConfirmCancel(true)}>
              Cancel order
            </Button>
          )}
        </div>
      </div>

      {(data.status === 'Failed' || data.status === 'Cancelled') && data.failureReason && (
        <div role="alert" className="rounded-2xl border border-rose-500/30 bg-rose-500/5 px-4 py-3 text-sm">
          <span className="font-medium">{data.status === 'Cancelled' ? 'Cancelled: ' : 'Could not complete: '}</span>
          {data.failureReason}
        </div>
      )}

      <Card className="p-6">
        <OrderStepper state={state} timeline={entries} />
      </Card>

      <div className="grid gap-6 lg:grid-cols-5">
        <Card className="p-6 lg:col-span-3">
          <h2 className="mb-5 font-semibold">Live updates</h2>
          <OrderTimeline entries={entries} />
        </Card>

        <Card className="p-6 lg:col-span-2">
          <h2 className="mb-4 flex items-center gap-2 font-semibold">
            <Receipt className="size-4 text-muted" /> Summary
          </h2>
          <ul className="divide-y divide-line">
            {data.lines.map((line) => (
              <li key={line.productId} className="flex justify-between gap-3 py-2.5 text-sm">
                <span>
                  {line.quantity} × {nameOf(line.productId)}
                </span>
                <span className="tabular-nums">{formatMoney(line.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex justify-between border-t border-line pt-3 font-semibold">
            <span>Total</span>
            <span className="tabular-nums">{formatMoney(data.totalAmount)}</span>
          </div>
          {progress.data?.paymentTransactionId && <p className="mt-4 break-all text-xs text-muted">Payment reference: {progress.data.paymentTransactionId}</p>}
        </Card>
      </div>

      <ConfirmDialog
        open={confirmCancel}
        title="Cancel this order?"
        confirmLabel="Cancel order"
        cancelLabel="Keep it"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setConfirmCancel(false)}
      >
        Your items are released and any payment is refunded.
      </ConfirmDialog>
    </div>
  )
}
