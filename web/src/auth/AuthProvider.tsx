import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi } from '../api/endpoints'
import { refreshSession, setSessionLostHandler } from '../api/client'
import { getRefreshToken } from '../api/tokens'
import type { User } from '../api/types'

type Status = 'loading' | 'authenticated' | 'anonymous'

interface AuthContextValue {
  user: User | null
  status: Status
  isAdmin: boolean
  login: (email: string, password: string) => Promise<User>
  register: (email: string, password: string, displayName: string) => Promise<User>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<User | null>(null)
  const [status, setStatus] = useState<Status>('loading')

  const clearSession = useCallback(() => {
    setUser(null)
    setStatus('anonymous')
    queryClient.clear()
  }, [queryClient])

  // Restore the session after a page reload using the stored refresh token.
  useEffect(() => {
    let cancelled = false

    async function restore() {
      if (!getRefreshToken() || !(await refreshSession())) {
        if (!cancelled) setStatus('anonymous')
        return
      }

      try {
        const me = await authApi.me()
        if (!cancelled) {
          setUser(me)
          setStatus('authenticated')
        }
      } catch {
        if (!cancelled) setStatus('anonymous')
      }
    }

    void restore()
    return () => {
      cancelled = true
    }
  }, [])

  // If a refresh fails mid-session (revoked, expired), drop back to the login page.
  useEffect(() => {
    setSessionLostHandler(clearSession)
  }, [clearSession])

  const signIn = useCallback((signedIn: User) => {
    setUser(signedIn)
    setStatus('authenticated')
    return signedIn
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      status,
      isAdmin: user?.role === 'Admin',
      login: async (email, password) => signIn(await authApi.login(email, password)),
      register: async (email, password, displayName) => signIn(await authApi.register(email, password, displayName)),
      logout: async () => {
        await authApi.logout(getRefreshToken())
        clearSession()
      },
    }),
    [user, status, signIn, clearSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
