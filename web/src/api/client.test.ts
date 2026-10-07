import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, request, setSessionLostHandler } from './client'
import { getAccessToken, getRefreshToken, setAccessToken, setRefreshToken } from './tokens'

const json = (body: unknown, status = 200, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } })

const session = (access: string, refresh: string) => ({
  accessToken: access,
  accessTokenExpiresAtUtc: new Date(Date.now() + 15 * 60_000).toISOString(),
  refreshToken: refresh,
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 86_400_000).toISOString(),
  user: { id: 'u1', email: 'a@b.c', displayName: 'A', role: 'Customer' },
})

describe('api client', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
    setAccessToken('old-access', new Date(Date.now() + 60_000).toISOString())
    setRefreshToken('refresh-1')
    setSessionLostHandler(() => {})
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
    setRefreshToken(null)
  })

  it('sends the bearer token', async () => {
    fetchMock.mockResolvedValueOnce(json({ ok: true }))

    await request('/api/v1/orders')

    const headers = fetchMock.mock.calls[0]![1]!.headers as Record<string, string>
    expect(headers.Authorization).toBe('Bearer old-access')
  })

  it('turns a validation problem into a readable ApiError', async () => {
    fetchMock.mockResolvedValueOnce(json({ title: 'Validation failed', errors: { Quantity: ['Quantity must be greater than 0.'] } }, 400))

    const error = (await request('/x').catch((caught: unknown) => caught)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(400)
    expect(error.message).toBe('Quantity must be greater than 0.')
  })

  it('explains rate limiting using Retry-After', async () => {
    fetchMock.mockResolvedValueOnce(json({}, 429, { 'Retry-After': '42' }))

    const error = (await request('/x').catch((caught: unknown) => caught)) as ApiError

    expect(error.status).toBe(429)
    expect(error.message).toContain('42')
  })

  it('refreshes once on 401 and retries the original request with the new token', async () => {
    fetchMock
      .mockResolvedValueOnce(json({}, 401))
      .mockResolvedValueOnce(json(session('new-access', 'refresh-2')))
      .mockResolvedValueOnce(json({ id: 'order-1' }))

    const result = await request<{ id: string }>('/api/v1/orders/1')

    expect(result.id).toBe('order-1')
    expect(getAccessToken()).toBe('new-access')
    expect(getRefreshToken()).toBe('refresh-2')
    const retryHeaders = fetchMock.mock.calls[2]![1]!.headers as Record<string, string>
    expect(retryHeaders.Authorization).toBe('Bearer new-access')
  })

  it('shares one refresh between parallel requests (a rotated token only works once)', async () => {
    fetchMock.mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/auth/refresh')) return Promise.resolve(json(session('new-access', 'refresh-2')))
      const authorized = (fetchMock.mock.calls.at(-1)?.[1]?.headers as Record<string, string> | undefined)?.Authorization === 'Bearer new-access'
      return Promise.resolve(authorized ? json({ ok: true }) : json({}, 401))
    })

    await Promise.all([request('/a'), request('/b'), request('/c')])

    const refreshCalls = fetchMock.mock.calls.filter(([input]) => String(input).endsWith('/auth/refresh'))
    expect(refreshCalls).toHaveLength(1)
  })

  it('reports a lost session when the refresh token is rejected', async () => {
    const lost = vi.fn()
    setSessionLostHandler(lost)
    fetchMock.mockResolvedValueOnce(json({}, 401)).mockResolvedValueOnce(json({ title: 'Unauthorized' }, 401))

    await expect(request('/api/v1/orders')).rejects.toBeInstanceOf(ApiError)

    expect(lost).toHaveBeenCalledOnce()
    expect(getRefreshToken()).toBeNull()
  })

  it('does not try to refresh for calls that are not authenticated', async () => {
    fetchMock.mockResolvedValueOnce(json({ title: 'Invalid email or password.' }, 401))

    await expect(request('/api/v1/auth/login', { method: 'POST', body: {}, auth: false })).rejects.toThrow('Invalid email or password.')

    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
