import { describe, expect, it, vi } from 'vitest'
import { adaptSemester, adaptWeek } from '../src/api/adapters'
import { combineSchedule } from '../src/lib/schedule-state'
import { courseWeekCount, coursesInWeek, personalTerms, semesterRuler, termLabel, weekTimeRange } from '../src/lib/schedule-view'
import { row, semester, week } from './fixtures'

vi.hoisted(() => vi.stubGlobal('__XKE_PREVIEW__', false))

describe('schedule display range', () => {
  it('uses the selected week instead of stretching every week to semester outliers', () => {
    const schedule = adaptSemester(semester({ rows: [
      row({ week_numbers: [3], time_segments: ['10:10-12:00'] }),
      row({ week_numbers: [7], time_segments: ['07:10-08:00'] }),
      row({ week_numbers: [9], time_segments: ['20:00-22:30'] }),
    ] }))
    expect(weekTimeRange(coursesInWeek(schedule, 3, 7))).toEqual({ from: 600, to: 720 })
    expect(weekTimeRange(coursesInWeek(schedule, 9, 7))).toEqual({ from: 1200, to: 1380 })
    expect(schedule.courses).toHaveLength(3)
  })

  it('keeps real evening classes and the actual gap between morning and evening', () => {
    const schedule = adaptSemester(semester({ rows: [
      row({ time_segments: ['08:10-09:40'] }), row({ time_segments: ['19:00-21:30'] }),
    ] }))
    expect(weekTimeRange(coursesInWeek(schedule, 3, 7))).toEqual({ from: 480, to: 1320 })
  })

  it('does not let hidden weekend columns extend the displayed hours', () => {
    const schedule = adaptSemester(semester({ rows: [
      row({ time_segments: ['10:00-12:00'] }),
      row({ weekday: '星期日', time_segments: ['20:00-22:00'] }),
    ] }))
    expect(weekTimeRange(coursesInWeek(schedule, 3, 5))).toEqual({ from: 600, to: 720 })
    expect(weekTimeRange(coursesInWeek(schedule, 3, 7))).toEqual({ from: 600, to: 1320 })
  })

  it('an empty week has a usable axis without inventing courses', () => {
    expect(weekTimeRange([])).toEqual({ from: 480, to: 1080 })
  })

  it('honors the server timetable when the selected week starts later', () => {
    const schedule = adaptSemester(semester({
      rows: [row({ time_segments: ['10:00-11:40'] })],
      block_times: [
        { section: '1–2', block_time: '08:00-09:40' },
        { section: '3–4', block_time: '10:00-11:40' },
        { section: '9–10', block_time: '19:00-20:40' },
      ],
    }))
    expect(weekTimeRange(coursesInWeek(schedule, 1, 7), schedule.blockTimes, schedule.courses))
      .toEqual({ from: 480, to: 1260 })
  })

  it('uses all known semester courses when the current week starts later', () => {
    const schedule = adaptSemester(semester({ rows: [
      row({ week_numbers: [3], time_segments: ['10:00-11:40'] }),
      row({ week_numbers: [7], time_segments: ['07:10-08:00'] }),
    ] }))
    expect(weekTimeRange(coursesInWeek(schedule, 3, 7), schedule.blockTimes, schedule.courses))
      .toEqual({ from: 420, to: 720 })
  })

  it('trims only the calendar tail once the full semester is known', () => {
    const snapshot = adaptWeek(week(3, {
      available_weeks: [{ value: '2026-12-28', label: '第17周', selected: false }],
    }))
    const full = combineSchedule(adaptSemester(semester(), snapshot), [snapshot], 3)!
    expect(full.scope).toBe('week')
    expect(full.semesterLoaded).toBe(true)
    expect(courseWeekCount(full, 3)).toBe(9)
    // Empty weeks between course weeks remain navigable.
    expect(coursesInWeek(full, 2, 7)).toEqual([])
    expect(courseWeekCount(snapshot, 3)).toBe(17)
  })

  it('keeps the current and selected empty weeks reachable after the last class', () => {
    const full = adaptSemester(semester(), adaptWeek(week(3, {
      available_weeks: [{ value: '2026-12-28', label: '第17周', selected: false }],
    })))
    expect(courseWeekCount(full, 3, 12)).toBe(12)
    expect(courseWeekCount(full, 14, 12)).toBe(14)
    expect(courseWeekCount(full, 3, 28)).toBe(9) // Today is outside this semester.
  })

  it('a term directory alone does not prove full course coverage', () => {
    const snapshot = adaptWeek(week())
    expect(courseWeekCount({ ...snapshot, availableTerms: [{ value: '2026-2027-1', label: '秋' }], term: { ...snapshot.term, totalWeeks: 20 } }, 3)).toBe(20)
  })
})

describe('personal term navigation', () => {
  const terms = ['2027-2028-1', '2026-2027-2', '2026-2027-1', '2025-2026-2', '2025-2026-1', '2016-2017-2']
  const schedule = () => adaptSemester(semester({ available_terms: terms.map((value) => ({ value, label: value, selected: false })) }))

  it('excludes school-wide terms before enrollment and after the current term', () => {
    expect(personalTerms(schedule(), '2025-09-01').map((o) => o.value))
      .toEqual(['2026-2027-1', '2025-2026-2', '2025-2026-1'])
  })

  it('preserves the current term when inspecting a historical term', () => {
    const history = { ...schedule(), term: { ...schedule().term, id: '2025-2026-2' } }
    expect(personalTerms(history, '2025-09-01', '2026-2027-1').map((o) => o.value))
      .toEqual(['2026-2027-1', '2025-2026-2', '2025-2026-1'])
  })

  it('without enrollment information exposes only the current and selected terms', () => {
    expect(personalTerms(schedule(), '').map((o) => o.value)).toEqual(['2026-2027-1'])
    const history = { ...schedule(), term: { ...schedule().term, id: '2025-2026-2' } }
    expect(personalTerms(history, '', '2026-2027-1').map((o) => o.value)).toEqual(['2026-2027-1', '2025-2026-2'])
  })

  it('uses the previous academic year for spring enrollment and concise semester labels', () => {
    expect(personalTerms(schedule(), '2026-02-01').map((o) => o.value))
      .toEqual(['2026-2027-1', '2025-2026-2'])
    expect(termLabel('2025-2026-2', '2025-09-01')).toBe('大一下学期')
    expect(termLabel('2026-2027-1', '2025-09-01')).toBe('大二上学期')
    expect(termLabel('2026-2027-1')).toBe('上学期')
  })
})


describe('stable semester ruler', () => {
  it('keeps all tick lengths and the scale while a newly browsed week overrides the table', () => {
    const full = adaptSemester(semester({ rows: [row({ week_numbers: Array.from({ length: 12 }, (_, i) => i + 1) })] }), adaptWeek(week()))
    const before = semesterRuler(full, combineSchedule(full, [], 3), 3, 3)
    const snapshot = adaptWeek(week(10, { rows: [
      row({ course_name: '新增安排一' }),
      row({ course_name: '新增安排二', time_segments: ['14:00-15:40'] }),
      row({ course_name: '新增安排三', time_segments: ['16:00-17:40'] }),
    ], updated_at: '2026-09-22T08:00:00+08:00' }))
    const displayed = combineSchedule(full, [snapshot], 10)!
    expect(coursesInWeek(displayed, 10, 7)).toHaveLength(3)
    expect(semesterRuler(full, displayed, 10, 3)).toEqual(before)
    expect(before.counts).toEqual(Array(12).fill(1))
    expect(before.peak).toBe(1)
  })

  it('recomputes the overview when the complete semester itself is updated', () => {
    const full = adaptSemester(semester({ rows: [row({ week_numbers: [1, 10] })] }))
    const updated = adaptSemester(semester({ rows: [row({ week_numbers: [1, 10] }),
      row({ course_name: '新增安排', week_numbers: [10], time_segments: ['14:00-15:40'] })] }))
    expect(semesterRuler(full, full, 1, null).counts[9]).toBe(1)
    expect(semesterRuler(updated, updated, 1, null).counts[9]).toBe(2)
  })

  it('does not fabricate a semester distribution from the currently loaded week', () => {
    const partial = adaptWeek(week())
    partial.term.totalWeeks = 20
    const result = semesterRuler(null, partial, 3, 3)
    expect(result.total).toBe(20)
    expect(result.counts).toEqual(Array(20).fill(null))
    expect(result.peak).toBe(1)
  })
})
