import { adaptNetwork, adaptSemester, adaptSettings, adaptWeek, settingsPatches } from './adapters'
import type { AccountSettingsDto, AuthDto, CaptchaDto, ConfigDto, DesktopPayloadDto, JobStateDto, NetworkConfigDto, NetworkStatusDto, NetworkSummaryDto, SemesterDto, WeekDto } from './dto'
import { ApiError, HttpClient } from './http'
import type { Api, AuthResult, Captcha, Schedule, Settings, Workspace } from './types'

export function challenge(dto: CaptchaDto, purpose: Captcha['purpose']): Captcha {
  return { kind: 'captcha', purpose, id: dto.challenge_id, image: dto.captcha_image,
    expiresAt: Date.now() + dto.expires_in_seconds * 1000, message: dto.message }
}
export const isCaptchaDto = (dto: AuthDto | CaptchaDto | WeekDto): dto is CaptchaDto => 'status' in dto && dto.status === 'captcha_required'

/** Independent HTTP runtime; never imports synthetic data or invokes school APIs. */
export class RuntimeApi implements Api {
  readonly preview = false
  private auth: AuthDto | null = null
  private desktop: DesktopPayloadDto | null = null
  private desktopMode = false
  private retryConfig = false
  private authAttempt = 0
  private schoolRequests = new Set<AbortController>()
  private async school<T>(path: string, body: unknown, anonymous = false) {
    const controller = new AbortController()
    this.schoolRequests.add(controller)
    try { return await this.http.request<T>(path, body, false, anonymous, controller.signal) }
    finally { this.schoolRequests.delete(controller) }
  }
  private readonly http: HttpClient
  constructor(http: HttpClient) { this.http = http }

  cancelAuth() { this.authAttempt++; for (const request of this.schoolRequests) request.abort(new ApiError(409, '登录已取消')); this.schoolRequests.clear() }
  clearSession() { this.cancelAuth(); this.auth = null; this.http.clearSession() }
  private async authenticate(dto: AuthDto | CaptchaDto, purpose: 'login' | 'connect', attempt: number): Promise<AuthResult> {
    if (attempt !== this.authAttempt) throw new ApiError(409, '登录已取消')
    if (isCaptchaDto(dto)) return challenge(dto, purpose)
    if (!dto.session_id || !dto.student) throw new Error('登录响应缺少会话或个人资料')
    // Machine settings are optional in a plain HTTP host and never overwrite account preferences.
    let desktop = this.desktop
    if (this.desktopMode) {
      try { desktop = await this.http.request<DesktopPayloadDto>('/api/desktop/settings') }
      catch (error) { if (attempt !== this.authAttempt) throw error; desktop = null }
    }
    if (attempt !== this.authAttempt) throw new ApiError(409, '登录已取消')
    this.http.setSession(dto.session_id)
    this.desktop = desktop
    this.auth = dto
    return { kind: 'authenticated', workspace: this.workspace() }
  }
  private workspace(): Workspace {
    if (!this.auth) throw new ApiError(401, '请先登录')
    return {
      student: this.auth.student, connected: this.auth.connected,
      settings: adaptSettings(this.auth.settings, this.auth.student.student_id, this.desktop),
      schedule: this.auth.cached_schedule ? adaptWeek(this.auth.cached_schedule) : null,
    }
  }
  async bootstrap() {
    const attempt = ++this.authAttempt
    this.retryConfig = true
    let config: ConfigDto
    try { config = await this.http.request<ConfigDto>('/api/config', undefined, true, true) }
    catch (error) { this.retryConfig = true; throw error }
    if (attempt !== this.authAttempt) return null
    this.desktopMode = config.development.host_mode === 'desktop-webview'
    this.retryConfig = false
    if (!this.desktopMode) return null
    try {
      const payload = await this.http.request<{ auth: AuthDto }>('/api/desktop/auth-bootstrap', {}, false, true)
      const result = await this.authenticate(payload.auth, 'login', attempt)
      return result.kind === 'authenticated' ? result.workspace : null
    } catch (error) {
      if (error instanceof ApiError && (error.status === 403 || error.status === 404)) return null
      throw error
    }
  }
  async login(user: string, password: string) {
    const attempt = ++this.authAttempt
    if (this.retryConfig) {
      const config = await this.http.request<ConfigDto>('/api/config', undefined, true, true)
      if (attempt !== this.authAttempt) throw new ApiError(409, '登录已取消')
      this.desktopMode = config.development.host_mode === 'desktop-webview'
      this.retryConfig = false
    }
    const dto = await this.school<AuthDto | CaptchaDto>(this.auth ? '/api/auth/reinitialize' : '/api/auth/login', { user, password }, true)
    return this.authenticate(dto, 'login', attempt)
  }
  async connect() {
    if (!this.auth) throw new ApiError(401, '请先设置教务账号')
    const attempt = ++this.authAttempt
    const dto = await this.school<AuthDto | CaptchaDto>('/api/auth/connect', {})
    return this.authenticate(dto, 'connect', attempt)
  }
  async session() {
    if (!this.auth) throw new ApiError(401, '请先登录')
    const dto = await this.http.request<AuthDto>('/api/auth/session')
    this.auth = dto
    return this.workspace()
  }
  async logout() {
    this.cancelAuth()
    const result = this.auth ? this.http.request<void>('/api/auth/logout', {}) : null
    this.auth = null
    this.http.clearSession()
    // The logout request's response belongs to the closed session; do not restore it.
    try { await result } catch (error) {
      if (!(error instanceof ApiError && error.status === 409)) throw error
    }
  }
  async submitCaptcha(c: Captcha, code: string): Promise<AuthResult | Schedule> {
    const body = { challenge_id: c.id, captcha_code: code }
    if (c.purpose === 'schedule') {
      const dto = await this.school<WeekDto | CaptchaDto>('/api/auth/jw-captcha/submit', body)
      return isCaptchaDto(dto) ? challenge(dto, 'schedule') : adaptWeek(dto)
    }
    const attempt = this.authAttempt
    const dto = await this.school<AuthDto>('/api/auth/captcha', body, true)
    return this.authenticate(dto, c.purpose, attempt)
  }
  /** Abortable reads keep the timeout and session boundary in the shared HttpClient. */
  private async read<T>(path: string, signal?: AbortSignal): Promise<T> {
    signal?.throwIfAborted()
    const result = await this.http.request<T>(path, undefined, false, false, signal)
    signal?.throwIfAborted()
    return result
  }
  async getSchedule(week?: number, term?: string, signal?: AbortSignal) {
    const query = new URLSearchParams()
    if (week !== undefined) query.set('week_label', `第${week}周`)
    if (term) query.set('academic_term', term)
    const result = adaptWeek(await this.read<WeekDto>(`/api/schedule/local?${query}`, signal))
    if (term && this.auth?.cached_schedule?.term.academic_term !== term) result.currentWeek = null
    return result
  }
  async syncSchedule(week?: number, term?: string) {
    const historical = !!term && this.auth?.cached_schedule?.term.academic_term !== term
    const body = { mode: week === undefined ? 'current' : 'manual',
      ...(week !== undefined ? { manual_week: String(week) } : {}), ...(historical ? { academic_term: term } : {}) }
    const dto = await this.school<WeekDto | CaptchaDto>('/api/schedule/preferred', body)
    if (isCaptchaDto(dto)) return challenge(dto, 'schedule')
    const result = adaptWeek(dto)
    if (historical) result.currentWeek = null
    return result
  }
  private async readSemester(path: string, term?: string, context?: Schedule | null, signal?: AbortSignal) {
    const requestedTerm = term || context?.term.id
    const query = requestedTerm ? `?academic_term=${encodeURIComponent(requestedTerm)}` : ''
    const result = adaptSemester(await this.read<SemesterDto>(`${path}${query}`, signal), context)
    if (requestedTerm && result.term.id !== requestedTerm) throw new Error('返回课表与所选学期不一致')
    return result
  }
  getLocalSemester(term?: string, context?: Schedule | null, signal?: AbortSignal) {
    return this.readSemester('/api/schedule/semester/local', term, context, signal)
  }
  async getSemester(term?: string, context?: Schedule | null) {
    const controller = new AbortController()
    this.schoolRequests.add(controller)
    try { return await this.readSemester('/api/schedule/semester', term, context, controller.signal) }
    finally { this.schoolRequests.delete(controller) }
  }
  getAppUpdate(signal?: AbortSignal) {
    return this.read<import('./dto').AppUpdateDto>('/api/app/updates', signal)
  }
  private async updateAction(action: 'check' | 'download' | 'apply', signal?: AbortSignal) {
    signal?.throwIfAborted()
    const result = await this.http.request<import('./dto').AppUpdateDto>(`/api/app/updates/${action}`, {})
    signal?.throwIfAborted()
    return result
  }
  checkAppUpdate(signal?: AbortSignal) { return this.updateAction('check', signal) }
  downloadAppUpdate(signal?: AbortSignal) { return this.updateAction('download', signal) }
  applyAppUpdate(signal?: AbortSignal) { return this.updateAction('apply', signal) }

  private authForSession(sessionId: string) {
    if (this.auth?.session_id !== sessionId) throw new ApiError(409, '账号已切换')
    return this.auth
  }
  async getSettings() {
    if (!this.auth) throw new ApiError(401, '请先登录')
    const sessionId = this.auth.session_id
    const dto = await this.http.request<{ settings: AccountSettingsDto }>('/api/auth/preferences')
    this.auth = { ...this.authForSession(sessionId), settings: dto.settings }
    return this.workspace().settings
  }
  async updateSettings(patch: Partial<Settings>) {
    if (!this.auth) throw new ApiError(401, '请先登录')
    const sessionId = this.auth.session_id
    const { account, desktop } = settingsPatches(patch)
    if (Object.keys(account).length) {
      const dto = await this.http.request<{ settings: AccountSettingsDto }>('/api/auth/preferences', account)
      this.auth = { ...this.authForSession(sessionId), settings: dto.settings }
    }
    if (Object.keys(desktop).length) {
      if (!this.desktopMode) throw new ApiError(501, '当前运行环境不支持桌面设置')
      const result = await this.http.request<DesktopPayloadDto>('/api/desktop/settings', desktop)
      this.authForSession(sessionId)
      this.desktop = result
    }
    return this.workspace().settings
  }
  async updateNickname(nickname: string) {
    if (!this.auth) throw new ApiError(401, '请先登录')
    const attempt = this.authAttempt, sessionId = this.auth.session_id
    const value = nickname.trim()
    const result = await this.http.request<{ student: import('./dto').StudentDto }>('/api/auth/profile', { nickname: value, use_nickname: !!value })
    if (attempt !== this.authAttempt || this.auth?.session_id !== sessionId) throw new ApiError(409, '账号已切换')
    this.auth = { ...this.auth, student: result.student }
    return result.student
  }
  async getNetStatus() {
    return adaptNetwork(await this.http.request<NetworkStatusDto>('/api/network/status'), this.auth?.student.student_id || '')
  }
  async netLogin() {
    return adaptNetwork(await this.http.request<NetworkStatusDto>('/api/network/maintain', { force: false }), this.auth?.student.student_id || '')
  }
  async netLogout(): Promise<never> { throw new ApiError(501, '后端暂不支持断开校园网') }
  async kickDevice(): Promise<never> { throw new ApiError(501, '后端暂不支持设备列表及设备下线') }
  getNetworkConfig() { return this.http.request<NetworkConfigDto>('/api/network/config') }
  getNetworkSummary() { return this.http.request<NetworkSummaryDto>('/api/network/summary') }
  saveNetworkConfig(patch: Record<string, string | number | boolean | string[]>) {
    return this.http.request<NetworkConfigDto>('/api/network/config', patch)
  }
  getCaptchaRecognition(signal?: AbortSignal) { return this.read<import('./dto').CaptchaRecognitionDto>('/api/auth/captcha-recognition', signal) }
  async getJobs(signal?: AbortSignal) {
    const [network, schedule] = await Promise.allSettled([
      this.read<JobStateDto>('/api/network/maintenance', signal), this.read<JobStateDto>('/api/schedule/refresh-status', signal),
    ])
    if (network.status === 'rejected') throw network.reason
    if (schedule.status === 'rejected') throw schedule.reason
    return { network: network.value, schedule: schedule.value }
  }
}
