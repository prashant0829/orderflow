// Mirrors the JSON the backend returns (ASP.NET camelCase).

export type Role = 'Customer' | 'Admin'

export interface User {
  id: string
  email: string
  displayName: string
  role: Role
}

export interface AuthResult {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  user: User
}

export interface StockItem {
  productId: string
  name: string
  unitPrice: number
  availableQuantity: number
  updatedAtUtc: string
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Failed'

export interface OrderLine {
  productId: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface Order {
  id: string
  customerId: string
  status: OrderStatus
  totalAmount: number
  failureReason: string | null
  createdAtUtc: string
  updatedAtUtc: string
  lines: OrderLine[]
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

/** Where an order is in the saga (more detailed than the order status). */
export type ProgressState = 'Submitted' | 'AwaitingStock' | 'AwaitingPayment' | 'Confirmed' | 'Failed' | 'Cancelled'

export interface OrderProgress {
  orderId: string
  state: ProgressState
  failureReason: string | null
  paymentTransactionId: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface TimelineEntry {
  state: ProgressState
  message: string
  occurredAtUtc: string
}

/** Pushed over SignalR whenever an order moves forward. */
export interface StatusChange {
  orderId: string
  customerId: string
  state: ProgressState
  message: string
  occurredAtUtc: string
}

export interface AppNotification {
  id: string
  orderId: string
  type: string
  channel: string
  recipient: string
  subject: string
  body: string
  createdAtUtc: string
}

export interface Payment {
  id: string
  orderId: string
  customerId: string
  amount: number
  status: PaymentStatus
  transactionId: string | null
  providerPaymentId: string | null
  failureReason: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export type PaymentStatus = 'AwaitingCustomer' | 'Completed' | 'Failed' | 'Refunded' | 'Cancelled'

/** What the order page needs to take the customer's payment. The secret only exists while the customer still has to pay. */
export interface Checkout {
  status: PaymentStatus
  amount: number
  provider: string | null
  publishableKey: string | null
  clientSecret: string | null
  currency: string | null
}

export interface DeadLetterQueue {
  queue: string
  originalQueue: string
  messageCount: number
}

export interface DeadLetterMessage {
  messageId: string | null
  payload: string
  exceptionType: string | null
  exceptionMessage: string | null
  failedAtUtc: string | null
}

export interface ServiceHealth {
  service: string
  status: 'Healthy' | 'Unhealthy' | 'Unreachable'
  durationMs: number
  error: string | null
}

export interface DependencyHealth {
  status: 'Healthy' | 'Degraded'
  services: ServiceHealth[]
}
