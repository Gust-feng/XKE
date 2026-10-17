import { cancelFrame, frame, springValue } from 'motion'

export type SemesterMotionState = 'in' | 'out' | null
export interface SemesterMotion {
  toggle(): void
  restore(): void
  reset(): void
  destroy(): void
}
interface Dot {
  delay: number
  render(progress: number): void
}

// All dots must finish 88% of this leg before ordinary clicks may join the next leg.
const JOIN_PROGRESS = 0.88

/** Persistent springs carry momentum; a single pending switch protects the main movement. */
export function createSemesterMotion(dots: Dot[], onState: (state: SemesterMotionState) => void): SemesterMotion {
  let target = 0
  let active = false
  let disposed = false
  let pending: number | undefined
  let origins = dots.map(() => 0)
  let returnTimer: ReturnType<typeof setTimeout> | undefined
  const departures = new Set<ReturnType<typeof setTimeout>>()
  const values = dots.map(() => springValue<number>(0, { stiffness: 100, damping: 20, restDelta: 0.001, restSpeed: 0.01 }))

  const clearTimers = () => {
    clearTimeout(returnTimer)
    returnTimer = undefined
    departures.forEach(clearTimeout)
    departures.clear()
  }
  const canJoin = () => departures.size === 0 && values.every((value, i) =>
    Math.abs(target - value.get()) <= Math.max(0.001, Math.abs(target - origins[i]) * (1 - JOIN_PROGRESS)))
  const settle = () => {
    if (disposed || !active) return
    if (pending !== undefined && canJoin()) {
      steer(pending, false)
      return
    }
    if (departures.size || values.some(value => value.isAnimating())) return
    if (target === 0) {
      active = false
      onState(null)
    } else if (returnTimer === undefined) {
      // Reading time starts when every glyph has arrived, not during its staggered entrance.
      returnTimer = setTimeout(() => steer(0, true), 2600)
    }
  }
  // One shared end-of-frame check, not a full-field scan/retarget for every dot or burst click.
  const scheduleSettle = () => frame.postRender(settle)
  const steer = (next: number, stagger: boolean) => {
    clearTimers()
    pending = undefined
    origins = values.map(value => value.get())
    target = next
    active = true
    onState(next === 1 ? 'in' : 'out')
    values.forEach((value, i) => {
      const delay = stagger ? next === 1 ? dots[i].delay : 360 - Math.min(360, dots[i].delay) : 0
      if (delay > 0) {
        const timer = setTimeout(() => {
          departures.delete(timer)
          value.set(next)
          scheduleSettle()
        }, delay)
        departures.add(timer)
      } else value.set(next)
    })
    scheduleSettle()
  }
  const unsubscribe = values.flatMap((value, i) => [
    value.on('change', progress => { dots[i].render(progress); scheduleSettle() }),
    value.on('animationComplete', scheduleSettle),
  ])
  const reset = () => {
    clearTimers()
    pending = undefined
    target = 0
    active = false
    values.forEach((value, i) => {
      value.jump(0)
      dots[i].render(0)
    })
    cancelFrame(settle)
  }

  return {
    toggle() {
      if (disposed || pending !== undefined) return
      if (active && !canJoin()) {
        // Repeated clicks during one protected leg all request ONE next leg, never a queue.
        pending = target === 1 ? 0 : 1
        return
      }
      steer(target === 1 ? 0 : 1, !active)
    },
    restore() {
      if (disposed || !active) return
      pending = undefined
      // Escape is an explicit dismissal, unlike ordinary clicks. Do not restart an existing return.
      if (target === 1) steer(0, false)
    },
    reset() {
      if (disposed) return
      reset()
      onState(null)
    },
    destroy() {
      if (disposed) return
      disposed = true
      unsubscribe.forEach(remove => remove())
      reset()
      values.forEach(value => value.destroy())
    },
  }
}
