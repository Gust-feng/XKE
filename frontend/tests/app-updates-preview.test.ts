import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
vi.mock('../src/lib/time', () => ({ now: () => new Date('2026-10-17T04:00:00Z') }))
import { mockApi } from '../src/api/mock'

beforeEach(() => {
  const values = new Map<string, string>()
  vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null, setItem: vi.fn((key: string, value: string) => values.set(key, value)) })
  vi.stubGlobal('fetch', vi.fn(() => { throw new Error('preview must not use HTTP') }))
})
afterEach(() => vi.unstubAllGlobals())

describe('explicit synthetic update preview', () => {
  it('labels every action unsupported and never contacts GitHub, the local host, or school APIs', async () => {
    for (const result of [await mockApi.getAppUpdate(), await mockApi.checkAppUpdate(), await mockApi.downloadAppUpdate(), await mockApi.applyAppUpdate()]) {
      expect(result.status).toBe('unsupported')
      expect(result.current_version).toContain('preview')
      expect(result.error).toContain('合成预览')
    }
    expect(fetch).not.toHaveBeenCalled()
  })
  it('holds the preview preference in memory, never in browser storage', async () => {
    expect((await mockApi.updateSettings({ autoUpdate: true })).autoUpdate).toBe(true)
    expect((await mockApi.getSettings()).autoUpdate).toBe(true)
    expect(localStorage.setItem).not.toHaveBeenCalled()
    await mockApi.updateSettings({ showWeekend: false })
    for (const [, value] of vi.mocked(localStorage.setItem).mock.calls) expect(JSON.parse(value)).not.toHaveProperty('autoUpdate')
    expect((await mockApi.updateSettings({ autoUpdate: false })).autoUpdate).toBe(false)
    expect(fetch).not.toHaveBeenCalled()
  })
})
