import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import type { StockItem } from '../../api/types'
import { useCart } from '../../cart/cartStore'
import { ProductCard } from './ProductCard'

const product = (overrides: Partial<StockItem> = {}): StockItem => ({
  productId: 'p1',
  name: 'Mechanical Keyboard',
  unitPrice: 89.99,
  availableQuantity: 10,
  updatedAtUtc: '2026-10-07T10:00:00Z',
  ...overrides,
})

const renderCard = (item: StockItem) =>
  render(
    <MemoryRouter>
      <ProductCard product={item} />
    </MemoryRouter>,
  )

describe('ProductCard', () => {
  beforeEach(() => useCart.getState().clear())

  it('adds the chosen quantity to the cart', async () => {
    renderCard(product())

    await userEvent.click(screen.getByRole('button', { name: /increase quantity/i }))
    await userEvent.click(screen.getByRole('button', { name: /add to cart/i }))

    expect(useCart.getState().items).toEqual([{ productId: 'p1', name: 'Mechanical Keyboard', unitPrice: 89.99, quantity: 2 }])
  })

  it('warns when stock is low', () => {
    renderCard(product({ availableQuantity: 3 }))

    expect(screen.getByText('Only 3 left')).toBeInTheDocument()
  })

  it('cannot be bought when out of stock', () => {
    renderCard(product({ availableQuantity: 0 }))

    expect(screen.getByText('Out of stock')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /add to cart/i })).toBeDisabled()
  })

  it('does not let the customer add more than what is left', async () => {
    renderCard(product({ availableQuantity: 2 }))

    for (let click = 0; click < 5; click++) await userEvent.click(screen.getByRole('button', { name: /increase quantity/i }))
    await userEvent.click(screen.getByRole('button', { name: /add to cart/i }))

    expect(useCart.getState().items[0]?.quantity).toBe(2)
  })
})
