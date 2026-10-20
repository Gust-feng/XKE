/** HTTP contracts owned by Xke.Contracts. View models live in types.ts. */
export interface StudentDto {
  student_id: string; name: string; nickname: string; use_nickname: boolean
  academy_name: string; major_name: string; class_name: string; training_level: string
  campus_name: string; program_duration_years: number | null; enrollment_date: string
}
export interface AccountSettingsDto {
  auth?: { prelogin_mode: string; auto_captcha_enabled: boolean }
  appearance: { theme: string }
  ui: { show_weekend: boolean; inactive_course_mode: 'dim' | 'hide' }
  schedule: {
    lesson_reminder_enabled: boolean; lesson_reminder_minutes: number
    auto_refresh_enabled: boolean; auto_refresh_mode: 'daily' | 'interval'
    auto_refresh_daily_time: string; auto_refresh_interval_hours: number
  }
  network: NetworkSettingsDto
}
export interface NetworkSettingsDto {
  provider: 'campus' | 'telecom' | 'unicom' | 'mobile'
  heartbeat_enabled: boolean; heartbeat_interval_minutes: number
  portal_url: string; connectivity_check_urls: string[]
}
export interface DesktopSettingsDto {
  launch_at_startup: boolean; silent_startup: boolean; close_to_tray: boolean; auto_update_enabled: boolean
}
export interface DesktopPayloadDto { settings: DesktopSettingsDto; startup_registered: boolean | null }
export interface AuthDto {
  session_id: string; student: StudentDto; settings: AccountSettingsDto
  connected: boolean; cached_schedule: WeekDto | null
}
export interface CaptchaDto {
  message?: string; status: 'captcha_required'; challenge_id: string; captcha_image: string; expires_in_seconds: number
}
export interface RowDto {
  weekday: string; course_name: string; teacher: string; room: string; section: string
  section_numbers: number[]; week_numbers: number[]; time_segments: string[]
  weeks: string; time_range: string; block_time: string; credit: string
  hours_text: string; hours_variants?: string[]
  [key: string]: unknown
}
export interface BlockTimeDto { section: string; block_time: string }
export interface WeekDto {
  rows: RowDto[]; term: { academic_term: string; label: string }
  week_label: string; current_week_number: number | null; viewed_week_number: number | null
  current_week_label: string; available_weeks: { value: string; label: string; selected: boolean }[]
  week_dates: Record<string, string>; block_times: BlockTimeDto[]; updated_at: string; cached: boolean
  notes?: string[]; supplementary_courses?: Record<string, string>[]
  data_warnings?: { message: string }[]
}
export interface SemesterDto {
  rows: RowDto[]; term: { academic_term: string; label: string }; block_times: BlockTimeDto[]
  available_terms: { value: string; label: string; selected: boolean }[]
  notes: string[]; supplementary_courses: Record<string, string>[]; updated_at: string; cached?: boolean
}
export interface NetworkStatusDto {
  checked_at: string; campus_connected: boolean; internet_connected: boolean
  interface: { ipv4?: string }; portal: { online_user?: string; login_time?: string }
  settings: NetworkSettingsDto; message: string; operation_error?: string
}
/** Whitelisted self-service facts, independent of portal reachability and credentials. */
export interface NetworkSummaryDto {
  configured: boolean; available: boolean; cached: boolean; updated_at: string | null; message: string
  account_status: string | null; plan_name: string | null; billing_method: string | null
  period_start: string | null; period_end: string | null
}
export interface NetworkConfigDto {
  settings: NetworkSettingsDto; campus_account: string
  credentials: Record<string, { account: string; password: string }>
}
export interface ConfigDto { prelogin_mode: string; development: { host_mode: string } }
export interface JobStateDto {
  enabled: boolean; next_run_at: string | null; last_run_at: string | null
  last_error: string; last_message?: string; requires_login?: boolean
}

/** Local application updates only. Release selection and installation belong to the native host. */
export interface AppUpdateDto {
  current_version: string; repository_url: string; release_url: string; latest_version: string | null
  status: 'unsupported' | 'idle' | 'checking' | 'available' | 'downloading' | 'ready' | 'installing' | 'up_to_date' | 'error'
  progress: number; error: string; checked_at: string | null
}

export interface CaptchaRecognitionDto {
  supported: boolean; trusted: boolean; enabled: boolean
  state: 'idle' | 'disabled' | 'unavailable' | 'recognizing' | 'manual_required'
  attempt: number; message: string
}
