import { describe, expect, it } from 'vitest'
import { reminderMinutes, reminderOptions, validRefreshInput } from '../src/lib/settings-options'
import { adaptSettings, settingsPatches } from '../src/api/adapters'
import { account } from './fixtures'

describe('reminder lead times', () => {
  it('offers off/15/30/45 without silently enabling an existing disabled reminder', () => {
    const dto = account(); dto.schedule.lesson_reminder_enabled = false; dto.schedule.lesson_reminder_minutes = 5
    expect(adaptSettings(dto, 'synthetic').remindBefore).toBe(0)
    expect(reminderOptions(0)).toEqual([0, 15, 30, 45])
    expect(settingsPatches({ remindBefore: 0 }).account).toEqual({ schedule: { lesson_reminder_enabled: false } })
  })
  it.each([5, 10, 14])('normalizes an enabled old %i minute reminder to the same 15 minute floor as the backend', (minutes) => {
    const dto = account(); dto.schedule.lesson_reminder_enabled = true; dto.schedule.lesson_reminder_minutes = minutes
    expect(adaptSettings(dto, 'synthetic').remindBefore).toBe(15)
    expect(settingsPatches({ remindBefore: minutes }).account).toEqual({ schedule: { lesson_reminder_enabled: true, lesson_reminder_minutes: 15 } })
  })
  it.each([18, 60])('keeps custom %i minute values selected without rewriting unrelated preferences', (minutes) => {
    expect(reminderMinutes(minutes)).toBe(minutes)
    expect(reminderOptions(minutes)).toContain(minutes)
    expect(settingsPatches({ remindBefore: minutes }).account).toHaveProperty('schedule.lesson_reminder_minutes', minutes)
    expect(settingsPatches({ autoRefresh: true }).account).toEqual({ schedule: { auto_refresh_enabled: true } })
  })
  it('uses existing daily/interval fields and sparse settings patches', () => {
    expect(settingsPatches({ refreshMode: 'daily', refreshTime: '07:30' }).account).toEqual({ schedule: { auto_refresh_mode: 'daily', auto_refresh_daily_time: '07:30' } })
    expect(settingsPatches({ autoRefresh: false, refreshMode: 'interval', refreshHours: 6 }).account).toEqual({ schedule: { auto_refresh_enabled: false, auto_refresh_mode: 'interval', auto_refresh_interval_hours: 6 } })
  })
})


describe('refresh field commits', () => {
  it.each(['00:00', '07:30', '23:59'])('accepts a complete daily time %s', (value) => expect(validRefreshInput('daily', value)).toBe(true))
  it.each(['', '7:30', '07:', '24:00', '12:60'])('does not save partial/invalid daily time %s', (value) => expect(validRefreshInput('daily', value)).toBe(false))
  it.each(['1', '6', '8760'])('accepts supported interval hours %s', (value) => expect(validRefreshInput('interval', value)).toBe(true))
  it.each(['', '0', '-1', '1.5', '8761'])('does not save unsupported interval hours %s', (value) => expect(validRefreshInput('interval', value)).toBe(false))
})
