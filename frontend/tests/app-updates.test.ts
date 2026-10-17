import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adaptSettings, settingsPatches } from '../src/api/adapters'
import type { AppUpdateDto } from '../src/api/dto'
import { HttpClient } from '../src/api/http'
import { RuntimeApi } from '../src/api/runtime'
import { AppUpdateContent, type AppUpdatePanelProps } from '../src/editions/one/AppUpdatePanel'
import { emptyUpdateObservation, observeAppUpdates, projectLink, updateActionForStatus, updateProgress, type UpdateObservation } from '../src/lib/app-updates'
import { account, auth, response } from './fixtures'

const update = (status: AppUpdateDto['status'] = 'idle', patch: Partial<AppUpdateDto> = {}): AppUpdateDto => ({
  current_version: '1.0.0', repository_url: 'https://github.com/Gust-feng/XKE',
  release_url: 'https://github.com/Gust-feng/XKE/releases', latest_version: null,
  status, progress: 0, error: '', checked_at: null, ...patch,
})
const deferred = <T>() => {
  let resolve!: (result: T) => void
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}
function harness(status: AppUpdateDto['status'] = 'idle') {
  let latest: UpdateObservation = { ...emptyUpdateObservation }
  const api = {
    getAppUpdate: vi.fn(async () => update(status)),
    checkAppUpdate: vi.fn(async () => update('available', { latest_version: '1.1.0' })),
    downloadAppUpdate: vi.fn(async () => update('downloading', { latest_version: '1.1.0' })),
    applyAppUpdate: vi.fn(async () => update('installing')),
  }
  const change = vi.fn((value: UpdateObservation) => { latest = value })
  const observer = observeAppUpdates({ api, onChange: change })
  return { api, observer, change, state: () => latest }
}
const settle = async () => { await Promise.resolve(); await Promise.resolve(); await Promise.resolve() }
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })

describe('local update API and machine preference', () => {
  it('reads backend-selected versions without requiring a school login', async () => {
    const value = update('unsupported', { current_version: '1.0.0-dev', error: '开发运行不支持安装更新' })
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(value))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await expect(api.getAppUpdate()).resolves.toEqual(value)
    expect(fetcher).toHaveBeenCalledExactlyOnceWith('/api/app/updates', expect.objectContaining({ method: 'GET' }))
    expect(fetcher.mock.calls[0][1]?.headers).not.toHaveProperty('X-XKE-Session')
  })
  it.each(['check', 'download', 'apply'] as const)('posts %s only to the local host and returns its real state', async (action) => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(update('ready')))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const call = { check: () => api.checkAppUpdate(), download: () => api.downloadAppUpdate(), apply: () => api.applyAppUpdate() }
    await expect(call[action]()).resolves.toMatchObject({ status: 'ready' })
    expect(fetcher).toHaveBeenCalledExactlyOnceWith(`/api/app/updates/${action}`, expect.objectContaining({ method: 'POST', body: '{}' }))
  })
  it.each(['read', 'check', 'download', 'apply'] as const)('a pre-cancelled %s makes no request', async (action) => {
    const fetcher = vi.fn<typeof fetch>()
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const controller = new AbortController(); controller.abort()
    const call = { read: () => api.getAppUpdate(controller.signal), check: () => api.checkAppUpdate(controller.signal), download: () => api.downloadAppUpdate(controller.signal), apply: () => api.applyAppUpdate(controller.signal) }
    await expect(call[action]()).rejects.toMatchObject({ name: 'AbortError' })
    expect(fetcher).not.toHaveBeenCalled()
  })
  it('ignores late responses instead of reporting an old apply/check as current success', async () => {
    const done = deferred<Response>()
    const api = new RuntimeApi(new HttpClient('', vi.fn<typeof fetch>().mockReturnValue(done.promise)))
    const controller = new AbortController()
    const pending = api.checkAppUpdate(controller.signal)
    controller.abort(); done.resolve(response(update('up_to_date')))
    await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
  })
  it('keeps automatic app updates in a sparse machine patch, preserving explicit false', () => {
    expect(settingsPatches({ autoUpdate: false })).toEqual({ account: {}, desktop: { auto_update_enabled: false } })
    expect(settingsPatches({ autoUpdate: true })).toEqual({ account: {}, desktop: { auto_update_enabled: true } })
    expect(settingsPatches({ showWeekend: false }).desktop).not.toHaveProperty('auto_update_enabled')
    expect(adaptSettings(account(), 'synthetic')).toMatchObject({ autoUpdate: false })
    expect(adaptSettings(account(), 'synthetic', { settings: { auto_update_enabled: true, launch_at_startup: false, silent_startup: false, close_to_tray: true }, startup_registered: false })).toMatchObject({ autoUpdate: true })
  })
  it('saving auto-update does not post account preferences or alter other machine options', async () => {
    const desktop = { settings: { auto_update_enabled: true, launch_at_startup: true, silent_startup: true, close_to_tray: true }, startup_registered: true }
    const fetcher = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ development: { host_mode: 'desktop-webview' } })))
      .mockResolvedValueOnce(response({ auth: auth() })).mockResolvedValueOnce(response(desktop))
      .mockResolvedValueOnce(response({ ...desktop, settings: { ...desktop.settings, auto_update_enabled: false } }))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.bootstrap()
    await expect(api.updateSettings({ autoUpdate: false })).resolves.toMatchObject({ autoUpdate: false, launchAtLogin: true, silentStartup: true, closeToTray: true })
    expect(fetcher.mock.calls.at(-1)).toMatchObject(['/api/desktop/settings', { method: 'POST', body: '{"auto_update_enabled":false}' }])
    expect(fetcher.mock.calls.some(([path]) => String(path).includes('/api/auth/preferences'))).toBe(false)
  })
})

describe('visible-only serial update observer', () => {
  it('a closed panel reads the compact version once, but never polls', async () => {
    vi.useFakeTimers()
    const { api, observer, state } = harness()
    await observer.refresh()
    expect(state().data?.current_version).toBe('1.0.0')
    await vi.advanceTimersByTimeAsync(60_000)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
    observer.stop()
  })
  it('polls each second during download, then each ten seconds without starting a download itself', async () => {
    vi.useFakeTimers()
    const { api, observer } = harness('downloading')
    observer.setVisible(true); await settle()
    await vi.advanceTimersByTimeAsync(999)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
    api.getAppUpdate.mockResolvedValue(update('ready'))
    await vi.advanceTimersByTimeAsync(1)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(9_999)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(1)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(3)
    expect(api.downloadAppUpdate).not.toHaveBeenCalled()
    expect(api.applyAppUpdate).not.toHaveBeenCalled()
    observer.stop()
  })
  it('hiding or closing aborts an old generation; reopening waits for it to drain', async () => {
    vi.useFakeTimers()
    const { api, observer, state } = harness()
    const done = deferred<AppUpdateDto>()
    api.getAppUpdate.mockReturnValueOnce(done.promise)
    observer.setVisible(true)
    const signal = (api.getAppUpdate.mock.calls[0] as unknown as [AbortSignal])[0]
    observer.setVisible(false)
    expect(signal.aborted).toBe(true)
    observer.setVisible(true)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
    done.resolve(update('ready')); await settle(); await settle()
    expect(api.getAppUpdate).toHaveBeenCalledTimes(2)
    expect(state().data?.status).toBe('idle')
    observer.setVisible(false)
    await vi.advanceTimersByTimeAsync(60_000)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(2)
    observer.stop()
  })
  it('slow requests and repeated clicks never overlap or queue duplicate commands', async () => {
    vi.useFakeTimers()
    const { api, observer } = harness()
    observer.setVisible(true); await settle()
    const done = deferred<AppUpdateDto>()
    api.checkAppUpdate.mockReturnValueOnce(done.promise)
    const check = observer.run('check')
    void observer.run('check'); void observer.refresh()
    await vi.advanceTimersByTimeAsync(45_000)
    expect(api.checkAppUpdate).toHaveBeenCalledTimes(1)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
    done.resolve(update('available')); await check
    observer.stop()
  })
  it('applies only a ready release after explicit interaction and never auto-retries apply', async () => {
    vi.useFakeTimers()
    const { api, observer, state } = harness('ready')
    observer.setVisible(true); await settle()
    api.applyAppUpdate.mockRejectedValueOnce(new Error('无法安全退出'))
    await observer.run('apply')
    expect(state().error).toBe('无法安全退出')
    await vi.advanceTimersByTimeAsync(30_000)
    expect(api.applyAppUpdate).toHaveBeenCalledTimes(1)
    observer.stop()
  })
  it.each(['unsupported', 'downloading', 'installing', 'checking'] as const)('blocks commands for %s', async (status) => {
    const { api, observer } = harness(status)
    observer.setVisible(true); await settle()
    await observer.run('apply'); await observer.run('download'); await observer.run('check')
    expect(api.checkAppUpdate).not.toHaveBeenCalled()
    expect(api.downloadAppUpdate).not.toHaveBeenCalled()
    expect(api.applyAppUpdate).not.toHaveBeenCalled()
    observer.stop()
  })
  it('does not install an available-but-not-downloaded release', async () => {
    const { api, observer } = harness('available')
    observer.setVisible(true); await settle()
    await observer.run('apply')
    expect(api.applyAppUpdate).not.toHaveBeenCalled()
    await observer.run('download')
    expect(api.downloadAppUpdate).toHaveBeenCalledTimes(1)
    observer.stop()
  })
  it('shows a transient read error inline, recovers quietly, and never retries a mutation', async () => {
    vi.useFakeTimers()
    const { api, observer, state } = harness()
    api.getAppUpdate.mockRejectedValueOnce(new Error('暂时无法读取更新状态'))
    observer.setVisible(true); await settle()
    expect(state().data).toBeNull()
    expect(state().error).toBe('暂时无法读取更新状态')
    await vi.advanceTimersByTimeAsync(10_000)
    expect(state().data?.status).toBe('idle')
    expect(state().error).toBe('')
    expect(api.checkAppUpdate).not.toHaveBeenCalled()
    observer.stop()
  })
  it('only an explicit successful empty check reports a single inline result, polling is silent', async () => {
    vi.useFakeTimers()
    const onNoUpdate = vi.fn()
    const api = { getAppUpdate: vi.fn(async () => update('up_to_date')), checkAppUpdate: vi.fn(async () => update('up_to_date')),
      downloadAppUpdate: vi.fn(async () => update('ready')), applyAppUpdate: vi.fn(async () => update('installing')) }
    const observer = observeAppUpdates({ api, onChange: vi.fn(), onNoUpdate })
    observer.setVisible(true); await settle()
    expect(onNoUpdate).not.toHaveBeenCalled()
    await observer.run('check')
    expect(onNoUpdate).toHaveBeenCalledExactlyOnceWith()
    await vi.advanceTimersByTimeAsync(20_000)
    expect(onNoUpdate).toHaveBeenCalledTimes(1)
    observer.stop()
  })
  it('a cancelled manual check does not show an out-of-context no-update result', async () => {
    const h = harness(), onNoUpdate = vi.fn(), delayed = deferred<AppUpdateDto>()
    const observer = observeAppUpdates({ api: h.api, onChange: vi.fn(), onNoUpdate })
    observer.setVisible(true); await settle()
    h.api.checkAppUpdate.mockReturnValueOnce(delayed.promise)
    const check = observer.run('check')
    observer.setVisible(false)
    delayed.resolve(update('up_to_date')); await check
    expect(onNoUpdate).not.toHaveBeenCalled()
    observer.stop(); h.observer.stop()
  })
  it('closing never sends a download cancellation or starts an installer', async () => {
    vi.useFakeTimers()
    const { api, observer } = harness('downloading')
    observer.setVisible(true); await settle()
    observer.setVisible(false)
    await observer.run('apply'); await observer.run('download'); await observer.run('check')
    await vi.advanceTimersByTimeAsync(60_000)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
    expect(api.applyAppUpdate).not.toHaveBeenCalled()
    expect(api.downloadAppUpdate).not.toHaveBeenCalled()
    expect(api.checkAppUpdate).not.toHaveBeenCalled()
    observer.stop()
  })
  it('unmount stops timers and all late state callbacks', async () => {
    vi.useFakeTimers()
    const { api, observer, change } = harness()
    const done = deferred<AppUpdateDto>()
    api.getAppUpdate.mockReturnValueOnce(done.promise)
    observer.setVisible(true); observer.stop()
    const calls = change.mock.calls.length
    done.resolve(update('ready')); await settle()
    await vi.advanceTimersByTimeAsync(60_000)
    expect(change).toHaveBeenCalledTimes(calls)
    expect(api.getAppUpdate).toHaveBeenCalledTimes(1)
  })
})

const content = (status: AppUpdateDto['status'], patch: Partial<AppUpdateDto> = {}, props: Partial<AppUpdatePanelProps> = {}) => renderToStaticMarkup(createElement(AppUpdateContent, {
  state: { ...emptyUpdateObservation, data: update(status, patch) }, autoUpdate: true, desktopSupported: true, preview: false,
  onAction: vi.fn(), onRefresh: vi.fn(), onAutoChange: vi.fn(), onClose: vi.fn(), ...props,
}))
describe('restrained update panel states', () => {
  it('unsupported keeps the same panel and only reports the installation boundary', () => {
    const html = content('unsupported')
    expect(html).not.toContain('one-update-feedback')
    expect(html).toMatch(/disabled=""[^>]*>安装不可用/)
    expect(html).not.toContain('重启更新')
  })
  it.each([true, false])('ready offers a concise restart action without an instruction paragraph (automatic=%s)', (autoUpdate) => {
    const html = content('ready', { latest_version: '1.1.0' }, { autoUpdate })
    expect(html).toContain('>下载完成</h2>')
    expect(html).toContain('>重启更新</button>')
    expect(html).not.toContain('已就绪')
    expect(html).not.toContain('真正退出')
    expect(html).not.toContain('可立即')
    expect(html).not.toContain('检查更新</button>')
    expect(html).toContain('1.0.0'); expect(html).toContain('1.1.0')
  })
  it('a manual empty result replaces the button without a corner notice or a second paragraph', () => {
    const html = content('up_to_date', {}, { noUpdate: true })
    expect(html).toContain('one-update-checked')
    expect(html).toContain('data-result="no-update"')
    expect(html).not.toContain('<path d="m3 8 3 3 7-7"')
    expect(html).toContain('暂无更新')
    expect(html).not.toContain('one-update-notice')
    expect(html).not.toContain('暂无可用更新')
    expect(content('up_to_date')).toContain('>检查更新</button>')
    expect(content('checking')).toContain('>检查中</button>')
    expect(content('checking')).toContain('>应用更新</h2>')
  })
  it('the product title follows backend state and preview adds no visible internal labels', () => {
    for (const [status, title] of [['idle', '应用更新'], ['available', '发现新版本'], ['downloading', '正在下载'], ['ready', '下载完成']] as const) {
      const html = content(status, { latest_version: status === 'idle' ? null : '1.0.1' }, { preview: true })
      expect(html).toContain(`>${title}</h2>`)
      expect(html).not.toContain('合成预览')
      expect(html).not.toContain('后台检查并下载')
      expect(html).not.toContain('one-update-status')
    }
  })
  it('an empty release feed means no available update, not proof that the version is latest', () => {
    const html = content('up_to_date', { latest_version: null })
    expect(html).not.toContain('暂无可用更新')
    expect(html).not.toContain('已是最新版本')
    expect(html).not.toContain('最新版本 <b')
    expect(html).toContain('>检查更新</button>')
  })
  it('uses backend status, not frontend version comparison', () => {
    expect(updateActionForStatus('available')).toBe('download')
    expect(updateActionForStatus('ready')).toBe('apply')
    expect(content('up_to_date', { current_version: '1.0.0', latest_version: '99.0.0' })).toContain('>应用更新</h2>')
  })
  it('download replaces the existing action with a bounded percentage, without an extra track or reserved row', () => {
    const html = content('downloading', { progress: 107 })
    expect(html).toContain('role="progressbar"')
    expect(html).toContain('aria-valuenow="100"')
    expect(html).toMatch(/one-update-primary[\s\S]*>100%<\/span><\/span><\/button>/)
    expect(html).not.toContain('one-update-feedback')
    expect(html).not.toContain('one-update-track')
    expect(html).not.toContain('立即更新并重启')
    expect(updateProgress(-10)).toBe(0)
    expect(updateProgress(Number.NaN)).toBe(0)
  })
  it('renders errors inline and installing as waiting, not a fake success', () => {
    expect(content('error', { error: '下载未完成，请重试' })).toContain('下载未完成，请重试')
    const html = content('installing')
    expect(html).toContain('>正在重启</h2>')
    expect(html).toMatch(/disabled=""[^>]*>正在重启/)
    expect(html).not.toContain('更新成功')
  })
  it('keeps project links as noopener ordinary anchors and rejects foreign/script targets', () => {
    const html = content('idle')
    expect(html).toContain('href="https://github.com/Gust-feng/XKE" target="_blank" rel="noopener noreferrer"')
    expect(html).not.toContain('window.open')
    for (const value of ['javascript:alert(1)', 'https://example.com/', 'https://github.com/Gust-feng/XKE-fake', 'https://user:pass@github.com/Gust-feng/XKE', 'http://github.com/Gust-feng/XKE']) expect(projectLink(value)).toBeNull()
    expect(projectLink('https://github.com/Gust-feng/XKE/releases/tag/v1.0.0')).toContain('/releases/tag/v1.0.0')
  })
})
