import { describe, expect, it } from 'vitest'
import { adaptNetwork, adaptSemester, adaptSettings, adaptWeek, settingsPatches, weekNumber } from '../src/api/adapters'
import { combineSchedule, saveWeek } from '../src/lib/schedule-state'
import { account, row, semester, week } from './fixtures'
import { networkPatch } from '../src/api/network-patch'

describe('real DTO adaptation', () => {
  it('keeps an explicit authentication failure even when an external route is already online', () => {
    const status = adaptNetwork({ checked_at: '', campus_connected: true, internet_connected: true, interface: { ipv4: '192.0.2.8' }, portal: {}, settings: account().network, message: '认证失败', operation_error: '服务器限制当前IP' }, '')
    expect(status.online).toBe(true)
    expect(status.error).toBe('服务器限制当前IP')
  })
  it('uses labels rather than date query values as week numbers and derives the source Monday', () => {
    const data = adaptWeek(week())
    expect(data.term.startDate).toBe('2026-09-07')
    expect(data.availableWeeks?.map((o) => o.number)).toEqual([1, 3])
    expect(weekNumber('2026-09-21')).toBeUndefined()
    expect(data.courses[0].periods).toBe('2、3、4')
  })
  it('does not invent dates for a historical semester', () => {
    const data = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' } }), adaptWeek(week()))
    expect(data.term.startDate).toBe('')
    expect(data.currentWeek).toBeNull()
  })
  it('preserves odd/list weeks, distinct rooms, hours and unknown source fields', () => {
    const data = adaptSemester(semester({ rows: [row({ unknown_field: 'kept' }), row({ room: '测试202', week_numbers: [2, 4, 6] })] }))
    expect(data.courses).toHaveLength(2)
    expect(data.courses[0].weeks).toEqual([[1, 1], [3, 3], [5, 5], [7, 7], [9, 9]])
    expect(data.courses[0].source?.unknown_field).toBe('kept')
    expect(data.courses[0].source?.hours_text).toContain('理实一体:64')
    expect(data.courses[1].room).toBe('测试202')
    expect(data.courses.every((c) => c.kind === 'unknown' && c.teacher === '')).toBe(true)
  })
  it('renders server segments separately and retains the complete list', () => {
    const data = adaptSemester(semester({ rows: [row({ time_segments: ['08:00-09:30', '10:00-11:30'] })] }))
    expect(data.courses.map((c) => [c.start, c.end])).toEqual([['08:00', '09:30'], ['10:00', '11:30']])
    expect(data.courses[1].timeSegments).toHaveLength(2)
  })
  it('keeps unknown times and weekdays without fake coordinates', () => {
    const data = adaptWeek(week(3, { rows: [row({ weekday: '待定', time_segments: [], time_range: '' })] }))
    expect(data.courses[0]).toMatchObject({ start: '', end: '', timed: false, weekday: -1 })
    expect(data.courses[0].name).toBe('合成课程')
  })
  it('accepts a valid empty week', () => expect(adaptWeek(week(3, { rows: [] })).courses).toEqual([]))
  it('requires a recognizable returned week rather than silently substituting another', () => {
    expect(() => adaptWeek(week(3, { viewed_week_number: null, week_label: '无法识别' }))).toThrow('周次')
  })
})
describe('semester and weekly snapshots', () => {
  it('an empty fetched week removes only that week from the semester rules', () => {
    const base = adaptSemester(semester())
    const merged = combineSchedule(base, [adaptWeek(week(3, { rows: [] }))], 3)!
    expect(merged.courses.some((c) => c.weeks.some(([a, b]) => a <= 3 && b >= 3))).toBe(false)
    expect(merged.courses.some((c) => c.weeks.some(([a, b]) => a <= 5 && b >= 5))).toBe(true)
  })
  it('a fetched change replaces its week, leaves other weeks and latest snapshot wins', () => {
    const old = adaptWeek(week())
    const next = adaptWeek(week(3, { rows: [row({ room: '新教室' })] }))
    const snapshots = saveWeek([old], next)
    expect(snapshots).toHaveLength(1)
    const merged = combineSchedule(adaptSemester(semester()), snapshots, 3)!
    expect(merged.courses.find((c) => c.weeks.some(([a, b]) => a <= 3 && b >= 3))?.room).toBe('新教室')
    expect(merged.courses.find((c) => c.weeks.some(([a, b]) => a <= 5 && b >= 5))?.room).toBe('测试101')
  })
  it('does not substitute another semester with the same week number', () => {
    const other = adaptWeek(week(3, { term: { academic_term: '2025-2026-2', label: '历史' } }))
    expect(combineSchedule(adaptSemester(semester()), [other], 3)?.courses).toHaveLength(1)
  })
  it('retains rows with missing week rules for the unplaced list', () => {
    const base = adaptSemester(semester({ rows: [row({ week_numbers: [] })] }))
    expect(combineSchedule(base, [], 3)?.courses).toHaveLength(1)
  })
})
describe('settings and network', () => {
  it('network patches omit untouched accounts and masked passwords', () => {
    expect(networkPatch({ heartbeat_enabled: false }, 'telecom', null, '')).toEqual({ heartbeat_enabled: false })
    expect(networkPatch({}, 'unicom', ' new-account ', 'new-password')).toEqual({ unicom_account: 'new-account', unicom_password: 'new-password' })
    expect(networkPatch({ provider: 'campus' }, 'campus', ' campus-account ', 'campus-password')).toEqual({ provider: 'campus', campus_account: 'campus-account', campus_password: 'campus-password' })
    expect(networkPatch({ heartbeat_interval_minutes: 9 }, 'campus', null, '')).toEqual({ heartbeat_interval_minutes: 9 })
  })
  it('splits account/machine patches and preserves explicit false and reminder disable', () => {
    expect(settingsPatches({ showWeekend: false, netAutoLogin: false, launchAtLogin: false, remindBefore: 0 }))
      .toEqual({ account: { ui: { show_weekend: false }, network: { heartbeat_enabled: false }, schedule: { lesson_reminder_enabled: false } }, desktop: { launch_at_startup: false } })
  })
  it('keeps unknown theme without sending a replacement and arbitrary reminder minutes', () => {
    const dto = account(); dto.appearance.theme = 'custom-personal'
    expect(adaptSettings(dto, '20260001')).toMatchObject({ remindBefore: 17, themeValue: 'custom-personal', desktopSupported: false })
    expect(settingsPatches({ showWeekend: false }).account).not.toHaveProperty('appearance')
  })
  it('does not invent device counts, traffic, connection time or successful internet access', () => {
    expect(adaptNetwork({ campus_connected: true, internet_connected: false, settings: account().network,
      checked_at: '', interface: { ipv4: '192.0.2.10' }, portal: {}, message: '仍离线' }, '20260001'))
      .toMatchObject({ online: false, campusConnected: true, usedGB: null, since: null, devicesSupported: false, ip: '192.0.2.10' })
  })
})
