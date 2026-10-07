import { useQuery } from '@tanstack/react-query'
import { Bell, Mail } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { notificationsApi } from '../api/endpoints'
import { markNotificationsSeen } from '../components/layout/useUnreadNotifications'
import { Card, EmptyState, ErrorState, PageHeader, Skeleton } from '../components/ui/primitives'
import { formatDateTime, shortId, timeAgo } from '../lib/format'

export function NotificationsPage() {
  const { data, isPending, isError, error, refetch } = useQuery({ queryKey: ['notifications'], queryFn: () => notificationsApi.list() })

  useEffect(() => {
    if (data) markNotificationsSeen()
  }, [data])

  return (
    <>
      <PageHeader title="Inbox" description="The emails we would send you. In this demo they are stored here instead of leaving the system." />

      {isPending && (
        <div className="space-y-3">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-24" />
          ))}
        </div>
      )}
      {isError && <ErrorState message={error instanceof Error ? error.message : 'Could not load your inbox.'} onRetry={() => void refetch()} />}
      {data?.length === 0 && <EmptyState icon={<Bell className="size-6" />} title="Nothing here yet" description="Messages about your orders will show up here." />}

      <ul className="space-y-3">
        {data?.map((notification) => (
          <li key={notification.id}>
            <Card className="animate-slide-in p-4">
              <div className="flex items-start gap-3">
                <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 dark:text-brand-300">
                  <Mail className="size-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-baseline justify-between gap-x-3">
                    <p className="font-medium">{notification.subject}</p>
                    <time className="text-xs text-muted" title={formatDateTime(notification.createdAtUtc)}>
                      {timeAgo(notification.createdAtUtc)}
                    </time>
                  </div>
                  <p className="mt-1 text-sm text-muted">{notification.body}</p>
                  <p className="mt-2 text-xs text-muted">
                    To {notification.recipient} ·{' '}
                    <Link to={`/orders/${notification.orderId}`} className="text-brand-600 hover:underline dark:text-brand-300">
                      View order {shortId(notification.orderId)}
                    </Link>
                  </p>
                </div>
              </div>
            </Card>
          </li>
        ))}
      </ul>
    </>
  )
}
