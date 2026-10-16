import { demoData } from './demo-data'
import { now } from '../lib/time'
import type { Api, NetStatus, Schedule, Settings } from './types'
import type { NetworkConfigDto, NetworkSettingsDto } from './dto'

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))
let schedule: Schedule = structuredClone(demoData.schedule)
let net: NetStatus = structuredClone(demoData.net)
let autoUpdate = false
let networkConfig: NetworkConfigDto = structuredClone(demoData.networkConfig)
const SETTINGS_KEY = 'xke.design-v7.settings'

const loadSettings = (): Settings => {
  try {
    return { ...demoData.settings, ...JSON.parse(localStorage.getItem(SETTINGS_KEY) || '{}'), layout: 'one', autoUpdate }
  } catch {
    return { ...demoData.settings, autoUpdate }
  }
}

/** In-memory demo operations only. No HTTP requests or system changes. */
export const mockApi: Api = {
  preview: true,
  cancelAuth() {},
  clearSession() {},
  async bootstrap() {
    return { student: { student_id: demoData.settings.portalAccount, name: '演示学生', nickname: '', use_nickname: false,
      academy_name: '', major_name: '', class_name: '', training_level: '', campus_name: '', program_duration_years: null, enrollment_date: '2024-09-01' },
      connected: true, settings: loadSettings(), schedule: { ...schedule, scope: 'semester',
        currentWeek: Math.floor((now().getTime() - new Date(`${schedule.term.startDate}T00:00:00`).getTime()) / (7 * 864e5)) + 1 } }
  },
  async login() { return { kind: 'authenticated', workspace: (await this.bootstrap())! } },
  async connect() { return { kind: 'authenticated', workspace: (await this.bootstrap())! } },
  async session() { return (await this.bootstrap())! },
  async logout() {},
  async submitCaptcha() { throw new Error('预览不运行验证码协议') },
  async getLocalSemester() { return { ...schedule, scope: 'semester', cached: true } },
  async getSemester() { return { ...schedule, scope: 'semester' } },
  async getNetworkSummary() { return structuredClone(demoData.networkSummary) },
  async getNetworkConfig() { return structuredClone(networkConfig) },
  async saveNetworkConfig(patch) {
    const settingsPatch = Object.fromEntries(Object.entries(patch).filter(([key]) => key in networkConfig.settings)) as Partial<NetworkSettingsDto>
    const credentials = { ...networkConfig.credentials }
    for (const provider of ['campus', 'telecom', 'unicom', 'mobile']) {
      const stored = credentials[provider] || { account: '', password: '' }
      const account = typeof patch[`${provider}_account`] === 'string' ? String(patch[`${provider}_account`]).trim() : stored.account
      const changedPassword = typeof patch[`${provider}_password`] === 'string' && !!patch[`${provider}_password`]
      credentials[provider] = { account, password: changedPassword ? '********' : account === stored.account ? stored.password : '' }
    }
    networkConfig = { ...networkConfig, settings: { ...networkConfig.settings, ...settingsPatch }, credentials, campus_account: credentials.campus.account }
    await this.updateSettings({ netAutoLogin: networkConfig.settings.heartbeat_enabled, heartbeatMinutes: networkConfig.settings.heartbeat_interval_minutes })
    return structuredClone(networkConfig)
  },
  async getJobs() { throw new Error('预览没有后台任务') },
  async getSchedule() { await wait(120); return schedule },
  async syncSchedule() {
    await wait(1100)
    schedule = { ...schedule, syncedAt: now().toISOString() }
    return schedule
  },
  async getNetStatus() { await wait(80); return net },
  async netLogin() {
    await wait(1300)
    net = { ...net, online: true, ip: demoData.connectionIp, since: now().toISOString() }
    return net
  },
  async netLogout() { await wait(500); net = { ...net, online: false, ip: null, since: null }; return net },
  async kickDevice(id) {
    await wait(400)
    net = { ...net, devices: net.devices.filter((device) => device.id !== id) }
    return net
  },
  async getAppUpdate() {
    return { current_version: '1.0.0-preview', repository_url: 'https://github.com/Gust-feng/XKE',
      release_url: 'https://github.com/Gust-feng/XKE/releases', latest_version: null,
      status: 'unsupported' as const, progress: 0, error: '合成预览不检查、下载或安装更新', checked_at: null }
  },
  async checkAppUpdate() { return this.getAppUpdate() },
  async downloadAppUpdate() { return this.getAppUpdate() },
  async applyAppUpdate() { return this.getAppUpdate() },
  async getSettings() { return loadSettings() },
  async updateSettings(patch) {
    const settings = { ...loadSettings(), ...patch, layout: 'one' as const }
    const { autoUpdate: nextAutoUpdate, ...persisted } = settings
    autoUpdate = nextAutoUpdate
    if (Object.keys(patch).some((key) => key !== 'autoUpdate')) localStorage.setItem(SETTINGS_KEY, JSON.stringify(persisted))
    return settings
  },
}
