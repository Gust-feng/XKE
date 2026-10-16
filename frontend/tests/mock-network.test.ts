import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
vi.mock('../src/lib/time', () => ({ now: () => new Date('2026-10-15T04:00:00Z') }))
import { mockApi } from '../src/api/mock'

beforeEach(() => {
  const values = new Map<string, string>()
  vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null, setItem: vi.fn((key: string, value: string) => values.set(key, value)) })
})
afterEach(() => vi.unstubAllGlobals())

describe('explicit preview network configuration', () => {
  it.each(['campus', 'telecom', 'unicom', 'mobile'] as const)('saves %s account with only a masked password marker', async (provider) => {
    const result = await mockApi.saveNetworkConfig({ provider, [`${provider}_account`]: 'preview-account', [`${provider}_password`]: 'never-store-this-password', heartbeat_interval_minutes: 9 })
    expect(result.credentials[provider].account).toBe('preview-account')
    expect(result.credentials[provider].password).toBe('********')
    expect(JSON.stringify(result)).not.toContain('never-store-this-password')
    for (const call of vi.mocked(localStorage.setItem).mock.calls) expect(call[1]).not.toContain('never-store-this-password')
    expect((await mockApi.getSettings()).heartbeatMinutes).toBe(9)
  })
  it('does not reuse a saved password marker after changing the account', async () => {
    await mockApi.saveNetworkConfig({ campus_account: 'before', campus_password: 'before-secret' })
    const changed = await mockApi.saveNetworkConfig({ campus_account: 'after' })
    expect(changed.credentials.campus).toEqual({ account: 'after', password: '' })
  })
})
