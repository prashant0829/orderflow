import { Activity, Boxes, Package, ShieldCheck } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthProvider'
import { Button } from '../components/ui/Button'
import { Field } from '../components/ui/primitives'
import { ThemeToggle } from '../components/layout/ThemeToggle'

function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-2">
      <aside className="relative hidden overflow-hidden bg-gradient-to-br from-brand-700 via-brand-600 to-purple-600 p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="absolute -right-24 -top-24 size-96 rounded-full bg-white/10 blur-2xl" />
        <div className="absolute -bottom-32 -left-16 size-96 rounded-full bg-black/10 blur-2xl" />
        <div className="relative flex items-center gap-2 text-lg font-semibold">
          <span className="flex size-9 items-center justify-center rounded-xl bg-white/20">
            <Package className="size-5" />
          </span>
          OrderFlow
        </div>
        <div className="relative space-y-8">
          <h2 className="max-w-md text-4xl font-semibold leading-tight tracking-tight">Watch every order move, step by step.</h2>
          <ul className="space-y-4 text-white/85">
            {[
              [Activity, 'Live tracking', 'Updates arrive the moment stock, payment and confirmation happen.'],
              [Boxes, 'Safe by design', 'If a step fails, earlier steps are undone automatically.'],
              [ShieldCheck, 'Secure sessions', 'Short-lived tokens that rotate, with theft detection.'],
            ].map(([Icon, heading, text]) => {
              const I = Icon as typeof Activity
              return (
                <li key={heading as string} className="flex gap-3">
                  <I className="mt-0.5 size-5 shrink-0" />
                  <div>
                    <p className="font-medium text-white">{heading as string}</p>
                    <p className="text-sm">{text as string}</p>
                  </div>
                </li>
              )
            })}
          </ul>
        </div>
        <p className="relative text-sm text-white/60">Event-driven demo: .NET, RabbitMQ, SQL Server, Redis.</p>
      </aside>

      <main className="relative flex items-center justify-center p-6">
        <div className="absolute right-4 top-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm">
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          <p className="mb-8 mt-1 text-sm text-muted">{subtitle}</p>
          {children}
        </div>
      </main>
    </div>
  )
}

function useRedirectTarget() {
  const location = useLocation()
  return (location.state as { from?: string } | null)?.from ?? '/'
}

const errorMessage = (error: unknown) => (error instanceof ApiError ? error.message : 'Something went wrong. Please try again.')

export function LoginPage() {
  const { login, status } = useAuth()
  const navigate = useNavigate()
  const target = useRedirectTarget()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)

  if (status === 'authenticated') return <Navigate to={target} replace />

  async function submit(event: FormEvent, credentials = { email, password }) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await login(credentials.email, credentials.password)
      void navigate(target, { replace: true })
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  async function quickLogin(who: 'customer' | 'admin') {
    const credentials = who === 'admin' ? { email: 'admin@orderflow.local', password: 'Admin#12345' } : { email: 'customer@orderflow.local', password: 'Customer#12345' }
    setEmail(credentials.email)
    setPassword(credentials.password)
    await submit({ preventDefault() {} } as FormEvent, credentials)
  }

  return (
    <AuthLayout title="Welcome back" subtitle="Sign in to track your orders.">
      <form onSubmit={submit} className="space-y-4">
        <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(event) => setEmail(event.target.value)} />
        <Field label="Password" type="password" autoComplete="current-password" required value={password} onChange={(event) => setPassword(event.target.value)} />
        {error && (
          <p role="alert" className="rounded-xl bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-300">
            {error}
          </p>
        )}
        <Button type="submit" size="lg" className="w-full" loading={busy}>
          Sign in
        </Button>
      </form>

      <div className="my-6 flex items-center gap-3 text-xs text-muted">
        <span className="h-px flex-1 bg-line" /> Demo accounts <span className="h-px flex-1 bg-line" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Button variant="secondary" disabled={busy} onClick={() => void quickLogin('customer')}>
          Customer
        </Button>
        <Button variant="secondary" disabled={busy} onClick={() => void quickLogin('admin')}>
          Admin
        </Button>
      </div>

      <p className="mt-8 text-center text-sm text-muted">
        New here?{' '}
        <Link to="/register" className="font-medium text-brand-600 hover:underline dark:text-brand-300">
          Create an account
        </Link>
      </p>
    </AuthLayout>
  )
}

export function RegisterPage() {
  const { register, status } = useAuth()
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)

  if (status === 'authenticated') return <Navigate to="/" replace />

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await register(email, password, name)
      toast.success('Account created. Welcome!')
      void navigate('/', { replace: true })
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Create your account" subtitle="It takes a few seconds.">
      <form onSubmit={submit} className="space-y-4">
        <Field label="Name" autoComplete="name" value={name} onChange={(event) => setName(event.target.value)} />
        <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(event) => setEmail(event.target.value)} />
        <Field
          label="Password"
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          hint="At least 8 characters with upper case, lower case and a number."
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
        {error && (
          <p role="alert" className="rounded-xl bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-300">
            {error}
          </p>
        )}
        <Button type="submit" size="lg" className="w-full" loading={busy}>
          Create account
        </Button>
      </form>
      <p className="mt-8 text-center text-sm text-muted">
        Already registered?{' '}
        <Link to="/login" className="font-medium text-brand-600 hover:underline dark:text-brand-300">
          Sign in
        </Link>
      </p>
    </AuthLayout>
  )
}
