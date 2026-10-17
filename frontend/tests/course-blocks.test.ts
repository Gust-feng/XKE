import { describe, expect, it, vi } from 'vitest'
import { adaptSemester, adaptWeek } from '../src/api/adapters'
import type { RowDto } from '../src/api/dto'
import { courseBlocks, nextCourseBlock } from '../src/lib/course-blocks'
import { combineSchedule } from '../src/lib/schedule-state'
import { row, semester, week } from './fixtures'
import demo from '../src/api/demo-data.json'
import type { Course } from '../src/api/types'

vi.hoisted(() => vi.stubGlobal('__XKE_PREVIEW__', false))
const lesson = (patch: Partial<RowDto> = {}) => row({ teacher: '测试教师', room: 'A101',
  week_numbers: [3], time_segments: ['14:00-15:40'], section_numbers: [5, 6], ...patch })
const continuation = (patch: Partial<RowDto> = {}) => lesson({ time_segments: ['16:00-17:40'], section_numbers: [7, 8], ...patch })
const schedule = (rows: RowDto[]) => adaptSemester(semester({ rows }), adaptWeek(week()))

describe('weekly display blocks', () => {
  it('merges a same-room chain while retaining original rows, gaps, and exact periods', () => {
    const source = schedule([lesson(), continuation(), continuation({ time_segments: ['18:00-19:40'], section_numbers: [9, 10] })])
    const original = structuredClone(source)
    const blocks = courseBlocks(source.courses, 3)
    expect(blocks).toHaveLength(1)
    expect(blocks[0]).toMatchObject({ start: '14:00', end: '19:40', periods: '5、6、7、8、9、10',
      weeks: [[3, 3]], timeSegments: ['14:00–15:40', '16:00–17:40', '18:00–19:40'] })
    expect(blocks[0].parts).toEqual(source.courses)
    expect(source).toEqual(original)
    expect(blocks[0].parts[1].source).toBe(source.courses[1].source)
  })

  it('resolves actual occurrences instead of requiring identical semester week rules', () => {
    const source = schedule([lesson({ week_numbers: [1, 3, 5] }), continuation({ week_numbers: [3, 7] })])
    expect(courseBlocks(source.courses, 3)[0].parts).toHaveLength(2)
    expect(courseBlocks(source.courses, 5)[0].parts).toHaveLength(1)
    expect(courseBlocks(source.courses, 7)[0].start).toBe('16:00')
    expect(courseBlocks(source.courses, 2)).toEqual([])
  })

  it.each([
    { teacher: '另一位教师' }, { course_name: '另一门课' }, { weekday: '星期二' },
    { time_segments: ['16:11-17:40'] }, { time_segments: ['15:00-17:40'] }, { room: '' },
  ])('does not merge a distinct or nonadjacent lesson: %j', (patch) => {
    expect(courseBlocks(schedule([lesson(), continuation(patch)]).courses, 3)).toHaveLength(2)
  })

  it('keeps distinct course kinds and unknown rooms separate', () => {
    const source = schedule([lesson({ room: '' }), continuation({ room: '' })])
    expect(courseBlocks(source.courses, 3)).toHaveLength(2)
    expect(courseBlocks(source.courses, 3).every((c) => !c.moveFrom)).toBe(true)
    const courses = schedule([lesson(), continuation()]).courses
    courses[1].kind = 'practice'
    expect(courseBlocks(courses, 3)).toHaveLength(2)
  })

  it('does not merge across an intervening class or an overlapping arrangement', () => {
    const interleaved = schedule([lesson(), continuation(), lesson({ course_name: '另一门课', time_segments: ['15:40-15:55'] })])
    expect(courseBlocks(interleaved.courses, 3)).toHaveLength(3)
    const overlapping = schedule([lesson(), continuation(), lesson({ course_name: '重叠安排', time_segments: ['14:30-16:10'] })])
    const blocks = courseBlocks(overlapping.courses, 3)
    expect(blocks).toHaveLength(3)
    expect(blocks.every((c) => !c.moveFrom)).toBe(true)
  })

  it('does not mark a room change between different subjects', () => {
    const blocks = courseBlocks(schedule([lesson(), continuation(), continuation({ course_name: '下一门课', room: 'B202', time_segments: ['18:00-19:40'] })]).courses, 3)
    expect(blocks).toHaveLength(2)
    expect(blocks[1].moveFrom).toBeUndefined()
    expect(blocks[1].room).toBe('B202')
  })

  it('marks only a continuous lesson of the same course when its room changes', () => {
    const blocks = courseBlocks(schedule([lesson(), continuation({ room: 'B202' })]).courses, 3)
    expect(blocks).toHaveLength(2)
    expect(blocks[1].moveFrom).toEqual({ room: 'A101', previousId: blocks[0].id })
  })

  it.each([{ teacher: '另一位教师' }, { course_name: '下一门课' }])('does not treat unrelated lessons as a classroom continuation: %j', (patch) => {
    const blocks = courseBlocks(schedule([lesson(), continuation({ room: 'B202', ...patch })]).courses, 3)
    expect(blocks.every((block) => !block.moveFrom)).toBe(true)
  })

  it('keeps display identities when a weekly cache replaces semester rows with different source IDs and row positions', () => {
    const rows = [lesson(), continuation({ room: 'B202' })]
    const full = schedule([lesson({ week_numbers: [5], course_name: '其他周的课' }), ...rows])
    const weekly = adaptWeek(week(3, { rows, updated_at: '2026-09-22T08:00:00+08:00' }))
    const before = courseBlocks(combineSchedule(full, [], 3)!.courses, 3)
    const after = courseBlocks(combineSchedule(full, [weekly], 3)!.courses, 3)
    expect(after.map((block) => block.id)).toEqual(before.map((block) => block.id))
    expect(after[1].moveFrom?.previousId).toBe(after[0].id)
    expect(after[0].parts[0].id).not.toBe(before[0].parts[0].id)
  })

  it('keeps separate overlapping copies distinguishable without depending on source row IDs', () => {
    const courses = schedule([lesson(), lesson()]).courses
    const blocks = courseBlocks(courses, 3)
    expect(new Set(blocks.map((block) => block.id)).size).toBe(2)
    const changedIds = courses.map((course, index) => ({ ...course, id: `new-source-${index}` }))
    expect(courseBlocks(changedIds, 3).map((block) => block.id)).toEqual(blocks.map((block) => block.id))
  })

  it('never carries a room reminder across different weeks or a long break', () => {
    const source = schedule([lesson({ week_numbers: [3] }), continuation({ room: 'B202', week_numbers: [5] })])
    expect(courseBlocks(source.courses, 3)[0].moveFrom).toBeUndefined()
    expect(courseBlocks(source.courses, 5)[0].moveFrom).toBeUndefined()
    expect(courseBlocks(schedule([lesson(), continuation({ room: 'B202', time_segments: ['18:00-19:40'] })]).courses, 3)[1].moveFrom).toBeUndefined()
  })

  it('uses a refreshed week classroom without changing the rest of the semester', () => {
    const full = schedule([lesson({ week_numbers: [3, 5] }), continuation({ week_numbers: [3, 5] })])
    const snapshot = adaptWeek(week(3, { rows: [lesson(), continuation({ room: 'B202' })], updated_at: '2026-09-22T08:00:00+08:00' }))
    const combined = combineSchedule(full, [snapshot], 3)!
    expect(courseBlocks(combined.courses, 3)).toHaveLength(2)
    expect(courseBlocks(combined.courses, 3)[1].moveFrom?.room).toBe('A101')
    expect(courseBlocks(combined.courses, 5)).toHaveLength(1)
  })

  it('preserves each real segment rather than duplicating a source row time list', () => {
    const source = schedule([lesson({ time_segments: ['14:00-15:40', '16:00-17:40'], section_numbers: [5, 6, 7, 8] })])
    const block = courseBlocks(source.courses, 3)[0]
    expect(block.timeSegments).toEqual(['14:00–15:40', '16:00–17:40'])
    expect(block.periods).toBe('5、6、7、8')
    expect(block.parts).toHaveLength(2)
  })

  it('the shared synthetic preview exercises merging and a separate classroom move', () => {
    const blocks = courseBlocks(demo.schedule.courses as Course[], 5)
    const merged = blocks.find((c) => c.name === '云计算基础')!
    expect(merged.parts).toHaveLength(2)
    expect(merged.periods).toBe('5–6、7–8')
    const moved = blocks.find((c) => c.name === '大学英语' && c.room === 'B-203')!
    expect(moved.moveFrom?.room).toBe('B-102')
  })

  it('leaves courses without usable coordinates in the source for the unplaced list', () => {
    const source = schedule([lesson({ time_segments: [], time_range: '', weekday: '待定' }), lesson({ week_numbers: [] })])
    expect(courseBlocks(source.courses, 3)).toEqual([])
    expect(source.courses).toHaveLength(2)
  })
})

describe('next lesson and room reminder', () => {
  it.each([
    { hour: 13, minute: 50, part: '14:00', inBreak: false },
    { hour: 14, minute: 30, part: '14:00', inBreak: false },
    { hour: 15, minute: 40, part: '16:00', inBreak: true },
    { hour: 15, minute: 50, part: '16:00', inBreak: true },
    { hour: 16, minute: 0, part: '16:00', inBreak: false },
  ])('at $hour:$minute keeps the merged block and selects the real segment', ({ hour, minute, part, inBreak }) => {
    const source = schedule([lesson(), continuation()])
    const next = nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, hour, minute))!
    expect(next.course.parts).toHaveLength(2)
    expect(next.part.start).toBe(part)
    expect(next.inBreak).toBe(inBreak)
    expect(next.end.getHours()).toBe(part === '14:00' ? 15 : 17)
  })

  it('warns before the move and marks the destination as soon as it becomes next', () => {
    const source = schedule([lesson(), continuation({ room: 'B202' })])
    const current = nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, 15))!
    expect(current.moveTo).toMatchObject({ start: '16:00', room: 'B202' })
    const next = nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, 15, 50))!
    expect(next.course.moveFrom?.room).toBe('A101')
    expect(next.course.room).toBe('B202')
    expect(next.inBreak).toBe(false)
  })

  it('never chooses a room-change hint from an unrelated week', () => {
    const source = schedule([lesson({ week_numbers: [3, 5] }), continuation({ room: 'B202', week_numbers: [5] })])
    const next = nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, 14, 30))!
    expect(next.moveTo).toBeUndefined()
    const later = nextCourseBlock(source.term, source.courses, new Date(2026, 9, 5, 14, 30))!
    expect(later.moveTo?.room).toBe('B202')
  })

  it('requires a semester date and stops offering a finished one-off lesson', () => {
    const source = schedule([lesson(), continuation()])
    expect(nextCourseBlock({ ...source.term, startDate: '' }, source.courses, new Date(2026, 8, 21))).toBeNull()
    expect(nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, 17, 40))).toBeNull()
  })

  it('finds the next scheduled occurrence beyond a long break without claiming the term is over', () => {
    const source = schedule([lesson({ week_numbers: [9] })])
    const next = nextCourseBlock(source.term, source.courses, new Date(2026, 8, 21, 17, 40))!
    expect(next.dayOffset).toBe(42)
    expect(next.start).toEqual(new Date(2026, 10, 2, 14))
  })
})
