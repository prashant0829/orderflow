import { beforeEach, describe, expect, it } from 'vitest'
import { cartCount, cartTotal, useCart } from './cartStore'

const keyboard = { productId: 'p1', name: 'Keyboard', unitPrice: 89.99 }
const mouse = { productId: 'p2', name: 'Mouse', unitPrice: 39.5 }

describe('cart', () => {
  beforeEach(() => useCart.getState().clear())

  it('adds new items and merges repeats of the same product', () => {
    useCart.getState().add(keyboard, 1, 10)
    useCart.getState().add(keyboard, 2, 10)

    expect(useCart.getState().items).toEqual([{ ...keyboard, quantity: 3 }])
  })

  it('never lets the quantity pass what is in stock', () => {
    useCart.getState().add(keyboard, 4, 5)
    useCart.getState().add(keyboard, 4, 5)

    expect(useCart.getState().items[0]?.quantity).toBe(5)
  })

  it('ignores adding something that is out of stock', () => {
    useCart.getState().add(keyboard, 1, 0)

    expect(useCart.getState().items).toHaveLength(0)
  })

  it('removes a line when its quantity drops to zero', () => {
    useCart.getState().add(keyboard, 2, 10)
    useCart.getState().setQuantity('p1', 0)

    expect(useCart.getState().items).toHaveLength(0)
  })

  it('totals quantity and price across lines', () => {
    useCart.getState().add(keyboard, 2, 10)
    useCart.getState().add(mouse, 1, 10)
    const { items } = useCart.getState()

    expect(cartCount(items)).toBe(3)
    expect(cartTotal(items)).toBeCloseTo(2 * 89.99 + 39.5)
  })

  it('keeps the same idempotency key for a retried checkout', () => {
    useCart.getState().add(keyboard, 1, 10)

    const first = useCart.getState().ensureCheckoutKey()
    const retry = useCart.getState().ensureCheckoutKey()

    expect(retry).toBe(first)
  })

  it('creates a new idempotency key once the basket changes', () => {
    useCart.getState().add(keyboard, 1, 10)
    const first = useCart.getState().ensureCheckoutKey()

    useCart.getState().add(mouse, 1, 10)

    expect(useCart.getState().ensureCheckoutKey()).not.toBe(first)
  })
})
