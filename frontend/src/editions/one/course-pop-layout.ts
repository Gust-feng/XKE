export interface CardBounds { left: number; top: number; width: number; height: number }
export interface Viewport { width: number; height: number }
export const POP_MARGIN = 24
export const POP_WIDTH = 360

/** Wide sidebar cards retain their width; narrow timetable cells can expand. */
export function popupWidth(source: CardBounds, viewport: Viewport, preferredWidth = POP_WIDTH) {
  return Math.min(Math.max(preferredWidth, source.width), Math.max(1, viewport.width - POP_MARGIN * 2))
}

export function popupBounds(source: CardBounds, viewport: Viewport, height: number, preferredWidth = POP_WIDTH): CardBounds {
  const width = popupWidth(source, viewport, preferredWidth)
  // A card touching the viewport edge must remain covered, even inside the usual margin.
  const sourceFits = source.left >= 0 && source.left + source.width <= viewport.width
  const leftLimit = sourceFits ? Math.min(POP_MARGIN, source.left) : POP_MARGIN
  const rightLimit = sourceFits ? Math.max(viewport.width - POP_MARGIN, source.left + source.width) : viewport.width - POP_MARGIN
  const preferredLeft = source.left + width <= rightLimit ? source.left : source.left + source.width - width
  const left = Math.max(leftLimit, Math.min(preferredLeft, rightLimit - width))
  const top = Math.max(POP_MARGIN, Math.min(source.top, viewport.height - POP_MARGIN - height))
  return { left, top, width, height }
}

export function cardBounds(rect: Pick<DOMRect, 'left' | 'top' | 'width' | 'height'>): CardBounds {
  return { left: rect.left, top: rect.top, width: rect.width, height: rect.height }
}
