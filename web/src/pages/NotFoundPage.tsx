import { Compass } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '../components/ui/Button'
import { EmptyState } from '../components/ui/primitives'

export function NotFoundPage() {
  return (
    <EmptyState
      icon={<Compass className="size-6" />}
      title="Page not found"
      description="That address does not exist."
      action={
        <Link to="/">
          <Button>Back to the shop</Button>
        </Link>
      }
    />
  )
}
