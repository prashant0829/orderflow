import { useQueries, useQuery } from '@tanstack/react-query'
import { Activity, BarChart3, ExternalLink, FileSearch, Network } from 'lucide-react'
import { Link } from 'react-router-dom'
import { adminApi } from '../../api/endpoints'
import type { OrderStatus, ServiceHealth } from '../../api/types'
import { Badge, Card, ErrorState, Skeleton } from '../../components/ui/primitives'
import { cn } from '../../lib/cn'

const STATUSES: { status: OrderStatus; label: string; tone: string }[] = [
  { status: 'Pending', label: 'Processing', tone: 'text-sky-600 dark:text-sky-300' },
  { status: 'Confirmed', label: 'Confirmed', tone: 'text-emerald-600 dark:text-emerald-300' },
  { status: 'Failed', label: 'Failed', tone: 'text-rose-600 dark:text-rose-300' },
  { status: 'Cancelled', label: 'Cancelled', tone: 'text-muted' },
]

const tools = [
  { label: 'Dashboards', hint: 'Grafana', url: import.meta.env.VITE_GRAFANA_URL ?? 'http://localhost:3000', icon: BarChart3 },
  { label: 'Traces', hint: 'Jaeger', url: import.meta.env.VITE_JAEGER_URL ?? 'http://localhost:16686', icon: Network },
  { label: 'Logs', hint: 'Seq', url: import.meta.env.VITE_SEQ_URL ?? 'http://localhost:5341', icon: FileSearch },
  { label: 'Queues', hint: 'RabbitMQ', url: import.meta.env.VITE_RABBITMQ_URL ?? 'http://localhost:15672', icon: Activity },
]

function ServiceCard({ service }: { service: ServiceHealth }) {
  const healthy = service.status === 'Healthy'
  return (
    <Card className="flex items-center gap-3 p-4">
      <span className={cn('size-3 shrink-0 rounded-full', healthy ? 'bg-emerald-500' : 'animate-pulse bg-rose-500')} />
      <div className="min-w-0 flex-1">
        <p className="font-medium capitalize">{service.service}</p>
        <p className="truncate text-xs text-muted">{healthy ? `${service.durationMs} ms` : (service.error ?? service.status)}</p>
      </div>
      <Badge tone={healthy ? 'success' : 'danger'}>{service.status}</Badge>
    </Card>
  )
}

export function AdminOverview() {
  const health = useQuery({ queryKey: ['admin', 'health'], queryFn: adminApi.health, refetchInterval: 10_000 })
  const deadLetters = useQuery({ queryKey: ['admin', 'dead-letters'], queryFn: adminApi.deadLetters, refetchInterval: 10_000 })
  const counts = useQueries({
    queries: STATUSES.map(({ status }) => ({
      queryKey: ['admin', 'orders-count', status],
      queryFn: () => adminApi.orders(status, 1, 1),
      refetchInterval: 10_000,
    })),
  })

  const failedMessages = deadLetters.data?.reduce((sum, queue) => sum + queue.messageCount, 0) ?? 0

  return (
    <div className="space-y-8">
      <section aria-labelledby="health-heading">
        <div className="mb-3 flex items-center justify-between">
          <h2 id="health-heading" className="font-semibold">
            Services
          </h2>
          {health.data && <Badge tone={health.data.status === 'Healthy' ? 'success' : 'danger'}>{health.data.status === 'Healthy' ? 'All systems normal' : 'Degraded'}</Badge>}
        </div>
        {health.isPending && (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-[72px]" />
            ))}
          </div>
        )}
        {health.isError && <ErrorState message="Could not read service health." onRetry={() => void health.refetch()} />}
        {health.data && (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {health.data.services.map((service) => (
              <ServiceCard key={service.service} service={service} />
            ))}
          </div>
        )}
      </section>

      <section aria-labelledby="orders-heading">
        <h2 id="orders-heading" className="mb-3 font-semibold">
          Orders
        </h2>
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
          {STATUSES.map(({ status, label, tone }, index) => (
            <Link key={status} to={`/admin/orders?status=${status}`}>
              <Card className="p-4 transition-colors hover:border-brand-500/50">
                <p className="text-sm text-muted">{label}</p>
                <p className={cn('mt-1 text-3xl font-semibold tabular-nums', tone)}>{counts[index]?.data?.totalCount ?? '–'}</p>
              </Card>
            </Link>
          ))}
          <Link to="/admin/dead-letters">
            <Card className={cn('p-4 transition-colors hover:border-brand-500/50', failedMessages > 0 && 'border-rose-500/50 bg-rose-500/5')}>
              <p className="text-sm text-muted">Failed messages</p>
              <p className={cn('mt-1 text-3xl font-semibold tabular-nums', failedMessages > 0 && 'text-rose-600 dark:text-rose-300')}>
                {deadLetters.data ? failedMessages : '–'}
              </p>
            </Card>
          </Link>
        </div>
      </section>

      <section aria-labelledby="tools-heading">
        <h2 id="tools-heading" className="mb-3 font-semibold">
          Observability tools
        </h2>
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {tools.map(({ label, hint, url, icon: Icon }) => (
            <a key={label} href={url} target="_blank" rel="noreferrer">
              <Card className="flex items-center gap-3 p-4 transition-colors hover:border-brand-500/50">
                <Icon className="size-5 text-brand-500" />
                <div className="flex-1">
                  <p className="text-sm font-medium">{label}</p>
                  <p className="text-xs text-muted">{hint}</p>
                </div>
                <ExternalLink className="size-3.5 text-muted" />
              </Card>
            </a>
          ))}
        </div>
      </section>
    </div>
  )
}
