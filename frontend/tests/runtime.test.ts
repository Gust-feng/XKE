import { describe, expect, it, vi } from 'vitest'
import { HttpClient } from '../src/api/http'
import { RuntimeApi } from '../src/api/runtime'
import { account, auth, captcha, response, semester, week } from './fixtures'

describe('HTTP runtime', () => {
  it('reads account metadata through its independent authenticated GET route', async () => {
    const summary = { configured: true, available: true, cached: false, updated_at: null, message: '',
      account_status: '正常', plan_name: '合成套餐', billing_method: '包月', period_start: '2026-10-01', period_end: '2026-10-31' }
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(summary))
    const http = new HttpClient('', fetcher)
    http.setSession('synthetic-memory-session')
    const api = new RuntimeApi(http)
    expect(await api.getNetworkSummary()).toEqual(summary)
    expect(fetcher).toHaveBeenCalledOnce()
    expect(fetcher.mock.calls[0]).toMatchObject(['/api/network/summary', { method: 'GET', headers: { 'X-XKE-Session': 'synthetic-memory-session' } }])
  })
  it('binds the default browser transport to the global object', async () => {
    const fetcher = vi.fn<typeof fetch>(function (this: unknown) {
      expect(this).toBe(globalThis)
      return Promise.resolve(response({ ready: true }))
    })
    vi.stubGlobal('fetch', fetcher)
    try {
      await expect(new HttpClient('').request('/api/health')).resolves.toEqual({ ready: true })
    } finally { vi.unstubAllGlobals() }
  })
  it.each([
    [new TypeError('Failed to fetch'), '暂时无法连接'],
    [new DOMException('The operation timed out', 'TimeoutError'), '连接超时'],
  ])('shows a concise transport error for %s', async (error, message) => {
    const fetcher = vi.fn<typeof fetch>().mockRejectedValue(error)
    await expect(new HttpClient('', fetcher).request('/api/config')).rejects.toMatchObject({ status: 0, message })
  })
  it('an anonymous connect/logout never makes a request without an application session', async () => {
    const fetcher = vi.fn<typeof fetch>()
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await expect(api.connect()).rejects.toMatchObject({ status: 401, authenticated: false })
    await api.logout()
    expect(fetcher).not.toHaveBeenCalled()
  })
  it.each([403, 404])('desktop bootstrap %i returns null without initiating a login flow', async (status) => {
    const fetcher = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockResolvedValueOnce(response(null, status))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect(await api.bootstrap()).toBeNull()
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/config', '/api/desktop/auth-bootstrap'])
  })
  it('cancelling bootstrap while config is delayed makes a later login reread config and obtain desktop settings', async () => {
    let done!: (r: Response) => void
    const config = () => new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } }))
    const fetcher = vi.fn<typeof fetch>()
      .mockImplementationOnce(() => new Promise((resolve) => { done = resolve }))
      .mockResolvedValueOnce(config()).mockResolvedValueOnce(response(auth()))
      .mockResolvedValueOnce(response({ settings: { launch_at_startup: true, silent_startup: false, close_to_tray: true }, startup_registered: true }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const bootstrap = api.bootstrap()
    api.cancelAuth(); done(config())
    expect(await bootstrap).toBeNull()
    const login = await api.login('20260001', 'test-password')
    expect(login.kind === 'authenticated' && login.workspace.settings).toMatchObject({ desktopSupported: true, launchAtLogin: true, startupRegistered: true })
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/config', '/api/config', '/api/auth/login', '/api/desktop/settings'])
  })
  it('logout before bootstrap returns prevents a delayed workspace from installing its session', async () => {
    let done!: (r: Response) => void
    const fetcher = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockImplementationOnce(() => new Promise((resolve) => { done = resolve }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const pending = api.bootstrap()
    await vi.waitFor(() => expect(done).toBeDefined())
    await api.logout(); done(response({ auth: auth() }))
    await expect(pending).rejects.toMatchObject({ status: 409 })
    await expect(api.session()).rejects.toMatchObject({ status: 401 })
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/config', '/api/desktop/auth-bootstrap'])
  })
  it('cancelling a saved-account connection discards its late captcha without clearing the local workspace', async () => {
    let done!: (r: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth()))
      .mockImplementationOnce(() => new Promise((resolve) => { done = resolve })).mockResolvedValueOnce(response(auth()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    const pending = api.connect()
    api.cancelAuth(); done(response(captcha))
    await expect(pending).rejects.toThrow('登录已取消')
    expect((await api.session()).student.student_id).toBe('20260001')
    expect(fetcher.mock.calls[2][1]?.headers).toMatchObject({ 'X-XKE-Session': auth().session_id })
  })
  it('after logout local session verification fails before making an unauthenticated request', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response(null))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    await api.logout()
    const calls = fetcher.mock.calls.length
    await expect(api.session()).rejects.toMatchObject({ status: 401, authenticated: false })
    expect(fetcher).toHaveBeenCalledTimes(calls)
  })
  it('keeps session in a header and uses no session query string', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response(week()))
    const api = new RuntimeApi(new HttpClient('http://127.0.0.1:1234', fetcher))
    await api.login('20260001', 'synthetic-password')
    await api.getSchedule(3, '2026-2027-1')
    const [url, init] = fetcher.mock.calls[1]
    expect(String(url)).not.toContain('session')
    expect(init?.headers).toMatchObject({ 'X-XKE-Session': auth().session_id })
    expect(url).toContain('academic_term=2026-2027-1')
  })
  it('distinguishes login and schedule challenges and uses their separate endpoints', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(captcha)).mockResolvedValueOnce(response(auth()))
      .mockResolvedValueOnce(response(captcha)).mockResolvedValueOnce(response(week()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const login = await api.login('20260001', 'test-password')
    expect(login.kind).toBe('captcha')
    if (login.kind !== 'captcha') throw new Error('Missing login challenge')
    expect(login.purpose).toBe('login')
    await api.submitCaptcha(login, '1234')
    const refresh = await api.syncSchedule(3)
    if (!('kind' in refresh)) throw new Error('Missing schedule challenge')
    expect(refresh.purpose).toBe('schedule')
    await api.submitCaptcha(refresh, '1234')
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/auth/login', '/api/auth/captcha', '/api/schedule/preferred', '/api/auth/jw-captcha/submit'])
    expect(fetcher.mock.calls[3][1]?.headers).toMatchObject({ 'X-XKE-Session': auth().session_id })
  })
  it('cancelled delayed login cannot install its user or session', async () => {
    let done!: (r: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockImplementationOnce(() => new Promise((resolve) => { done = resolve }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const pending = api.login('20260001', 'test-password')
    api.cancelAuth(); done(response(auth()))
    await expect(pending).rejects.toThrow('登录已取消')
    await expect(api.getSettings()).rejects.toThrow('请先登录')
  })
  it('logout discards an in-flight captcha success', async () => {
    let done!: (r: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(captcha))
      .mockImplementationOnce(() => new Promise((resolve) => { done = resolve })).mockResolvedValueOnce(response(null))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const c = await api.login('20260001', 'test-password')
    if (c.kind !== 'captcha') throw new Error('Missing challenge')
    const pending = api.submitCaptcha(c, '1234')
    await api.logout(); done(response(auth()))
    await expect(pending).rejects.toThrow('会话已切换')
    await expect(api.getSettings()).rejects.toThrow('请先登录')
  })
  it('school 401 preserves the local HTTP session for explicit verification', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response(null, 401)).mockResolvedValueOnce(response(auth()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    await expect(api.syncSchedule(3)).rejects.toMatchObject({ status: 401, authenticated: true })
    expect((await api.session()).student.student_id).toBe('20260001')
    expect(fetcher.mock.calls[2][1]?.headers).toMatchObject({ 'X-XKE-Session': auth().session_id })
  })
  it('login 401 is not classified as a failure of the existing local session', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(null, 401))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await expect(api.login('20260001', 'wrong')).rejects.toMatchObject({ status: 401, authenticated: false })
  })
  it('current week refresh preserves normal directory acquisition; historical requests require explicit week/term', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockImplementation(async () => response(week()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    await api.syncSchedule(3, '2026-2027-1')
    await api.syncSchedule(7, '2025-2026-2')
    expect(JSON.parse(String(fetcher.mock.calls[1][1]?.body))).toEqual({ mode: 'manual', manual_week: '3' })
    expect(JSON.parse(String(fetcher.mock.calls[2][1]?.body))).toEqual({ mode: 'manual', manual_week: '7', academic_term: '2025-2026-2' })
  })
  it('plain HTTP hosts do not use desktop bootstrap or assume machine capabilities', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(new Response(JSON.stringify({ prelogin_mode: '', development: { host_mode: 'web' } })))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect(await api.bootstrap()).toBeNull()
    expect(fetcher).toHaveBeenCalledTimes(1)
  })
  it('a login after startup connection failure rechecks host mode and recovers machine settings', async () => {
    const fetcher = vi.fn<typeof fetch>().mockRejectedValueOnce(new TypeError('offline'))
      .mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response({ settings: { launch_at_startup: true, silent_startup: false, close_to_tray: true }, startup_registered: true }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await expect(api.bootstrap()).rejects.toMatchObject({ status: 0 })
    const result = await api.login('20260001', 'test-password')
    expect(result.kind === 'authenticated' && result.workspace.settings.desktopSupported).toBe(true)
  })
  it('cancelled desktop bootstrap cannot restore the old workspace', async () => {
    let done!: (r: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockImplementationOnce(() => new Promise((resolve) => { done = resolve }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const pending = api.bootstrap()
    await vi.waitFor(() => expect(done).toBeDefined())
    api.cancelAuth(); done(response({ auth: auth() }))
    await expect(pending).rejects.toThrow('登录已取消')
    await expect(api.session()).rejects.toMatchObject({ status: 401 })
  })
  it('desktop bootstrap obtains the local workspace and reads actual machine settings', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockResolvedValueOnce(response({ auth: auth() })).mockResolvedValueOnce(response({ settings: { launch_at_startup: true, silent_startup: false, close_to_tray: true }, startup_registered: true }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect((await api.bootstrap())?.settings).toMatchObject({ launchAtLogin: true, desktopSupported: true, startupRegistered: true })
  })
  it('ordinary preference updates send only account fields and preserve false', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response({ settings: auth().settings }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    await api.updateSettings({ showWeekend: false })
    expect(JSON.parse(String(fetcher.mock.calls[1][1]?.body))).toEqual({ ui: { show_weekend: false } })
  })
  it('non-JSON HTTP responses do not switch to synthetic data', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response('<html>SPA</html>'))
    await expect(new HttpClient('', fetcher).request('/api/schedule/local')).rejects.toThrow('响应异常')
  })
  it('acquires semester through GET and unsupported device actions issue no requests', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(semester()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect((await api.getSemester()).scope).toBe('semester')
    expect(fetcher.mock.calls[0][1]?.method).toBe('GET')
    await expect(api.kickDevice()).rejects.toMatchObject({ status: 501 })
    await expect(api.netLogout()).rejects.toMatchObject({ status: 501 })
    expect(fetcher).toHaveBeenCalledTimes(1)
  })
  it('reads a local semester using the in-memory session, explicit week context and actual capture time', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth()))
      .mockResolvedValueOnce(response(semester({ cached: true })))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const login = await api.login('20260001', 'test-password')
    if (login.kind !== 'authenticated') throw new Error('Expected workspace')
    const saved = await api.getLocalSemester(undefined, login.workspace.schedule)
    expect(fetcher.mock.calls[1][0]).toBe('/api/schedule/semester/local?academic_term=2026-2027-1')
    expect(fetcher.mock.calls[1][1]).toMatchObject({ method: 'GET', headers: { 'X-XKE-Session': auth().session_id } })
    expect(saved).toMatchObject({ cached: true, syncedAt: semester().updated_at, viewedWeek: 3 })
  })
  it('local reads without context return the saved term without claiming current-school week information', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(semester({
      term: { academic_term: '2025-2026-2', label: '历史' }, updated_at: '', cached: true,
    }))).mockResolvedValueOnce(response(semester()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect(await api.getLocalSemester()).toMatchObject({ cached: true, syncedAt: null, currentWeek: null, term: { startDate: '' } })
    expect(fetcher.mock.calls[0][0]).toBe('/api/schedule/semester/local')
    expect((await api.getSemester()).cached).toBe(false)
    expect(fetcher.mock.calls[1][0]).toBe('/api/schedule/semester')
  })
  it.each(['getLocalSemester', 'getSemester'] as const)('%s rejects a semester from another term', async (method) => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(semester()))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await expect(api[method]('2025-2026-2')).rejects.toThrow('所选学期')
  })
})


describe('desktop native capability fallback', () => {
  it('allows ordinary browser manual authentication after bootstrap/settings 403 without granting desktop capabilities', async () => {
    const fetcher = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockResolvedValueOnce(response(null, 403))
      .mockResolvedValueOnce(response(auth()))
      .mockResolvedValueOnce(response(null, 403))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    expect(await api.bootstrap()).toBeNull()
    const result = await api.login('20260001', 'synthetic-password')
    expect(result.kind === 'authenticated' && result.workspace.settings).toMatchObject({ desktopSupported: false, launchAtLogin: false })
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/config', '/api/desktop/auth-bootstrap', '/api/auth/login', '/api/desktop/settings'])
  })
})


describe('cancellable local background reads', () => {
  it('does not issue requests for an already cancelled job/cache read', async () => {
    const fetcher = vi.fn<typeof fetch>(), api = new RuntimeApi(new HttpClient('', fetcher)), controller = new AbortController()
    controller.abort()
    await expect(api.getSchedule(3, '2026-2027-1', controller.signal)).rejects.toMatchObject({ name: 'AbortError' })
    await expect(api.getLocalSemester('2026-2027-1', null, controller.signal)).rejects.toMatchObject({ name: 'AbortError' })
    await expect(api.getJobs(controller.signal)).rejects.toMatchObject({ name: 'AbortError' })
    expect(fetcher).not.toHaveBeenCalled()
  })
  it('discards an in-flight local result after logical cancellation without changing HttpClient transport', async () => {
    let resolve!: (response: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockImplementation(() => new Promise((done) => { resolve = done }))
    const api = new RuntimeApi(new HttpClient('', fetcher)), controller = new AbortController()
    const pending = api.getSchedule(3, '2026-2027-1', controller.signal)
    const assertion = expect(pending).rejects.toMatchObject({ name: 'AbortError' })
    controller.abort(); resolve(response(week()))
    await assertion
    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(fetcher.mock.calls[0][1]?.method).toBe('GET')
  })
  it('drains both parallel job reads on partial failure before another observation can start', async () => {
    let resolve!: (response: Response) => void
    const fetcher = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(response(null, 503))
      .mockImplementationOnce(() => new Promise((done) => { resolve = done }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    let settled = false
    const pending = api.getJobs().finally(() => { settled = true })
    const assertion = expect(pending).rejects.toMatchObject({ status: 503 })
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2))
    expect(settled).toBe(false)
    resolve(response({ enabled: false, next_run_at: null, last_run_at: null, last_error: '' }))
    await assertion
    expect(settled).toBe(true)
  })
})

describe('profile display preferences', () => {
  it('saves only nickname fields and leaves the real name and academic data intact', async () => {
    const initial = auth(), updated = { ...initial.student, nickname: '我的昵称', use_nickname: true }
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(initial)).mockResolvedValueOnce(response({ student: updated }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    expect(await api.updateNickname(' 我的昵称 ')).toEqual(updated)
    expect(fetcher.mock.calls[1][0]).toBe('/api/auth/profile')
    expect(JSON.parse(String(fetcher.mock.calls[1][1]?.body))).toEqual({ nickname: '我的昵称', use_nickname: true })
    expect(updated.name).toBe(initial.student.name)
  })
  it('clearing the nickname restores the default-name preference', async () => {
    const initial = auth()
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(initial)).mockResolvedValueOnce(response({ student: initial.student }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    await api.updateNickname('  ')
    expect(JSON.parse(String(fetcher.mock.calls[1][1]?.body))).toEqual({ nickname: '', use_nickname: false })
  })
  it('ignores a delayed profile response after logout', async () => {
    let resolve!: (response: Response) => void
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockImplementationOnce(() => new Promise((done) => { resolve = done }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('20260001', 'test-password')
    const pending = api.updateNickname('过期昵称')
    api.clearSession()
    resolve(response({ student: { ...auth().student, nickname: '过期昵称', use_nickname: true } }))
    await expect(pending).rejects.toMatchObject({ status: 409 })
  })
})

describe('session isolation during response parsing', () => {
  it('rejects an old response if the session changes while JSON is being parsed', async () => {
    let finish!: (value: unknown) => void
    const reply = response(null)
    const parse = vi.spyOn(reply, 'json').mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const http = new HttpClient('', vi.fn<typeof fetch>().mockResolvedValue(reply))
    http.setSession('synthetic-A')
    const pending = http.request('/api/synthetic')
    await vi.waitFor(() => expect(parse).toHaveBeenCalledOnce())
    http.clearSession()
    finish({ ok: true, data: { stale: true } })
    await expect(pending).rejects.toMatchObject({ status: 409 })
  })
  it('does not overwrite account B preferences with account A after parsing completes', async () => {
    let finish!: (value: unknown) => void
    const reply = response(null)
    const parse = vi.spyOn(reply, 'json').mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const a = auth()
    const b = { ...auth(), session_id: 'synthetic-B', student: { ...auth().student, student_id: 'synthetic-student-B' } }
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(a)).mockResolvedValueOnce(reply).mockResolvedValueOnce(response(b))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('synthetic-A', 'synthetic-password')
    const pending = api.updateSettings({ showWeekend: false })
    await vi.waitFor(() => expect(parse).toHaveBeenCalledOnce())
    api.clearSession()
    await api.login('synthetic-B', 'synthetic-password')
    finish({ ok: true, data: { settings: { ...account(), appearance: { theme: 'night' }, ui: { ...account().ui, show_weekend: false } } } })
    await expect(pending).rejects.toMatchObject({ status: 409 })
  })
})

describe('settings API state ownership', () => {
  it.each(['read', 'write'] as const)('rejects a late %s before mutating a newly authenticated account', async (operation) => {
    const a = auth(), b = { ...auth(), session_id: 'synthetic-B', student: { ...auth().student, student_id: 'synthetic-student-B' } }
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(a)).mockResolvedValueOnce(response(b))
    const http = new HttpClient('', fetcher), api = new RuntimeApi(http)
    await api.login('synthetic-A', 'synthetic-password')
    let finish!: (value: unknown) => void
    // Isolate the API commit boundary, after transport ownership has already been checked.
    const transport = vi.spyOn(http, 'request').mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    const pending = operation === 'read' ? api.getSettings() : api.updateSettings({ showWeekend: false })
    api.clearSession(); transport.mockRestore()
    await api.login('synthetic-B', 'synthetic-password')
    finish({ settings: { ...account(), ui: { ...account().ui, show_weekend: false } } })
    await expect(pending).rejects.toMatchObject({ status: 409 })
    // A subsequent no-op reads API memory without making another request.
    expect(await api.updateSettings({})).toMatchObject({ portalAccount: 'synthetic-student-B', showWeekend: true })
  })
})
