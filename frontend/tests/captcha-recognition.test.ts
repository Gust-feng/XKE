import { describe, expect, it, vi } from 'vitest'
import { adaptSettings, settingsPatches } from '../src/api/adapters'
import { RuntimeApi } from '../src/api/runtime'
import { HttpClient } from '../src/api/http'
import { account, auth, captcha, response } from './fixtures'

describe('account CAPTCHA preference and status', () => {
  it('defaults on after authentication and preserves an explicit off', () => {
    expect(adaptSettings(account(), 'synthetic').autoCaptchaEnabled).toBe(true)
    const dto = account(); dto.auth = { prelogin_mode: 'protocol', auto_captcha_enabled: false }
    expect(adaptSettings(dto, 'synthetic').autoCaptchaEnabled).toBe(false)
    expect(settingsPatches({ autoCaptchaEnabled: false })).toEqual({ account: { auth: { auto_captcha_enabled: false } }, desktop: {} })
  })
  it('reads status using only the authenticated local GET', async () => {
    const status = { supported: true, trusted: true, enabled: true, state: 'recognizing', attempt: 2, message: '' }
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response(status))
    const http = new HttpClient('', fetcher); http.setSession('synthetic-session')
    expect(await new RuntimeApi(http).getCaptchaRecognition()).toEqual(status)
    expect(fetcher).toHaveBeenCalledOnce()
    expect(fetcher.mock.calls[0]).toMatchObject(['/api/auth/captcha-recognition', { method: 'GET', headers: { 'X-XKE-Session': 'synthetic-session' } }])
  })
  it('actually aborts the transport when the user cancels recognition', async () => {
    let signal: AbortSignal | undefined
    const fetcher = vi.fn<typeof fetch>((_url, init) => {
      signal = init?.signal as AbortSignal
      return new Promise((_resolve, reject) => { signal!.addEventListener('abort', () => reject(signal!.reason), { once: true }) })
    })
    const api = new RuntimeApi(new HttpClient('', fetcher))
    const operation = api.login('synthetic', 'synthetic-only')
    api.cancelAuth()
    await expect(operation).rejects.toThrow('登录已取消')
    expect(signal?.aborted).toBe(true)
  })
  it('uses explicit school reauthentication when an existing account changes its password', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response(captcha))
    const api = new RuntimeApi(new HttpClient('', fetcher))
    await api.login('synthetic', 'old-password')
    await api.login('synthetic', 'new-password')
    expect(fetcher.mock.calls[1][0]).toBe('/api/auth/reinitialize')
  })
  it('keeps a renewed manual challenge instead of parsing it as a timetable', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(response(auth())).mockResolvedValueOnce(response({ ...captcha, message: '验证码错误，请手动输入' }))
    const api = new RuntimeApi(new HttpClient('', fetcher)); await api.login('synthetic', 'synthetic-only')
    const result = await api.submitCaptcha({ kind: 'captcha', purpose: 'schedule', id: 'old', image: '', expiresAt: Date.now() + 60_000 }, 'wrong')
    expect(result).toMatchObject({ kind: 'captcha', purpose: 'schedule', message: '验证码错误，请手动输入' })
  })
})
