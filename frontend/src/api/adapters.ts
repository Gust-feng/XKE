import { reminderMinutes } from '../lib/settings-options'
import type { AccountSettingsDto, DesktopPayloadDto, NetworkStatusDto, RowDto, SemesterDto, WeekDto } from './dto'
import type { Course, Schedule, Settings } from './types'

export function weekNumber(label: string): number | undefined {
  const m = /^(?:第\s*)?(\d+)(?:\s*周)?$/.exec(label.trim())
  return m ? Number(m[1]) : undefined
}
const dayNames = ['一', '二', '三', '四', '五', '六', '日']
const syncedAt = (value: string): string | null => value && Number.isFinite(Date.parse(value)) ? value : null
function weekday(label: string) {
  const value = label.replace('星期', '').replace('周', '').replace('天', '日').trim()
  const index = dayNames.indexOf(value)
  return index >= 0 ? index : /^[1-7]$/.test(value) ? Number(value) - 1 : -1
}
export function ranges(numbers: number[]): [number, number][] {
  const out: [number, number][] = []
  for (const n of [...new Set(numbers)].sort((a, b) => a - b)) {
    const last = out.at(-1)
    if (last && n === last[1] + 1) last[1] = n
    else out.push([n, n])
  }
  return out
}
function times(text: string): [string, string][] {
  return [...text.matchAll(/(\d{1,2}:\d{2})\s*[-–—~～至]\s*(\d{1,2}:\d{2})/g)]
    .map((m) => [m[1].padStart(5, '0'), m[2].padStart(5, '0')])
}
function rowCourses(rows: RowDto[], week: number | undefined): Course[] {
  return rows.flatMap((row, index) => {
    const segments = row.time_segments?.length ? row.time_segments : [row.time_range || row.block_time || '']
    const spans = segments.flatMap(times)
    const weeks = ranges(week !== undefined ? [week] : row.week_numbers ?? [])
    const base = {
      name: row.course_name, teacher: row.teacher || '', room: row.room || '',
      kind: 'unknown' as const, weekday: weekday(row.weekday), weeks,
      periods: row.section_numbers?.length ? row.section_numbers.join('、') : row.section || '',
      timeSegments: segments.filter(Boolean), source: row,
    }
    // Keep unplaceable rows in the model and the details list; never invent a time.
    return (spans.length ? spans : [['', '']]).map(([start, end], part) => ({
      ...base, id: `${index}:${part}:${row.course_name}:${row.weekday}:${row.section}:${row.room}`,
      start, end, timed: spans.length > 0 && base.weekday >= 0 && end > start,
    }))
  })
}
function startDate(dto: WeekDto, viewed: number | undefined) {
  const monday = dto.week_dates?.['周一'] || dto.week_dates?.['星期一'] || dto.week_dates?.['1']
    || dto.available_weeks?.find((o) => weekNumber(o.label) === viewed)?.value
  if (!viewed || !monday || !/^\d{4}-\d{2}-\d{2}$/.test(monday)) return ''
  const date = new Date(`${monday}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() - (viewed - 1) * 7)
  return date.toISOString().slice(0, 10)
}
export function adaptWeek(dto: WeekDto): Schedule {
  if (!dto || !Array.isArray(dto.rows) || !dto.term) throw new Error('课表响应缺少 rows 或 term')
  const viewed = dto.viewed_week_number ?? weekNumber(dto.week_label)
  if (viewed === undefined) throw new Error('课表响应没有可识别的周次')
  const options = (dto.available_weeks || []).flatMap((o) => {
    const number = weekNumber(o.label)
    return number ? [{ number, value: o.value, label: o.label }] : []
  })
  return {
    term: { id: dto.term.academic_term, name: dto.term.label || dto.term.academic_term,
      startDate: startDate(dto, viewed), totalWeeks: Math.max(viewed, ...options.map((o) => o.number)) },
    scope: 'week', courses: rowCourses(dto.rows, viewed), blockTimes: dto.block_times.map(({ section, block_time }) => ({ section, blockTime: block_time })), viewedWeek: viewed,
    currentWeek: dto.current_week_number ?? weekNumber(dto.current_week_label) ?? null,
    availableWeeks: options, syncedAt: syncedAt(dto.updated_at), cached: dto.cached,
    notes: dto.notes || [], supplementary: dto.supplementary_courses || [],
    warnings: (dto.data_warnings || []).map((w) => w.message),
  }
}
export function adaptSemester(dto: SemesterDto, context?: Schedule | null): Schedule {
  if (!dto || !Array.isArray(dto.rows) || !dto.term) throw new Error('学期响应缺少 rows 或 term')
  const sameTerm = context?.term.id === dto.term.academic_term
  const courses = rowCourses(dto.rows, undefined)
  return {
    term: { id: dto.term.academic_term, name: dto.term.label || dto.term.academic_term,
      startDate: sameTerm ? context.term.startDate : '',
      totalWeeks: Math.max(1, ...dto.rows.flatMap((r) => r.week_numbers || []), sameTerm ? context.term.totalWeeks : 1) },
    scope: 'semester', semesterLoaded: true, courses,
    blockTimes: dto.block_times.map(({ section, block_time }) => ({ section, blockTime: block_time })),
    syncedAt: syncedAt(dto.updated_at),
    currentWeek: sameTerm ? context.currentWeek : null, viewedWeek: sameTerm ? context.viewedWeek : 1,
    availableWeeks: sameTerm ? context.availableWeeks : [], availableTerms: dto.available_terms,
    cached: dto.cached ?? false, notes: dto.notes, supplementary: dto.supplementary_courses,
    warnings: courses.some((c) => !c.weeks.length) ? ['部分条目未提供可识别的上课周，请查看未定位课程'] : [],
  }
}
export function adaptSettings(account: AccountSettingsDto, id: string, desktop?: DesktopPayloadDto | null): Settings {
  const theme = account.appearance.theme
  return {
    portalBound: true, portalAccount: id, layout: 'one',
    theme: ['night', 'paper', 'mono', 'dusk'].includes(theme) ? theme as Settings['theme'] : 'night', themeValue: theme,
    showWeekend: account.ui.show_weekend, inactiveCourseMode: account.ui.inactive_course_mode,
    remindBefore: account.schedule.lesson_reminder_enabled ? reminderMinutes(account.schedule.lesson_reminder_minutes) : 0,
    autoCaptchaEnabled: account.auth?.auto_captcha_enabled ?? true,
    autoRefresh: account.schedule.auto_refresh_enabled, refreshMode: account.schedule.auto_refresh_mode,
    refreshTime: account.schedule.auto_refresh_daily_time, refreshHours: account.schedule.auto_refresh_interval_hours,
    netAutoLogin: account.network.heartbeat_enabled, heartbeatMinutes: account.network.heartbeat_interval_minutes,
    autoUpdate: desktop?.settings.auto_update_enabled ?? false,
    launchAtLogin: desktop?.settings.launch_at_startup ?? false,
    silentStartup: desktop?.settings.silent_startup ?? false, closeToTray: desktop?.settings.close_to_tray ?? true,
    desktopSupported: !!desktop, startupRegistered: desktop?.startup_registered,
  }
}
export function settingsPatches(patch: Partial<Settings>) {
  const account: Record<string, unknown> = {}
  const desktop: Record<string, boolean> = {}
  if (patch.autoCaptchaEnabled !== undefined) account.auth = { auto_captcha_enabled: patch.autoCaptchaEnabled }
  if (patch.theme !== undefined) account.appearance = { theme: patch.theme }
  const ui: Record<string, unknown> = {}
  if (patch.showWeekend !== undefined) ui.show_weekend = patch.showWeekend
  if (patch.inactiveCourseMode !== undefined) ui.inactive_course_mode = patch.inactiveCourseMode
  if (Object.keys(ui).length) account.ui = ui
  const schedule: Record<string, unknown> = {}
  if (patch.remindBefore !== undefined) {
    schedule.lesson_reminder_enabled = patch.remindBefore > 0
    if (patch.remindBefore > 0) schedule.lesson_reminder_minutes = reminderMinutes(patch.remindBefore)
  }
  if (patch.autoRefresh !== undefined) schedule.auto_refresh_enabled = patch.autoRefresh
  if (patch.refreshMode !== undefined) schedule.auto_refresh_mode = patch.refreshMode
  if (patch.refreshTime !== undefined) schedule.auto_refresh_daily_time = patch.refreshTime
  if (patch.refreshHours !== undefined) schedule.auto_refresh_interval_hours = patch.refreshHours
  if (Object.keys(schedule).length) account.schedule = schedule
  const network: Record<string, unknown> = {}
  if (patch.netAutoLogin !== undefined) network.heartbeat_enabled = patch.netAutoLogin
  if (patch.heartbeatMinutes !== undefined) network.heartbeat_interval_minutes = patch.heartbeatMinutes
  if (Object.keys(network).length) account.network = network
  if (patch.autoUpdate !== undefined) desktop.auto_update_enabled = patch.autoUpdate
  if (patch.launchAtLogin !== undefined) desktop.launch_at_startup = patch.launchAtLogin
  if (patch.silentStartup !== undefined) desktop.silent_startup = patch.silentStartup
  if (patch.closeToTray !== undefined) desktop.close_to_tray = patch.closeToTray
  return { account, desktop }
}
export function adaptNetwork(dto: NetworkStatusDto, account: string) {
  return {
    online: dto.internet_connected, campusConnected: dto.campus_connected,
    intranet: dto.campus_connected, internet: dto.internet_connected,
    account, ip: dto.interface.ipv4 || null, since: null,
    usedGB: null, devices: [], devicesSupported: false, disconnectSupported: false,
    message: dto.message, error: dto.operation_error || '', checkedAt: dto.checked_at,
  }
}
