import { AlertOctagon, Boxes, CreditCard, LayoutDashboard, ListOrdered } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'
import { PageHeader } from '../../components/ui/primitives'
import { cn } from '../../lib/cn'

const tabs = [
  { to: '/admin', label: 'Overview', icon: LayoutDashboard, end: true },
  { to: '/admin/orders', label: 'Orders', icon: ListOrdered },
  { to: '/admin/dead-letters', label: 'Failed messages', icon: AlertOctagon },
  { to: '/admin/payments', label: 'Payments', icon: CreditCard },
  { to: '/admin/inventory', label: 'Inventory', icon: Boxes },
]

export function AdminLayout() {
  return (
    <>
      <PageHeader title="Admin console" description="Health, orders, failed messages, payments and stock." />
      <nav className="-mx-1 mb-6 flex gap-1 overflow-x-auto px-1 pb-1" aria-label="Admin sections">
        {tabs.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              cn(
                'inline-flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-sm font-medium transition-colors',
                isActive ? 'bg-brand-600 text-white shadow-sm shadow-brand-600/30' : 'text-muted hover:bg-surface-2 hover:text-fg',
              )
            }
          >
            <Icon className="size-4" />
            {label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </>
  )
}
