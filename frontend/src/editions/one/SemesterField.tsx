import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import { useStore } from '../../lib/store'
import { useNow } from '../../lib/time'
import { semesterProgress, type SemesterProgress } from '../../lib/semester-progress'

/**
 * 设置页底部的一条「课时」：一个方点是一节课，按上课先后从左往右排满整个学期。
 * 上过的是墨色，正在上或下一节是红色，还没上的是浅色。
 * 进入设置页时，墨色从第一节课一路长到现在（快出长尾）。
 *
 * 彩蛋：点击课时区域，所有方点离开原位，汇成两个数：上过的课按先后汇成「已上」，没上的汇成「还剩」，
 * 红色的此刻站在两者中间。停一会儿，各回原位。Esc 或再点一次提前结束。
 */

const easeOut = (t: number) => 1 - Math.pow(1 - t, 4)

const CELL = 8

/** 5×7 点阵字 */
const FONT: Record<string, string[]> = {
  '0': ['01110', '10001', '10011', '10101', '11001', '10001', '01110'],
  '1': ['00100', '01100', '00100', '00100', '00100', '00100', '01110'],
  '2': ['01110', '10001', '00001', '00010', '00100', '01000', '11111'],
  '3': ['11111', '00010', '00100', '00010', '00001', '10001', '01110'],
  '4': ['00010', '00110', '01010', '10010', '11111', '00010', '00010'],
  '5': ['11111', '10000', '11110', '00001', '00001', '10001', '01110'],
  '6': ['00110', '01000', '10000', '11110', '10001', '10001', '01110'],
  '7': ['11111', '00001', '00010', '00100', '01000', '01000', '01000'],
  '8': ['01110', '10001', '10001', '01110', '10001', '10001', '01110'],
  '9': ['01110', '10001', '10001', '01111', '00001', '00010', '01100'],
}

/** 一个数的点阵像素（列, 行），从 x0 起排；返回像素和占用的宽度 */
function pixels(txt: string, x0: number, top: number) {
  const out: [number, number][] = []
  let x = x0
  for (const ch of txt) {
    const g = FONT[ch]
    if (!g) continue
    for (let c = 0; c < 5; c++) for (let r = 0; r < 7; r++) if (g[r][c] === '1') out.push([x + c, top + r])
    x += 6
  }
  return { out, end: x - 1 }
}

export function SemesterField() {
  const { currentSchedule } = useStore()
  const now = useNow(60_000)
  const minute = Math.floor(now.getTime() / 60_000)
  const data = useMemo(() => semesterProgress(currentSchedule, new Date(minute * 60_000)), [currentSchedule, minute])
  if (!data || !data.cells.length) return null
  // A refreshed count/progress starts a new animation lifecycle; old egg timers cannot move new dots.
  return <SemesterDots key={(currentSchedule?.term.id || currentSchedule?.term.name || '') + ':' + data.cells.join(',')} data={data} />
}

function SemesterDots({ data }: { data: SemesterProgress }) {
  // 回放进度：filled = 已经长出来的课时；null 表示静止
  const [filled, setFilled] = useState<number | null>(null)
  const raf = useRef(0)

  const replay = () => {
    if (!data.done) return
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return
    cancelAnimationFrame(raf.current)
    const n = data.done
    const dur = Math.min(1600, 700 + n * 3)
    const t0 = performance.now()
    let last = -1
    const tick = (t: number) => {
      const p = Math.min(1, (t - t0) / dur)
      const k = Math.round(easeOut(p) * n)
      if (k !== last) { last = k; setFilled(k) }
      if (p < 1) raf.current = requestAnimationFrame(tick)
      else setFilled(null)
    }
    setFilled(0)
    raf.current = requestAnimationFrame(tick)
  }

  // 行数：一条细带铺满设置页宽度，课时多了就多一行
  const box = useRef<HTMLDivElement>(null)
  const [rows, setRows] = useState(5)
  const total = data.cells.length
  useLayoutEffect(() => {
    const el = box.current
    if (!el || !total) return
    const fit = () => {
      const perRow = Math.max(1, Math.floor((el.clientWidth + 3) / (CELL + 3)))
      // 行数至少 7（放得下 5×7 点阵字），在能放下的几种行数里挑最后一列最整齐的
      const min = Math.max(7, Math.ceil(total / perRow))
      let best = min
      for (let r = min; r <= min + 3; r++) if ((r - total % r) % r < (best - total % best) % best) best = r
      setRows(best)
    }
    fit()
    const ro = new ResizeObserver(fit)
    ro.observe(el)
    return () => ro.disconnect()
  }, [total])

  useEffect(() => {
    replay()
    return () => cancelAnimationFrame(raf.current)
    // 只在进入设置页（挂载）和课表就绪时回放
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [total])

  // 彩蛋：点击课时区域，每一个方点都离开原位，汇成「已上 | 还剩」两个数：
  // 上过的课按先后汇成左边的数（最早的课落在第一笔），没上的汇成右边的数，红色的此刻站在两者之间。
  // 停一会儿，所有方点各回原位。egg = 'in' 汇聚中/停住，'out' 回家，null 平时。
  const [egg, setEgg] = useState<null | 'in' | 'out'>(null)
  const [pitch, setPitch] = useState<[number, number]>([CELL + 3, CELL + 3])
  const eggT = useRef(0)
  const endEgg = () => { clearTimeout(eggT.current); setEgg('out'); eggT.current = window.setTimeout(() => setEgg(null), 1500) }
  const startEgg = () => {
    if (egg === 'in') { endEgg(); return }
    if (egg || !targets || matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const grid = box.current?.firstElementChild as HTMLElement | null
    const cols = Math.ceil(total / rows)
    if (grid) setPitch([cols > 1 ? (grid.clientWidth - CELL) / (cols - 1) : CELL + 3, CELL + 3])
    cancelAnimationFrame(raf.current); setFilled(null)
    clearTimeout(eggT.current); setEgg('in')
    eggT.current = window.setTimeout(endEgg, 4200)
  }
  useEffect(() => {
    if (!egg) return
    const k = (e: KeyboardEvent) => { if (e.key === 'Escape' && egg === 'in') endEgg() }
    const resize = () => { clearTimeout(eggT.current); setEgg(null) }
    addEventListener('keydown', k); addEventListener('resize', resize)
    return () => { removeEventListener('keydown', k); removeEventListener('resize', resize) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [egg])
  useEffect(() => () => clearTimeout(eggT.current), [])

  // 每个方点的去处：dx/dy 以格为单位，dl 是出发延迟
  const targets = useMemo(() => {
    if (rows < 7) return null
    const top = Math.floor((rows - 7) / 2)
    const left = pixels(String(data.done), 0, top)
    const mid = left.end + 3
    const right = pixels(String(total - data.done), mid + 3, top)
    // Small/empty halves cannot form readable glyphs without inventing extra course dots.
    if (data.done < left.out.length || total - data.done - data.cells.filter((c) => c === 'now').length < right.out.length) return null
    const idx = { past: 0, now: 0, future: 0 }
    const nPast = data.done, nNow = data.cells.filter((c) => c === 'now').length, nFut = total - nPast - nNow
    return data.cells.map((c, i) => {
      const col = Math.floor(i / rows), row = i % rows
      const k = idx[c]++
      let t: [number, number]
      let dl: number
      if (c === 'past') { t = left.out[Math.floor(k * left.out.length / Math.max(1, nPast))]; dl = 360 * k / Math.max(1, nPast) }
      else if (c === 'now') { t = [mid, top + 3 - Math.floor(nNow / 2) + k]; dl = 380 }
      else { t = right.out[Math.floor(k * right.out.length / Math.max(1, nFut))]; dl = 120 + 360 * k / Math.max(1, nFut) }
      return { dx: t[0] - col, dy: t[1] - row, dl: Math.round(dl) }
    })
  }, [data, total, rows])

  const playing = filled !== null
  let pastIndex = 0

  return <section className={`one-sf${playing ? ' is-playing' : ''}${egg ? ` is-egg is-egg-${egg}` : ''}`} aria-label="本学期课时">
    <div className="one-sf-k">
      <span className="one-k"><i className="one-k-mark" aria-hidden="true" />课时</span>
      <span className="sr-only">已结束 {data.done} 节计划课程，共 {total} 节</span>
    </div>
    <div className="one-sf-box" ref={box}>
      <div className="one-sf-grid" style={{ '--rows': rows, '--cell': `${CELL}px` } as CSSProperties}>
        {data.cells.map((c, i) => {
          const ordinal = c === 'past' ? pastIndex++ : -1
          const s = playing && (c === 'now' || (c === 'past' && ordinal >= filled!)) ? 'future' : c
          const t = targets?.[i]
          const st = egg && t ? {
            transform: egg === 'in' ? `translate(${(t.dx * pitch[0]).toFixed(1)}px, ${(t.dy * pitch[1]).toFixed(1)}px)` : undefined,
            transitionDelay: `${egg === 'in' ? t.dl : 360 - Math.min(360, t.dl)}ms`,
          } as CSSProperties : undefined
          return <i key={i} className="one-sf-d" data-s={s} style={st} />
        })}
      </div>
    </div>
    {targets && <button type="button" className="one-sf-trigger" onClick={startEgg} aria-label={`已上 ${data.done} 节，共 ${total} 节`} aria-pressed={egg === 'in'} />}
  </section>
}
