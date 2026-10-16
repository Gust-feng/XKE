import type { Api, Schedule } from '../api/types'
import type { JobStateDto } from '../api/dto'
import { ApiError } from '../api/http'

export type Jobs = Awaited<ReturnType<Api['getJobs']>>

export function scheduleJobNotice(job?: JobStateDto | null, error = '') {
  if (job?.enabled && job.requires_login) return { label: '需连接教务', detail: job.last_error || '重新连接教务后继续更新', requiresLogin: true }
  if (job?.enabled && job.last_error) return { label: '自动更新失败', detail: job.last_error, requiresLogin: false }
  if (error) return { label: '更新状态不可用', detail: error, requiresLogin: false }
  return null
}

/** Only local GETs: observing a completed background job must never request another school sync. */
export async function readScheduleCache(api: Pick<Api, 'getSchedule' | 'getLocalSemester'>, term: string,
  context: Schedule | null, selectedWeek: number | undefined, signal: AbortSignal) {
  const local = async (read: () => Promise<Schedule>) => {
    signal.throwIfAborted()
    try {
      const result = await read()
      signal.throwIfAborted()
      if (result.term.id !== term) throw new Error('本地课表学期不一致，已保留当前课表')
      return result
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  }
  const latest = await local(() => api.getSchedule(undefined, term, signal))
  const semester = await local(() => api.getLocalSemester(term, latest || context, signal))
  const selected = selectedWeek !== undefined && selectedWeek !== latest?.viewedWeek
    ? await local(() => api.getSchedule(selectedWeek, term, signal)) : null
  if (selected && selected.viewedWeek !== selectedWeek) throw new Error('本地课表周次不一致，已保留当前课表')
  return { semester, weeks: [latest, selected].filter((week): week is Schedule => week !== null) }
}

interface ObserverOptions {
  read(signal: AbortSignal): Promise<Jobs>
  reload(signal: AbortSignal): Promise<boolean>
  isCurrent(): boolean
  appliedRun(): string | null
  onAppliedRun(run: string): void
  onJobs(jobs: Jobs): void
  onError(error: unknown): void
}

/** One serial visible-only loop. A hidden/unmounted workspace cancels follow-up work and ignores late results. */
export function observeScheduleJobs(options: ObserverOptions) {
  let visible = false, stopped = false, resume = false
  let timer: ReturnType<typeof setTimeout> | undefined
  let controller: AbortController | null = null
  let pending: Promise<void> | null = null
  const clearTimer = () => { clearTimeout(timer); timer = undefined }
  const refresh = (): Promise<void> => {
    if (pending) return pending
    if (stopped || !visible || !options.isCurrent()) return Promise.resolve()
    clearTimer()
    const request = new AbortController()
    controller = request
    const valid = () => !stopped && visible && !request.signal.aborted && options.isCurrent()
    pending = (async () => {
      try {
        const jobs = await options.read(request.signal)
        if (!valid()) return
        options.onJobs(jobs)
        const job = jobs.schedule
        if (job.last_run_at && !job.last_error && !job.requires_login && options.appliedRun() !== job.last_run_at) {
          const loaded = await options.reload(request.signal)
          if (valid() && loaded) options.onAppliedRun(job.last_run_at)
        }
      } catch (error) { if (valid()) options.onError(error) }
    })().finally(() => {
      pending = null; controller = null
      if (stopped || !visible || !options.isCurrent()) return
      if (resume) { resume = false; void refresh() }
      else timer = setTimeout(() => { void refresh() }, 30_000)
    })
    return pending
  }
  return {
    refresh,
    setVisible(next: boolean) {
      if (stopped || next === visible) return
      visible = next; clearTimer()
      if (!next) { resume = false; controller?.abort() }
      else if (pending) resume = true
      else void refresh()
    },
    stop() { stopped = true; visible = false; clearTimer(); controller?.abort() },
  }
}
