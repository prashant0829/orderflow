import { Minus, Plus, ShoppingBag } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { StockItem } from '../../api/types'
import { useCart } from '../../cart/cartStore'
import { useCartUi } from '../../cart/useCartUi'
import { formatMoney } from '../../lib/format'
import { Button } from '../ui/Button'
import { Badge, Card } from '../ui/primitives'
import { ProductArt } from './ProductArt'

const LOW_STOCK_THRESHOLD = 5

export function StockBadge({ available }: { available: number }) {
  if (available <= 0) return <Badge tone="danger">Out of stock</Badge>
  if (available <= LOW_STOCK_THRESHOLD) return <Badge tone="warning">Only {available} left</Badge>
  return <Badge tone="success">In stock</Badge>
}

export function ProductCard({ product }: { product: StockItem }) {
  const [quantity, setQuantity] = useState(1)
  const inCart = useCart((state) => state.items.find((line) => line.productId === product.productId)?.quantity ?? 0)
  const add = useCart((state) => state.add)
  const openCart = useCartUi((state) => state.setOpen)

  const remaining = Math.max(product.availableQuantity - inCart, 0)
  const soldOut = remaining === 0

  function addToCart() {
    add({ productId: product.productId, name: product.name, unitPrice: product.unitPrice }, quantity, product.availableQuantity)
    toast.success(`${quantity} × ${product.name} added`, { action: { label: 'View cart', onClick: () => openCart(true) } })
    setQuantity(1)
  }

  return (
    <Card role="article" aria-label={product.name} className="group flex flex-col overflow-hidden transition-shadow hover:shadow-lg hover:shadow-brand-500/10">
      <ProductArt name={product.name} className="aspect-[4/3] transition-transform duration-500 group-hover:scale-[1.03]" />
      <div className="flex flex-1 flex-col gap-3 p-4">
        <div className="flex items-start justify-between gap-2">
          <h3 className="font-semibold leading-snug">{product.name}</h3>
          <StockBadge available={product.availableQuantity} />
        </div>
        <p className="text-xl font-semibold tracking-tight">{formatMoney(product.unitPrice)}</p>

        <div className="mt-auto flex items-center gap-2">
          <div className="flex items-center rounded-xl border border-line">
            <button
              type="button"
              aria-label={`Decrease quantity of ${product.name}`}
              className="flex size-9 items-center justify-center text-muted hover:text-fg disabled:opacity-40"
              onClick={() => setQuantity((value) => Math.max(1, value - 1))}
              disabled={soldOut || quantity <= 1}
            >
              <Minus className="size-4" />
            </button>
            <span className="w-7 text-center text-sm tabular-nums" aria-live="polite">
              {soldOut ? 0 : Math.min(quantity, remaining)}
            </span>
            <button
              type="button"
              aria-label={`Increase quantity of ${product.name}`}
              className="flex size-9 items-center justify-center text-muted hover:text-fg disabled:opacity-40"
              onClick={() => setQuantity((value) => Math.min(remaining, value + 1))}
              disabled={soldOut || quantity >= remaining}
            >
              <Plus className="size-4" />
            </button>
          </div>
          <Button className="flex-1" onClick={addToCart} disabled={soldOut} icon={<ShoppingBag className="size-4" />}>
            {soldOut && inCart > 0 ? 'All in cart' : 'Add to cart'}
          </Button>
        </div>
      </div>
    </Card>
  )
}
