import { describe, expect, it, vi } from 'vitest'
import { adaptSemester, adaptWeek } from '../src/api/adapters'
import { ApiError } from '../src/api/http'
import type { Api, Schedule } from '../src/api/types'
import { cacheMissingSemesters, loadSemester } from '../src/lib/schedule-loading'
import { combineSchedule, OperationEpoch, saveSemester } from '../src/lib/schedule-state'
import { semester, week } from './fixtures'

const local = () => adaptSemester(semester({ cached: true }), adaptWeek(week()))
const client = () => ({
  getLocalSemester: vi.fn<Api['getLocalSemester']>().mockResolvedValue(local()),
  getSemester: vi.fn<Api['getSemester']>().mockResolvedValue(adaptSemester(semester())),
  getSchedule: vi.fn<Api['getSchedule']>().mockRejectedValue(new ApiError(404, '没有周快照')),
})

describe('local semester loading', () => {
  it('offline workspace restoration reads its explicit term without refreshing school', async () => {
    const api = client(), accept = vi.fn()
    const context = adaptWeek(week())
    expect(await loadSemester(api, { context, connected: false, refresh: true, accept, isCurrent: () => true })).toBe(true)
    expect(api.getLocalSemester).toHaveBeenCalledWith(context.term.id, context)
    expect(accept).toHaveBeenCalledWith(local())
    expect(combineSchedule(accept.mock.calls[0][0], [context], 5)?.courses.some((c) => c.weeks.some(([a, b]) => a <= 5 && 5 <= b))).toBe(true)
    expect(api.getSemester).not.toHaveBeenCalled()
  })

  it('with no week context the latest saved semester does not become a current-school calendar', async () => {
    const api = client(), accept = vi.fn()
    const saved = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' }, cached: true }))
    api.getLocalSemester.mockResolvedValue(saved)
    await loadSemester(api, { connected: false, accept, isCurrent: () => true })
    expect(api.getLocalSemester).toHaveBeenCalledWith(undefined, undefined)
    expect(api.getSchedule).toHaveBeenCalledWith(undefined, '2025-2026-2')
    expect(accept.mock.calls[0][0]).toMatchObject({ currentWeek: null, term: { startDate: '' } })
  })

  it('publishes local content before waiting for an online refresh and retains it on failure', async () => {
    const api = client()
    let fail!: (error: Error) => void
    api.getSemester.mockImplementation(() => new Promise((_resolve, reject) => { fail = reject }))
    let visible: Schedule | null = null
    const pending = loadSemester(api, { connected: true, refresh: true, isCurrent: () => true,
      accept: (next) => { visible = saveSemester(visible, next) } })
    await vi.waitFor(() => expect(api.getSemester).toHaveBeenCalledTimes(1))
    expect(visible).toEqual(local())
    fail(new ApiError(503, '刷新失败'))
    await expect(pending).rejects.toMatchObject({ status: 503 })
    expect(visible).toEqual(local())
  })

  it('switching terms uses a local hit without an unnecessary network request', async () => {
    const api = client(), accept = vi.fn()
    const saved = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' }, cached: true }))
    api.getLocalSemester.mockResolvedValue(saved)
    await loadSemester(api, { term: saved.term.id, context: adaptWeek(week()), connected: true, accept, isCurrent: () => true })
    expect(api.getLocalSemester.mock.calls[0][0]).toBe(saved.term.id)
    expect(accept).toHaveBeenCalledWith(saved)
    expect(api.getSemester).not.toHaveBeenCalled()
  })

  it('a missing local semester refreshes only when connected and uses the requested term', async () => {
    const api = client(), accept = vi.fn(), context = adaptWeek(week())
    api.getLocalSemester.mockRejectedValue(new ApiError(404, '未保存'))
    await loadSemester(api, { term: '2025-2026-2', context, connected: true, accept, isCurrent: () => true })
    expect(api.getSemester).toHaveBeenCalledWith('2025-2026-2', context)
    expect(accept).toHaveBeenCalledTimes(1)
    api.getSemester.mockClear(); accept.mockClear()
    expect(await loadSemester(api, { term: '2025-2026-2', context, connected: false, accept, isCurrent: () => true })).toBe(false)
    expect(api.getSemester).not.toHaveBeenCalled()
    expect(accept).not.toHaveBeenCalled()
  })

  it('a local read error other than a cache miss is not masked by a school refresh', async () => {
    const api = client(), accept = vi.fn()
    api.getLocalSemester.mockRejectedValue(new ApiError(401, '会话无效'))
    await expect(loadSemester(api, { connected: true, refresh: true, accept, isCurrent: () => true })).rejects.toMatchObject({ status: 401 })
    expect(api.getSemester).not.toHaveBeenCalled()
    expect(accept).not.toHaveBeenCalled()
  })

  it('a successful empty local semester is a cache hit', async () => {
    const api = client(), accept = vi.fn()
    const empty = adaptSemester(semester({ rows: [], cached: true }))
    api.getLocalSemester.mockResolvedValue(empty)
    expect(await loadSemester(api, { connected: true, accept, isCurrent: () => true })).toBe(true)
    expect(accept).toHaveBeenCalledWith(empty)
    expect(api.getSemester).not.toHaveBeenCalled()
  })

  it.each(['local', 'refresh'])('a late %s response after logout or a term change is discarded', async (stage) => {
    const api = client(), accept = vi.fn(), epoch = new OperationEpoch(), ticket = epoch.next()
    let done!: (schedule: Schedule) => void
    const delayed = () => new Promise<Schedule>((resolve) => { done = resolve })
    if (stage === 'local') api.getLocalSemester.mockImplementation(delayed)
    else api.getSemester.mockImplementation(delayed)
    const pending = loadSemester(api, { connected: true, refresh: true, accept, isCurrent: () => epoch.matches(ticket) })
    await vi.waitFor(() => expect(done).toBeDefined())
    const calls = accept.mock.calls.length
    epoch.next(); done(local())
    expect(await pending).toBe(false)
    expect(accept).toHaveBeenCalledTimes(calls)
    if (stage === 'local') expect(api.getSemester).not.toHaveBeenCalled()
  })
})


describe('semester calendar restoration', () => {
  it('uses any local same-term week, not an arbitrary first week, to restore dates', async () => {
    const api = client(), accept = vi.fn()
    const saved = adaptSemester(semester({ cached: true }))
    const context = adaptWeek(week(6))
    api.getLocalSemester.mockResolvedValue(saved)
    api.getSchedule.mockResolvedValue(context)
    expect(await loadSemester(api, { connected: false, accept, isCurrent: () => true })).toBe(true)
    expect(api.getSchedule).toHaveBeenCalledWith(undefined, saved.term.id)
    expect(accept.mock.calls.at(-1)?.[0]).toMatchObject({ term: { startDate: context.term.startDate },
      syncedAt: saved.syncedAt, courses: saved.courses })
    expect(api.getSemester).not.toHaveBeenCalled()
  })

  it('publishes a local week fallback when the semester file is absent, without mixing terms', async () => {
    const api = client(), accept = vi.fn(), acceptWeek = vi.fn()
    const snapshot = adaptWeek(week())
    api.getLocalSemester.mockRejectedValue(new ApiError(404, '没有学期快照'))
    api.getSchedule.mockResolvedValue(snapshot)
    expect(await loadSemester(api, { term: snapshot.term.id, connected: false, accept, acceptWeek, isCurrent: () => true })).toBe(true)
    expect(accept).not.toHaveBeenCalled()
    expect(acceptWeek).toHaveBeenCalledWith(snapshot)
    expect(api.getSemester).not.toHaveBeenCalled()
  })

  it('preserves the local semester if no dated week was cached', async () => {
    const api = client(), accept = vi.fn()
    api.getLocalSemester.mockResolvedValue(adaptSemester(semester()))
    expect(await loadSemester(api, { connected: false, accept, isCurrent: () => true })).toBe(true)
    expect(accept).toHaveBeenCalledTimes(1)
    expect(accept.mock.calls[0][0].term.startDate).toBe('')
  })

  it('passes the recovered calendar into refresh and discards cancelled calendar responses', async () => {
    const api = client(), accept = vi.fn(), context = adaptWeek(week())
    api.getLocalSemester.mockResolvedValue(adaptSemester(semester()))
    api.getSchedule.mockResolvedValue(context)
    await loadSemester(api, { connected: true, refresh: true, accept, isCurrent: () => true })
    expect(api.getSemester.mock.calls[0][1]?.term.startDate).toBe(context.term.startDate)
    const epoch = new OperationEpoch(), ticket = epoch.next()
    let done!: (schedule: Schedule) => void
    api.getSchedule.mockImplementation(() => new Promise((resolve) => { done = resolve }))
    const pending = loadSemester(api, { connected: true, refresh: true, accept, isCurrent: () => epoch.matches(ticket) })
    await vi.waitFor(() => expect(done).toBeDefined())
    const before = accept.mock.calls.length
    epoch.next(); done(context)
    expect(await pending).toBe(false)
    expect(accept).toHaveBeenCalledTimes(before)
  })
})

describe('explicit semester acquisition and personal history cache', () => {
  it('fetches the whole selected semester after a local restore even when connected is false', async () => {
    const api = client(), accept = vi.fn()
    api.getLocalSemester.mockRejectedValue(new ApiError(404, '未缓存'))
    expect(await loadSemester(api, { term: '2025-2026-2', connected: false, fetchMissing: true, accept, isCurrent: () => true })).toBe(true)
    expect(api.getSemester).toHaveBeenCalledWith('2025-2026-2', undefined)
    expect(accept).toHaveBeenCalledTimes(1)
  })

  it('reuses complete local semesters and obtains missing terms serially without fetching individual weeks from school', async () => {
    const api = client(), accept = vi.fn()
    const forTerm = (term: string) => adaptSemester(semester({ term: { academic_term: term, label: term } }))
    api.getLocalSemester.mockImplementation(async (term) => {
      if (term === '2025-2026-2') return forTerm(term)
      throw new ApiError(404, '未缓存')
    })
    let fetching = false
    api.getSemester.mockImplementation(async (term) => {
      expect(fetching).toBe(false)
      fetching = true
      await Promise.resolve()
      fetching = false
      return forTerm(term!)
    })
    const terms = ['2025-2026-2', '2025-2026-1', '2024-2025-2', '2025-2026-1']
    expect(await cacheMissingSemesters(api, terms, { isCurrent: () => true, accept })).toEqual([])
    expect(api.getSemester.mock.calls.map(([term]) => term)).toEqual(['2025-2026-1', '2024-2025-2'])
    expect(accept.mock.calls.map(([schedule]) => schedule.term.id)).toEqual(['2025-2026-2', '2025-2026-1', '2024-2025-2'])
  })

  it('keeps successful history cache fills when one term fails and stops if school authentication expires', async () => {
    const api = client(), accept = vi.fn()
    api.getLocalSemester.mockRejectedValue(new ApiError(404, '未缓存'))
    api.getSemester.mockRejectedValueOnce(new ApiError(500, '暂时失败')).mockResolvedValueOnce(local())
    expect(await cacheMissingSemesters(api, ['2025-2026-2', '2025-2026-1'], { isCurrent: () => true, accept })).toEqual(['2025-2026-2'])
    expect(accept).toHaveBeenCalledTimes(1)
    api.getSemester.mockReset().mockRejectedValue(new ApiError(401, '需连接教务'))
    await expect(cacheMissingSemesters(api, ['2025-2026-2', '2025-2026-1'], { isCurrent: () => true, accept })).rejects.toMatchObject({ status: 401 })
    expect(api.getSemester).toHaveBeenCalledTimes(1)
  })

  it('discards an in-flight result and skips remaining terms after logout or a new selection', async () => {
    const api = client(), accept = vi.fn(), epoch = new OperationEpoch(), ticket = epoch.current()
    api.getLocalSemester.mockRejectedValue(new ApiError(404, '未缓存'))
    let resolve!: (value: Schedule) => void
    api.getSemester.mockImplementation(() => new Promise((done) => { resolve = done }))
    const pending = cacheMissingSemesters(api, ['2025-2026-2', '2025-2026-1'], { isCurrent: () => epoch.matches(ticket), accept })
    await vi.waitFor(() => expect(resolve).toBeDefined())
    epoch.next(); resolve(local())
    await pending
    expect(accept).not.toHaveBeenCalled()
    expect(api.getSemester).toHaveBeenCalledTimes(1)
  })
})
