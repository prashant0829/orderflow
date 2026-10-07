import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface CartItem {
  productId: string
  name: string
  /** Price shown in the shop. The server prices the order itself, so this is only an estimate. */
  unitPrice: number
  quantity: number
}

interface CartState {
  items: CartItem[]
  /** Reused if the same checkout is retried, so a double click or a retry never creates two orders. */
  checkoutKey: string | null
  add: (item: Omit<CartItem, 'quantity'>, quantity: number, maxQuantity: number) => void
  setQuantity: (productId: string, quantity: number, maxQuantity?: number) => void
  remove: (productId: string) => void
  clear: () => void
  ensureCheckoutKey: () => string
}

const newKey = () => crypto.randomUUID()

export const useCart = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      checkoutKey: null,

      add: (item, quantity, maxQuantity) =>
        set((state) => {
          const existing = state.items.find((line) => line.productId === item.productId)
          const wanted = Math.min((existing?.quantity ?? 0) + quantity, Math.max(maxQuantity, 0))
          if (wanted <= 0) return state

          const items = existing
            ? state.items.map((line) => (line.productId === item.productId ? { ...line, quantity: wanted, unitPrice: item.unitPrice } : line))
            : [...state.items, { ...item, quantity: wanted }]
          // A different basket is a different checkout, so it needs a new idempotency key.
          return { items, checkoutKey: null }
        }),

      setQuantity: (productId, quantity, maxQuantity) =>
        set((state) => {
          if (quantity <= 0) return { items: state.items.filter((line) => line.productId !== productId), checkoutKey: null }
          const capped = maxQuantity === undefined ? quantity : Math.min(quantity, maxQuantity)
          return { items: state.items.map((line) => (line.productId === productId ? { ...line, quantity: capped } : line)), checkoutKey: null }
        }),

      remove: (productId) => set((state) => ({ items: state.items.filter((line) => line.productId !== productId), checkoutKey: null })),

      clear: () => set({ items: [], checkoutKey: null }),

      ensureCheckoutKey: () => {
        const existing = get().checkoutKey
        if (existing) return existing
        const created = newKey()
        set({ checkoutKey: created })
        return created
      },
    }),
    { name: 'orderflow.cart', version: 1 },
  ),
)

export const cartCount = (items: CartItem[]) => items.reduce((sum, line) => sum + line.quantity, 0)

export const cartTotal = (items: CartItem[]) => items.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0)
