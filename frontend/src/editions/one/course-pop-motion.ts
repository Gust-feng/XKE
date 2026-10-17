import { cardBounds, type CardBounds } from './course-pop-layout'

export interface CourseTextFrame {
  role: string; text: string; rect: CardBounds; size: number; opacity: number
  ink: { x: number; y: number }; css: Record<string, string>
}
export interface SharedCourseText {
  element: HTMLElement; ghost: HTMLElement; source: CourseTextFrame; target: CourseTextFrame
  local: { x: number; y: number }
}
const TEXT_CSS = ['font-family', 'font-size', 'font-weight', 'font-style', 'font-stretch', 'font-feature-settings',
  'font-variant-numeric', 'line-height', 'letter-spacing', 'color', 'white-space', 'word-break', 'overflow-wrap',
  'text-align', 'text-transform', 'text-overflow', 'overflow', 'display', '-webkit-line-clamp', '-webkit-box-orient']

function measureText(element: HTMLElement): CourseTextFrame {
  const style = getComputedStyle(element)
  const rect = cardBounds(element.getBoundingClientRect())
  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT)
  let node: Node | null
  let ink = { x: 0, y: 0 }
  while ((node = walker.nextNode())) {
    const text = node.textContent || ''
    const first = text.search(/\S/)
    if (first < 0) continue
    const range = document.createRange()
    const length = Array.from(text.slice(first))[0].length
    range.setStart(node, first); range.setEnd(node, first + length)
    const glyph = range.getBoundingClientRect()
    ink = { x: glyph.left - rect.left, y: glyph.top - rect.top }
    break
  }
  let opacity = 1
  for (let parent: HTMLElement | null = element; parent; parent = parent.parentElement) {
    opacity *= Number(getComputedStyle(parent).opacity)
  }
  return { role: element.dataset.courseText!, text: element.textContent!.trim(), rect, ink, opacity,
    size: parseFloat(style.fontSize), css: Object.fromEntries(TEXT_CSS.map((key) => [key, style.getPropertyValue(key)])) }
}

export function captureCourseText(element: HTMLElement) {
  return Array.from(element.querySelectorAll<HTMLElement>('[data-course-text]'), measureText)
}

/** Freeze both text layouts. Only compositor transforms/opacity change during the morph. */
export function prepareCourseText(content: HTMLElement, source: CourseTextFrame[], targetBox: CardBounds): SharedCourseText[] {
  const targets = Array.from(content.querySelectorAll<HTMLElement>('[data-course-text]'))
  const labels = source.flatMap((frame) => {
    const element = targets.find((node) => node.dataset.courseText === frame.role && node.textContent!.trim() === frame.text)
    if (!element) return []
    const target = measureText(element)
    const ghost = document.createElement('span')
    ghost.dataset.courseGhost = frame.role
    ghost.setAttribute('aria-hidden', 'true')
    ghost.textContent = frame.text
    const label = { element, ghost, source: frame, target, local: { x: target.rect.left - targetBox.left, y: target.rect.top - targetBox.top } }
    return [label]
  })
  for (const label of labels) {
    label.element.dataset.popShared = ''
    Object.assign(label.element.style, { width: label.target.rect.width + 'px', height: label.target.rect.height + 'px', transformOrigin: '0 0' })
    refreshSourceText(label, label.source)
    content.parentElement!.appendChild(label.ghost)
  }
  return labels
}

export function refreshSourceText(label: SharedCourseText, frame: CourseTextFrame) {
  for (const [key, value] of Object.entries(frame.css)) label.ghost.style.setProperty(key, value)
  Object.assign(label.ghost.style, { position: 'absolute', left: '0', top: '0', margin: '0', padding: '0',
    width: frame.rect.width + 'px', height: frame.rect.height + 'px', transformOrigin: '0 0', pointerEvents: 'none' })
}

/** Anchor the first glyph, rather than a changing line box/baseline. */
export function sourceTextStyle(label: SharedCourseText, frame: CourseTextFrame, box: CardBounds) {
  const scale = frame.size / label.target.size
  const x = frame.rect.left - box.left + frame.ink.x - label.target.ink.x * scale - label.local.x
  const y = frame.rect.top - box.top + frame.ink.y - label.target.ink.y * scale - label.local.y
  return { transform: `translate(${x}px, ${y}px) scale(${scale})`, opacity: 0 }
}

export function sourceGhostStyle(frame: CourseTextFrame, box: CardBounds) {
  return { transform: `translate(${frame.rect.left - box.left}px, ${frame.rect.top - box.top}px) scale(1)`, opacity: frame.opacity }
}

export function targetTextStyle() { return { transform: 'translate(0px, 0px) scale(1)', opacity: 1 } }

export function targetGhostStyle(label: SharedCourseText) {
  const scale = label.target.size / label.source.size
  const x = label.local.x + label.target.ink.x - label.source.ink.x * scale
  const y = label.local.y + label.target.ink.y - label.source.ink.y * scale
  return { transform: `translate(${x}px, ${y}px) scale(${scale})`, opacity: 0 }
}

export function placeCourseText(label: SharedCourseText, style: ReturnType<typeof sourceTextStyle>) {
  Object.assign(label.element.style, style)
}

export function resetCourseText(label: SharedCourseText) {
  label.ghost.remove()
  delete label.element.dataset.popShared
  for (const property of ['transform', 'transform-origin', 'width', 'height', 'opacity']) label.element.style.removeProperty(property)
}
