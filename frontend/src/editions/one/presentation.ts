import type { Course } from '../../api/types'
import { pad } from '../../lib/time'

/** 倒计时：≥1 小时 → 「H:MM」+ 小号秒；不足 1 小时 → 「MM:SS」 */
export function countdown(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000))
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const ss = s % 60
  return h > 0 ? { main: `${h}:${pad(m)}`, sec: pad(ss) } : { main: `${pad(m)}:${pad(ss)}`, sec: null }
}

export const kindClass = (c: Course) => `k-${c.kind}`

/** 仪表借来的「档位」弹簧：短促、带一点回弹，像拨到位 */
export const DETENT = { type: 'spring', stiffness: 560, damping: 22, mass: 0.9 } as const