import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api } from '../api'
import { ApiError } from '../api/http'
import type { JobStateDto, NetworkConfigDto, NetworkSummaryDto } from '../api/dto'
import type { AuthResult, Captcha, Course, NetStatus, Schedule, Settings, Workspace } from '../api/types'
import { applyTheme } from './themes'
import { OperationEpoch, saveSemesters, scheduleForTerm, saveWeek } from './schedule-state'
import { loadSemester } from './schedule-loading'
import { observeScheduleJobs, readScheduleCache } from './schedule-jobs'
import type { CourseBlock } from './course-blocks'
import { now, weekOf } from './time'

export type View = 'week' | 'today' | 'net' | 'settings'
export type Sheet = { type: 'course'; course: Course | CourseBlock } | { type: 'devices' } | { type: 'network' } | null
interface Store {
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
  updateSettings(p: Partial<Settings>): Promise<void>
  loadNetworkConfig(): Promise<void>
  loadNetworkSummary(): Promise<void>
  saveNetworkConfig(p: Record<string, string | number | boolean | string[]>): Promise<boolean>
  refreshJobs(): Promise<void>
}
const Ctx = createContext<Store | null>(null)
const message = (error: unknown) => error instanceof Error ? error.message : '操作失败，请重试'
const initialView = (): View => {
  const v = new URLSearchParams(location.search).get('v')
  return v === 'net' || v === 'settings' ? v : 'week'
}
export function StoreProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<Store['phase']>('loading')
  const [user, setUser] = useState<Workspace | null>(null)
  const [settings, setSettings] = useState<Settings | null>(null)
  const [semesters, setSemesters] = useState<Schedule[]>([])
  const [currentTerm, setCurrentTerm] = useState('')
  const [viewedTerm, setViewedTerm] = useState<string | null>(null)
  const [weeks, setWeeks] = useState<Schedule[]>([])
  const [selectedWeek, setSelectedWeek] = useState(1)
  const [scheduleError, setScheduleError] = useState('')
  const [authBusy, setAuthBusy] = useState(false)
  const [authOpen, setAuthOpen] = useState(false)
  const [authError, setAuthError] = useState('')
  const [captcha, setCaptcha] = useState<Captcha | null>(null)
  const [net, setNet] = useState<NetStatus | null>(null)
  const [netBusy, setNetBusy] = useState(false)
  const [netError, setNetError] = useState('')
  const [syncing, setSyncing] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const [networkConfig, setNetworkConfig] = useState<NetworkConfigDto | null>(null)
  const [networkSummary, setNetworkSummary] = useState<NetworkSummaryDto | null>(null)
  const [summaryBusy, setSummaryBusy] = useState(false)
  const [summaryError, setSummaryError] = useState('')
  const [jobs, setJobs] = useState<Store['jobs']>(null)
  const [jobsError, setJobsError] = useState('')
  const [jobGeneration, setJobGeneration] = useState(0)
  const jobObserver = useRef<ReturnType<typeof observeScheduleJobs> | null>(null)
  const appliedJobRun = useRef<string | null>(null)
  const [view, setView] = useState<View>(initialView)
  const [sheet, openSheet] = useState<Sheet>(null)
  const [toast, setToast] = useState<Store['toast']>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const epoch = useRef(new OperationEpoch())
  const selection = useRef(new OperationEpoch())
  const semesterSelection = useRef(new OperationEpoch())
  const workspaceAccount = useRef<string | null>(null)
  const boot = useRef<Promise<Workspace | null> | null>(null)
  const settingsQueue = useRef(Promise.resolve())
  const jobReadQueue = useRef(Promise.resolve())
  const pendingWeek = useRef<number | null>(null)
  const networkRequest = useRef<number | null>(null)
  const summaryRequest = useRef<{ epoch: number } | null>(null)
  const notify = useCallback((text: string, action?: { label: string; run(): void }, duration = 1800) => {
    setToast({ id: Date.now(), text, action }); clearTimeout(timer.current)
    timer.current = setTimeout(() => setToast(null), duration)
  }, [])
  useEffect(() => () => clearTimeout(timer.current), [])
  const activeTerm = currentTerm || user?.schedule?.term.id || ''
  const schedule = useMemo(() => scheduleForTerm(semesters, weeks, viewedTerm ?? activeTerm, selectedWeek), [semesters, weeks, viewedTerm, activeTerm, selectedWeek])
  const currentSchedule = useMemo(() => scheduleForTerm(semesters, weeks, activeTerm, user?.schedule?.viewedWeek || 1), [semesters, weeks, activeTerm, user])
  const semesterSchedule = useMemo(() => semesters.find((snapshot) => (snapshot.term.id || '') === (viewedTerm ?? activeTerm)) || null, [semesters, viewedTerm, activeTerm])
  const browsing = viewedTerm !== null && viewedTerm !== activeTerm
  const currentScheduleRef = useRef(currentSchedule)
  useEffect(() => { currentScheduleRef.current = currentSchedule }, [currentSchedule])
  const term = schedule?.term.id
  const connected = user?.connected ?? false
  const authenticated = !!user
  const clearWorkspace = useCallback(() => {
    epoch.current.next(); selection.current.next(); semesterSelection.current.next(); api.clearSession()
    workspaceAccount.current = null
    setUser(null); setSettings(null); setSemesters([]); setCurrentTerm(''); setViewedTerm(null); setWeeks([]); setNet(null)
    setNetworkConfig(null); setJobs(null); setJobsError(''); appliedJobRun.current = null; jobObserver.current?.stop(); setCaptcha(null); setAuthOpen(false); setAuthError(''); openSheet(null)
    setAuthBusy(false); setSyncing(false); setRefreshing(false); setNetBusy(false); setNetError(''); setScheduleError('')
    setNetworkSummary(null); setSummaryBusy(false); setSummaryError('')
    pendingWeek.current = null; clearTimeout(timer.current); setToast(null); setPhase('anonymous')
  }, [])
  const handleError = useCallback(async (error: unknown, presentation: 'toast' | 'inline' = 'toast') => {
    if (presentation === 'toast') notify(message(error))
    if (error instanceof ApiError && error.status === 401 && error.authenticated) {
      const ticket = epoch.current.current()
      try {
        const workspace = await api.session()
        if (epoch.current.matches(ticket)) setUser({ ...workspace, connected: false })
      } catch (sessionError) {
        if (epoch.current.matches(ticket) && sessionError instanceof ApiError && sessionError.status === 401) clearWorkspace()
      }
    }
  }, [clearWorkspace, notify])
  const acceptWorkspace = useCallback((workspace: Workspace) => {
    const ticket = epoch.current.next(); selection.current.next()
    setJobGeneration(ticket)
    const semesterTicket = semesterSelection.current.next()
    const sameAccount = workspaceAccount.current === workspace.student.student_id
    workspaceAccount.current = workspace.student.student_id
    setUser(workspace); setSettings(workspace.settings); applyTheme(workspace.settings.theme)
    setCurrentTerm((old) => workspace.schedule?.term.id || (sameAccount ? old : ''))
    setViewedTerm(null)
    setWeeks((old) => {
      const saved = sameAccount ? old : []
      return workspace.schedule && workspace.schedule.scope !== 'semester' ? saveWeek(saved, workspace.schedule) : saved
    })
    setSemesters((old) => {
      const saved = sameAccount ? old : []
      return workspace.schedule?.scope === 'semester' ? saveSemesters(saved, workspace.schedule) : saved
    })
    setSelectedWeek(workspace.schedule?.viewedWeek || workspace.schedule?.currentWeek || 1)
    setScheduleError(''); setCaptcha(null); setAuthOpen(false); setAuthError(''); setAuthBusy(false); setSyncing(false); setRefreshing(false); setNetBusy(false); setPhase('ready'); openSheet(null)
    setNet(null); setNetError(''); setNetworkConfig(null); setJobs(null); setJobsError(''); appliedJobRun.current = null; jobObserver.current?.stop()
    setNetworkSummary((old) => sameAccount ? old : null); setSummaryBusy(false); setSummaryError('')
    if (!api.preview) {
      // Week navigation does not cancel restoration of the same semester.
      const valid = () => epoch.current.matches(ticket) && semesterSelection.current.matches(semesterTicket)
      void loadSemester(api, {
        context: workspace.schedule, connected: workspace.connected, refresh: true, isCurrent: valid,
        accept: (result) => {
          setSemesters((old) => saveSemesters(old, result))
          setCurrentTerm((old) => old || result.term.id || '')
        },
        acceptWeek: (result) => setWeeks((old) => saveWeek(old, result)),
      }).catch(async (error) => {
        if (!valid()) return
        notify(message(error), undefined, 6000)
        if (error instanceof ApiError && error.status === 401) await handleError(error, 'inline')
      })
    }
  }, [handleError, notify])
  useEffect(() => {
    let active = true
    const ticket = epoch.current.current()
    boot.current ??= api.bootstrap()
    boot.current.then((workspace) => {
      if (!active || !epoch.current.matches(ticket)) return
      if (workspace) acceptWorkspace(workspace)
      else setPhase('anonymous')
    }).catch((error) => {
      if (!active || !epoch.current.matches(ticket)) return
      setAuthError(message(error)); setPhase('anonymous')
    })
    return () => { active = false }
  }, [acceptWorkspace])
  const authAction = useCallback(async (action: () => Promise<AuthResult>) => {
    const ticket = epoch.current.next()
    setAuthOpen(true); setAuthBusy(true); setAuthError(''); setCaptcha(null)
    try {
      const result = await action()
      if (!epoch.current.matches(ticket)) return
      if (result.kind === 'captcha') setCaptcha(result)
      else acceptWorkspace(result.workspace)
    } catch (error) {
      if (epoch.current.matches(ticket)) { setAuthError(message(error)); await handleError(error, 'inline') }
    } finally { if (epoch.current.matches(ticket)) setAuthBusy(false) }
  }, [acceptWorkspace, handleError])
  const login = useCallback((account: string, password: string) => authAction(() => api.login(account, password)), [authAction])
  const cancelAuth = useCallback(() => {
    setJobGeneration(epoch.current.next()); selection.current.next(); semesterSelection.current.next(); api.cancelAuth()
    setCaptcha(null); setAuthOpen(false); setAuthBusy(false); setSyncing(false); setRefreshing(false); setNetBusy(false); setAuthError('')
    setPhase((old) => old === 'loading' ? 'anonymous' : old)
    pendingWeek.current = null
  }, [])
  const openAuth = useCallback(() => {
    cancelAuth(); openSheet(null); setAuthOpen(true)
  }, [cancelAuth])
  const connect = useCallback(async () => {
    if (!authenticated) { openAuth(); return }
    await authAction(() => api.connect())
  }, [authenticated, openAuth, authAction])
  const logout = useCallback(async () => {
    const pending = api.logout()
    clearWorkspace()
    try { await pending } catch (error) { notify(message(error)) }
  }, [clearWorkspace, notify])
  const submitCaptcha = useCallback(async (code: string) => {
    if (!captcha || authBusy) return
    if (Date.now() >= captcha.expiresAt) { setAuthError('验证码已过期'); return }
    const ticket = epoch.current.current()
    setAuthBusy(true); setAuthError('')
    try {
      const result = await api.submitCaptcha(captcha, code)
      if (!epoch.current.matches(ticket)) return
      if ('kind' in result) {
        if (result.kind === 'authenticated') acceptWorkspace(result.workspace)
        else setCaptcha(result)
      } else {
        if (pendingWeek.current != null && result.viewedWeek !== pendingWeek.current) throw new Error('返回课表与所选周不一致')
        setWeeks((old) => saveWeek(old, result)); setCaptcha(null); setAuthOpen(false); notify('课表已更新')
      }
    } catch (error) {
      if (!epoch.current.matches(ticket)) return
      setAuthError(message(error))
      if (captcha.purpose !== 'schedule' || error instanceof ApiError && error.status === 410) setCaptcha(null)
      await handleError(error, 'inline')
    } finally { if (epoch.current.matches(ticket)) setAuthBusy(false) }
  }, [captcha, authBusy, acceptWorkspace, handleError, notify])
  const loadWeek = useCallback(async function loadWeek(week: number, force = false) {
    if (!authenticated) return
    if (api.preview) {
      setSelectedWeek(week)
      if (force) {
        setSyncing(true); setRefreshing(true)
        try { const result = await api.getSemester(term, schedule); setSemesters((old) => saveSemesters(old, result)); notify('课表已更新') }
        finally { setSyncing(false); setRefreshing(false) }
      }
      return
    }
    const authTicket = epoch.current.current()
    const ticket = selection.current.next()
    setSelectedWeek(week); setSyncing(true); setRefreshing(force); setScheduleError(''); openSheet(null)
    pendingWeek.current = week
    const valid = () => epoch.current.matches(authTicket) && selection.current.matches(ticket)
    try {
      let result: Schedule | Captcha
      if (force) result = await api.syncSchedule(week, term)
      else {
        try { result = await api.getSchedule(week, term) }
        catch (error) {
          if (!(error instanceof ApiError && error.status === 404)) throw error
          if (!valid() || !connected) return
          result = await api.syncSchedule(week, term)
        }
      }
      if (!valid()) return
      if ('kind' in result) { setCaptcha(result); setAuthOpen(true) }
      else {
        if (!api.preview && (result.viewedWeek !== week || term && result.term.id !== term)) throw new Error('教务返回的学期或周次对不上，课表没动')
        setWeeks((old) => saveWeek(old, result))
        if (force) notify(result.cached ? '已保留较新的本地课表' : '课表已更新')
      }
    } catch (error) {
      if (valid()) {
        notify(message(error), { label: '重试', run: () => void loadWeek(week, force) }, 6000)
        await handleError(error, 'inline')
      }
    } finally { if (valid()) { setSyncing(false); setRefreshing(false) } }
  }, [authenticated, term, connected, handleError, notify, schedule])
  const selectWeek = useCallback((week: number) => loadWeek(week), [loadWeek])
  const sync = useCallback(async () => {
    if (!authenticated) { setView('settings'); return }
    if (syncing || captcha) return
    if (schedule) await loadWeek(selectedWeek, true)
    else {
      const ticket = epoch.current.current()
      setSyncing(true); setRefreshing(true)
      try {
        const result = await api.syncSchedule()
        if (!epoch.current.matches(ticket)) return
        if ('kind' in result) { setCaptcha(result); setAuthOpen(true) }
        else { setWeeks([result]); setSelectedWeek(result.viewedWeek || 1) }
      } catch (error) { if (epoch.current.matches(ticket)) { setScheduleError(message(error)); await handleError(error) } }
      finally { if (epoch.current.matches(ticket)) { setSyncing(false); setRefreshing(false) } }
    }
  }, [authenticated, syncing, captcha, schedule, selectedWeek, loadWeek, handleError])
  const selectTerm = useCallback(async function selectTerm(id: string): Promise<void> {
    if (!authenticated || id === term) return
    const authTicket = epoch.current.current(); const ticket = selection.current.next()
    semesterSelection.current.next()
    setSyncing(true); setRefreshing(false); setScheduleError(''); openSheet(null); setCaptcha(null)
    const valid = () => epoch.current.matches(authTicket) && selection.current.matches(ticket)
    const isCurrentTerm = id === activeTerm
    const applySelection = (result: Schedule) => {
      setViewedTerm(isCurrentTerm ? null : id)
      const calendarWeek = isCurrentTerm && result.term.startDate ? weekOf(result.term, now()) : result.currentWeek
      setSelectedWeek(isCurrentTerm ? Math.max(1, Math.min(result.term.totalWeeks, calendarWeek || 1)) : 1)
    }
    try {
      const saved = scheduleForTerm(semesters, weeks, id, 1)
      if (saved) { applySelection(saved); return }
      const loaded = await loadSemester(api, {
        term: id, context: schedule, connected, isCurrent: valid,
        accept: (result) => { setSemesters((old) => saveSemesters(old, result)); applySelection(result) },
        acceptWeek: (result) => { setWeeks((old) => saveWeek(old, result)); applySelection(result) },
      })
      if (valid() && !loaded) notify('这个学期还没有保存课表', { label: '重试', run: () => void selectTerm(id) }, 6000)
    } catch (error) {
      if (valid()) { notify(message(error), { label: '重试', run: () => void selectTerm(id) }, 6000); await handleError(error, 'inline') }
    } finally { if (valid()) { setSyncing(false); setRefreshing(false) } }
  }, [authenticated, term, activeTerm, semesters, weeks, schedule, connected, handleError, notify])
  const requestNetwork = useCallback(async (action: () => Promise<NetStatus>, announce = true) => {
    if (!authenticated) return
    const ticket = epoch.current.current()
    if (networkRequest.current === ticket) return
    networkRequest.current = ticket; if (announce) { setNetBusy(true); setNetError('') }
    try {
      const result = await action()
      if (epoch.current.matches(ticket)) {
        setNet(result)
        if (announce) setNetError(result.error || (result.online ? '' : result.message || '未能连接校园网'))
        // A successful read-only probe must not erase a rejected authentication attempt.
      }
    }
    catch (error) { if (epoch.current.matches(ticket)) { setNetError(message(error)); await handleError(error, 'inline') } }
    finally {
      if (networkRequest.current === ticket) networkRequest.current = null
      if (epoch.current.matches(ticket)) setNetBusy(false)
    }
  }, [authenticated, handleError])
  const refreshNet = useCallback(() => requestNetwork(() => api.getNetStatus(), false), [requestNetwork])
  const toggleNet = useCallback(() => requestNetwork(() =>
    net?.online && net.disconnectSupported !== false ? api.netLogout() : api.netLogin()), [requestNetwork, net])
  const kick = useCallback(async (id: string) => {
    if (!authenticated) return
    const ticket = epoch.current.current()
    try { const result = await api.kickDevice(id); if (epoch.current.matches(ticket)) { setNet(result); notify('已让该设备下线') } }
    catch (error) { if (epoch.current.matches(ticket)) await handleError(error) }
  }, [authenticated, handleError, notify])
  const updateSettings = useCallback(async (patch: Partial<Settings>) => {
    if (!authenticated) return
    const ticket = epoch.current.current()
    const save = async () => {
      if (!epoch.current.matches(ticket)) return
      try {
        const result = await api.updateSettings(patch)
        if (epoch.current.matches(ticket)) { setSettings(result); applyTheme(result.theme) }
      } catch (error) {
        if (epoch.current.matches(ticket)) {
          await handleError(error)
          try { const actual = await api.getSettings(); if (epoch.current.matches(ticket)) { setSettings(actual); applyTheme(actual.theme) } } catch { /* retain previous visible values */ }
        }
      }
    }
    settingsQueue.current = settingsQueue.current.then(save)
    await settingsQueue.current
  }, [authenticated, handleError])
  const loadNetworkConfig = useCallback(async () => {
    if (!authenticated) return
    const ticket = epoch.current.current()
    try { const result = await api.getNetworkConfig(); if (epoch.current.matches(ticket)) setNetworkConfig(result) }
    catch (error) { if (epoch.current.matches(ticket)) await handleError(error) }
  }, [authenticated, handleError])
  const loadNetworkSummary = useCallback(async () => {
    if (!authenticated) return
    const ticket = epoch.current.current()
    if (summaryRequest.current?.epoch === ticket) return
    const request = { epoch: ticket }
    summaryRequest.current = request
    const valid = () => epoch.current.matches(ticket) && summaryRequest.current === request
    setSummaryBusy(true); setSummaryError('')
    try {
      const result = await api.getNetworkSummary()
      if (valid()) { setNetworkSummary(result); setSummaryError(result.message) }
    } catch (error) {
      if (valid()) { setSummaryError(message(error)); await handleError(error, 'inline') }
    } finally {
      if (summaryRequest.current === request) {
        summaryRequest.current = null
        if (epoch.current.matches(ticket)) setSummaryBusy(false)
      }
    }
  }, [authenticated, handleError])
  const saveNetworkConfig = useCallback(async (patch: Record<string, string | number | boolean | string[]>) => {
    if (!authenticated) return false
    const ticket = epoch.current.current()
    try {
      const result = await api.saveNetworkConfig(patch)
      if (!epoch.current.matches(ticket)) return false
      setNetworkConfig(result)
      const actual = await api.getSettings()
      if (!epoch.current.matches(ticket)) return false
      setSettings(actual)
      if (patch.campus_account !== undefined || patch.campus_password !== undefined) {
        summaryRequest.current = null
        setNetworkSummary(null); setSummaryError('')
        void loadNetworkSummary()
      }
      notify('校园网配置已保存'); return true
    } catch (error) { if (epoch.current.matches(ticket)) await handleError(error); return false }
  }, [authenticated, handleError, notify, loadNetworkSummary])
  const readInBackground = useCallback(<T,>(signal: AbortSignal, read: () => Promise<T>): Promise<T> => {
    // HttpClient owns its timeout. Drain in-flight reads before a replacement observer starts;
    // cancelled queued work never issues another request or revives a previous workspace.
    const pending = jobReadQueue.current.then(() => { signal.throwIfAborted(); return read() })
    jobReadQueue.current = pending.then(() => {}, () => {})
    return pending
  }, [])
  const refreshJobs = useCallback(async () => { await jobObserver.current?.refresh() }, [])
  useEffect(() => {
    if (api.preview || phase !== 'ready' || !authenticated || authOpen || authBusy || syncing) return
    const ticket = epoch.current.current(), selected = selection.current.current()
    const valid = () => epoch.current.matches(ticket) && selection.current.matches(selected)
    const observer = observeScheduleJobs({
      read: (signal) => readInBackground(signal, () => {
        if (!valid()) throw new DOMException('读取已取消', 'AbortError')
        return api.getJobs(signal)
      }),
      isCurrent: valid,
      appliedRun: () => appliedJobRun.current,
      onAppliedRun: (run) => { appliedJobRun.current = run },
      onJobs: (result) => { setJobs(result); setJobsError('') },
      onError: (error) => { setJobsError(message(error)); void handleError(error, 'inline') },
      reload: (signal) => readInBackground(signal, async () => {
        if (!valid() || !activeTerm) return false
        const result = await readScheduleCache(api, activeTerm, currentScheduleRef.current,
          viewedTerm === null || viewedTerm === activeTerm ? selectedWeek : undefined, signal)
        if (!valid() || signal.aborted) return false
        const semester = result.semester
        if (semester) setSemesters((old) => saveSemesters(old, semester))
        setWeeks((old) => result.weeks.reduce(saveWeek, old))
        return !!result.semester || result.weeks.length > 0
      }),
    })
    jobObserver.current = observer
    const visibilityChanged = () => observer.setVisible(!document.hidden)
    document.addEventListener('visibilitychange', visibilityChanged)
    visibilityChanged()
    return () => {
      observer.stop()
      document.removeEventListener('visibilitychange', visibilityChanged)
      if (jobObserver.current === observer) jobObserver.current = null
    }
  }, [phase, authenticated, jobGeneration, authOpen, authBusy, syncing, activeTerm, viewedTerm, selectedWeek, handleError, readInBackground])
  useEffect(() => {
    if (phase !== 'ready' || view !== 'net') return
    const refreshVisible = () => { if (!document.hidden) void refreshNet() }
    refreshVisible()
    const interval = setInterval(refreshVisible, 30_000)
    document.addEventListener('visibilitychange', refreshVisible)
    return () => { clearInterval(interval); document.removeEventListener('visibilitychange', refreshVisible) }
  }, [phase, view, refreshNet])
  useEffect(() => { if (phase === 'ready' && view === 'net') void loadNetworkSummary() }, [phase, view, loadNetworkSummary, user?.student.student_id, net?.online])
  const weekLoaded = api.preview || schedule?.scope === 'week'
  const value: Store = {
    preview: api.preview, phase, user, authOpen, authBusy, authError, captcha, schedule, semesterSchedule, currentSchedule, browsing, settings, net,
    netBusy, netError, syncing, refreshing, scheduleError, selectedWeek, weekLoaded, jobs, jobsError, networkConfig,
    networkSummary, summaryBusy, summaryError, loadNetworkSummary,
    view, sheet, toast, setView, openSheet, notify, login, connect, submitCaptcha, openAuth, cancelAuth, logout,
    sync, selectWeek, selectTerm, refreshNet, toggleNet, kick, updateSettings, loadNetworkConfig, saveNetworkConfig, refreshJobs,
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}
export function useStore() {
  const store = useContext(Ctx)
  if (!store) throw new Error('useStore outside provider')
  return store
}
