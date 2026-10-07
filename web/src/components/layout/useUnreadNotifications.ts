import { useEffect, useState } from 'react'
import type { AppNotification } from '../../api/types'
import { parseUtc } from '../../lib/format'

const SEEN_KEY = 'orderflow.notifications.seen'
const SEEN_EVENT = 'orderflow:notifications-seen'

const readSeen = () => {
  try {
    return Number(localStorage.getItem(SEEN_KEY)) || 0
  } catch {
    return 0
  }
}

/** Marks everything up to now as read. Called when the inbox page is open. */
export function markNotificationsSeen() {
  try {
    localStorage.setItem(SEEN_KEY, String(Date.now()))
  } catch {
    // Unread badge just stays; harmless.
  }
  window.dispatchEvent(new Event(SEEN_EVENT))
}

export function countUnread(notifications: AppNotification[] | undefined, seenAt: number): number {
  return (notifications ?? []).filter((notification) => parseUtc(notification.createdAtUtc).getTime() > seenAt).length
}

export function useUnreadNotifications(notifications: AppNotification[] | undefined): number {
  const [seenAt, setSeenAt] = useState(readSeen)

  useEffect(() => {
    const update = () => setSeenAt(readSeen())
    window.addEventListener(SEEN_EVENT, update)
    return () => window.removeEventListener(SEEN_EVENT, update)
  }, [])

  return countUnread(notifications, seenAt)
}
