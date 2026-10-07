import { useQuery } from '@tanstack/react-query'
import { Bell, LayoutDashboard, LogOut, Package, ShoppingBag, Store } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'
import { notificationsApi } from '../../api/endpoints'
import { useAuth } from '../../auth/AuthProvider'
import { cartCount, useCart } from '../../cart/cartStore'
import { useCartUi } from '../../cart/useCartUi'
import { cn } from '../../lib/cn'
import { initials } from '../../lib/format'
import { Button } from '../ui/Button'
import { CartDrawer } from './CartDrawer'
import { ConnectionIndicator } from './ConnectionIndicator'
import { ThemeToggle } from './ThemeToggle'
import { useUnreadNotifications } from './useUnreadNotifications'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
    isActive ? 'bg-brand-500/10 text-brand-600 dark:text-brand-300' : 'text-muted hover:bg-surface-2 hover:text-fg',
  )

export function AppShell() {
  const { user, isAdmin, logout } = useAuth()
  const itemCount = useCart((state) => cartCount(state.items))
  const setCartOpen = useCartUi((state) => state.setOpen)
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: () => notificationsApi.list(), enabled: !!user })
  const unread = useUnreadNotifications(notifications.data)

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-line bg-app/80 backdrop-blur-lg">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-2 px-3 sm:gap-4 sm:px-4">
          <NavLink to="/" className="flex items-center gap-2 font-semibold tracking-tight">
            <span className="flex size-8 items-center justify-center rounded-lg bg-gradient-to-br from-brand-500 to-purple-500 text-white">
              <Package className="size-4" />
            </span>
            <span className="hidden sm:inline">OrderFlow</span>
          </NavLink>

          <nav className="flex flex-1 items-center gap-1" aria-label="Main">
            <NavLink to="/" end className={linkClass}>
              <Store className="size-4" />
              <span className="hidden sm:inline">Shop</span>
            </NavLink>
            <NavLink to="/orders" className={linkClass}>
              <ShoppingBag className="size-4" />
              <span className="hidden sm:inline">My orders</span>
            </NavLink>
            <NavLink to="/notifications" className={linkClass}>
              <span className="relative">
                <Bell className="size-4" />
                {unread > 0 && (
                  <span className="absolute -right-1.5 -top-1.5 flex size-4 items-center justify-center rounded-full bg-rose-500 text-[10px] font-bold text-white">
                    {unread > 9 ? '9+' : unread}
                  </span>
                )}
              </span>
              <span className="hidden sm:inline">Inbox</span>
            </NavLink>
            {isAdmin && (
              <NavLink to="/admin" className={linkClass}>
                <LayoutDashboard className="size-4" />
                <span className="hidden sm:inline">Admin</span>
              </NavLink>
            )}
          </nav>

          <span className="hidden md:inline-flex">
            <ConnectionIndicator />
          </span>
          <ThemeToggle />
          <Button variant="secondary" size="sm" className="relative" aria-label={`Open cart, ${itemCount} items`} onClick={() => setCartOpen(true)}>
            <ShoppingBag className="size-4" />
            {itemCount > 0 && (
              <span className="flex min-w-5 items-center justify-center rounded-full bg-brand-600 px-1.5 text-xs font-semibold text-white">{itemCount}</span>
            )}
          </Button>

          <details className="relative">
            <summary
              role="button"
              className="flex size-9 cursor-pointer list-none items-center justify-center rounded-full bg-gradient-to-br from-brand-500 to-purple-500 text-xs font-semibold text-white"
              aria-label="Account menu"
            >
              {initials(user?.displayName ?? user?.email ?? '?')}
            </summary>
            <div className="absolute right-0 mt-2 w-60 rounded-xl border border-line bg-surface p-2 shadow-xl">
              <div className="px-3 py-2">
                <p className="truncate text-sm font-medium">{user?.displayName}</p>
                <p className="truncate text-xs text-muted">{user?.email}</p>
                <p className="mt-1 text-xs text-brand-600 dark:text-brand-300">{user?.role}</p>
              </div>
              <button
                type="button"
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted hover:bg-surface-2 hover:text-fg"
                onClick={() => void logout()}
              >
                <LogOut className="size-4" /> Sign out
              </button>
            </div>
          </details>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>

      <CartDrawer />
    </div>
  )
}
