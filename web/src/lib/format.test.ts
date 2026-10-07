import { describe, expect, it } from 'vitest'
import { formatMoney, initials, parseUtc, shortId, timeAgo } from './format'

describe('format helpers', () => {
  it('formats money', () => {
    expect(formatMoney(1234.5)).toBe('$1,234.50')
  })

  it('shortens ids the way people quote them', () => {
    expect(shortId('496d2cf3-1aef-440f-a95b-5a053bdd9732')).toBe('#496D2CF3')
  })

  it('treats timestamps without a zone as UTC', () => {
    expect(parseUtc('2026-10-07T06:21:01').toISOString()).toBe('2026-10-07T06:21:01.000Z')
    expect(parseUtc('2026-10-07T06:21:01Z').toISOString()).toBe('2026-10-07T06:21:01.000Z')
  })

  it('describes how long ago something happened', () => {
    const now = Date.parse('2026-10-07T12:00:00Z')
    expect(timeAgo('2026-10-07T11:59:55Z', now)).toBe('just now')
    expect(timeAgo('2026-10-07T11:59:00Z', now)).toBe('1m ago')
    expect(timeAgo('2026-10-07T09:00:00Z', now)).toBe('3h ago')
  })

  it('makes initials from names and emails', () => {
    expect(initials('Demo Customer')).toBe('DC')
    expect(initials('alice@example.com')).toBe('AE')
  })
})
