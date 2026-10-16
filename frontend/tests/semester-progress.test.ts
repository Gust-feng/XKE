import { describe, expect, it, vi } from 'vitest'
import { adaptSemester, adaptWeek } from '../src/api/adapters'
import type { Schedule } from '../src/api/types'
import { combineSchedule } from '../src/lib/schedule-state'
import { semesterProgress } from '../src/lib/semester-progress'
import { row, semester, week } from './fixtures'

vi.hoisted(() => { vi.stubGlobal('__XKE_PREVIEW__', false) })

const at = (s: string) => new Date(s)
const base = () => adaptSemester(semester(), adaptWeek(week()))

describe('scheduled semester progress', () => {
  it('counts normalized sections, not the first two numbers of their display label', () => {
    const data = semesterProgress(base(), at('2026-09-07T12:00:00'))!
    expect(data.cells).toHaveLength(15) // three periods on five odd weeks
    expect(data.done).toBe(3)
    expect(data.cells.slice(3, 6)).toEqual(['now', 'now', 'now'])
  })
  it('counts split spans of one raw row only once and does not infer periods from duration', () => {
    const model = adaptSemester(semester({ rows: [row({ section_numbers: [2, 4, 7], time_segments: ['10:10-12:00', '14:00-17:40'] })] }), adaptWeek(week()))
    expect(model.courses).toHaveLength(2)
    const duringBreak = semesterProgress(model, at('2026-09-07T13:00:00'))!
    expect(duringBreak.cells).toHaveLength(15)
    expect(duringBreak.done).toBe(0) // finish an occurrence only after its last actual span
    expect(semesterProgress(model, at('2026-09-07T17:40:00'))!.done).toBe(3)
  })
  it('honours fresher empty and changed week snapshots without double counting them', () => {
    const changed = adaptWeek(week(3, { rows: [row({ section_numbers: [5, 6] })] }))
    const empty = adaptWeek(week(5, { rows: [] }))
    const merged = combineSchedule(base(), [changed, empty], 3)!
    expect(merged.semesterLoaded).toBe(true)
    expect(merged.scope).toBe('week')
    expect(semesterProgress(merged, at('2026-10-01T00:00:00'))!.cells).toHaveLength(11)
  })
  it('marks the complete historical term fully elapsed and a future term not elapsed', () => {
    expect(semesterProgress(base(), at('2027-01-01T00:00:00'))).toEqual({ cells: Array(15).fill('past'), done: 15 })
    const future = semesterProgress(base(), at('2026-08-01T00:00:00'))!
    expect(future.done).toBe(0)
    expect(future.cells.filter(c => c === 'now')).toHaveLength(3)
  })
  it('keeps exact boundaries and treats breaks as part of an unfinished occurrence', () => {
    expect(semesterProgress(base(), at('2026-09-07T11:59:00'))!.done).toBe(0)
    expect(semesterProgress(base(), at('2026-09-07T12:00:00'))!.done).toBe(3)
  })
  it('does not claim a full-semester total from partial caches or missing date/section/time/week data', () => {
    expect(semesterProgress(null, new Date())).toBeNull()
    expect(semesterProgress(adaptWeek(week()), new Date())).toBeNull()
    for (const patch of [{ section_numbers: [] }, { week_numbers: [] }, { weekday: '待定' }, { time_segments: [], time_range: '', block_time: '' }]) {
      const model = adaptSemester(semester({ rows: [row(patch)] }), adaptWeek(week()))
      expect(semesterProgress(model, new Date())).toBeNull()
    }
    expect(semesterProgress({ ...base(), term: { ...base().term, startDate: '' } }, new Date())).toBeNull()
  })
  it('supports the accepted synthetic design labels without changing runtime DTOs', () => {
    const model: Schedule = { term: { name: '合成', startDate: '2026-09-07', totalWeeks: 1 }, scope: 'semester', syncedAt: null,
      courses: [{ id: 'demo', name: '合成课程', room: '', teacher: '', kind: 'unknown', weekday: 0, start: '09:00', end: '12:00', periods: '1–3、5', weeks: [[1, 1]] }] }
    expect(semesterProgress(model, at('2026-09-07T12:00:00'))!.done).toBe(4)
    expect(semesterProgress({ ...model, courses: [] }, new Date())).toEqual({ cells: [], done: 0 })
  })
})
