import { createContext, useContext } from 'react'
import type { JobStateDto, NetworkConfigDto, NetworkSummaryDto } from '../api/dto'
import type { Captcha, Course, NetStatus, Schedule, Settings, Workspace } from '../api/types'
import type { CourseBlock } from './course-blocks'

export type View = 'week' | 'today' | 'net' | 'settings'
export type Sheet = { type: 'course'; course: Course | CourseBlock } | { type: 'devices' } | { type: 'network' } | null
export interface Store {
  preview: boolean; phase: 'loading' | 'anonymous' | 'ready'
  user: Workspace | null; authOpen: boolean; authBusy: boolean; authError: string; captcha: Captcha | null
  schedule: Schedule | null; semesterSchedule: Schedule | null; currentSchedule: Schedule | null; browsing: boolean; settings: Settings | null; net: NetStatus | null
  netBusy: boolean; netError: string; syncing: boolean; refreshing: boolean; scheduleError: string; selectedWeek: number
  weekLoaded: boolean; jobs: { network: JobStateDto; schedule: JobStateDto } | null
  jobsError: string
  networkConfig: NetworkConfigDto | null
  networkSummary: NetworkSummaryDto | null; summaryBusy: boolean; summaryError: string
  view: View; sheet: Sheet; toast: { id: number; text: string; action?: { label: string; run(): void } } | null
  setView(v: View): void; openSheet(s: Sheet): void; notify(text: string, action?: { label: string; run(): void }, duration?: number): void
  login(user: string, password: string): Promise<void>; connect(): Promise<void>
  submitCaptcha(code: string): Promise<void>; openAuth(): void; cancelAuth(): void; logout(): Promise<void>
  sync(): Promise<void>; selectWeek(week: number): Promise<void>; selectTerm(term: string): Promise<void>
  refreshNet(): Promise<void>; toggleNet(): Promise<void>; kick(id: string): Promise<void>
  updateSettings(p: Partial<Settings>, presentation?: 'toast' | 'inline'): Promise<void>
  updateNickname(nickname: string): Promise<void>
  loadNetworkConfig(): Promise<void>
  loadNetworkSummary(): Promise<void>
  saveNetworkConfig(p: Record<string, string | number | boolean | string[]>): Promise<boolean>
  refreshJobs(): Promise<void>
}
export const StoreContext = createContext<Store | null>(null)
export function useStore() {
  const store = useContext(StoreContext)
  if (!store) throw new Error('useStore outside provider')
  return store
}
