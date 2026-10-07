import { useQuery } from '@tanstack/react-query'
import { PackageSearch, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { catalogApi } from '../api/endpoints'
import { ProductCard } from '../components/catalog/ProductCard'
import { EmptyState, ErrorState, PageHeader, Skeleton } from '../components/ui/primitives'

export function CatalogPage() {
  const { data, isPending, isError, error, refetch } = useQuery({ queryKey: ['catalog'], queryFn: catalogApi.list, refetchInterval: 30_000 })
  const [search, setSearch] = useState('')

  const products = useMemo(
    () => (data ?? []).filter((product) => product.name.toLowerCase().includes(search.trim().toLowerCase())),
    [data, search],
  )

  return (
    <>
      <PageHeader
        title="Shop"
        description="Stock is checked and reserved the moment you order."
        actions={
          <label className="relative block">
            <span className="sr-only">Search products</span>
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted" />
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search products"
              className="h-10 w-full rounded-xl border border-line bg-surface pl-9 pr-3 text-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/25 sm:w-64"
            />
          </label>
        }
      />

      {isPending && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-80" />
          ))}
        </div>
      )}
      {isError && <ErrorState message={error instanceof Error ? error.message : 'Could not load the shop.'} onRetry={() => void refetch()} />}
      {data && products.length === 0 && (
        <EmptyState icon={<PackageSearch className="size-6" />} title="No products found" description={search ? `Nothing matches "${search}".` : 'The shop is empty right now.'} />
      )}
      {products.length > 0 && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {products.map((product) => (
            <ProductCard key={product.productId} product={product} />
          ))}
        </div>
      )}
    </>
  )
}
