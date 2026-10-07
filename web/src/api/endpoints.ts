import { request, storeSession } from './client'
import { setAccessToken, setRefreshToken } from './tokens'
import type {
  AppNotification,
  AuthResult,
  DeadLetterMessage,
  DeadLetterQueue,
  DependencyHealth,
  Order,
  OrderProgress,
  OrderStatus,
  Paged,
  Payment,
  StockItem,
  TimelineEntry,
  User,
} from './types'

const v1 = '/api/v1'

export const authApi = {
  async login(email: string, password: string) {
    const result = await request<AuthResult>(`${v1}/auth/login`, { method: 'POST', body: { email, password }, auth: false })
    storeSession(result)
    return result.user
  },
  async register(email: string, password: string, displayName: string) {
    const result = await request<AuthResult>(`${v1}/auth/register`, { method: 'POST', body: { email, password, displayName }, auth: false })
    storeSession(result)
    return result.user
  },
  async logout(refreshToken: string | null) {
    try {
      if (refreshToken) await request<void>(`${v1}/auth/logout`, { method: 'POST', body: { refreshToken }, auth: false })
    } finally {
      setAccessToken(null)
      setRefreshToken(null)
    }
  },
  me: () => request<User>(`${v1}/auth/me`),
}

export const catalogApi = {
  list: () => request<StockItem[]>(`${v1}/stock`, { auth: false }),
}

export interface PlaceOrderLine {
  productId: string
  quantity: number
}

export const ordersApi = {
  /** The Idempotency-Key makes a retried click return the same order instead of creating a second one. */
  place: (lines: PlaceOrderLine[], idempotencyKey: string) =>
    request<Order>(`${v1}/orders`, { method: 'POST', body: { lines }, headers: { 'Idempotency-Key': idempotencyKey } }),
  get: (id: string) => request<Order>(`${v1}/orders/${id}`),
  list: (page: number, pageSize = 10) => request<Paged<Order>>(`${v1}/orders?page=${page}&pageSize=${pageSize}`),
  progress: (id: string) => request<OrderProgress>(`${v1}/orders/${id}/progress`),
  cancel: (id: string, reason: string) => request<Order>(`${v1}/orders/${id}/cancel`, { method: 'POST', body: { reason } }),
}

export const notificationsApi = {
  list: (count = 50) => request<AppNotification[]>(`${v1}/notifications?count=${count}`),
  timeline: (orderId: string) => request<TimelineEntry[]>(`${v1}/timeline/${orderId}`),
}

export const adminApi = {
  health: () => request<DependencyHealth>('/health/dependencies'),
  orders: (status: OrderStatus | undefined, page: number, pageSize = 10) =>
    request<Paged<Order>>(`${v1}/admin/orders?page=${page}&pageSize=${pageSize}${status ? `&status=${status}` : ''}`),
  payments: (count = 50) => request<Payment[]>(`${v1}/payments?count=${count}`),
  deadLetters: () => request<DeadLetterQueue[]>(`${v1}/admin/dead-letters`),
  peekDeadLetters: (queue: string) => request<DeadLetterMessage[]>(`${v1}/admin/dead-letters/${encodeURIComponent(queue)}/messages?count=10`),
  replayDeadLetters: (queue: string) =>
    request<{ queue: string; replayed: number }>(`${v1}/admin/dead-letters/${encodeURIComponent(queue)}/replay`, { method: 'POST' }),
  createProduct: (name: string, unitPrice: number, initialQuantity: number) =>
    request<StockItem>(`${v1}/stock`, { method: 'POST', body: { name, unitPrice, initialQuantity } }),
  restock: (productId: string, quantity: number) =>
    request<StockItem>(`${v1}/stock/${productId}/restock`, { method: 'POST', body: { quantity } }),
}
