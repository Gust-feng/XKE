// @vitest-environment happy-dom
import { act, createElement, useLayoutEffect, type ReactNode } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { MotionConfig } from 'motion/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../src/api/http'
import { adaptSemester, adaptSettings, adaptWeek } from '../src/api/adapters'
import type { Schedule, Workspace } from '../src/api/types'
import { auth, row, semester, week } from './fixtures'

vi.hoisted(() => { vi.stubGlobal('__XKE_PREVIEW__', false) })
const client = vi.hoisted(() => ({
  preview: true, bootstrap: vi.fn(), clearSession: vi.fn(), cancelAuth: vi.fn(), logout: vi.fn(),
  getLocalSemester: vi.fn(), getSemester: vi.fn(), getSchedule: vi.fn(), syncSchedule: vi.fn(),
  getJobs: vi.fn(), getSettings: vi.fn(), updateSettings: vi.fn(), updateNickname: vi.fn(),
  getAppUpdate: vi.fn(), checkAppUpdate: vi.fn(), connect: vi.fn(),
  getNetworkSummary: vi.fn(), getNetStatus: vi.fn(),
}))
const motion = vi.hoisted(() => ({ create: vi.fn() }))
vi.mock('../src/api', () => ({ api: client }))
vi.mock('../src/editions/one/semester-motion', () => ({ createSemesterMotion: motion.create }))
import { StoreProvider } from '../src/lib/store'
import { useStore } from '../src/lib/store-context'
import { ProfileName } from '../src/editions/one/ProfileName'
import { SemesterField } from '../src/editions/one/SemesterField'
import { SwissSettings } from '../src/editions/one/SwissSettings'
import { OneLayout } from '../src/editions/one/OneLayout'

let store: ReturnType<typeof useStore>
let root: Root
let container: HTMLDivElement
let workspace: Workspace
type RenderStore = (value: ReturnType<typeof useStore>) => ReactNode
function Probe({ render }: { render?: RenderStore }) {
  const value = useStore()
  useLayoutEffect(() => { store = value }, [value])
  return render?.(value) ?? null
}
async function mount(render?: RenderStore) {
  await act(async () => { root.render(createElement(MotionConfig, { skipAnimations: true }, createElement(StoreProvider, null, createElement(Probe, { render })))) })
}
const deferred = <T,>() => {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail })
  return { promise, resolve, reject }
}
function edit(input: HTMLInputElement, value: string) {
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, value)
  input.dispatchEvent(new Event('input', { bubbles: true }))
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true)
  client.preview = true
  history.replaceState(null, '', '/')
  workspace = { student: auth().student, connected: true,
    settings: { ...adaptSettings(auth().settings, auth().student.student_id), desktopSupported: true }, schedule: adaptWeek(week()) }
  client.bootstrap.mockImplementation(async () => workspace)
  client.logout.mockResolvedValue(undefined)
  client.getNetworkSummary.mockResolvedValue({ configured: true, available: true, message: '', updated_at: null })
  client.getNetStatus.mockResolvedValue({ online: false, error: '', message: '未连接' })
  client.checkAppUpdate.mockResolvedValue({ current_version: '1.0.0', repository_url: 'https://github.com/Gust-feng/XKE', release_url: '', latest_version: null, status: 'up_to_date', progress: 0, error: '', checked_at: null })
  client.getSettings.mockImplementation(async () => workspace.settings)
  client.updateSettings.mockImplementation(async (patch) => ({ ...workspace.settings, ...patch }))
  client.updateNickname.mockImplementation(async (nickname) => ({ ...workspace.student, nickname, use_nickname: !!nickname }))
  client.getJobs.mockResolvedValue({ network: { enabled: false, last_run_at: null }, schedule: { enabled: false, last_run_at: null } })
  client.getAppUpdate.mockResolvedValue({ current_version: '1.0.0', repository_url: 'https://github.com/Gust-feng/XKE',
    release_url: '', latest_version: null, status: 'idle', progress: 0, error: '', checked_at: null })
  container = document.createElement('div'); document.body.append(container)
  root = createRoot(container)
})
afterEach(async () => {
  await act(async () => root.unmount())
  container.remove()
  vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals()
})

describe('settings save feedback and queue', () => {
  it('returns an inline save failure without a background toast and continues queued saves', async () => {
    await mount()
    client.updateSettings.mockRejectedValueOnce(new ApiError(500, '设置保存失败'))
    await act(async () => {
      const failed = expect(store.updateSettings({ autoUpdate: true }, 'inline')).rejects.toThrow('设置保存失败')
      const next = store.updateSettings({ showWeekend: false }, 'inline')
      await Promise.all([failed, next])
    })
    expect(client.updateSettings).toHaveBeenCalledTimes(2)
    expect(store.settings?.showWeekend).toBe(false)
    expect(store.toast).toBeNull()
  })
  it('retains normal setting-row toast feedback without leaking an unhandled rejection', async () => {
    await mount()
    client.updateSettings.mockRejectedValueOnce(new ApiError(500, '设置保存失败'))
    await act(async () => { await expect(store.updateSettings({ showWeekend: false })).resolves.toBeUndefined() })
    expect(store.toast?.text).toBe('设置保存失败')
    expect(store.settings?.showWeekend).toBe(true)
  })
  it('shows auto-update save errors inside the open update panel and supports retry', async () => {
    await mount(() => createElement(SwissSettings))
    await act(async () => (container.querySelector('.st-version') as HTMLButtonElement).click())
    client.updateSettings.mockRejectedValueOnce(new ApiError(500, '自动更新设置保存失败'))
    await act(async () => (document.querySelector('input[aria-label="自动更新应用"]') as HTMLInputElement).click())
    expect(document.querySelector('.one-update-notice')?.textContent).toBe('自动更新设置保存失败')
    expect(store.toast).toBeNull()
    await act(async () => (document.querySelector('input[aria-label="自动更新应用"]') as HTMLInputElement).click())
    expect(store.settings?.autoUpdate).toBe(true)
    expect(document.querySelector('.one-update-notice')).toBeNull()
  })
})

describe('nickname focus', () => {
  it('keeps a failed draft without pulling focus away from the control the user selected', async () => {
    const pending = deferred<Workspace['student']>()
    client.updateNickname.mockReturnValueOnce(pending.promise)
    await mount((value) => createElement('div', null,
      value.user && createElement(ProfileName, { student: value.user.student }), createElement('button', { id: 'outside' }, '其他操作')))
    await act(async () => (container.querySelector('.one-profile-edit') as HTMLButtonElement).click())
    const input = container.querySelector('input')!
    await act(async () => edit(input, '合成昵称'))
    const outside = container.querySelector('#outside') as HTMLButtonElement
    await act(async () => outside.focus())
    expect(client.updateNickname).toHaveBeenCalledWith('合成昵称')
    await act(async () => pending.reject(new ApiError(500, '昵称保存失败')))
    // Flush the frame the old failure path used to steal focus.
    await act(async () => { await new Promise(resolve => requestAnimationFrame(resolve)) })
    expect(document.activeElement).toBe(outside)
    expect(input.value).toBe('合成昵称')
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('昵称保存失败')
    expect(client.updateNickname).toHaveBeenCalledOnce()
    await act(async () => { input.focus(); input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })) })
    expect(container.querySelector('input')).toBeNull()
    expect(store.user?.student.nickname).toBe('')
  })
})

describe('account-owned semester cache restoration', () => {
  const current = '2026-2027-1', selected = '2025-2026-2', oldest = '2025-2026-1'
  function history() {
    client.preview = false
    workspace.student = { ...workspace.student, enrollment_date: '2025-09-01' }
    const full = adaptSemester(semester({ available_terms: [current, selected, oldest].map(value => ({ value, label: value, selected: value === current })) }), workspace.schedule)
    const pending = deferred<Schedule>()
    client.getLocalSemester.mockImplementation(async (term?: string) => term && term !== current ? { ...full, term: { ...full.term, id: term } } : full)
    client.getSemester.mockImplementation((term?: string) => term === current ? pending.promise : Promise.resolve(full))
    return { full, pending }
  }
  it('finishes restoring all personal semesters after the user switches the visible semester', async () => {
    const { full, pending } = history()
    await mount()
    expect(client.getSemester).toHaveBeenCalledWith(current, expect.anything())
    await act(async () => { await store.selectTerm(selected) })
    expect(store.schedule?.term.id).toBe(selected)
    await act(async () => pending.resolve(full))
    expect(client.getLocalSemester.mock.calls.some(([term]) => term === oldest)).toBe(true)
    expect(store.schedule?.term.id).toBe(selected)
    expect(store.selectedWeek).toBe(1)
  })
  it('keeps a manual refresh cache fill alive without pulling the user out of a historical semester', async () => {
    const { full, pending } = history()
    await mount()
    await act(async () => pending.resolve(full))
    client.getLocalSemester.mockClear()
    const refreshed = deferred<Schedule>()
    client.getSemester.mockReturnValueOnce(refreshed.promise)
    client.syncSchedule.mockResolvedValueOnce(adaptWeek(week()))
    let sync!: Promise<void>
    await act(async () => { sync = store.sync() })
    await act(async () => { await store.selectTerm(selected) })
    await act(async () => { refreshed.resolve(full); await sync })
    expect(client.getLocalSemester.mock.calls.some(([term]) => term === oldest)).toBe(true)
    expect(store.schedule?.term.id).toBe(selected)
    expect(store.selectedWeek).toBe(1)
  })
  it('discards the same restoration after logout without starting more semester reads', async () => {
    const { full, pending } = history()
    await mount()
    await act(async () => { await store.logout() })
    const reads = client.getLocalSemester.mock.calls.length
    await act(async () => pending.resolve(full))
    expect(client.getLocalSemester).toHaveBeenCalledTimes(reads)
    expect(store.user).toBeNull()
    expect(store.schedule).toBeNull()
  })
})

describe('semester motion lifecycle', () => {
  it('preserves the controller across minute ticks and equivalent account-cache snapshots', async () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-30T12:00:00+08:00'))
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(600)
    motion.create.mockImplementation((_dots, setState) => {
      let active = false
      return { toggle: vi.fn(() => { active = !active; setState(active ? 'in' : 'out') }),
        reset: vi.fn(), restore: vi.fn(), destroy: vi.fn() }
    })
    const full = adaptSemester(semester({ rows: [row({ section_numbers: Array.from({ length: 10 }, (_, i) => i + 1),
      week_numbers: Array.from({ length: 20 }, (_, i) => i + 1) })] }), workspace.schedule)
    workspace.schedule = full
    await mount(() => createElement(SemesterField))
    expect(motion.create).toHaveBeenCalledOnce()
    const controller = motion.create.mock.results[0].value
    await act(async () => (container.querySelector('.one-sf-trigger') as HTMLButtonElement).click())
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(motion.create).toHaveBeenCalledOnce()
    expect(controller.destroy).not.toHaveBeenCalled()
    await act(async () => (container.querySelector('.one-sf-trigger') as HTMLButtonElement).click())
    expect(controller.toggle).toHaveBeenCalledTimes(2)
    expect(motion.create).toHaveBeenCalledOnce()
    client.connect.mockResolvedValueOnce({ kind: 'authenticated', workspace: { ...workspace, schedule: { ...full, courses: full.courses.map(course => ({ ...course })) } } })
    await act(async () => { await store.connect() })
    expect(motion.create).toHaveBeenCalledOnce()
    expect(controller.destroy).not.toHaveBeenCalled()
    vi.setSystemTime(new Date('2026-10-07T12:00:00+08:00'))
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(motion.create).toHaveBeenCalledTimes(2)
    expect(controller.destroy).toHaveBeenCalledOnce()
    await act(async () => root.unmount())
    expect(controller.destroy).toHaveBeenCalledOnce()
  })
})


describe('event-owned view and update feedback', () => {
  it('clears the no-update result when closing and immediately reopening the panel', async () => {
    await mount(() => createElement(SwissSettings))
    await act(async () => (container.querySelector('.st-version') as HTMLButtonElement).click())
    await act(async () => (document.querySelector('.one-update-primary') as HTMLButtonElement).click())
    expect(document.querySelector('.one-update-primary')?.textContent).toBe('暂无更新')
    await act(async () => (document.querySelector('[aria-label="关闭应用更新"]') as HTMLButtonElement).click())
    expect(document.querySelector('.one-update-dialog')).toBeNull()
    await act(async () => (container.querySelector('.st-version') as HTMLButtonElement).click())
    expect(document.querySelector('.one-update-primary')?.textContent).toBe('检查更新')
  })
  it('loads summaries on network entry and connectivity changes, not equivalent status refreshes', async () => {
    await mount()
    expect(client.getNetworkSummary).not.toHaveBeenCalled()
    await act(async () => store.setView('net'))
    expect(client.getNetworkSummary).toHaveBeenCalledOnce()
    await act(async () => { await store.refreshNet() })
    expect(client.getNetworkSummary).toHaveBeenCalledOnce()
    client.getNetStatus.mockResolvedValueOnce({ online: true, error: '', message: '已连接' })
    await act(async () => { await store.refreshNet() })
    expect(client.getNetworkSummary).toHaveBeenCalledTimes(2)
    await act(async () => store.setView('settings'))
    await act(async () => store.setView('net'))
    expect(client.getNetworkSummary).toHaveBeenCalledTimes(3)
  })
  it('loads an initial network-view summary after authentication and ignores a late result after logout', async () => {
    history.replaceState(null, '', '/?v=net')
    const pending = deferred<unknown>()
    client.getNetworkSummary.mockReturnValueOnce(pending.promise)
    await mount()
    expect(client.getNetworkSummary).toHaveBeenCalledOnce()
    expect(store.summaryBusy).toBe(true)
    await act(async () => { await store.logout() })
    await act(async () => pending.resolve({ configured: true, available: true, message: 'old response' }))
    expect(store.networkSummary).toBeNull()
    expect(store.summaryBusy).toBe(false)
  })
  it('places week controls through the layout-owned slot and restores them after leaving the view', async () => {
    await mount(() => createElement(OneLayout))
    expect(container.querySelector('#one-slot .one-term')).not.toBeNull()
    await act(async () => (container.querySelector('[aria-label="上一周"]') as HTMLButtonElement).click())
    expect(store.selectedWeek).toBe(2)
    await act(async () => store.setView('settings'))
    expect(container.querySelector('#one-slot')).toBeNull()
    await act(async () => store.setView('week'))
    expect(container.querySelector('#one-slot .one-term')).not.toBeNull()
    expect(store.selectedWeek).toBe(2)
  })
})
