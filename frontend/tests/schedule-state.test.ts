import { describe, expect, it } from 'vitest'
import { adaptSemester, adaptWeek } from '../src/api/adapters'
import type { Schedule } from '../src/api/types'
import { combineSchedule, saveSemester, saveWeek } from '../src/lib/schedule-state'
import { row, semester, week } from './fixtures'

const older = '2026-09-21T08:00:00+08:00'
const newer = '2026-09-22T08:00:00+08:00'
const coursesAt = (schedule: Schedule, week: number) => schedule.courses.filter((c) => c.weeks.some(([a, b]) => a <= week && week <= b))

describe('snapshot freshness', () => {
  it.each([
    { semesterTime: newer, weekTime: older, expected: '学期教室', scope: 'semester' },
    { semesterTime: older, weekTime: newer, expected: '单周教室', scope: 'week' },
    // The strings differ but describe the same instant.
    { semesterTime: older, weekTime: '2026-09-21T00:00:00Z', expected: '单周教室', scope: 'week' },
    { semesterTime: newer, weekTime: '', expected: '学期教室', scope: 'semester' },
    { semesterTime: '', weekTime: newer, expected: '单周教室', scope: 'week' },
    { semesterTime: '', weekTime: 'unknown', expected: '单周教室', scope: 'week' },
  ])('chooses $scope for semester=$semesterTime / week=$weekTime', ({ semesterTime, weekTime, expected, scope }) => {
    const base = adaptSemester(semester({ rows: [row({ room: '学期教室' })], updated_at: semesterTime, cached: true }))
    const snapshot = adaptWeek(week(3, { rows: [row({ room: '单周教室' })], updated_at: weekTime, cached: false }))
    const result = combineSchedule(base, [snapshot], 3)!
    expect(coursesAt(result, 3).map((c) => c.room)).toEqual([expected])
    expect(coursesAt(result, 5).map((c) => c.room)).toEqual(['学期教室'])
    const source = scope === 'week' ? snapshot : base
    expect(result).toMatchObject({ scope, syncedAt: source.syncedAt, cached: source.cached })
  })

  it('ignores an older empty week, then a newer empty week clears only its own week', () => {
    const base = adaptSemester(semester({ updated_at: newer }))
    const empty = adaptWeek(week(3, { rows: [], updated_at: older }))
    expect(coursesAt(combineSchedule(base, [empty], 3)!, 3)).toHaveLength(1)
    const result = combineSchedule(base, [{ ...empty, syncedAt: '2026-09-23T00:00:00Z' }], 3)!
    expect(coursesAt(result, 3)).toEqual([])
    expect(coursesAt(result, 5)).toHaveLength(1)
    expect(result.scope).toBe('week')
  })

  it('a newer empty semester replaces older weekly content', () => {
    const base = adaptSemester(semester({ rows: [], updated_at: newer }))
    const result = combineSchedule(base, [adaptWeek(week(3, { updated_at: older }))], 3)!
    expect(result.courses).toEqual([])
    expect(result).toMatchObject({ scope: 'semester', syncedAt: newer })
  })

  it.each([older, '', 'unknown'])('saveWeek retains a newer week when the incoming capture time is %s', (updated_at) => {
    const snapshots = [adaptWeek(week(3, { updated_at: newer, rows: [row({ room: '新教室' })] }))]
    const incoming = adaptWeek(week(3, { updated_at, rows: [] }))
    expect(saveWeek(snapshots, incoming)).toBe(snapshots)
    expect(coursesAt(combineSchedule(null, saveWeek(snapshots, incoming), 3)!, 3)[0].room).toBe('新教室')
  })

  it('saveWeek accepts successful empty data at an equal or later capture time', () => {
    const old = adaptWeek(week(3, { updated_at: older }))
    const empty = adaptWeek(week(3, { updated_at: '2026-09-21T00:00:00Z', rows: [] }))
    expect(saveWeek([old], empty)).toEqual([empty])
    const later = { ...empty, syncedAt: newer }
    expect(saveWeek([empty], later)).toEqual([later])
  })

  it('keeps different weeks and terms regardless of their timestamps', () => {
    const old = adaptWeek(week(3, { updated_at: newer }))
    const otherWeek = adaptWeek(week(5, { updated_at: older }))
    const otherTerm = adaptWeek(week(3, { updated_at: older, term: { academic_term: '2025-2026-2', label: '历史' } }))
    const snapshots = saveWeek(saveWeek([old], otherWeek), otherTerm)
    expect(snapshots).toHaveLength(3)
    expect(combineSchedule(adaptSemester(semester({ updated_at: newer })), snapshots, 3)?.courses.some((c) => c.id.startsWith('5/'))).toBe(false)
    expect(combineSchedule(adaptSemester(semester({ rows: [], updated_at: newer })), [otherTerm], 3)?.courses).toEqual([])
  })

  it('keeps unknown times unknown, including a selected week that has never been loaded', () => {
    const unknown = adaptWeek(week(3, { updated_at: '' }))
    const invalid = adaptWeek(week(3, { updated_at: 'unknown', rows: [] }))
    expect(saveWeek([unknown], invalid)).toEqual([invalid])
    expect(combineSchedule(adaptSemester(semester({ updated_at: '' })), [invalid], 3)?.syncedAt).toBeNull()
    expect(combineSchedule(null, [adaptWeek(week())], 5)).toMatchObject({ syncedAt: null, scope: undefined })
  })

  it('does not downgrade an already loaded semester with older local or refresh data', () => {
    const base = adaptSemester(semester({ updated_at: newer }))
    expect(saveSemester(base, adaptSemester(semester({ updated_at: older, cached: true })))).toBe(base)
    expect(saveSemester(base, adaptSemester(semester({ updated_at: '' })))).toBe(base)
    const other = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' }, updated_at: older }))
    expect(saveSemester(base, other)).toBe(other)
  })
})


describe('separate active and browsed schedules', () => {
  it('does not let a later-read archive replace the active term or its calendar', async () => {
    const { saveSemesters, scheduleForTerm } = await import('../src/lib/schedule-state')
    const current = adaptSemester(semester(), adaptWeek(week()))
    const history = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' }, updated_at: '2026-10-13T08:00:00+08:00' }))
    let saved = saveSemesters([], current)
    saved = saveSemesters(saved, history)
    expect(scheduleForTerm(saved, [], current.term.id!, 3)?.term).toEqual(current.term)
    expect(scheduleForTerm(saved, [], history.term.id!, 1)?.term).toEqual(history.term)
    expect(scheduleForTerm(saved, [], '2024-2025-1', 1)).toBeNull()
    expect(saved).toHaveLength(2)
  })
})


describe('explicit preview term lookup', () => {
  it('resolves the id-free preview semester by the empty in-memory key', async () => {
    const { scheduleForTerm } = await import('../src/lib/schedule-state')
    const { default: fixture } = await import('../src/api/demo-data.json')
    const preview = fixture.schedule as import('../src/api/types').Schedule
    expect(preview.term.id).toBeUndefined()
    const found = scheduleForTerm([preview], [], '', 3)
    expect(found?.courses).toHaveLength(preview.courses.length)
    expect(found?.term.name).toBe(preview.term.name)
    expect(scheduleForTerm([preview], [], '2025-2026-2', 3)).toBeNull()
  })
})


describe('calendar metadata is independent of course freshness', () => {
  it('does not let a newer undated week directory erase an earlier same-term date anchor', () => {
    const dated = adaptWeek(week(3, { updated_at: older }))
    const next = adaptWeek(week(6, { week_dates: {}, available_weeks: [{ label: '第6周', value: '6', selected: true }], updated_at: newer }))
    const merged = combineSchedule(adaptSemester(semester()), [dated, next], 6)!
    expect(merged.term.startDate).toBe(dated.term.startDate)
    expect(merged.availableWeeks).toEqual(next.availableWeeks)
    expect(merged.syncedAt).toBe(newer)
  })
})
