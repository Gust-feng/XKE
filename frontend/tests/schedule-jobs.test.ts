import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../src/api/http'
import { adaptWeek, adaptSemester } from '../src/api/adapters'
import { observeScheduleJobs, readScheduleCache, scheduleJobNotice, type Jobs } from '../src/lib/schedule-jobs'
import { semester, week } from './fixtures'

const job = (patch: Partial<Jobs['schedule']> = {}): Jobs => ({ network: { enabled: false, next_run_at: null, last_run_at: null, last_error: '' }, schedule: { enabled: true, next_run_at: null, last_run_at: null, last_error: '', ...patch } })
const success = '2026-10-16T09:00:00Z'
const deferred = <T>() => { let resolve!: (value: T) => void; let reject!: (error: unknown) => void; const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no }); return { promise, resolve, reject } }
const flush = async () => { await vi.advanceTimersByTimeAsync(0) }
function harness() {
  let applied: string | null = null, current = true
  const read = vi.fn<(signal: AbortSignal) => Promise<Jobs>>().mockResolvedValue(job())
  const reload = vi.fn<(signal: AbortSignal) => Promise<boolean>>().mockResolvedValue(true)
  const onJobs = vi.fn(), onError = vi.fn(), onAppliedRun = vi.fn((run: string) => { applied = run })
  const observer = observeScheduleJobs({ read, reload, onJobs, onError, onAppliedRun, isCurrent: () => current, appliedRun: () => applied })
  return { observer, read, reload, onJobs, onError, onAppliedRun, invalidate: () => { current = false } }
}
beforeEach(() => vi.useFakeTimers())
afterEach(() => vi.useRealTimers())

describe('visible background schedule observation', () => {
  it('does nothing while hidden and polls once every 30 seconds after completion', async () => {
    const h = harness()
    await h.observer.refresh(); await vi.advanceTimersByTimeAsync(60_000)
    expect(h.read).not.toHaveBeenCalled()
    h.observer.setVisible(true); await flush()
    expect(h.read).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(30_000)
    expect(h.read).toHaveBeenCalledTimes(2)
    h.observer.setVisible(false); await vi.advanceTimersByTimeAsync(90_000)
    expect(h.read).toHaveBeenCalledTimes(2)
    h.observer.stop()
  })
  it('deduplicates repeated refresh and never overlaps a slow job read', async () => {
    const h = harness(), pending = deferred<Jobs>()
    h.read.mockReturnValueOnce(pending.promise)
    h.observer.setVisible(true)
    const first = h.observer.refresh(); const second = h.observer.refresh()
    expect(first).toBe(second)
    await vi.advanceTimersByTimeAsync(120_000)
    expect(h.read).toHaveBeenCalledTimes(1)
    pending.resolve(job()); await first
    expect(h.read).toHaveBeenCalledTimes(1)
    h.observer.stop()
  })
  it('aborts on hide, ignores a late response and resumes once without overlap', async () => {
    const h = harness(), pending = deferred<Jobs>()
    h.read.mockReturnValueOnce(pending.promise)
    h.observer.setVisible(true)
    const signal = h.read.mock.calls[0][0]
    h.observer.setVisible(false); h.observer.setVisible(true)
    expect(signal.aborted).toBe(true)
    expect(h.read).toHaveBeenCalledTimes(1)
    pending.resolve(job({ last_run_at: success })); await flush()
    expect(h.reload).not.toHaveBeenCalled()
    expect(h.read).toHaveBeenCalledTimes(2)
    expect(h.onJobs).toHaveBeenCalledTimes(1)
    h.observer.stop()
  })
  it('reloads local caches once for each successful job, including the first visible snapshot', async () => {
    const h = harness(); h.read.mockResolvedValue(job({ last_run_at: success }))
    h.observer.setVisible(true); await flush()
    await vi.advanceTimersByTimeAsync(30_000)
    expect(h.reload).toHaveBeenCalledTimes(1)
    expect(h.onAppliedRun).toHaveBeenCalledWith(success)
    h.read.mockResolvedValue(job({ last_run_at: '2026-10-16T10:00:00Z' }))
    await vi.advanceTimersByTimeAsync(30_000)
    expect(h.reload).toHaveBeenCalledTimes(2)
    h.observer.stop()
  })
  it.each([{ last_error: '学校连接过期' }, { requires_login: true }])('does not reload after a failed/auth-required run: %o', async (error) => {
    const h = harness(); h.read.mockResolvedValue(job({ last_run_at: success, ...error }))
    h.observer.setVisible(true); await flush()
    expect(h.reload).not.toHaveBeenCalled()
    h.observer.stop()
  })
  it('retries a failed local read on the next visible tick without acknowledging it early', async () => {
    const h = harness(); h.read.mockResolvedValue(job({ last_run_at: success })); h.reload.mockRejectedValueOnce(new Error('本地读取失败'))
    h.observer.setVisible(true); await flush()
    expect(h.onAppliedRun).not.toHaveBeenCalled()
    expect(h.onError).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(30_000)
    expect(h.onAppliedRun).toHaveBeenCalledWith(success)
    h.observer.stop()
  })
  it('never acknowledges or publishes a stale auth/selection generation', async () => {
    const h = harness(), pending = deferred<boolean>()
    h.read.mockResolvedValue(job({ last_run_at: success })); h.reload.mockReturnValueOnce(pending.promise)
    h.observer.setVisible(true); await flush()
    h.invalidate(); pending.resolve(true); await flush()
    expect(h.onAppliedRun).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(60_000)
    expect(h.read).toHaveBeenCalledTimes(1)
    h.observer.stop()
  })
  it('aborts on stop and suppresses stale errors and subsequent reads', async () => {
    const h = harness(), pending = deferred<Jobs>()
    h.read.mockReturnValueOnce(pending.promise); h.observer.setVisible(true)
    const signal = h.read.mock.calls[0][0]
    h.observer.stop(); pending.reject(new Error('旧会话失败')); await flush()
    expect(signal.aborted).toBe(true)
    expect(h.onError).not.toHaveBeenCalled()
    expect(h.onJobs).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(60_000)
    expect(h.read).toHaveBeenCalledTimes(1)
  })
})

describe('local cache rehydration', () => {
  const term = week().term.academic_term
  const latest = () => adaptWeek(week(4))
  it('reads only local week/semester endpoints and preserves a different selected week', async () => {
    const current = latest(), selected = adaptWeek(week(2))
    const api = { getSchedule: vi.fn().mockResolvedValueOnce(current).mockResolvedValueOnce(selected), getLocalSemester: vi.fn().mockResolvedValue(adaptSemester(semester(), current)) }
    const signal = new AbortController().signal
    const result = await readScheduleCache(api, term, null, 2, signal)
    expect(api.getSchedule.mock.calls).toEqual([[undefined, term, signal], [2, term, signal]])
    expect(api.getLocalSemester).toHaveBeenCalledWith(term, current, signal)
    expect(result.weeks.map((w) => w.viewedWeek)).toEqual([4, 2])
    expect(result.semester?.term.id).toBe(term)
  })
  it('refreshes the active semester without reading or selecting the historical semester on screen', async () => {
    const current = latest(), api = { getSchedule: vi.fn().mockResolvedValue(current), getLocalSemester: vi.fn().mockResolvedValue(adaptSemester(semester(), current)) }
    await readScheduleCache(api, term, null, undefined, new AbortController().signal)
    expect(api.getSchedule).toHaveBeenCalledTimes(1)
    expect(api.getSchedule.mock.calls[0][1]).toBe(term)
  })
  it('keeps available semester data when a local week is absent, never falling back to school', async () => {
    const api = { getSchedule: vi.fn().mockRejectedValue(new ApiError(404, '没有缓存')), getLocalSemester: vi.fn().mockResolvedValue(adaptSemester(semester())) }
    const result = await readScheduleCache(api, term, null, undefined, new AbortController().signal)
    expect(result.weeks).toEqual([])
    expect(result.semester).not.toBeNull()
  })
  it('rejects a mismatched term or week before publishing the batch', async () => {
    const api = { getSchedule: vi.fn().mockResolvedValue(adaptWeek(week(4, { term: { academic_term: '2025-2026-2', label: '历史' } }))), getLocalSemester: vi.fn() }
    await expect(readScheduleCache(api, term, null, 2, new AbortController().signal)).rejects.toThrow('学期不一致')
    expect(api.getLocalSemester).not.toHaveBeenCalled()
    api.getSchedule.mockResolvedValueOnce(latest()).mockResolvedValueOnce(adaptWeek(week(3)))
    api.getLocalSemester.mockResolvedValue(adaptSemester(semester()))
    await expect(readScheduleCache(api, term, null, 2, new AbortController().signal)).rejects.toThrow('周次不一致')
  })
  it('does not start another local read after cancellation, even if transport returns late', async () => {
    const pending = deferred<ReturnType<typeof latest>>()
    const api = { getSchedule: vi.fn().mockReturnValue(pending.promise), getLocalSemester: vi.fn() }, controller = new AbortController()
    const result = readScheduleCache(api, term, null, 2, controller.signal)
    const assertion = expect(result).rejects.toMatchObject({ name: 'AbortError' })
    controller.abort(); pending.resolve(latest()); await assertion
    expect(api.getLocalSemester).not.toHaveBeenCalled()
  })
})

describe('quiet sync status', () => {
  it('prioritizes reconnection, reports failures, and clears notice on a later healthy result', () => {
    expect(scheduleJobNotice(job({ requires_login: true, last_error: '登录过期' }).schedule)).toMatchObject({ label: '需连接教务', requiresLogin: true })
    expect(scheduleJobNotice(job({ last_error: '学校暂不可用' }).schedule)).toMatchObject({ label: '自动更新失败' })
    expect(scheduleJobNotice(job().schedule)).toBeNull()
    expect(scheduleJobNotice(job({ enabled: false, last_error: '旧失败', requires_login: true }).schedule)).toBeNull()
    expect(scheduleJobNotice(null, '无法读取')).toMatchObject({ label: '更新状态不可用' })
  })
})
