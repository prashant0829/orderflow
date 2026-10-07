import { Cable, Keyboard, Layers, Monitor, Mouse, Package, type LucideIcon } from 'lucide-react'
import { cn } from '../../lib/cn'

const icons: [RegExp, LucideIcon][] = [
  [/keyboard/i, Keyboard],
  [/mouse/i, Mouse],
  [/monitor|display|screen/i, Monitor],
  [/dock|hub|cable|usb/i, Cable],
  [/mat|pad|desk/i, Layers],
]

export const iconFor = (name: string): LucideIcon => icons.find(([pattern]) => pattern.test(name))?.[1] ?? Package

/** The catalog has no photos, so each product gets a stable gradient and icon derived from its name. */
export function hueFor(name: string): number {
  let hash = 0
  for (const character of name) hash = (hash * 31 + character.charCodeAt(0)) % 360
  return hash
}

export function ProductArt({ name, className }: { name: string; className?: string }) {
  const Icon = iconFor(name)
  const hue = hueFor(name)

  return (
    <div
      className={cn('relative flex items-center justify-center overflow-hidden', className)}
      style={{ background: `linear-gradient(135deg, hsl(${hue} 80% 62%), hsl(${(hue + 50) % 360} 75% 48%))` }}
      aria-hidden
    >
      <div className="absolute -right-6 -top-6 size-28 rounded-full bg-white/15 blur-sm" />
      <div className="absolute -bottom-8 -left-4 size-24 rounded-full bg-black/10" />
      <Icon className="relative size-1/3 min-h-8 min-w-8 text-white drop-shadow-md" strokeWidth={1.6} />
    </div>
  )
}
