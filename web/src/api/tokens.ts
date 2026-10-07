// The access token lives only in memory (short-lived, gone on reload).
// The refresh token is kept in localStorage so a reload can sign you back in.
// Trade-off: an XSS bug could read it. Production hardening: move it to an httpOnly cookie set by the gateway.

const REFRESH_KEY = 'orderflow.refresh'
const EXPIRY_SAFETY_MS = 15_000

let accessToken: string | null = null
let accessTokenExpiresAt = 0

export function setAccessToken(token: string | null, expiresAtUtc?: string) {
  accessToken = token
  accessTokenExpiresAt = token && expiresAtUtc ? new Date(expiresAtUtc.endsWith('Z') ? expiresAtUtc : `${expiresAtUtc}Z`).getTime() : 0
}

export function getAccessToken(): string | null {
  return accessToken
}

export function accessTokenIsFresh(now = Date.now()): boolean {
  return accessToken !== null && accessTokenExpiresAt - EXPIRY_SAFETY_MS > now
}

export function getRefreshToken(): string | null {
  try {
    return localStorage.getItem(REFRESH_KEY)
  } catch {
    return null
  }
}

export function setRefreshToken(token: string | null) {
  try {
    if (token) localStorage.setItem(REFRESH_KEY, token)
    else localStorage.removeItem(REFRESH_KEY)
  } catch {
    // Storage can be blocked (private mode); the session then lasts until reload.
  }
}
