import { useLayoutEffect, useRef, type CSSProperties, type ReactNode } from 'react'

/** Request completion releases the scan at each tick's next full-cycle boundary. */
export function ScheduleRuler({ refreshing, reducedMotion, weeks, children }: {
  refreshing: boolean
  reducedMotion: boolean
  weeks: number
  children?: ReactNode
}) {
  const ref = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    const ruler = ref.current!
    if (reducedMotion) {
      ruler.classList.remove('is-syncing')
      return
    }
    if (refreshing) ruler.classList.add('has-refreshed', 'is-syncing')
    if (!ruler.classList.contains('is-syncing')) return

    const scans = ruler.getAnimations({ subtree: true })
      .filter(animation => 'animationName' in animation && animation.animationName === 'one-scan')
    let current = true
    for (const scan of scans) {
      const effect = scan.effect!
      if (refreshing) {
        effect.updateTiming({ iterations: Infinity })
        scan.play()
      }
      else {
        const { delay, duration } = effect.getComputedTiming()
        const elapsed = Math.max(0, Number(scan.currentTime ?? 0) - (delay ?? 0))
        effect.updateTiming({ iterations: Math.max(1, Math.ceil(elapsed / Number(duration))) })
      }
    }
    if (!refreshing) {
      // Include the last staggered tick; no network delay or arbitrary minimum timeout.
      void Promise.all(scans.map(scan => scan.finished.catch(() => undefined))).then(() => {
        if (current) ruler.classList.remove('is-syncing')
      })
    }
    return () => { current = false }
  }, [refreshing, reducedMotion, weeks])

  return <div ref={ref} className="one-ticks" style={{ '--n': weeks } as CSSProperties}>{children}</div>
}
