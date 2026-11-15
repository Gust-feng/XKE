import { useEffect, useLayoutEffect, useRef } from 'react'

type Layer = { panel(): HTMLElement | null; close(): void; returnTo: HTMLElement | null }
const layers: Layer[] = []
let previousOverflow = ''
export function hasOverlay() { return layers.length > 0 }
function fields(panel: HTMLElement) {
  return Array.from(panel.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]'))
    .filter((node) => node.tabIndex >= 0 && !node.closest('[inert]') && node.getClientRects().length > 0)
}
function keydown(event: KeyboardEvent) {
  const layer = layers.at(-1)
  if (!layer) return
  if (event.key === 'Escape') {
    event.preventDefault(); event.stopImmediatePropagation(); layer.close(); return
  }
  if (event.key !== 'Tab') return
  const panel = layer.panel()
  if (!panel) return
  const focusable = fields(panel), first = focusable[0], last = focusable.at(-1)
  if (!first || !last) { event.preventDefault(); panel.focus(); return }
  if (!panel.contains(document.activeElement) || document.activeElement === panel) {
    event.preventDefault(); (event.shiftKey ? last : first).focus()
  } else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
}
function focusin(event: FocusEvent) {
  const panel = layers.at(-1)?.panel()
  if (panel && !panel.contains(event.target as Node)) (fields(panel)[0] || panel).focus({ preventScroll: true })
}

/** All modal surfaces share keyboard ownership and restore the actual opening control. */
export function useOverlay(open: boolean, panel: () => HTMLElement | null, close: () => void) {
  const latest = useRef({ panel, close })
  useLayoutEffect(() => { latest.current = { panel, close } }, [panel, close])
  useEffect(() => {
    if (!open) return
    const layer: Layer = { panel: () => latest.current.panel(), close: () => latest.current.close(),
      returnTo: document.activeElement instanceof HTMLElement ? document.activeElement : null }
    if (!layers.length) {
      previousOverflow = document.body.style.overflow
      document.body.style.overflow = 'hidden'
      addEventListener('keydown', keydown, true); document.addEventListener('focusin', focusin)
    }
    layers.push(layer)
    const frame = requestAnimationFrame(() => {
      const node = layer.panel()
      if (node && layers.at(-1) === layer) {
        if (!(node instanceof HTMLDialogElement)) node.style.zIndex = String(100 + layers.length * 2)
        if (!node.contains(document.activeElement)) (fields(node)[0] || node).focus({ preventScroll: true })
      }
    })
    return () => {
      cancelAnimationFrame(frame)
      const wasTop = layers.at(-1) === layer
      const index = layers.indexOf(layer)
      if (index >= 0) layers.splice(index, 1)
      if (!layers.length) {
        document.body.style.overflow = previousOverflow
        removeEventListener('keydown', keydown, true); document.removeEventListener('focusin', focusin)
      }
      if (wasTop && layer.returnTo?.isConnected) layer.returnTo.focus({ preventScroll: true })
    }
  }, [open])
}
