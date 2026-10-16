import type { AccountSettingsDto, AuthDto, RowDto, SemesterDto, WeekDto } from '../src/api/dto'
export const row = (patch: Partial<RowDto> = {}): RowDto => ({
  weekday: '星期一', course_name: '合成课程', teacher: '', room: '测试101', section: '第2-4节',
  section_numbers: [2, 3, 4], week_numbers: [1, 3, 5, 7, 9], time_segments: ['10:10-12:00'],
  time_range: '10:10-12:00', block_time: '', weeks: '1-9单周', credit: '', hours_text: '((实践:20),理实一体:64)', ...patch,
})
export const account = (): AccountSettingsDto => ({
  appearance: { theme: 'paper' }, ui: { show_weekend: true, inactive_course_mode: 'dim' },
  schedule: { lesson_reminder_enabled: true, lesson_reminder_minutes: 17,
    auto_refresh_enabled: false, auto_refresh_mode: 'daily', auto_refresh_daily_time: '20:00', auto_refresh_interval_hours: 4 },
  network: { provider: 'campus', heartbeat_enabled: false, heartbeat_interval_minutes: 5,
    portal_url: 'http://192.0.2.1/', connectivity_check_urls: ['https://example.com/'],
  },
})
export const week = (n = 3, patch: Partial<WeekDto> = {}): WeekDto => ({
  rows: [row()], term: { academic_term: '2026-2027-1', label: '合成秋季' }, week_label: `第${n}周`,
  current_week_number: 3, viewed_week_number: n, current_week_label: '第3周',
  available_weeks: [{ value: '2026-09-07', label: '第1周', selected: n === 1 }, { value: '2026-09-21', label: '第3周', selected: n === 3 }],
  week_dates: { 星期一: n === 3 ? '2026-09-21' : '2026-09-07' }, block_times: [],
  updated_at: '2026-09-21T08:00:00+08:00', cached: true, ...patch,
})
export const semester = (patch: Partial<SemesterDto> = {}): SemesterDto => ({
  rows: [row()], term: { academic_term: '2026-2027-1', label: '合成秋季' },
  available_terms: [{ value: '2026-2027-1', label: '合成秋季', selected: true }],
  block_times: [], notes: ['合成备注'], supplementary_courses: [{ course_name: '未排课条目' }], updated_at: '2026-09-21T08:00:00+08:00', ...patch,
})
export const auth = (): AuthDto => ({
  session_id: 'synthetic-memory-session', student: { student_id: '20260001', name: '测试学生', nickname: '', use_nickname: false,
    academy_name: '测试学院', major_name: '', class_name: '', training_level: '', campus_name: '', program_duration_years: 4, enrollment_date: '2026-09-01' },
  settings: account(), connected: true, cached_schedule: week(),
})
export const captcha = { status: 'captcha_required' as const, challenge_id: 'synthetic-challenge', captcha_image: 'data:image/png;base64,AAAA', expires_in_seconds: 120 }
export const response = (data: unknown, status = 200) => new Response(JSON.stringify({ ok: status === 200, msg: status === 200 ? '成功' : '合成错误', data }), { status, headers: { 'Content-Type': 'application/json' } })
