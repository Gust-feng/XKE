import { flushSync } from 'react-dom'

export type ThemeId = 'night' | 'paper' | 'mono' | 'dusk'
export const LAYOUTS = [{ id: 'one', name: '合成' }] as const
export const isEdition = (layout: string) => layout === 'one'

export function withTransition(fn: () => void) {
  if (document.startViewTransition && !matchMedia('(prefers-reduced-motion: reduce)').matches) {
    document.startViewTransition(() => flushSync(fn))
  } else fn()
}

export function applyTheme(id: ThemeId) { document.documentElement.dataset.theme = id }

/** The v7 edition supplies its own palette; no alternate design registry. */
export function bootTheme() {
  document.documentElement.dataset.edition = 'one'
  document.documentElement.dataset.theme = 'night'
}
