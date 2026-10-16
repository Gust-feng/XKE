import type { Api } from '../api/types'
import type { AppUpdateDto } from '../api/dto'

export type UpdateAction = 'check' | 'download' | 'apply'
export interface UpdateObservation {
  data: AppUpdateDto | null
  pending: boolean
  action: UpdateAction | null
  error: string
}
export const emptyUpdateObservation: UpdateObservation = { data: null, pending: false, action: null, error: '' }

export const updateInProgress = (status?: AppUpdateDto['status']) =>
  status === 'checking' || status === 'downloading' || status === 'installing'

/** A server-selected state, never a frontend version comparison or installer invocation. */
export function updateActionForStatus(status?: AppUpdateDto['status']): UpdateAction | null {
  if (!status || status === 'unsupported' || updateInProgress(status)) return null
  if (status === 'available') return 'download'
  if (status === 'ready') return 'apply'
  return 'check'
}

export function updateProgress(progress: number) {
  return Number.isFinite(progress) ? Math.min(100, Math.max(0, Math.round(progress))) : 0
}

/** Links remain ordinary external anchors; only the declared public project is eligible. */
export function projectLink(value?: string) {
  if (!value) return null
  try {
    const url = new URL(value)
    if (url.protocol !== 'https:' || url.hostname !== 'github.com' || url.port || url.username || url.password) return null
    if (!/^\/Gust-feng\/XKE(?:\/|$)/i.test(url.pathname)) return null
    return url.href
  } catch { return null }
}

interface UpdateObserverOptions {
  api: Pick<Api, 'getAppUpdate' | 'checkAppUpdate' | 'downloadAppUpdate' | 'applyAppUpdate'>
  onChange(state: UpdateObservation): void
  onNoUpdate?(): void
}

/** One serial local-HTTP loop. Closing/hiding cancels old generations, not the native background download.
 * Already-issued HTTP requests drain before a new read/action; commands are never retried automatically.
 */
export function observeAppUpdates({ api, onChange, onNoUpdate }: UpdateObserverOptions) {
  let state = { ...emptyUpdateObservation }
  let visible = false, stopped = false, resume = false
  let timer: ReturnType<typeof setTimeout> | undefined
  let controller: AbortController | null = null
  let pending: Promise<void> | null = null
  const clearTimer = () => { clearTimeout(timer); timer = undefined }
  const publish = (patch: Partial<UpdateObservation>) => {
    state = { ...state, ...patch }
    if (!stopped) onChange(state)
  }
  const request = (action: UpdateAction | null = null): Promise<void> => {
    if (pending) return pending
    if (stopped || (action && (!visible || updateInProgress(state.data?.status) || state.data?.status === 'unsupported'))) return Promise.resolve()
    if (action === 'download' && state.data?.status !== 'available') return Promise.resolve()
    if (action === 'apply' && state.data?.status !== 'ready') return Promise.resolve()
    clearTimer()
    const current = new AbortController()
    controller = current
    const valid = () => !stopped && !current.signal.aborted
    publish({ pending: true, action, ...(action ? { error: '' } : {}) })
    pending = (async () => {
      try {
        const data = await (action === 'check' ? api.checkAppUpdate(current.signal)
          : action === 'download' ? api.downloadAppUpdate(current.signal)
          : action === 'apply' ? api.applyAppUpdate(current.signal) : api.getAppUpdate(current.signal))
        if (valid()) {
          publish({ data, error: '' })
          if (action === 'check' && data.status === 'up_to_date') onNoUpdate?.()
        }
      } catch (error) {
        if (valid()) publish({ error: error instanceof Error ? error.message : '暂时无法读取更新状态' })
      }
    })().finally(() => {
      pending = null; controller = null
      publish({ pending: false, action: null })
      if (stopped || !visible) return
      if (resume) { resume = false; void request() }
      else timer = setTimeout(() => { void request() }, state.data?.status === 'downloading' ? 1_000 : 10_000)
    })
    return pending
  }
  return {
    refresh: () => request(),
    run: (action: UpdateAction) => request(action),
    setVisible(next: boolean) {
      if (stopped || next === visible) return
      visible = next; clearTimer()
      if (!next) { resume = false; controller?.abort() }
      else if (pending) resume = true
      else void request()
    },
    stop() { stopped = true; visible = false; resume = false; clearTimer(); controller?.abort() },
  }
}
