import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Minus, Plus, ShoppingBag, Trash2, X } from 'lucide-react'
import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { catalogApi, ordersApi } from '../../api/endpoints'
import { ApiError } from '../../api/client'
import { cartTotal, useCart } from '../../cart/cartStore'
import { useCartUi } from '../../cart/useCartUi'
import { cn } from '../../lib/cn'
import { formatMoney } from '../../lib/format'
import { Button } from '../ui/Button'
import { EmptyState } from '../ui/primitives'
import { ProductArt } from '../catalog/ProductArt'

export function CartDrawer() {
  const { open, setOpen } = useCartUi()
  const { items, setQuantity, remove, clear, ensureCheckoutKey } = useCart()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const catalog = useQuery({ queryKey: ['catalog'], queryFn: catalogApi.list })

  const stockOf = (productId: string) => catalog.data?.find((product) => product.productId === productId)?.availableQuantity

  useEffect(() => {
    if (!open) return
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, setOpen])

  const placeOrder = useMutation({
    mutationFn: () =>
      ordersApi.place(
        items.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        ensureCheckoutKey(),
      ),
    onSuccess: (order) => {
      clear()
      setOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
      toast.success('Order placed', { description: 'Follow it live on the next screen.' })
      void navigate(`/orders/${order.id}`)
    },
    onError: (error) => {
      // The basket and its idempotency key are kept, so pressing the button again is a safe retry.
      toast.error(error instanceof ApiError ? error.message : 'Could not place the order. Please try again.')
    },
  })

  return (
    <>
      <div
        className={cn('fixed inset-0 z-40 bg-black/40 backdrop-blur-sm transition-opacity', open ? 'opacity-100' : 'pointer-events-none opacity-0')}
        onClick={() => setOpen(false)}
        aria-hidden
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Shopping cart"
        aria-hidden={!open}
        className={cn(
          'fixed inset-y-0 right-0 z-50 flex w-[min(100vw,26rem)] flex-col border-l border-line bg-surface shadow-2xl transition-transform duration-300',
          open ? 'translate-x-0' : 'translate-x-full',
        )}
      >
        <header className="flex items-center justify-between border-b border-line px-5 py-4">
          <h2 className="text-lg font-semibold">Your cart</h2>
          <Button variant="ghost" size="sm" aria-label="Close cart" onClick={() => setOpen(false)}>
            <X className="size-4" />
          </Button>
        </header>

        {items.length === 0 ? (
          <div className="flex flex-1 items-center p-5">
            <div className="w-full">
              <EmptyState
                icon={<ShoppingBag className="size-6" />}
                title="Your cart is empty"
                description="Add something from the shop and it will show up here."
                action={<Button onClick={() => setOpen(false)}>Keep shopping</Button>}
              />
            </div>
          </div>
        ) : (
          <>
            <ul className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
              {items.map((line) => {
                const max = stockOf(line.productId)
                return (
                  <li key={line.productId} className="flex gap-3">
                    <ProductArt name={line.name} className="size-16 shrink-0 rounded-xl" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium">{line.name}</p>
                      <p className="text-sm text-muted">{formatMoney(line.unitPrice)} each</p>
                      <div className="mt-2 flex items-center gap-2">
                        <div className="flex items-center rounded-lg border border-line">
                          <button type="button" aria-label={`Decrease ${line.name}`} className="flex size-7 items-center justify-center text-muted hover:text-fg" onClick={() => setQuantity(line.productId, line.quantity - 1, max)}>
                            <Minus className="size-3.5" />
                          </button>
                          <span className="w-6 text-center text-sm tabular-nums">{line.quantity}</span>
                          <button
                            type="button"
                            aria-label={`Increase ${line.name}`}
                            className="flex size-7 items-center justify-center text-muted hover:text-fg disabled:opacity-40"
                            disabled={max !== undefined && line.quantity >= max}
                            onClick={() => setQuantity(line.productId, line.quantity + 1, max)}
                          >
                            <Plus className="size-3.5" />
                          </button>
                        </div>
                        <button type="button" aria-label={`Remove ${line.name}`} className="rounded-lg p-1.5 text-muted hover:bg-rose-500/10 hover:text-rose-500" onClick={() => remove(line.productId)}>
                          <Trash2 className="size-4" />
                        </button>
                      </div>
                    </div>
                    <p className="font-medium tabular-nums">{formatMoney(line.quantity * line.unitPrice)}</p>
                  </li>
                )
              })}
            </ul>

            <footer className="space-y-3 border-t border-line px-5 py-4">
              <div className="flex items-center justify-between text-base font-semibold">
                <span>Estimated total</span>
                <span className="tabular-nums">{formatMoney(cartTotal(items))}</span>
              </div>
              <p className="text-xs text-muted">Prices are confirmed by the store when you place the order.</p>
              <Button size="lg" className="w-full" loading={placeOrder.isPending} onClick={() => placeOrder.mutate()}>
                Place order
              </Button>
              <Button variant="ghost" size="sm" className="w-full" onClick={clear}>
                Empty cart
              </Button>
            </footer>
          </>
        )}
      </aside>
    </>
  )
}
