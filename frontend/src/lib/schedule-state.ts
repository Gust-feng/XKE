import type { Schedule } from '../api/types'

export const snapshotKey = (schedule: Schedule) => `${schedule.term.id || schedule.term.name}/${schedule.viewedWeek}`

function timestamp(schedule: Schedule): number | null {
  const value = schedule.syncedAt ? Date.parse(schedule.syncedAt) : NaN
  return Number.isFinite(value) ? value : null
}

/** Known capture times outrank unknown ones; ties keep the incoming, more specific result. */
export function isAtLeastAsRecent(next: Schedule, previous: Schedule): boolean {
  const nextTime = timestamp(next), previousTime = timestamp(previous)
  return previousTime === null || nextTime !== null && nextTime >= previousTime
}

export function saveSemester(previous: Schedule | null, next: Schedule): Schedule {
  return previous && (previous.term.id || previous.term.name) === (next.term.id || next.term.name)
    && !isAtLeastAsRecent(next, previous) ? previous : next
}

/** Only weeks at least as recent as the semester override its rules, including empty results. */
export function combineSchedule(semester: Schedule | null, weeks: Schedule[], selected: number): Schedule | null {
  const latest = weeks.at(-1)
  const base = semester || latest
  if (!base) return null
  const sameTerm = weeks.filter((w) => (w.term.id || w.term.name) === (base.term.id || base.term.name))
  const appliedWeeks = sameTerm.filter((w) => !semester || isAtLeastAsRecent(w, semester))
  const overrides = new Set(appliedWeeks.map((w) => w.viewedWeek))
  const courses = semester ? semester.courses.flatMap((c) => {
    if (!c.weeks.length) return [c]
    const kept = c.weeks.flatMap(([a, b]) => Array.from({ length: b - a + 1 }, (_, n) => a + n))
      .filter((w) => !overrides.has(w))
    return kept.length ? [{ ...c, weeks: kept.map((w) => [w, w] as [number, number]) }] : []
  }) : []
  const selectedSnapshot = appliedWeeks.find((w) => w.viewedWeek === selected)
  const selectedSource = selectedSnapshot || semester
  const calendar = sameTerm.findLast((w) => w.term.startDate)
  const directory = sameTerm.findLast((w) => w.availableWeeks?.length)
  return {
    ...base, courses: [...courses, ...appliedWeeks.flatMap((w) => w.courses.map((c) => ({ ...c, id: `${w.viewedWeek}/${c.id}` })))],
    blockTimes: sameTerm.findLast((w) => w.blockTimes?.length)?.blockTimes || base.blockTimes,
    term: { ...base.term, startDate: calendar?.term.startDate || base.term.startDate,
      totalWeeks: Math.max(base.term.totalWeeks, ...sameTerm.map((w) => w.term.totalWeeks)) },
    availableWeeks: directory?.availableWeeks || base.availableWeeks,
    currentWeek: sameTerm.findLast((w) => w.currentWeek != null)?.currentWeek ?? base.currentWeek,
    viewedWeek: selected, scope: selectedSource?.scope, semesterLoaded: !!semester, syncedAt: selectedSource?.syncedAt ?? null,
    cached: selectedSource?.cached,
    warnings: [...(base.warnings || []), ...(selectedSnapshot?.warnings || [])],
  }
}

export function saveWeek(weeks: Schedule[], next: Schedule): Schedule[] {
  const previous = weeks.find((w) => snapshotKey(w) === snapshotKey(next))
  if (previous && !isAtLeastAsRecent(next, previous)) return weeks
  return [...weeks.filter((w) => snapshotKey(w) !== snapshotKey(next)), next]
}

/** Cancelling an operation prevents a delayed response from reviving its state. */
export class OperationEpoch {
  private value = 0
  next() { return ++this.value }
  current() { return this.value }
  matches(value: number) { return value === this.value }
}

/** Cache multiple semesters without confusing browsing with the active workspace. */
export function saveSemesters(snapshots: Schedule[], next: Schedule): Schedule[] {
  const previous = snapshots.find((s) => s.term.id === next.term.id)
  const saved = saveSemester(previous || null, next)
  return [...snapshots.filter((s) => s.term.id !== next.term.id), saved]
}

export function scheduleForTerm(semesters: Schedule[], weeks: Schedule[], term: string, selected: number): Schedule | null {
  // The explicit preview fixture has no server term id; its in-memory key is ''.
  const same = (s: Schedule) => (s.term.id || '') === term
  return combineSchedule(semesters.find(same) || null, weeks.filter(same), selected)
}
