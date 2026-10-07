import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { adminApi, catalogApi } from '../../api/endpoints'
import { ApiError } from '../../api/client'
import type { StockItem } from '../../api/types'
import { StockBadge } from '../../components/catalog/ProductCard'
import { ProductArt } from '../../components/catalog/ProductArt'
import { Button } from '../../components/ui/Button'
import { Card, ErrorState, Field, Skeleton } from '../../components/ui/primitives'
import { formatMoney } from '../../lib/format'

function RestockRow({ item }: { item: StockItem }) {
  const queryClient = useQueryClient()
  const [quantity, setQuantity] = useState(10)

  const restock = useMutation({
    mutationFn: () => adminApi.restock(item.productId, quantity),
    onSuccess: () => {
      toast.success(`Added ${quantity} × ${item.name}`)
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not restock.'),
  })

  return (
    <tr className="hover:bg-surface-2/60">
      <td className="px-4 py-3">
        <div className="flex items-center gap-3">
          <ProductArt name={item.name} className="size-10 rounded-lg" />
          <span className="font-medium">{item.name}</span>
        </div>
      </td>
      <td className="px-4 py-3 tabular-nums">{formatMoney(item.unitPrice)}</td>
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="w-10 tabular-nums">{item.availableQuantity}</span>
          <StockBadge available={item.availableQuantity} />
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="flex items-center justify-end gap-2">
          <input
            type="number"
            min={1}
            max={1000}
            value={quantity}
            aria-label={`Quantity to add for ${item.name}`}
            onChange={(event) => setQuantity(Math.max(1, Number(event.target.value) || 1))}
            className="h-9 w-20 rounded-lg border border-line bg-surface px-2 text-sm"
          />
          <Button size="sm" variant="secondary" loading={restock.isPending} onClick={() => restock.mutate()}>
            Restock
          </Button>
        </div>
      </td>
    </tr>
  )
}

export function AdminInventory() {
  const queryClient = useQueryClient()
  const { data, isPending, isError, refetch } = useQuery({ queryKey: ['catalog'], queryFn: catalogApi.list })
  const [name, setName] = useState('')
  const [price, setPrice] = useState('')
  const [initial, setInitial] = useState('10')

  const create = useMutation({
    mutationFn: () => adminApi.createProduct(name, Number(price), Number(initial)),
    onSuccess: (product) => {
      toast.success(`${product.name} added to the shop`)
      setName('')
      setPrice('')
      setInitial('10')
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not create the product.'),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate()
  }

  return (
    <div className="space-y-6">
      <Card className="p-5">
        <h2 className="mb-4 font-semibold">New product</h2>
        <form onSubmit={submit} className="grid items-end gap-4 sm:grid-cols-[1fr_9rem_9rem_auto]">
          <Field label="Name" required maxLength={200} value={name} onChange={(event) => setName(event.target.value)} />
          <Field label="Price" type="number" min={0} step="0.01" required value={price} onChange={(event) => setPrice(event.target.value)} />
          <Field label="Initial stock" type="number" min={0} required value={initial} onChange={(event) => setInitial(event.target.value)} />
          <Button type="submit" loading={create.isPending} icon={<Plus className="size-4" />}>
            Add product
          </Button>
        </form>
      </Card>

      {isPending && <Skeleton className="h-72" />}
      {isError && <ErrorState message="Could not load the catalog." onRetry={() => void refetch()} />}
      {data && (
        <Card className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-line text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3 font-medium">Product</th>
                <th className="px-4 py-3 font-medium">Price</th>
                <th className="px-4 py-3 font-medium">In stock</th>
                <th className="px-4 py-3 text-right font-medium">Add stock</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {data.map((item) => (
                <RestockRow key={item.productId} item={item} />
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
