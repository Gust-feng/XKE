import { animate, useReducedMotion } from 'motion/react'
import { createPortal } from 'react-dom'
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode, type KeyboardEvent as ReactKeyEvent } from 'react'
import { cardBounds, popupBounds, popupWidth, POP_MARGIN, POP_WIDTH } from './course-pop-layout'
import { captureCourseText, prepareCourseText, sourceTextStyle, sourceGhostStyle, targetTextStyle, targetGhostStyle, refreshSourceText, placeCourseText, resetCourseText, type CourseTextFrame, type SharedCourseText } from './course-pop-motion'

type Origin = { rect: DOMRect; background: string; dark: boolean; element: HTMLElement | null; text: CourseTextFrame[]; shadow: string }
type Item = { key: number; id: string; label: string; content: ReactNode; origin: Origin; closing: boolean }
const MORPH = { duration: 0.46, ease: [0.32, 0.72, 0, 1] } as const
const BACK = { duration: 0.28, ease: [0.4, 0, 0.2, 1] } as const

interface CardOptions {
  preferredWidth?: number; dynamic?: boolean; detailsSelector?: string; className?: string; popupId?: string; manageFocus?: boolean
}
interface CardPopoverProps extends CardOptions {
  open: boolean; identity: string; label: string; source: string; onClose(): void; children: ReactNode
}
interface SurfaceProps extends Required<Pick<CardOptions, 'preferredWidth' | 'dynamic' | 'detailsSelector' | 'className' | 'manageFocus'>> {
  item: Item; reduce: boolean; onGone(key: number): void; popupId?: string
}

function containFormFocus(event: ReactKeyEvent<HTMLDivElement>) {
  if (event.key !== 'Tab') return
  const panel = event.currentTarget
  const fields = Array.from(panel.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]'))
    .filter((field) => field.tabIndex >= 0 && field.getClientRects().length > 0)
  const first = fields[0], last = fields.at(-1)
  if (!first || !last) { event.preventDefault(); panel.focus(); return }
  if (document.activeElement === panel || !panel.contains(document.activeElement)) {
    event.preventDefault(); (event.shiftKey ? last : first).focus()
  } else if (event.shiftKey && document.activeElement === first) {
    event.preventDefault(); last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault(); first.focus()
  }
}

function readOrigin(element: HTMLElement | null): Origin | null {
  if (!element) return null
  const styles = getComputedStyle(element)
  const transparent = styles.backgroundColor === 'rgba(0, 0, 0, 0)' || styles.backgroundColor === 'transparent'
  const paper = getComputedStyle(document.documentElement).getPropertyValue('--paper').trim() || '#f2f1ec'
  const rgb = styles.backgroundColor.match(/\d+(?:\.\d+)?/g)?.map(Number)
  return { rect: element.getBoundingClientRect(), background: transparent ? paper : styles.backgroundColor,
    dark: !transparent && (!rgb || .299 * rgb[0] + .587 * rgb[1] + .114 * rgb[2] < 150),
    element, text: captureCourseText(element), shadow: styles.boxShadow }
}

/** One reversible source-card transition for course details and editable network settings. */
export function CardPopover({ open, identity, label, source, onClose, children, preferredWidth = POP_WIDTH,
  dynamic = false, detailsSelector = '[data-pop-detail], .one-pop-c-x', className = '', popupId, manageFocus = false }: CardPopoverProps) {
  const reduce = !!useReducedMotion()
  const nextOrigin = useRef<Origin | null>(null)
  const [items, setItems] = useState<Item[]>([])
  const [bare, setBare] = useState(false)
  const seq = useRef(0)
  const content = useRef({ label, children })
  useLayoutEffect(() => { content.current = { label, children } }, [label, children])
  useEffect(() => {
    const remember = (event: PointerEvent) => { nextOrigin.current = readOrigin((event.target as HTMLElement).closest(source)) }
    addEventListener('pointerdown', remember, true)
    return () => removeEventListener('pointerdown', remember, true)
  }, [source])
  useLayoutEffect(() => {
    const origin = nextOrigin.current || (manageFocus && typeof document !== 'undefined' ? readOrigin((document.activeElement as HTMLElement | null)?.closest(source) ?? null) : null)
    nextOrigin.current = null
    setBare(open && !origin)
    setItems((old) => {
      if (open && old.some((item) => !item.closing && item.id === identity)) return old
      const rest = old.map((item) => item.closing ? item : { ...item, closing: true })
      return !open || !origin ? rest : [...rest, { key: ++seq.current, id: identity, label: content.current.label, content: content.current.children, origin, closing: false }]
    })
  }, [open, identity, source, manageFocus])
  useEffect(() => {
    if (!open) return
    const closeOnBlank = (event: PointerEvent) => {
      const target = event.target as HTMLElement
      if (target.closest('.one-pop-c, .one-pop-c-static') || target.closest(source)) return
      onClose()
    }
    const key = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    addEventListener('pointerdown', closeOnBlank); addEventListener('keydown', key)
    if (!dynamic) addEventListener('resize', onClose)
    return () => { removeEventListener('pointerdown', closeOnBlank); removeEventListener('keydown', key); removeEventListener('resize', onClose) }
  }, [open, source, onClose, dynamic])
  const drop = useCallback((key: number) => setItems((old) => old.filter((item) => item.key !== key)), [])
  const server = typeof document === 'undefined' || !document.body
  const layers = <>
    {open && (bare || server) && <div id={popupId} className={['one-pop-c-static', className].filter(Boolean).join(' ')} role="dialog" aria-label={label} tabIndex={manageFocus ? -1 : undefined} onKeyDown={manageFocus ? containFormFocus : undefined}>
      <div className="one-pop-in">{children}</div>
    </div>}
    {items.map((item) => <MorphCard key={item.key} item={item} reduce={reduce} onGone={drop}
      preferredWidth={preferredWidth} dynamic={dynamic} detailsSelector={detailsSelector} className={className} popupId={popupId} manageFocus={manageFocus} />)}
  </>
  return typeof document !== 'undefined' && document.body ? createPortal(layers, document.body) : layers
}

function MorphCard({ item, reduce, onGone, preferredWidth, dynamic, detailsSelector, className, popupId, manageFocus }: SurfaceProps) {
  const box = useRef<HTMLDivElement>(null)
  const inner = useRef<HTMLDivElement>(null)
  const shared = useRef<SharedCourseText[]>([])
  const additional = useRef<HTMLElement[]>([])
  const running = useRef<{ stop(): void }[]>([])
  const isClosing = useRef(item.closing)
  const { origin, closing } = item
  useLayoutEffect(() => { isClosing.current = closing }, [closing])
  // Restore the source in the same commit that removes this layer, before paint.
  useLayoutEffect(() => {
    const element = box.current
    return () => {
      if (origin.element?.getAttribute('data-pop') === String(item.key)) origin.element.removeAttribute('data-pop')
      if (manageFocus && origin.element?.isConnected && (document.activeElement === document.body || element?.contains(document.activeElement)))
        origin.element.focus({ preventScroll: true })
    }
  }, [origin, manageFocus, item.key])

  useLayoutEffect(() => {
    const element = box.current, content = inner.current
    if (!element || !content) return
    shared.current.forEach(resetCourseText)
    additional.current.forEach((node) => { node.style.removeProperty('opacity'); node.style.removeProperty('transform') })
    const source = cardBounds(origin.rect)
    const viewport = { width: innerWidth, height: innerHeight }
    const width = popupWidth(source, viewport, preferredWidth)
    element.style.width = content.style.width = width + 'px'
    element.style.removeProperty('height')
    element.style.removeProperty('box-shadow')
    content.style.removeProperty('overflow')
    const height = Math.min(element.offsetHeight, innerHeight - POP_MARGIN * 2)
    let target = popupBounds(source, viewport, height, preferredWidth)
    const place = (bounds: typeof target) => Object.assign(element.style, {
      left: bounds.left + 'px', top: bounds.top + 'px', width: bounds.width + 'px', height: bounds.height + 'px',
    })
    place(target)
    const shadow = getComputedStyle(element).boxShadow
    const scrollable = content.scrollHeight > content.clientHeight + 1
    origin.element?.setAttribute('data-pop', String(item.key))
    const measureTarget = () => popupBounds(source, { width: innerWidth, height: innerHeight }, Math.min(content.offsetHeight, innerHeight - POP_MARGIN * 2), preferredWidth)
    if (reduce) {
      if (manageFocus) element.focus({ preventScroll: true })
      if (!dynamic) return
      const fit = () => {
        content.style.width = popupWidth(source, { width: innerWidth, height: innerHeight }, preferredWidth) + 'px'
        place(measureTarget())
      }
      const observer = new ResizeObserver(fit)
      observer.observe(content); addEventListener('resize', fit)
      return () => { observer.disconnect(); removeEventListener('resize', fit) }
    }
    content.style.overflow = 'visible'
    const labels = prepareCourseText(content, origin.text, target)
    shared.current = labels
    const details = Array.from(content.querySelectorAll<HTMLElement>(detailsSelector))
    additional.current = details
    for (const label of labels) {
      placeCourseText(label, sourceTextStyle(label, label.source, source))
      Object.assign(label.ghost.style, sourceGhostStyle(label.source, source))
    }
    for (const node of details) Object.assign(node.style, { opacity: '.35', transform: dynamic ? 'translateY(8px)' : 'translateY(8px) scale(.96)', transformOrigin: '0 0' })
    place(source)
    element.style.boxShadow = origin.shadow
    let cancelled = false, textDone = false, surfaceDone = false, generation = 0
    const controls = labels.flatMap((label) => [animate(label.element, targetTextStyle(), MORPH), animate(label.ghost, targetGhostStyle(label), MORPH)])
    controls.push(...details.map((node) => animate(node, { opacity: 1, transform: 'translateY(0px) scale(1)' }, MORPH)))
    running.current = controls
    const settle = () => {
      if (cancelled || isClosing.current || !textDone || !surfaceDone) return
      controls.forEach((control) => control.stop())
      place(target)
      element.style.boxShadow = shadow
      labels.forEach((label) => { placeCourseText(label, targetTextStyle()); Object.assign(label.ghost.style, targetGhostStyle(label)) })
      details.forEach((node) => Object.assign(node.style, { opacity: '1', transform: 'translateY(0px) scale(1)' }))
      if (!dynamic && scrollable) content.style.overflow = 'auto'
      if (manageFocus && document.activeElement === document.body) element.focus({ preventScroll: true })
    }
    let surface: { stop(): void } | undefined
    const grow = () => {
      surface?.stop()
      surfaceDone = false
      const current = ++generation
      const control = animate(element, { ...target, boxShadow: shadow }, MORPH)
      surface = control; controls.push(control)
      Promise.resolve(control).then(() => { if (!cancelled && current === generation) { surfaceDone = true; settle() } })
    }
    Promise.all([...controls]).then(() => { textDone = true; settle() })
    grow()
    const fit = () => {
      if (cancelled || isClosing.current) return
      content.style.width = popupWidth(source, { width: innerWidth, height: innerHeight }, preferredWidth) + 'px'
      const next = measureTarget()
      if (Math.abs(next.height - target.height) < .5 && Math.abs(next.top - target.top) < .5 && Math.abs(next.width - target.width) < .5 && Math.abs(next.left - target.left) < .5) return
      target = next
      grow()
    }
    const observer = dynamic ? new ResizeObserver(fit) : null
    observer?.observe(content)
    if (dynamic) addEventListener('resize', fit)
    return () => { cancelled = true; observer?.disconnect(); removeEventListener('resize', fit); controls.forEach((control) => control.stop()) }
  }, [origin, reduce, preferredWidth, dynamic, detailsSelector, manageFocus, item.key])

  useEffect(() => {
    if (!closing) return
    running.current.forEach((control) => control.stop())
    const element = box.current, content = inner.current
    const release = () => onGone(item.key)
    if (!element || !content || reduce) { release(); return }
    content.style.overflow = 'visible'
    const source = cardBounds(origin.element?.isConnected ? origin.element.getBoundingClientRect() : origin.rect)
    const frames = origin.element?.isConnected ? captureCourseText(origin.element) : origin.text
    let cancelled = false
    const labels = shared.current.flatMap((label) => {
      const frame = frames.find((frame) => frame.role === label.source.role && frame.text === label.source.text) || label.source
      refreshSourceText(label, frame)
      return [animate(label.element, sourceTextStyle(label, frame, source), BACK), animate(label.ghost, sourceGhostStyle(frame, source), BACK)]
    })
    const shadow = origin.element?.isConnected ? getComputedStyle(origin.element).boxShadow : origin.shadow
    const surface = animate(element, { ...source, boxShadow: shadow }, BACK)
    const controls = [surface, ...labels, ...additional.current.map((node) => animate(node,
      { opacity: 0, transform: dynamic ? 'translateY(8px)' : 'translateY(8px) scale(.96)' }, BACK))]
    running.current = controls
    Promise.all(controls).then(() => { if (!cancelled) release() })
    return () => { cancelled = true; controls.forEach((control) => control.stop()) }
  }, [closing, origin, reduce, onGone, item.key, dynamic])

  return (
    <div ref={box} className={['one-pop-c', className, origin.dark ? 'is-dark' : 'is-light', closing ? 'is-closing' : ''].filter(Boolean).join(' ')} style={{ left: 0, top: 0, width: POP_WIDTH, background: origin.background }}
      id={closing ? undefined : popupId} role="dialog" aria-label={item.label} tabIndex={manageFocus ? -1 : undefined} onKeyDown={manageFocus ? containFormFocus : undefined} inert={closing}>
      <div ref={inner} className="one-pop-in">{item.content}</div>
    </div>
  )
}
