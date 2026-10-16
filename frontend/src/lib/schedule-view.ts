import type { Course, Schedule } from '../api/types'
import { inWeek, toMin } from './time'
import { courseBlocks } from './course-blocks'

function timeRanges(values: string[]) {
  return values.flatMap((value) => [...value.matchAll(/(\d{1,2}:\d{2})\s*[-–—~～至]\s*(\d{1,2}:\d{2})/g)]
    .map((match) => [toMin(match[1].padStart(5, '0')), toMin(match[2].padStart(5, '0'))] as [number, number]))
}

/** Use the server timetable first, otherwise use all known course times. */
export function weekTimeRange(courses: Course[], blockTimes: Schedule['blockTimes'] = [], referenceCourses = courses) {
  const serverRanges = timeRanges((blockTimes || []).map((block) => block.blockTime))
  const timed = referenceCourses.filter((c) => c.timed !== false)
  const courseRanges = timed.flatMap((c) => [[toMin(c.start), toMin(c.end)] as [number, number]])
  const ranges = serverRanges.length ? serverRanges : courseRanges
  if (!ranges.length) return { from: 8 * 60, to: 18 * 60 }
  const earliest = Math.floor(Math.min(...ranges.map(([start]) => start)) / 60) * 60
  const from = earliest
  const to = Math.ceil(Math.max(...ranges.map(([, end]) => end)) / 60) * 60
  return { from, to }
}

/** A full semester can trim empty calendar tails without losing intermediate empty weeks. */
export function courseWeekCount(schedule: Schedule, selected: number, current = schedule.currentWeek) {
  if (!schedule.semesterLoaded && schedule.scope !== 'semester') return schedule.term.totalWeeks
  const last = Math.max(0, ...schedule.courses.flatMap((c) => c.weeks.map(([, end]) => end)))
  const currentInTerm = current && current >= 1 && current <= schedule.term.totalWeeks ? current : 0
  return last ? Math.max(last, selected, currentInTerm) : schedule.term.totalWeeks
}

/** The ruler describes the semester plan, not the week snapshots being browsed. */
export function semesterRuler(semester: Schedule | null, context: Schedule | null, selected: number, current: number | null) {
  const reference = semester || context
  const total = reference ? courseWeekCount(reference, selected, current) : 1
  const counts = Array.from({ length: total }, (_, i) => semester ? courseBlocks(semester.courses, i + 1).length : null)
  return { total, counts, peak: Math.max(1, ...counts.map((count) => count ?? 0)) }
}

function termParts(value: string) {
  const id = /^(\d{4})-(\d{4})-([12])$/.exec(value)
  if (id) return { startYear: Number(id[1]), semester: Number(id[3]) }
  const label = /^(\d{4})[–—-](\d{4}).*?(?:第?([一二两12])(?:学期|季))/.exec(value)
  if (!label) return null
  return { startYear: Number(label[1]), semester: /[一1]/.test(label[3]) ? 1 : 2 }
}

function enrollmentYear(value: string) {
  const match = /^(\d{4})(?:[-/.年](\d{1,2}))?/.exec(value)
  if (!match) return null
  const year = Number(match[1]); const month = Number(match[2] || 9)
  return month < 8 ? year - 1 : year
}

const gradeNames = ['', '一', '二', '三', '四', '五', '六', '七', '八', '九', '十']

export function termLabel(value: string, enrollmentDate = '') {
  const parts = termParts(value)
  if (!parts) return value
  const firstYear = enrollmentYear(enrollmentDate)
  const suffix = parts.semester === 1 ? '上学期' : '下学期'
  if (firstYear === null) return suffix
  const grade = Math.max(1, parts.startYear - firstYear + 1)
  return `大${gradeNames[grade] || grade}${suffix}`
}

/** The school's term directory spans all students; expose only this student's study period. */
export function personalTerms(schedule: Schedule, enrollmentDate: string, current = schedule.term.id) {
  const enrollment = /^(\d{4})(?:[-/.年](\d{1,2}))?/.exec(enrollmentDate)
  const firstTerm = enrollment ? Number(enrollment[1]) * 2 - (Number(enrollment[2] || 9) < 8 ? 1 : 0) : null
  const rank = (value: string) => {
    const parts = /^(\d{4})-\d{4}-([12])$/.exec(value)
    return parts ? Number(parts[1]) * 2 + Number(parts[2]) - 1 : null
  }
  const currentRank = current ? rank(current) : null
  const options = [...(schedule.availableTerms || []), { value: schedule.term.id || schedule.term.name, label: schedule.term.name }]
  if (current) options.push({ value: current, label: current })
  return options.filter((option, index) => options.findIndex((o) => o.value === option.value) === index)
    .filter((option) => {
      if (option.value === current || option.value === schedule.term.id) return true
      const order = rank(option.value)
      return firstTerm !== null && order !== null && order >= firstTerm && currentRank !== null && order <= currentRank
    })
    .sort((a, b) => (rank(b.value) || 0) - (rank(a.value) || 0))
    .map((option) => ({ ...option, label: termLabel(option.value, enrollmentDate) }))
}

export const coursesInWeek = (schedule: Schedule, week: number, days: number) =>
  schedule.courses.filter((c) => inWeek(c, week) && c.weekday >= 0 && c.weekday < days)
