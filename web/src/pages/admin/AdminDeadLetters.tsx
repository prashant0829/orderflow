import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { adminApi } from '../../api/endpoints'
import type { DeadLetterQueue } from '../../api/types'
import { ApiError } from '../../api/client'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Badge, Card, EmptyState, ErrorState, Skeleton } from '../../components/ui/primitives'
import { cn } from '../../lib/cn'

function prettyPayload(payload: string): string {
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

export function AdminDeadLetters() {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<string>()
  const [confirm, setConfirm] = useState(false)

  const queues = useQuery({ queryKey: ['admin', 'dead-letters'], queryFn: adminApi.deadLetters, refetchInterval: 5000 })
  const active = selected ?? queues.data?.find((queue) => queue.messageCount > 0)?.queue ?? queues.data?.[0]?.queue
  const activeQueue = queues.data?.find((queue) => queue.queue === active)

  const messages = useQuery({
    queryKey: ['admin', 'dead-letters', active, 'messages', activeQueue?.messageCount],
    queryFn: () => adminApi.peekDeadLetters(active!),
    enabled: !!active && (activeQueue?.messageCount ?? 0) > 0,
  })

  const replay = useMutation({
    mutationFn: () => adminApi.replayDeadLetters(active!),
    onSuccess: (result) => {
      setConfirm(false)
      toast.success(`Replayed ${result.replayed} message(s)`, { description: 'They go back to the original queue and run again.' })
      // RabbitMQ's management API reports queue sizes with a delay of a few seconds, so show the result now and re-check shortly after.
      queryClient.setQueryData<DeadLetterQueue[]>(['admin', 'dead-letters'], (current) =>
        current?.map((queue) => (queue.queue === result.queue ? { ...queue, messageCount: 0 } : queue)),
      )
      window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: ['admin', 'dead-letters'] }), 7000)
    },
    onError: (error) => {
      setConfirm(false)
      toast.error(error instanceof ApiError ? error.message : 'Replay failed.')
    },
  })

  if (queues.isPending) return <Skeleton className="h-64" />
  if (queues.isError) return <ErrorState message="Could not load the failed-message queues." onRetry={() => void queues.refetch()} />

  if (queues.data.length === 0) {
    return (
      <EmptyState
        icon={<CheckCircle2 className="size-6 text-emerald-500" />}
        title="No failed messages"
        description="When a message fails all its retries it is parked here so nothing is lost. You can inspect it and replay it."
      />
    )
  }

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <div className="space-y-2">
        {queues.data.map((queue) => (
          <button
            key={queue.queue}
            type="button"
            onClick={() => setSelected(queue.queue)}
            aria-pressed={active === queue.queue}
            className={cn(
              'w-full rounded-2xl border p-4 text-left transition-colors',
              active === queue.queue ? 'border-brand-500 bg-brand-500/5' : 'border-line bg-surface hover:bg-surface-2',
            )}
          >
            <div className="flex items-center justify-between gap-2">
              <p className="truncate text-sm font-medium">{queue.originalQueue}</p>
              <Badge tone={queue.messageCount > 0 ? 'danger' : 'success'}>{queue.messageCount}</Badge>
            </div>
            <p className="mt-0.5 truncate text-xs text-muted">{queue.queue}</p>
          </button>
        ))}
      </div>

      <div className="space-y-4 lg:col-span-2">
        <div className="flex items-center justify-between gap-3">
          <h2 className="font-semibold">{activeQueue?.messageCount ? `${activeQueue.messageCount} failed message(s)` : 'Nothing waiting in this queue'}</h2>
          <Button icon={<RotateCcw className="size-4" />} disabled={!activeQueue?.messageCount} onClick={() => setConfirm(true)}>
            Replay all
          </Button>
        </div>

        {messages.isPending && activeQueue?.messageCount ? <Skeleton className="h-40" /> : null}
        {messages.data?.map((message, index) => (
          <Card key={message.messageId ?? index} className="space-y-3 p-4">
            <div>
              <p className="text-sm font-medium text-rose-600 dark:text-rose-300">{message.exceptionType ?? 'Unknown error'}</p>
              <p className="text-sm text-muted">{message.exceptionMessage}</p>
            </div>
            <pre className="max-h-56 overflow-auto rounded-xl bg-surface-2 p-3 text-xs leading-relaxed">{prettyPayload(message.payload)}</pre>
          </Card>
        ))}
      </div>

      <ConfirmDialog
        open={confirm}
        title="Replay failed messages?"
        confirmLabel="Replay"
        loading={replay.isPending}
        onConfirm={() => replay.mutate()}
        onClose={() => setConfirm(false)}
      >
        Every message in <span className="font-mono text-fg">{active}</span> goes back to its original queue and is processed again. Only do this once the cause is fixed.
      </ConfirmDialog>
    </div>
  )
}
