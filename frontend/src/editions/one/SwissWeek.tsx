import { scheduleJobNotice } from '../../lib/schedule-jobs'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { createPortal } from 'react-dom'
import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react'
import { useStore } from '../../lib/store'
import { DAY, WEEKDAY_CN, at, inWeek, minOfDay, mondayOf, pad, toMin, useNow, weekOf, weekday } from '../../lib/time'
import { Ticker, kindClass } from './parts'
import { DETENT } from './parts'
import { semesterRuler, personalTerms, termLabel, weekTimeRange } from '../../lib/schedule-view'
import { courseBlocks, nextCourseBlock } from '../../lib/course-blocks'

const d = (i: number) => ({ '--i': i }) as CSSProperties

export function SwissWeek() {
  const { schedule, semesterSchedule, settings, openSheet, selectedWeek, selectWeek, selectTerm, currentSchedule, browsing, syncing, refreshing, sync, scheduleError, weekLoaded, captcha, authOpen, sheet, phase, user, setView } = useStore()
  const t = useNow(1000)
  const reduce = useReducedMotion()
  const term = schedule?.term
  const thisWeek = browsing ? null : term?.startDate ? weekOf(term, t) : schedule?.currentWeek ?? null
  const [dir, setDir] = useState(0)
  const [slot, setSlot] = useState<HTMLElement | null>(null)
  useEffect(() => setSlot(document.getElementById('one-slot')), [])
  const w = selectedWeek
  const { total, counts: load, peak } = useMemo(() => semesterRuler(semesterSchedule, schedule, w, thisWeek), [semesterSchedule, schedule, w, thisWeek])
  const blocks = useMemo(() => courseBlocks(schedule?.courses ?? [], w), [schedule, w])

  const go = (to: number) => {
    if (!term) return
    const n = Math.min(total, Math.max(1, to))
    if (n === w) return
    setDir(n > w ? 1 : -1)
    void selectWeek(n)
  }

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.defaultPrevented || e.metaKey || e.ctrlKey || e.altKey || captcha || authOpen || sheet || (e.target as HTMLElement).closest('input, select, textarea')) return
      if (e.key === 'ArrowLeft') go(w - 1)
      if (e.key === 'ArrowRight') go(w + 1)
      if (e.key.toLowerCase() === 'r') { e.preventDefault(); void sync() }
      if (e.key.toLowerCase() === 't') {
        if (browsing && currentSchedule?.term.id) { e.preventDefault(); void selectTerm(currentSchedule.term.id) }
        else if (thisWeek && thisWeek >= 1 && thisWeek <= total) go(thisWeek)
      }
    }
    addEventListener('keydown', onKey)
    return () => removeEventListener('keydown', onKey)
  })

  if (!schedule || !term || !settings) return <div className="runtime-placeholder">
    <p role="status">{phase === 'loading' ? '正在恢复本地课表…' : scheduleError || '暂无本地课表'}</p>
    <button className="sw-btn" onClick={user ? sync : () => setView('settings')} disabled={syncing}>{user ? '获取课表' : '连接教务'}</button>
  </div>

  const days = settings.showWeekend ? 7 : 5
  const mon = term.startDate ? mondayOf(term, w) : null
  const isNow = w === thisWeek
  const td = weekday(t)
  const all = blocks.filter((c) => c.weekday < days)
  // History remains available even when ended classes in the current week are hidden.
  const displayed = mon && isNow && settings.inactiveCourseMode === 'hide'
    ? all.filter((c) => at(new Date(mon.getTime() + c.weekday * DAY), c.end) > t)
    : all
  const unplaced = schedule.courses.filter((c) => (inWeek(c, w) && c.timed === false) || !c.weeks.length)
  const occ = mon ? nextCourseBlock(term, schedule.courses, t) : null
  const { from, to } = weekTimeRange(all, schedule.blockTimes, schedule.courses)
  const y = (m: number) => `${((m - from) / (to - from)) * 100}%`
  const hours = Array.from({ length: (to - from) / 60 + 1 }, (_, k) => from / 60 + k)
  const starts = new Set(all.map((c) => toMin(c.start)))
  const nowM = minOfDay(t)
  const cols = `repeat(${days}, minmax(0, 1fr))`

  return (
    <>
    <div className="sw-week sw-grid">
      {slot && createPortal(
        <div className="one-term">
          <TermCap />
          <div className="one-term-rule">
            <button className="one-term-ar" onClick={() => go(w - 1)} disabled={w <= 1} aria-label="上一周"><svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" aria-hidden="true" focusable="false"><path d="M10 16V4M5 9l5-5 5 5" /></svg></button>
            <div className={`one-ticks${refreshing ? ' is-syncing' : ''}`} style={{ '--n': total } as CSSProperties}>
              {load.map((n, i) => {
                const k = i + 1
                return (
                  <button
                    key={k}
                    className={`one-tick${browsing || thisWeek && k < thisWeek ? ' is-past' : ''}${k === thisWeek ? ' is-now' : ''}${k === w ? ' is-view' : ''}`}
                    style={{ '--h': n === null ? 0 : n / peak, ...d(i) } as CSSProperties}
                    onClick={() => go(k)}
                    aria-label={n === null ? `第 ${k} 周，课量未知` : `第 ${k} 周，学期安排 ${n} 次课`}
                  >
                    <i />
                  </button>
                )
              })}
              <motion.div
                className="one-term-flag"
                initial={false}
                animate={{ top: `calc(${(w - 0.5) / total} * 100%)` }}
                transition={reduce ? { duration: 0 } : DETENT}
              >
                <button className="sw-wk-num" onClick={() => thisWeek && !isNow && go(thisWeek)} disabled={isNow || !thisWeek || thisWeek < 1 || thisWeek > total} aria-label={isNow ? '本周' : '回到本周'}>
                  <span className="t5">第</span>
                  <Ticker key={w} text={String(w)} className="sw-giant num" step={26} />
                  <span className="t5">周</span>
                </button>
                {browsing && currentSchedule?.term.id
                  ? <button className="one-term-back" onClick={() => void selectTerm(currentSchedule.term.id!)}>回本学期</button>
                  : !isNow && thisWeek && thisWeek >= 1 && thisWeek <= total && <button className="one-term-back" onClick={() => go(thisWeek)}>回本周</button>}
              </motion.div>
              <motion.span
                className="one-tick-mark"
                initial={false}
                animate={{ top: `calc(${(w - 1) / total} * 100%)` }}
                transition={reduce ? { duration: 0 } : DETENT}
              />
            </div>
            <button className="one-term-ar" onClick={() => go(w + 1)} disabled={w >= total} aria-label="下一周"><svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" aria-hidden="true" focusable="false"><path d="M10 4v12M5 11l5 5 5-5" /></svg></button>
          </div>
        </div>,
        slot,
      )}

      <div className="sw-days">
        <motion.div
          key={w}
          className="sw-days-in"
          style={{ gridTemplateColumns: cols }}
          initial={reduce || !dir ? false : { x: dir * 56, opacity: 0 }}
          animate={{ x: 0, opacity: 1 }}
          transition={{ ...DETENT, opacity: { duration: 0.1 } }}
        >
          <div className="sw-week-rule" style={{ gridTemplateColumns: cols }} aria-hidden="true">
            {Array.from({ length: days }, (_, i) => <i key={i} style={d(i)} />)}
          </div>
          {Array.from({ length: days }, (_, i) => {
            const date = mon ? new Date(mon.getTime() + i * DAY) : null
            const today = isNow && i === td
            const list = displayed.filter((c) => c.weekday === i)
            const pastDay = isNow ? i < td : !!thisWeek && w < thisWeek
            return (
              <div key={i} className={`sw-day ${today ? 'is-today' : ''} ${pastDay ? 'is-pastday' : ''}`} style={d(i)}>
                <div className="sw-dh sw-in" style={d(i)}>
                  <span className="t5">{WEEKDAY_CN[i]}</span>
                  <b className="num">{date ? pad(date.getDate()) : '—'}</b>
                </div>
                <div className="sw-col">
                  {hours.map((h) => <i key={h} className="sw-hr" style={{ top: y(h * 60) }} />)}
                  {i === 0 && hours.slice(0, -1).map((h) => (
                    <span key={`l${h}`} className={`sw-hl num ${starts.has(h * 60) ? 'on' : ''}`} style={{ top: y(h * 60) }}>{pad(h)}</span>
                  ))}
                  {list.map((c, k) => {
                    const end = date ? at(date, c.end) : null
                    const past = !!end && end <= t
                    const target = !!occ && !!date && occ.course.id === c.id && occ.start.toDateString() === date.toDateString()
                    const live = target && occ!.start <= t
                    const todayCell = today && !past
                    return (
                      <motion.button
                        key={c.id}
                        className={`sw-cell ${kindClass(c)} ${todayCell ? 'is-todaycell' : ''} ${c.moveFrom ? 'is-move' : ''} ${past ? 'is-past' : ''} ${target ? 'is-target' : ''} ${live ? 'is-live' : ''}`}
                        style={{ top: y(toMin(c.start)), height: `calc(${y(toMin(c.end))} - ${y(toMin(c.start))})` }}
                        // 自上而下揭开，而不是纵向拉伸：文字在入场时不会被压扁或放大
                        initial={reduce ? false : { clipPath: 'inset(0px 0px 100% 0px)' }}
                        animate={{ clipPath: 'inset(-6px -6px -6px -6px)' }}
                        transition={{ duration: 0.42, ease: [0.22, 1, 0.36, 1], delay: (dir ? 0.06 : 0.16) + (i * 2 + k) * 0.03 }}
                        onClick={() => openSheet({ type: 'course', course: c })}
                        aria-label={`${c.name}，${c.timeSegments?.join('、')}，${c.moveFrom ? '换教室，' : ''}${c.room || '教室待定'}`}
                      >
                        {live && <i className="sw-elapsed" style={{ height: `${Math.min(100, Math.max(0, ((nowM - toMin(c.start)) / (toMin(c.end) - toMin(c.start))) * 100))}%` }} />}
                        <span className="sw-cell-title"><b data-course-text="title">{c.name}</b></span>
                        <span className="sw-cell-m">
                          {c.moveFrom && <em className="sw-move">换教室</em>}
                          {c.kind === 'practice' && !c.moveFrom && <em className="sw-kind">实践</em>}
                          <span className="num lat" data-course-text="room">{c.room || '教室待定'}</span>
                        </span>
                      </motion.button>
                    )
                  })}
                  {today && nowM >= from && nowM <= to && !list.some((c) => c.parts.some((part) => toMin(part.start) <= nowM && nowM < toMin(part.end))) && (
                    <i className="sw-nowline" style={{ top: y(nowM) }} />
                  )}
                </div>
              </div>
            )
          })}
          {!displayed.length && <div className="sw-empty" role="status">{syncing ? '正在获取课表' : all.length ? '本周课程已结束' : unplaced.length ? '课程时间待确认' : weekLoaded || schedule.scope === 'semester' ? '本周无课' : '此周暂无课表'}</div>}
        </motion.div>
      </div>
    </div>
    {!!unplaced.length && <div className="runtime-unplaced"><span>未定位课程</span>{unplaced.map((c) => <button key={c.id} className="sw-btn" onClick={() => openSheet({ type: 'course', course: c })}>{c.name} · {c.periods || '节次未提供'}</button>)}</div>}
    {!!schedule.warnings?.length && <p className="runtime-note">{[...new Set(schedule.warnings)].join('；')}</p>}
    </>
  )
}

/** 学期切换和刷新收进学期尺标题，主课表不再堆重复操作。 */
export function TermCap() {
  const { schedule, currentSchedule, user, selectTerm, sync, syncing, refreshing, captcha, jobs, jobsError, settings, browsing, connect } = useStore()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const enrollment = user?.student.enrollment_date || ''
  const current = schedule?.term

  useEffect(() => {
    if (!open) return
    const close = (event: PointerEvent) => { if (!ref.current?.contains(event.target as Node)) setOpen(false) }
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    addEventListener('pointerdown', close)
    addEventListener('keydown', escape)
    return () => { removeEventListener('pointerdown', close); removeEventListener('keydown', escape) }
  }, [open])

  if (!schedule || !current) return null
  const terms = personalTerms(schedule, enrollment, currentSchedule?.term.id || current.id || current.name)
  const updated = schedule.syncedAt ? new Date(schedule.syncedAt) : null
  const notice = browsing ? null : scheduleJobNotice(jobs?.schedule, settings?.autoRefresh ? jobsError : '')

  return (
    <div className="one-cap" ref={ref}>
      <button className={`one-cap-btn${open ? ' on' : ''}`} onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-controls="term-options">
        <span>{termLabel(current.id || current.name, enrollment)}</span>
        {(refreshing || notice) && <em className="one-cap-sync" role="status">{refreshing ? '更新中' : notice?.label}</em>}
      </button>
      <AnimatePresence>
        {open && <motion.div id="term-options" className="one-pop" initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -4 }} transition={DETENT}>
          <ol className="one-pop-terms">
            {terms.map((term) => <li key={term.value}>
              <button className={term.value === (current.id || current.name) ? 'on' : ''} disabled={syncing || !!captcha} onClick={() => { void selectTerm(term.value); setOpen(false) }}>
                {term.label}
              </button>
            </li>)}
          </ol>
          <div className="one-pop-sync">
            <span role="status">{refreshing ? '更新中' : notice ? notice.label : updated ? syncedLabel(updated) : '还没更新过'}</span>
            <button aria-label={notice?.requiresLogin ? '连接教务' : '刷新课表'} disabled={syncing || !!captcha} onClick={() => { setOpen(false); if (notice?.requiresLogin) void connect(); else void sync() }}>{notice?.requiresLogin ? '连接教务' : <>刷新<kbd>R</kbd></>}</button>
          </div>
          {notice && <p className="one-sync-error">{notice.detail}</p>}
        </motion.div>}
      </AnimatePresence>
    </div>
  )
}

function syncedLabel(value: Date) {
  const minutes = Math.round((Date.now() - value.getTime()) / 60_000)
  if (minutes < 1) return '刚刚更新'
  if (minutes < 60) return `${minutes} 分钟前更新`
  return new Date().toDateString() === value.toDateString()
    ? `今天 ${pad(value.getHours())}:${pad(value.getMinutes())} 更新`
    : `${value.getMonth() + 1} 月 ${value.getDate()} 日更新`
}
