import { getAccessToken, getRefreshToken, setAccessToken, setRefreshToken } from './tokens'
import type { AuthResult } from './types'

export const API_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5100').replace(/\/$/, '')

/** An error response from the backend, flattened into something a UI can show. */
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>
  readonly retryAfterSeconds?: number

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}, retryAfterSeconds?: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
    this.retryAfterSeconds = retryAfterSeconds
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // Not JSON (for example a gateway timeout page).
  }

  const retryAfter = Number(response.headers.get('Retry-After')) || undefined
  const fieldMessages = problem.errors ? Object.values(problem.errors).flat() : []
  const message =
    response.status === 429
      ? `Too many requests. Try again in ${retryAfter ?? 'a few'} seconds.`
      : fieldMessages[0] ?? problem.detail ?? problem.title ?? `Request failed (${response.status})`

  return new ApiError(response.status, message, problem.errors, retryAfter)
}

type SessionLostHandler = () => void
let onSessionLost: SessionLostHandler = () => {}

/** Called when the refresh token no longer works, so the app can send the user to the login page. */
export function setSessionLostHandler(handler: SessionLostHandler) {
  onSessionLost = handler
}

let refreshInFlight: Promise<boolean> | null = null

/** Exchanges the refresh token for a new pair. Concurrent callers share one request, because a rotated token only works once. */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    const refreshToken = getRefreshToken()
    if (!refreshToken) return false

    try {
      const response = await fetch(`${API_URL}/api/v1/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!response.ok) {
        setAccessToken(null)
        setRefreshToken(null)
        return false
      }

      storeSession((await response.json()) as AuthResult)
      return true
    } catch {
      return false
    }
  })().finally(() => {
    refreshInFlight = null
  })

  return refreshInFlight
}

export function storeSession(result: AuthResult) {
  setAccessToken(result.accessToken, result.accessTokenExpiresAtUtc)
  setRefreshToken(result.refreshToken)
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  /** Set false for the login/register/refresh calls. */
  auth?: boolean
  signal?: AbortSignal
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, headers = {}, auth = true, signal } = options

  const send = () => {
    const token = auth ? getAccessToken() : null
    return fetch(`${API_URL}${path}`, {
      method,
      signal,
      headers: {
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...headers,
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  }

  let response = await send()

  if (response.status === 401 && auth && getRefreshToken()) {
    if (await refreshSession()) {
      response = await send()
    } else {
      onSessionLost()
    }
  }

  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
