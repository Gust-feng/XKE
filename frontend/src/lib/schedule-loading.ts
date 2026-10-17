import { ApiError } from '../api/http'
import type { Api, Schedule } from '../api/types'

interface SemesterLoad {
  term?: string
  context?: Schedule | null
  connected: boolean
  refresh?: boolean
  fetchMissing?: boolean
  isCurrent(): boolean
  accept(schedule: Schedule): void
  acceptWeek?(schedule: Schedule): void
}

/** Calendar metadata must come from a snapshot of the same semester, never from today's date. */
function withCalendar(semester: Schedule, week: Schedule): Schedule {
  if (semester.term.id !== week.term.id) return semester
  return { ...semester,
    term: { ...semester.term, startDate: semester.term.startDate || week.term.startDate,
      totalWeeks: Math.max(semester.term.totalWeeks, week.term.totalWeeks) },
    currentWeek: semester.currentWeek ?? week.currentWeek,
    availableWeeks: semester.availableWeeks?.length ? semester.availableWeeks : week.availableWeeks,
    blockTimes: semester.blockTimes?.length ? semester.blockTimes : week.blockTimes,
  }
}

/** Publish local data first; calendar lookup and school refresh never replace it with another term. */
export async function loadSemester(api: Pick<Api, 'getLocalSemester' | 'getSemester' | 'getSchedule'>, load: SemesterLoad): Promise<boolean> {
  let term = load.term || load.context?.term.id || undefined
  let local: Schedule | null = null
  let context = load.context
  let weekHit = false
  try { local = await api.getLocalSemester(term, context) }
  catch (error) {
    if (!load.isCurrent()) return false
    if (!(error instanceof ApiError && error.status === 404)) throw error
  }
  if (!load.isCurrent()) return false
  if (local) {
    term = local.term.id || term
    load.accept(local)
    context = local
  }
  // The semester DTO has no dates. A local same-term week supplies its real calendar.
  // Missing metadata is not a failed semester read, nor a reason to request an arbitrary week.
  if (term && (!local || !local.term.startDate)) {
    try {
      const week = await api.getSchedule(undefined, term)
      if (!load.isCurrent()) return false
      if (week.term.id !== term) throw new Error('返回课表与所选学期不一致')
      if (local) { local = withCalendar(local, week); context = local; load.accept(local) }
      else {
        context = week
        if (load.acceptWeek) { weekHit = true; load.acceptWeek(week) }
      }
    } catch (error) {
      if (!load.isCurrent()) return false
      if (!(error instanceof ApiError && error.status === 404)) throw error
    }
  }
  if (local && !load.refresh || !load.connected && !load.fetchMissing) return !!local || weekHit
  const refreshed = await api.getSemester(term, context)
  if (!load.isCurrent()) return false
  load.accept(refreshed)
  return true
}

/** Serial cache fill over an already filtered school directory; never changes the selected semester. */
export async function cacheMissingSemesters(
  api: Pick<Api, 'getLocalSemester' | 'getSemester' | 'getSchedule'>,
  terms: string[],
  options: { isCurrent(): boolean; accept(schedule: Schedule): void },
): Promise<string[]> {
  const failed: string[] = []
  for (const term of new Set(terms)) {
    if (!options.isCurrent()) break
    try {
      await loadSemester(api, { term, connected: true, isCurrent: options.isCurrent, accept: options.accept })
    } catch (error) {
      if (!options.isCurrent()) break
      if (error instanceof ApiError && error.status === 401) throw error
      failed.push(term)
    }
  }
  return failed
}
