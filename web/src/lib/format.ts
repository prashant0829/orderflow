const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export const formatMoney = (value: number) => money.format(value)

/** The first 8 characters, upper case: how people refer to an order ("#7E87D115"). */
export const shortId = (id: string) => `#${id.replaceAll('-', '').slice(0, 8).toUpperCase()}`

/** The backend sends UTC without a trailing Z in some places; treat it as UTC either way. */
export function parseUtc(value: string): Date {
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`)
}

export function formatDateTime(value: string): string {
  return parseUtc(value).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function formatTime(value: string): string {
  return parseUtc(value).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function timeAgo(value: string, now = Date.now()): string {
  const seconds = Math.max(Math.round((now - parseUtc(value).getTime()) / 1000), 0)
  if (seconds < 10) return 'just now'
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.round(hours / 24)}d ago`
}

export const initials = (nameOrEmail: string) =>
  nameOrEmail
    .split(/[\s@.]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('')
