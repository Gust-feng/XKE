import type { Course, Term } from '../api/types'
import { DAY, WEEK, at, inWeek, mondayOf, startOfDay, toMin, weekOf, weekday } from './time'

/** A display block for one week; original rows remain available for details and export. */
export interface CourseBlock extends Course {
  parts: Course[]
  moveFrom?: { room: string; previousId: string }
}

const BREAK_MINUTES = 30
const identity = (course: Course) => [course.name.trim(), course.teacher.trim(), course.kind].join('\u0000')
const room = (course: Course) => course.room.trim()
const periods = (parts: Course[]) => parts.every((p) => p.source?.section_numbers?.length)
  ? [...new Set(parts.flatMap((p) => p.source?.section_numbers || []))].sort((a, b) => a - b).join('、')
  : [...new Set(parts.map((p) => p.periods).filter(Boolean))].join('、')

/** Merge chronological neighbours only after resolving this week's actual occurrences. */
export function courseBlocks(courses: Course[], week: number): CourseBlock[] {
  const ordered = courses.filter((c) => c.timed !== false && c.weekday >= 0 && inWeek(c, week))
    .sort((a, b) => a.weekday - b.weekday || toMin(a.start) - toMin(b.start))
  const overlaps = new Set<Course>()
  for (let i = 0; i < ordered.length; i++) {
    for (let j = i + 1; j < ordered.length && ordered[j].weekday === ordered[i].weekday
      && toMin(ordered[j].start) < toMin(ordered[i].end); j++) {
      overlaps.add(ordered[i]); overlaps.add(ordered[j])
    }
  }
  const blocks: CourseBlock[] = []
  for (const course of ordered) {
    const previous = blocks.at(-1)
    const gap = previous ? toMin(course.start) - toMin(previous.end) : -1
    // Overlapping lessons are kept separate and must not be bridged by a later block.
    const adjacent = previous && previous.weekday === course.weekday && gap >= 0 && gap <= BREAK_MINUTES
      && !overlaps.has(course) && !previous.parts.some((p) => overlaps.has(p))
    if (adjacent && room(previous) && room(previous) === room(course) && identity(previous) === identity(course)) {
      const parts = [...previous.parts, course]
      blocks[blocks.length - 1] = { ...previous, end: course.end, parts, periods: periods(parts),
        timeSegments: parts.map((p) => `${p.start}–${p.end}`) }
    } else {
      blocks.push({ ...course, id: `block:${week}:${course.id}`, weeks: [[week, week]], parts: [course],
        timeSegments: [`${course.start}–${course.end}`],
        moveFrom: adjacent && room(previous) && room(course) && room(previous) !== room(course)
          ? { room: previous.room, previousId: previous.id } : undefined })
    }
  }
  return blocks
}

export interface BlockOccurrence {
  course: CourseBlock
  part: Course
  start: Date
  end: Date
  dayOffset: number
  inBreak: boolean
  moveTo?: CourseBlock
}

/** The card and timetable share blocks, while countdowns still follow real lesson segments. */
export function nextCourseBlock(term: Term, courses: Course[], now: Date): BlockOccurrence | null {
  if (!term.startDate) return null
  const currentWeek = weekOf(term, now)
  let nextDay: Date | null = null
  for (const course of courses) {
    if (course.timed === false || course.weekday < 0) continue
    for (const [first, last] of course.weeks) {
      let week = Math.max(first, currentWeek)
      if (week > last) continue
      let day = new Date(mondayOf(term, week).getTime() + course.weekday * DAY)
      if (at(day, course.end) <= now) { week++; day = new Date(day.getTime() + WEEK) }
      if (week <= last && (!nextDay || day < nextDay)) nextDay = day
    }
  }
  if (!nextDay) return null
  const blocks = courseBlocks(courses, weekOf(term, nextDay)).filter((c) => c.weekday === weekday(nextDay))
  const course = blocks.find((c) => at(nextDay, c.end) > now)!
  const part = course.parts.find((p) => at(nextDay, p.end) > now)!
  const start = at(nextDay, part.start)
  return { course, part, start, end: at(nextDay, part.end),
    dayOffset: Math.round((nextDay.getTime() - startOfDay(now).getTime()) / DAY),
    inBreak: start > now && at(nextDay, course.start) <= now,
    moveTo: blocks.find((c) => c.moveFrom?.previousId === course.id) }
}
