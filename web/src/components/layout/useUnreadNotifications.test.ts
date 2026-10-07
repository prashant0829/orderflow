import { describe, expect, it } from 'vitest'
import type { AppNotification } from '../../api/types'
import { countUnread } from './useUnreadNotifications'

const note = (createdAtUtc: string): AppNotification => ({
  id: createdAtUtc,
  orderId: 'o1',
  type: 'OrderConfirmed',
  channel: 'Email',
  recipient: 'x@y.z',
  subject: 's',
  body: 'b',
  createdAtUtc,
})

describe('countUnread', () => {
  it('counts only notifications newer than the last time the inbox was opened', () => {
    const seenAt = Date.parse('2026-10-07T10:00:00Z')

    expect(countUnread([note('2026-10-07T09:59:00Z'), note('2026-10-07T10:01:00Z'), note('2026-10-07T10:02:00')], seenAt)).toBe(2)
  })

  it('handles no data', () => {
    expect(countUnread(undefined, 0)).toBe(0)
  })
})
