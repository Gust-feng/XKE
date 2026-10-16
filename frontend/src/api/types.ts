/**
 * 数据契约 —— 前端只依赖这里的类型。
 * 真实接入须建立 DTO、认证和刷新状态适配，不直接把此模型作为后端合同。
 */

/** 课程类别：决定卡片颜色（颜色本身就是暗示，不在界面上写图例） */
export type CourseKind = 'major' | 'general' | 'practice' | 'unknown'

export interface Course {
  id: string
  name: string
  teacher: string
  room: string
  kind: CourseKind
  /** 0 = 周一 … 6 = 周日 */
  weekday: number
  /** "HH:MM" */
  start: string
  end: string
  /** 节次，如 "5–8" */
  periods: string
  /** 上课周，闭区间。可多段：[[1, 8], [10, 16]] */
  weeks: [number, number][]
  timeSegments?: string[]
  source?: import('./dto').RowDto
  timed?: boolean
}

export interface Term {
  name: string
  /** 第 1 周周一，"YYYY-MM-DD" */
  startDate: string
  totalWeeks: number
  id?: string
}

export interface Schedule {
  term: Term
  courses: Course[]
  /** Server-provided timetable bounds, including the current summer/winter arrangement. */
  blockTimes?: { section: string; blockTime: string }[]
  /** 最后一次从教务同步的时间，ISO 字符串 */
  syncedAt: string | null
  scope?: 'semester' | 'week'
  /** Includes the full semester even when the selected week has a newer snapshot. */
  semesterLoaded?: boolean
  viewedWeek?: number
  currentWeek?: number | null
  availableWeeks?: { number: number; label: string; value: string }[]
  availableTerms?: { value: string; label: string }[]
  cached?: boolean
  notes?: string[]
  supplementary?: Record<string, string>[]
  warnings?: string[]
}

export interface NetDevice {
  id: string
  name: string
  ip: string
  since: string // ISO
  current: boolean
}

export interface NetStatus {
  online: boolean
  account: string
  ip: string | null
  since: string | null // ISO
  /** 本月已用流量，单位 GB */
  usedGB: number | null
  devices: NetDevice[]
  /** Whether the campus network itself is reachable. */
  intranet?: boolean
  /** Whether an external probe is reachable. */
  internet?: boolean
  campusConnected?: boolean
  devicesSupported?: boolean
  disconnectSupported?: boolean
  message?: string
  checkedAt?: string
  error?: string
}

export interface Settings {
  /** 教务账号是否已绑定 */
  portalBound: boolean
  portalAccount: string
  /** 课前提醒（分钟），0 = 关闭 */
  remindBefore: number
  /** 开机自动登录校园网 */
  netAutoLogin: boolean
  /** 显示周末列 */
  showWeekend: boolean
  /** 外观主题，见 src/lib/themes.ts */
  theme: 'night' | 'paper' | 'mono' | 'dusk'
  /** 唯一正式设计基线：第七版合成。 */
  layout: 'one'
  /** 开机启动 */
  /** 本机后台自动检查并下载应用更新；安装由原生宿主执行。 */
  autoUpdate: boolean
  launchAtLogin: boolean
  desktopSupported?: boolean
  startupRegistered?: boolean | null
  silentStartup?: boolean
  closeToTray?: boolean
  inactiveCourseMode?: 'dim' | 'hide'
  autoRefresh?: boolean
  refreshMode?: 'daily' | 'interval'
  refreshTime?: string
  refreshHours?: number
  heartbeatMinutes?: number
  themeValue?: string
}

/** UI API boundary, implemented by HTTP runtime or explicit preview. */
export interface Api {
  readonly preview: boolean
  cancelAuth(): void
  clearSession(): void
  bootstrap(): Promise<Workspace | null>
  login(user: string, password: string): Promise<AuthResult>
  connect(): Promise<AuthResult>
  session(): Promise<Workspace>
  logout(): Promise<void>
  submitCaptcha(challenge: Captcha, code: string): Promise<AuthResult | Schedule>
  /** Cancels queued/follow-up reads and discards late responses; issued HTTP requests drain normally. */
  getSchedule(week?: number, term?: string, signal?: AbortSignal): Promise<Schedule>
  syncSchedule(week?: number, term?: string): Promise<Schedule | Captcha>
  getLocalSemester(term?: string, context?: Schedule | null, signal?: AbortSignal): Promise<Schedule>
  getSemester(term?: string, context?: Schedule | null): Promise<Schedule>

  getNetStatus(): Promise<NetStatus>
  getNetworkSummary(): Promise<import('./dto').NetworkSummaryDto>
  netLogin(): Promise<NetStatus>
  netLogout(): Promise<NetStatus>
  kickDevice(id: string): Promise<NetStatus>

  getAppUpdate(signal?: AbortSignal): Promise<import('./dto').AppUpdateDto>
  checkAppUpdate(signal?: AbortSignal): Promise<import('./dto').AppUpdateDto>
  downloadAppUpdate(signal?: AbortSignal): Promise<import('./dto').AppUpdateDto>
  applyAppUpdate(signal?: AbortSignal): Promise<import('./dto').AppUpdateDto>

  getSettings(): Promise<Settings>
  updateSettings(patch: Partial<Settings>): Promise<Settings>
  getNetworkConfig(): Promise<import('./dto').NetworkConfigDto>
  saveNetworkConfig(patch: Record<string, string | number | boolean | string[]>): Promise<import('./dto').NetworkConfigDto>
  getJobs(signal?: AbortSignal): Promise<{ network: import('./dto').JobStateDto; schedule: import('./dto').JobStateDto }>
}

export interface Workspace {
  student: import('./dto').StudentDto
  connected: boolean
  settings: Settings
  schedule: Schedule | null
}
export interface Captcha {
  kind: 'captcha'
  purpose: 'login' | 'connect' | 'schedule'
  id: string; image: string; expiresAt: number
}
export type AuthResult = Captcha | { kind: 'authenticated'; workspace: Workspace }
