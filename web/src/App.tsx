import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Toaster } from 'sonner'
import { ApiError } from './api/client'
import { AuthProvider } from './auth/AuthProvider'
import { RequireAdmin, RequireAuth } from './auth/RequireAuth'
import { AppShell } from './components/layout/AppShell'
import { AdminDeadLetters } from './pages/admin/AdminDeadLetters'
import { AdminInventory } from './pages/admin/AdminInventory'
import { AdminLayout } from './pages/admin/AdminLayout'
import { AdminOrders } from './pages/admin/AdminOrders'
import { AdminOverview } from './pages/admin/AdminOverview'
import { AdminPayments } from './pages/admin/AdminPayments'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { CatalogPage } from './pages/CatalogPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { NotificationsPage } from './pages/NotificationsPage'
import { OrderDetailPage } from './pages/OrderDetailPage'
import { OrdersPage } from './pages/OrdersPage'
import { RealtimeProvider } from './realtime/RealtimeProvider'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5_000,
      refetchOnWindowFocus: true,
      // Retrying a 4xx never helps (not found, forbidden, rate limited); network and 5xx errors get two quiet retries.
      retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <RealtimeProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />

              <Route element={<RequireAuth />}>
                <Route element={<AppShell />}>
                  <Route index element={<CatalogPage />} />
                  <Route path="orders" element={<OrdersPage />} />
                  <Route path="orders/:id" element={<OrderDetailPage />} />
                  <Route path="notifications" element={<NotificationsPage />} />

                  <Route element={<RequireAdmin />}>
                    <Route path="admin" element={<AdminLayout />}>
                      <Route index element={<AdminOverview />} />
                      <Route path="orders" element={<AdminOrders />} />
                      <Route path="dead-letters" element={<AdminDeadLetters />} />
                      <Route path="payments" element={<AdminPayments />} />
                      <Route path="inventory" element={<AdminInventory />} />
                    </Route>
                  </Route>

                  <Route path="home" element={<Navigate to="/" replace />} />
                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Route>
            </Routes>
          </RealtimeProvider>
        </AuthProvider>
      </BrowserRouter>
      <Toaster position="bottom-right" richColors closeButton theme="system" />
    </QueryClientProvider>
  )
}
