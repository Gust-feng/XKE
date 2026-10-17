import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { useEffect, useState } from 'react'

/* ───────── 瑞士版：共用零件 ─────────
 * 动效语言：机械、精确。数字逐位「拨码」落定，变化时整格硬推（线性、无模糊、无淡入）。
 */


const DIG = '0123456789'
const LET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'

/** 入场时每一位先乱跳，再从左到右依次落定；之后每次变化逐格线性推入 */
export function Ticker({ text, className, step = 34 }: { text: string; className?: string; step?: number }) {
  const reduce = useReducedMotion()
  const done = text.length * 2 + 7
  const [frame, setFrame] = useState(reduce ? done : 0)
  useEffect(() => {
    if (frame >= done) return
    const id = setTimeout(() => setFrame((f) => f + 1), step)
    return () => clearTimeout(id)
  }, [frame, done, step])

  const settling = frame < done
  return (
    <span className={`tk ${className ?? ''}`} aria-label={text}>
      {text.split('').map((ch, i) => {
        const key = text.length - i
        if (settling) {
          const free = frame < 6 + i * 2
          let c = ch
          if (free && DIG.includes(ch)) c = DIG[(frame * 7 + i * 3) % 10]
          else if (free && LET.includes(ch)) c = LET[(frame * 11 + i * 5) % 26]
          return <span key={key} className={`tk-c ${ch === ':' ? 'tk-sep' : ''}`} aria-hidden><span>{c}</span></span>
        }
        return <Cell key={key} ch={ch} reduce={!!reduce} />
      })}
    </span>
  )
}

function Cell({ ch, reduce }: { ch: string; reduce: boolean }) {
  if (reduce) return <span className={`tk-c ${ch === ':' ? 'tk-sep' : ''}`} aria-hidden><span>{ch}</span></span>
  return (
    <span className={`tk-c ${ch === ':' ? 'tk-sep' : ''}`} aria-hidden>
      <AnimatePresence initial={false} mode="popLayout">
        <motion.span
          key={ch}
          initial={{ y: '100%' }}
          animate={{ y: '0%' }}
          exit={{ y: '-100%' }}
          transition={{ duration: 0.14, ease: 'linear' }}
        >
          {ch}
        </motion.span>
      </AnimatePresence>
    </span>
  )
}

/** 课程名里拉丁/数字和中文分开排：中文略放宽字距，拉丁收紧 */
export function Mixed({ text }: { text: string }) {
  const parts = text.split(/([A-Za-z0-9][A-Za-z0-9 .\-–]*)/).filter(Boolean)
  return (
    <>
      {parts.map((p, i) => (/^[A-Za-z0-9]/.test(p) ? <span key={i} className="lat">{p}</span> : <span key={i}>{p}</span>))}
    </>
  )
}
