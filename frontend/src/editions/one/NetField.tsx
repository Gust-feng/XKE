import { useEffect, useRef } from 'react'
import { now as clockTime } from '../../lib/time'

type Mode = 'off' | 'busy' | 'on'

/**
 * 校园网页中间的点阵。
 * · 未连接：整片安静，只有一格红点在中央缓慢明灭——那是一条在等你的蛇。
 *   点一下点阵就开始贪吃蛇（方向键 / WASD，Esc 结束；穿墙，撞到自己结束）。
 * · 认证中：一道竖向扫描光从左扫到右。
 * · 连接那一刻：一道红色波前从开关「连接」一侧扫过整页，点被它逐个「点亮」。
 * · 已连接：点阵变成一块时钟屏——当前时间 HH:MM 用实心墨点拼出，底下是一层随「风」轻微起伏的淡点。
 *   换分钟时，变化的点按列错开慢慢长出 / 收回。
 */
export function NetField({ mode, onScore }: { mode: Mode; onScore?: (n: number | null) => void }) {
  const cv = useRef<HTMLCanvasElement>(null)
  const modeRef = useRef(mode)
  const scoreRef = useRef(onScore)
  const wakeRef = useRef<(() => void) | null>(null)
  useEffect(() => {
    modeRef.current = mode
    scoreRef.current = onScore
    // Reduced-motion mode renders static frames only; wake it when inputs change.
    wakeRef.current?.()
  }, [mode, onScore])

  useEffect(() => {
    const c = cv.current
    if (!c) return
    const ctx = c.getContext('2d')
    if (!ctx) return
    const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches
    const css = getComputedStyle(document.documentElement)
    const rgb = (v: string, fb: [number, number, number]) => {
      const m = css.getPropertyValue(v).trim().match(/^#([0-9a-f]{6})$/i)
      if (!m) return fb
      const n = parseInt(m[1], 16)
      return [(n >> 16) & 255, (n >> 8) & 255, n & 255] as [number, number, number]
    }
    const INK = rgb('--ink', [17, 17, 15])
    const RED = rgb('--red', [232, 41, 28])
    const DIM = [200, 198, 190] as const

    let W = 0, H = 0, dpr = 1, cols = 0, rows = 0, ox = 0, oy = 0
    const STEP = 22
    const resize = () => {
      dpr = Math.min(2, devicePixelRatio || 1)
      const r = c.getBoundingClientRect()
      W = r.width; H = r.height
      c.width = Math.round(W * dpr); c.height = Math.round(H * dpr)
      cols = Math.max(1, Math.floor(W / STEP)); rows = Math.max(1, Math.floor(H / STEP))
      ox = (W - (cols - 1) * STEP) / 2; oy = (H - (rows - 1) * STEP) / 2
      // A reduced-motion canvas may have stopped its RAF loop after the last static frame.
      wakeRef.current?.()
    }
    resize()
    const ro = new ResizeObserver(resize)
    ro.observe(c)

    // —— 平滑值噪声（风） ——
    const hash = (i: number, j: number) => {
      let h = (i * 374761393 + j * 668265263) | 0
      h = Math.imul(h ^ (h >>> 13), 1274126177)
      return ((h ^ (h >>> 16)) >>> 0) / 4294967295
    }
    const sm = (u: number) => u * u * (3 - 2 * u)
    const noise = (x: number, y: number) => {
      const i = Math.floor(x), j = Math.floor(y), u = sm(x - i), v = sm(y - j)
      const a = hash(i, j), b = hash(i + 1, j), cc = hash(i, j + 1), d = hash(i + 1, j + 1)
      return a + (b - a) * u + (cc - a) * v + (a - b - cc + d) * u * v
    }

    // —— 贪吃蛇 ——
    type P = [number, number]
    const g = {
      on: false, body: [] as P[], dir: [1, 0] as P, next: [] as P[], food: [0, 0] as P,
      acc: 0, tick: 0.13, dead: 0, ghost: [] as P[],
    }
    const key = (p: P) => p[0] * 10000 + p[1]
    const placeFood = () => {
      const occ = new Set(g.body.map(key))
      for (let n = 0; n < 500; n++) {
        const p: P = [Math.floor(Math.random() * cols), Math.floor(Math.random() * rows)]
        if (!occ.has(key(p))) { g.food = p; return }
      }
    }
    const start = () => {
      const cx = Math.floor(cols / 2), cy = Math.floor(rows / 2)
      g.body = [[cx, cy], [cx - 1, cy], [cx - 2, cy]]
      g.dir = [1, 0]; g.next = []; g.acc = 0; g.tick = 0.13; g.on = true; g.dead = 0
      placeFood()
      scoreRef.current?.(0)
    }
    const end = () => {
      if (!g.on) return
      g.on = false; g.ghost = g.body; g.dead = 1
      scoreRef.current?.(null)
    }
    const click = () => {
      if (modeRef.current !== 'off' || g.on) return
      start()
      if (reduce) { cancelAnimationFrame(raf); raf = 0; schedule() }
    }
    const DIRS: Record<string, P> = {
      ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0],
      w: [0, -1], s: [0, 1], a: [-1, 0], d: [1, 0], W: [0, -1], S: [0, 1], A: [-1, 0], D: [1, 0],
    }
    const keydown = (e: KeyboardEvent) => {
      if (!g.on || (e.target as HTMLElement)?.closest?.('input, textarea, select, [contenteditable=true]')) return
      if (e.key === 'Escape') { end(); e.preventDefault(); e.stopImmediatePropagation(); return }
      const d = DIRS[e.key]
      if (!d) return
      e.preventDefault(); e.stopImmediatePropagation()
      const last = g.next.length ? g.next[g.next.length - 1] : g.dir
      if (d[0] === -last[0] && d[1] === -last[1]) return
      if (d[0] === last[0] && d[1] === last[1]) return
      if (g.next.length < 3) g.next.push(d)
    }
    const step = () => {
      if (g.next.length) g.dir = g.next.shift()!
      const h = g.body[0]
      const n: P = [(h[0] + g.dir[0] + cols) % cols, (h[1] + g.dir[1] + rows) % rows]
      const eat = n[0] === g.food[0] && n[1] === g.food[1]
      const body = eat ? g.body : g.body.slice(0, -1)
      if (body.some((p) => p[0] === n[0] && p[1] === n[1])) { end(); return }
      g.body = [n, ...body]
      if (eat) {
        g.tick = Math.max(0.065, g.tick * 0.97)
        placeFood()
        scoreRef.current?.(g.body.length - 3)
      }
    }

    c.addEventListener('click', click)
    addEventListener('keydown', keydown, true)

    const w = { off: modeRef.current === 'off' ? 1 : 0, busy: modeRef.current === 'busy' ? 1 : 0, on: modeRef.current === 'on' ? 1 : 0 }
    let raf = 0
    let clockTimer: ReturnType<typeof setTimeout> | undefined
    let last = performance.now()
    const schedule = () => {
      if (!raf) raf = requestAnimationFrame(frame)
    }
    let scanStart = last
    let onStart = modeRef.current === 'on' ? last - 1e5 : 0 // 首次进入就已连接：不放波前
    let prevMode = modeRef.current
    const SPEED = 760 // 波前速度 px/s

    // —— 已连接：点阵时钟 ——
    // 3×5 点阵数字；每个字形像素占 s×s 个格子
    const FONT = ['111101101101111', '010110010010111', '111001111100111', '111001111001111', '101101111001001', '111100111001111', '111100111101111', '111001001001001', '111101111101111', '111101111001111']
    const clk = { key: '', cur: new Uint8Array(0), prev: new Uint8Array(0), at: -1e9, n: 0 }
    const buildClock = (d: Date) => {
      const hh = String(d.getHours()).padStart(2, '0'), mm = String(d.getMinutes()).padStart(2, '0')
      const sc = cols >= 40 && rows >= 14 ? 2 : 1
      const totalW = 17 * sc, totalH = 5 * sc
      const x0 = Math.floor((cols - totalW) / 2), y0 = Math.floor((rows - totalH) / 2)
      const map = new Uint8Array(cols * rows)
      const put = (gx: number, gy: number) => {
        for (let a = 0; a < sc; a++) for (let b = 0; b < sc; b++) {
          const i = x0 + gx * sc + a, j = y0 + gy * sc + b
          if (i >= 0 && i < cols && j >= 0 && j < rows) map[j * cols + i] = 1
        }
      }
      const glyph = (ch: string, gx: number) => {
        const f = FONT[+ch]
        for (let r = 0; r < 5; r++) for (let q = 0; q < 3; q++) if (f[r * 3 + q] === '1') put(gx + q, r)
      }
      glyph(hh[0], 0); glyph(hh[1], 4); put(8, 1); put(8, 3); glyph(mm[0], 10); glyph(mm[1], 14)
      return { key: `${hh}${mm}-${cols}x${rows}`, map }
    }
    const ease = (u: number) => 1 - Math.pow(1 - Math.max(0, Math.min(1, u)), 4)

    const frame = (now: number) => {
      const dt = Math.min(0.05, (now - last) / 1000)
      last = now
      const t = now / 1000
      const m = modeRef.current
      if (m !== prevMode) {
        if (m === 'busy') scanStart = now
        if (m === 'on') onStart = reduce ? now - 1e5 : now
        if (m !== 'off') end()
        prevMode = m
      }
      const k = reduce ? 1 : 1 - Math.exp(-dt * 3.2)
      w.off += ((m === 'off' ? 1 : 0) - w.off) * k
      w.busy += ((m === 'busy' ? 1 : 0) - w.busy) * k
      // 连接时不靠权重淡入，由波前决定每一格何时进入「已连接」
      w.on += ((m === 'on' ? 1 : 0) - w.on) * (m === 'on' ? 1 : k)

      if (g.on) {
        g.acc += dt
        while (g.acc >= g.tick && g.on) { g.acc -= g.tick; step() }
      }
      if (g.dead > 0) g.dead = Math.max(0, g.dead - dt * 1.4)

      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
      ctx.clearRect(0, 0, W, H)

      const scanX = (((now - scanStart) / 1000) * 0.55 % 1.3 - 0.15) * W
      const sx = W * 0.75, sy = -STEP * 2
      const R = ((now - onStart) / 1000) * SPEED
      const playing = g.on || g.dead > 0
      // 玩的时候点阵退得更淡，让蛇和食物成为唯一的焦点
      const quiet = g.on ? 0.6 : 1
      if (w.on > 0.01) {
        const clockNow = clockTime()
        const clockKey = `${String(clockNow.getHours()).padStart(2, '0')}${String(clockNow.getMinutes()).padStart(2, '0')}-${cols}x${rows}`
        if (clockKey !== clk.key) {
          const nb = buildClock(clockNow)
          const sameSize = clk.cur.length === nb.map.length
          clk.prev = sameSize && clk.key ? clk.cur : nb.map
          clk.cur = nb.map; clk.key = nb.key; clk.at = sameSize && !reduce ? t : -1e9
        }
      }

      for (let i = 0; i < cols; i++) {
        for (let j = 0; j < rows; j++) {
          const x = ox + i * STEP, y = oy + j * STEP
          let v = 0.1, red = 0

          // 未连接：几乎静止，极慢的一层噪声
          const offV = 0.08 + 0.06 * noise(i * 0.12 + t * 0.05, j * 0.12)
          v = offV * quiet

          // 认证中：扫描光
          if (w.busy > 0.01) {
            const dx = (x - scanX) / 46
            const s = Math.exp(-dx * dx)
            const tail = x < scanX ? Math.exp(-(scanX - x) / 220) * 0.35 : 0
            v = v * (1 - w.busy) + (0.1 + 0.9 * s + tail) * w.busy
            red = Math.max(red, s * w.busy * 0.6)
          }

          // 已连接：波前 + 风
          if (w.on > 0.01) {
            const d = Math.hypot(x - sx, y - sy)
            const age = (R - d) / SPEED // 这一格被波前扫过多久了（秒）
            if (age > 0 || m !== 'on') {
              const wind = noise(i * 0.09 - t * 0.12, j * 0.09 + t * 0.04) * 0.6 + noise(i * 0.21 + t * 0.07, j * 0.21) * 0.4
              const wn = Math.max(0, Math.min(1, (wind - 0.5) * 2.4 + 0.5))
              let o = 0.1 + wn * wn * 0.26
              // 时钟：换分钟时，变了的点按列错开、慢慢长出 / 收回
              const idx = j * cols + i
              const a0 = clk.prev[idx] ?? 0, a1 = clk.cur[idx] ?? 0
              const lit = a0 === a1 ? a1 : a0 + (a1 - a0) * ease((t - clk.at - i * 0.018) / 0.9)
              o = o + (1 - o) * lit
              if (m === 'on' && age < 0.9) {
                const flash = Math.exp(-age * 10)
                o += flash * (1 - o)
                red = Math.max(red, flash)
              }
              v = v * (1 - w.on) + o * w.on
            }
          }

          draw(x, y, v, red)
        }
      }

      // 等待中的蛇：中央一格红点，慢慢明灭
      if (m === 'off' && !playing && w.off > 0.6) {
        const p = 0.5 + 0.5 * Math.sin(t * 1.6)
        const x = ox + Math.floor(cols / 2) * STEP, y = oy + Math.floor(rows / 2) * STEP
        cell(x, y, STEP * (0.42 + 0.12 * p), RED, (0.55 + 0.45 * p) * w.off)
      }
      if (g.on) {
        cell(ox + g.food[0] * STEP, oy + g.food[1] * STEP, STEP - 6, INK, 1)
        g.body.forEach((p, n) => cell(ox + p[0] * STEP, oy + p[1] * STEP, n === 0 ? STEP - 2 : STEP - 5, RED, n === 0 ? 1 : 1 - (n / g.body.length) * 0.35))
      } else if (g.dead > 0) {
        // 结束：蛇身从尾到头化开
        g.ghost.forEach((p, n) => {
          const a = Math.max(0, Math.min(1, g.dead * 1.6 - (1 - n / g.ghost.length) * 0.6))
          cell(ox + p[0] * STEP, oy + p[1] * STEP, (STEP - 5) * (0.4 + 0.6 * a), RED, a)
        })
      }

      raf = 0
      clearTimeout(clockTimer)
      if (!reduce || g.on) schedule()
      else if (m === 'on') clockTimer = setTimeout(schedule, 60_000 - clockTime().getTime() % 60_000 + 10)
    }

    function cell(x: number, y: number, s: number, C: readonly number[], a: number) {
      ctx!.fillStyle = `rgba(${C[0]},${C[1]},${C[2]},${a})`
      ctx!.fillRect(x - s / 2, y - s / 2, s, s)
    }
    function draw(x: number, y: number, v: number, red: number) {
      const size = 2 + v * 9
      const a = 0.25 + v * 0.75
      const r = DIM[0] + (INK[0] - DIM[0]) * v, gg = DIM[1] + (INK[1] - DIM[1]) * v, b = DIM[2] + (INK[2] - DIM[2]) * v
      const q = Math.min(1, red)
      ctx!.fillStyle = `rgba(${Math.round(r + (RED[0] - r) * q)},${Math.round(gg + (RED[1] - gg) * q)},${Math.round(b + (RED[2] - b) * q)},${a})`
      ctx!.fillRect(x - size / 2, y - size / 2, size, size)
    }

    wakeRef.current = schedule
    schedule()
    return () => {
      if (wakeRef.current === schedule) wakeRef.current = null
      cancelAnimationFrame(raf)
      clearTimeout(clockTimer)
      raf = 0
      ro.disconnect()
      c.removeEventListener('click', click)
      removeEventListener('keydown', keydown, true)
    }
  }, [])

  return <canvas ref={cv} className="one-field" aria-hidden />
}
