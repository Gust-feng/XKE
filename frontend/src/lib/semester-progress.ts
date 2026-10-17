import type { Course, Schedule } from '../api/types'
import { DAY, at, termStart } from './time'

export type ProgressCell = 'past' | 'now' | 'future'
export interface SemesterProgress { cells: ProgressCell[]; done: number }

/** Preview courses have display labels; runtime courses already carry normalized section numbers. */
function sectionCount(course: Course) {
  if (course.source) return new Set(course.source.section_numbers).size
  const sections = new Set<number>()
  for (const part of course.periods.replace(/第|节/g, '').split(/[、,，]/)) {
    const match = /^\s*(\d+)(?:\s*[-–—~～至]\s*(\d+))?\s*$/.exec(part)
    if (!match) return 0
    const first = Number(match[1]), last = Number(match[2] || match[1])
    if (first < 1 || last < first) return 0
    for (let n = first; n <= last; n++) sections.add(n)
  }
  return sections.size
}

/** Complete scheduled periods, not attendance. Never estimate totals from duration or partial week caches. */
export function semesterProgress(schedule: Schedule | null, now: Date): SemesterProgress | null {
  if (!schedule || !(schedule.semesterLoaded || schedule.scope === 'semester') || !schedule.term.startDate) return null
  const start = termStart(schedule.term).getTime()
  if (!Number.isFinite(start)) return null
  // The adapter splits a raw row into time spans. Count its sections once, not once per span.
  const groups = new Map<NonNullable<Course['source']> | Course, Course[]>()
  for (const course of schedule.courses) {
    const key = course.source || course
    const parts = groups.get(key) || []
    parts.push(course); groups.set(key, parts)
  }
  const sessions: { start: number; end: number; count: number }[] = []
  for (const parts of groups.values()) {
    const course = parts[0], count = sectionCount(course)
    if (!count || !course.weeks.length || parts.some((c) => c.timed === false || c.weekday < 0 || !c.start || !c.end)) return null
    const weeks = new Set(course.weeks.flatMap(([a, b]) => Array.from({ length: b - a + 1 }, (_, i) => a + i)))
    for (const week of weeks) {
      const day = new Date(start + ((week - 1) * 7 + course.weekday) * DAY)
      sessions.push({ start: Math.min(...parts.map((c) => at(day, c.start).getTime())),
        end: Math.max(...parts.map((c) => at(day, c.end).getTime())), count })
    }
  }
  sessions.sort((a, b) => a.start - b.start)
  const cells: ProgressCell[] = []
  let active = false
  for (const session of sessions) {
    const cell = session.end <= now.getTime() ? 'past' : active ? 'future' : 'now'
    if (cell === 'now') active = true
    for (let n = 0; n < session.count; n++) cells.push(cell)
  }
  return { cells, done: cells.filter((c) => c === 'past').length }
}
