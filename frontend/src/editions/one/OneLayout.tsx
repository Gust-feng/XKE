import '@fontsource-variable/archivo/wdth.css'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { useEffect } from 'react'
import { useStore, type View } from '../../lib/store'
import { MONTH_CN, WEEKDAY_CN, pad, relativeDay, useNow } from '../../lib/time'
import { SwissNet } from './SwissNet'
import { SwissSettings } from './SwissSettings'
import { SwissWeek } from './SwissWeek'
import { DETENT, Ticker, countdown } from './parts'
import './one.css'
import { nextCourseBlock } from '../../lib/course-blocks'
import { NetworkRail } from './NetworkRail'
import { IdCard } from './SwissSettings'

/**
 * 合成 —— 瑞士骨架。只有课表一页：左栏 = 大日期 + 此刻/下一节小卡 + 周次；
 * 导航收进右上角一行小字（第六版布局）。动效沿用第四版：档位弹簧、拨码数字、色块展开。
 */

const NAV: { v: View; label: string }[] = [
  { v: 'week', label: '课表' },
  { v: 'net', label: '校园网' },
  { v: 'settings', label: '设置' },
]

export function OneLayout({ desktop = false }: { desktop?: boolean }) {
  const { view, setView, sheet, captcha, authOpen, phase } = useStore()
  const t = useNow(60_000)
  const reduce = useReducedMotion()

  // 不再有「今天」页：今天的信息全部由左栏小卡承担
  useEffect(() => {
    if (view === 'today') setView('week')
  }, [view, setView])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.metaKey || e.ctrlKey || e.altKey || sheet || captcha || authOpen || (e.target as HTMLElement).closest('input, select, textarea, dialog[open]')) return
      const i = Number(e.key) - 1
      if (i >= 0 && i <= 3) {
        e.stopImmediatePropagation()
        if (i < NAV.length) setView(NAV[i].v)
      }
    }
    addEventListener('keydown', onKey, true)
    return () => removeEventListener('keydown', onKey, true)
  }, [setView, sheet, captcha, authOpen])

  const v = view === 'today' ? 'week' : view
  const d = t
  const wd = (d.getDay() + 6) % 7

  return (
    <div className="sw one" inert={phase !== 'loading' && (authOpen || !!captcha)}>
      <aside className="one-rail">
        <div className="one-date">
          <span className="one-date-meta t5"><span>{MONTH_CN[d.getMonth()]}</span><span>{WEEKDAY_CN[wd]}</span></span>
          <b className="sw-giant num">{pad(d.getDate())}</b>
        </div>
        {v === 'net' ? <NetworkRail /> : v === 'settings' ? <IdCard /> : <>
          <NextCard />
          <div className="one-slot" id="one-slot" />
        </>}
      </aside>

      <div className="one-main">
        <header className={`one-toolbar${desktop ? ' is-desktop' : ''}`}>
          <nav className="one-nav" aria-label="视图">
            {NAV.map((n) => (
              <button key={n.v} className={v === n.v ? 'on' : ''} onClick={() => setView(n.v)} aria-current={v === n.v ? 'page' : undefined}>
                {n.label}
                {v === n.v && <motion.i layoutId="one-nav-bar" className="one-nav-bar" transition={reduce ? { duration: 0 } : DETENT} />}
              </button>
            ))}
          </nav>
        </header>
        <main className="sw-main" key={v}>
          {v === 'week' && <SwissWeek />}
          {v === 'net' && <SwissNet />}
          {v === 'settings' && <SwissSettings />}
        </main>
      </div>

    </div>
  )
}

/** 左栏小卡：此刻 / 下一节，倒计时一眼可读 */
function NextCard() {
  const { currentSchedule: schedule, openSheet } = useStore()
  const t = useNow(1000)
  if (!schedule) return <div className="one-card is-idle"><span className="one-card-k t5">接下来</span><b className="one-card-name">暂无课表</b></div>
  if (!schedule.term.startDate) return null
  const occ = nextCourseBlock(schedule.term, schedule.courses, t)
  if (!occ) {
    return (
      <div className="one-card is-idle">
        <span className="one-card-k t5">接下来</span>
        <b className="one-card-name">没有课了</b>
      </div>
    )
  }
  const live = occ.start <= t
  const sameDay = occ.dayOffset === 0
  const c = occ.course
  const cd = countdown((live ? occ.end : occ.start).getTime() - t.getTime())
  const big = sameDay ? cd.main : occ.part.start
  const k = live ? '此刻 · 距下课' : occ.inBreak ? '课间 · 距续课' : sameDay ? '下一节 · 距上课' : relativeDay(occ.dayOffset, occ.start)
  const prog = live ? (t.getTime() - occ.start.getTime()) / (occ.end.getTime() - occ.start.getTime()) : 0
  const key = `${c.id}-${occ.part.id}-${occ.start.toDateString()}-${live}`

  return (
    <AnimatePresence mode="popLayout" initial={false}>
      <motion.button
        key={key}
        className={`one-card ${live ? 'is-live' : 'is-next'}`}
        onClick={() => openSheet({ type: 'course', course: c })}
        initial={{ y: 24, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        exit={{ y: -16, opacity: 0, transition: { duration: 0.12 } }}
        transition={DETENT}
      >
        <span className="one-card-k t5">
          <i className="sw-mark" />
          {k}
        </span>
        <span className="one-card-big sw-giant num">
          <Ticker key={big.length} text={big} step={26} />
          {sameDay && cd.sec && <small>{cd.sec}</small>}
        </span>
        <b className="one-card-name" data-course-text="title">{c.name}</b>
        <span className="one-card-m num lat">
          {c.moveFrom ? <em className="one-move">换教室 <span data-course-text="room">{c.room}</span></em> : <span data-course-text="room">{c.room || '教室待定'}</span>}
          {sameDay && <span data-course-text="time">{occ.part.start}–{occ.part.end}</span>}
        </span>
        {occ.moveTo && <span className="one-card-move num lat">{occ.moveTo.start} 换到 {occ.moveTo.room}</span>}
        {live && (
          <span className="one-card-bar"><motion.i initial={false} animate={{ scaleX: prog }} transition={{ duration: 0.6 }} /></span>
        )}
      </motion.button>
    </AnimatePresence>
  )
}
