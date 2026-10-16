import { useEffect, useState } from 'react'
import type { Course, Term } from '../api/types'

/** 预览用：URL 加 ?t=2026-10-08T15:10 可以把时钟拨到任意时刻 */
const override = __XKE_PREVIEW__ ? new URLSearchParams(location.search).get('t') : null
const offset = override ? new Date(override).getTime() - Date.now() : 0
export const now = () => new Date(Date.now() + offset)

export function useNow(intervalMs = 1000) {
  const [t, setT] = useState(now)
  useEffect(() => {
    const id = setInterval(() => setT(now()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])
  return t
}

export const DAY = 864e5
export const WEEK = DAY * 7
export const WEEKDAY_CN = ['周一', '周二', '周三', '周四', '周五', '周六', '周日']
export const WEEKDAY_EN = ['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN']
export const WEEKDAY_FULL = ['MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY', 'SUNDAY']
export const MONTH_CN = ['一月', '二月', '三月', '四月', '五月', '六月', '七月', '八月', '九月', '十月', '十一月', '十二月']

export const pad = (n: number) => String(n).padStart(2, '0')
export const toMin = (hhmm: string) => {
  const [h, m] = hhmm.split(':').map(Number)
  return h * 60 + m
}
export const minOfDay = (d: Date) => d.getHours() * 60 + d.getMinutes() + d.getSeconds() / 60
/** 0 = 周一 */
export const weekday = (d: Date) => (d.getDay() + 6) % 7
export const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())

export const termStart = (term: Term) => {
  const [y, m, d] = term.startDate.split('-').map(Number)
  return new Date(y, m - 1, d)
}
export const weekOf = (term: Term, d: Date) =>
  term.startDate ? Math.floor((startOfDay(d).getTime() - termStart(term).getTime()) / WEEK) + 1 : NaN
export const mondayOf = (term: Term, week: number) => new Date(termStart(term).getTime() + (week - 1) * WEEK)

export const inWeek = (c: Course, w: number) => c.weeks.some(([a, b]) => w >= a && w <= b)
export const weeksLabel = (c: Course) => c.weeks.map(([a, b]) => (a === b ? `${a}` : `${a}–${b}`)).join('、')

export const at = (day: Date, hhmm: string) => {
  const d = new Date(day)
  const [h, m] = hhmm.split(':').map(Number)
  d.setHours(h, m, 0, 0)
  return d
}

export function relativeDay(offset: number, d: Date) {
  if (offset === 0) return '今天'
  if (offset === 1) return '明天'
  if (offset === 2) return '后天'
  return offset < 7 ? WEEKDAY_CN[weekday(d)] : `${d.getMonth() + 1}月${d.getDate()}日 · ${WEEKDAY_CN[weekday(d)]}`
}

export function sinceLabel(iso: string | null, t: Date) {
  if (!iso) return '从未'
  const m = Math.floor((t.getTime() - new Date(iso).getTime()) / 6e4)
  if (m < 1) return '刚刚'
  if (m < 60) return `${m} 分钟前`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} 小时前`
  return `${Math.floor(h / 24)} 天前`
}
