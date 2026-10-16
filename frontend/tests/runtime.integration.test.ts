import { expect, it } from 'vitest'
import { HttpClient } from '../src/api/http'
import { RuntimeApi } from '../src/api/runtime'
import { combineSchedule } from '../src/lib/schedule-state'

/** Opt-in: point only at a freshly started synthetic Xke.FrontendIntegrationHost. */
it.skipIf(!process.env.XKE_INTEGRATION_URL)('real isolated WebHost HTTP flow', async () => {
  const base = process.env.XKE_INTEGRATION_URL!
  const diagnostics = await fetch(`${base}/api/config`).then((r) => r.json())
  expect(diagnostics.development.channel).toBe('isolated-frontend-integration')
  const api = new RuntimeApi(new HttpClient(base))
  // The host can already have local state if another isolated verification used it.
  await api.bootstrap()
  const login = await api.login('20260001', 'test-password')
  const result = login.kind === 'captcha' ? await api.submitCaptcha(login, '1234') : login
  if (!('kind' in result) || result.kind !== 'authenticated') throw new Error('Expected authenticated workspace')
  const workspace = result.workspace
  expect(workspace.student.student_id).toBe('20260001')
  expect(workspace.schedule?.courses.some((c) => c.periods === '2、3、4')).toBe(true)
  expect(workspace.schedule?.courses.some((c) => c.timed === false && c.name)).toBe(true)
  const semester = await api.getSemester(undefined, workspace.schedule)
  expect(semester.availableTerms?.length).toBeGreaterThan(1)
  expect(semester.courses.some((c) => c.weeks.length)).toBe(true)
  const localSemester = await api.getLocalSemester(semester.term.id, workspace.schedule)
  expect(localSemester).toMatchObject({ cached: true, syncedAt: semester.syncedAt, courses: semester.courses })
  const refresh = await api.syncSchedule(8, semester.term.id)
  if (!('kind' in refresh)) throw new Error('Use a fresh fixture host: first refresh should require captcha')
  expect(refresh.purpose).toBe('schedule')
  await expect(api.submitCaptcha(refresh, 'wrong')).rejects.toMatchObject({ status: 400 })
  const refreshed = await api.submitCaptcha(refresh, '1234')
  if ('kind' in refreshed) throw new Error('Expected a week after continuation')
  expect(refreshed.viewedWeek).toBe(8)
  const cachedBefore = combineSchedule(semester, [refreshed], 8)
  await expect(api.syncSchedule(8, semester.term.id)).rejects.toMatchObject({ status: 503 })
  expect(combineSchedule(semester, [refreshed], 8)).toEqual(cachedBefore)
  const actual = await api.getSchedule(8, semester.term.id)
  expect(actual.cached).toBe(true)
  expect(actual.viewedWeek).toBe(8)
  await api.updateSettings({ showWeekend: false })
  expect((await api.getSettings()).showWeekend).toBe(false)
  await api.updateSettings({ launchAtLogin: true })
  expect((await api.session()).settings.launchAtLogin).toBe(true)
  const config = await api.getNetworkConfig()
  await api.saveNetworkConfig({ heartbeat_interval_minutes: config.settings.heartbeat_interval_minutes + 1 })
  expect((await api.getNetStatus()).online).toBe(false)
  expect((await api.netLogin()).online).toBe(false)
  const historical = await api.getSemester('2025-2026-2', actual)
  expect(historical.term.startDate).toBe('')
  expect(historical.currentWeek).toBeNull()
  await api.logout()
  await expect(api.session()).rejects.toMatchObject({ status: 401 })
  const restored = await api.login('20260001', 'test-password')
  if (restored.kind !== 'authenticated') throw new Error('Expected offline workspace restoration')
  expect(restored.workspace.connected).toBe(false)
  const offlineSemester = await api.getLocalSemester(semester.term.id, restored.workspace.schedule)
  expect(offlineSemester).toMatchObject({ cached: true, courses: semester.courses, syncedAt: semester.syncedAt })
  await api.logout()
}, 60_000)
