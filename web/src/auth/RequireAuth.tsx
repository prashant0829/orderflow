import { ShieldAlert } from 'lucide-react'
import { Link, Navigate, Outlet, useLocation } from 'react-router-dom'
import { Button } from '../components/ui/Button'
import { EmptyState, Spinner } from '../components/ui/primitives'
import { useAuth } from './AuthProvider'

export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'loading') return <Spinner label="Signing you in" />
  if (status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}

/** The server enforces admin access; this only keeps customers from seeing a page full of errors. */
export function RequireAdmin() {
  const { isAdmin } = useAuth()
  if (isAdmin) return <Outlet />

  return (
    <EmptyState
      icon={<ShieldAlert className="size-6" />}
      title="Admins only"
      description="Your account does not have access to this area."
      action={
        <Link to="/">
          <Button>Back to the shop</Button>
        </Link>
      }
    />
  )
}
