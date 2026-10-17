import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import type { AppUpdateDto } from '../../api/dto'
import type { UpdateAction, UpdateObservation } from '../../lib/app-updates'
import { projectLink, updateActionForStatus, updateInProgress, updateProgress } from '../../lib/app-updates'
import './app-updates.css'

const titles: Record<AppUpdateDto['status'], string> = {
  unsupported: '应用更新', idle: '应用更新', checking: '应用更新', available: '发现新版本',
  downloading: '正在下载', ready: '下载完成', installing: '正在重启', up_to_date: '应用更新', error: '应用更新',
}
const actionLabels: Record<UpdateAction, string> = { check: '检查更新', download: '下载更新', apply: '重启更新' }

export interface AppUpdatePanelProps {
  state: UpdateObservation
  autoUpdate: boolean
  desktopSupported: boolean
  preview: boolean
  noUpdate?: boolean
  onAction(action: UpdateAction): void
  onRefresh(): void
  onAutoChange(value: boolean): Promise<void>
  onClose(): void
}

/** Native modal supplies focus containment, Escape dismissal and focus restoration without another overlay framework. */
export function AppUpdatePanel(props: AppUpdatePanelProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const returnFocus = useRef<HTMLElement | null>(null)
  useEffect(() => {
    const node = dialog.current
    if (node && !node.open) {
      returnFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      node.showModal()
    }
    // Closing during StrictMode cleanup would dispatch a stale close event. DOM removal closes it instead.
    return () => { if (!node?.isConnected && returnFocus.current?.isConnected) returnFocus.current.focus() }
  }, [])
  const panel = <dialog ref={dialog} className="one-update-dialog" aria-labelledby="app-update-title"
    onClose={props.onClose} onCancel={(event) => { event.preventDefault(); props.onClose() }} onClick={(event) => { if (event.target === event.currentTarget) props.onClose() }}>
    <AppUpdateContent {...props} />
  </dialog>
  return typeof document === 'undefined' ? panel : createPortal(panel, document.body)
}

/** Kept separate from the modal lifecycle for deterministic state/SSR coverage. */
export function AppUpdateContent({ state, autoUpdate, desktopSupported, noUpdate, onAction, onRefresh, onAutoChange, onClose }: AppUpdatePanelProps) {
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState('')
  const { data, pending, action, error } = state
  const supported = !!data && data.status !== 'unsupported'
  const busy = pending || updateInProgress(data?.status)
  const primary = updateActionForStatus(data?.status)
  const project = projectLink(data?.repository_url)
  const release = projectLink(data?.release_url)
  const progress = updateProgress(data?.progress ?? 0)
  const title = action ? { check: '应用更新', download: '正在下载', apply: '正在重启' }[action]
    : data ? titles[data.status] : '应用更新'
  const hasUpdate = !!data?.latest_version && !!data && ['available', 'downloading', 'ready', 'installing'].includes(data.status)
  const checkedEmpty = noUpdate && data?.status === 'up_to_date' && !busy
  const checking = action === 'check' || data?.status === 'checking'
  const feedback = saveError || error || (data?.status === 'error' ? data.error : '')
  const changeAutoUpdate = async (value: boolean) => {
    if (saving) return
    setSaving(true); setSaveError('')
    try { await onAutoChange(value) }
    catch (cause) { setSaveError(cause instanceof Error ? cause.message : '自动更新设置未保存') }
    finally { setSaving(false) }
  }
  return <section className="one-update-panel" data-update={hasUpdate || undefined}>
    <header className="one-update-head">
      <h2 id="app-update-title" aria-live="polite" aria-atomic="true">{title}</h2>
      <button type="button" className="one-update-close" onClick={onClose} aria-label="关闭应用更新" autoFocus>
        <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><path d="m5 5 10 10M15 5 5 15" /></svg>
      </button>
    </header>
    <div className="one-update-version">
      <b className="num">{hasUpdate ? data.latest_version : data?.current_version || '—'}</b>
      <span>{hasUpdate ? `当前版本 ${data.current_version}` : '当前版本'}</span>
    </div>
    <label className="one-update-auto">
      <span><b>自动更新</b></span>
      <input type="checkbox" role="switch" aria-label="自动更新应用" checked={autoUpdate} disabled={!supported || !desktopSupported || saving || data?.status === 'installing'} onChange={(event) => { void changeAutoUpdate(event.target.checked) }} />
    </label>
    <footer className="one-update-actions">
      <div className="one-update-links">
        {project && <a href={project} target="_blank" rel="noopener noreferrer">项目主页</a>}
        {release && <a href={release} target="_blank" rel="noopener noreferrer">发布记录</a>}
      </div>
      <div className="one-update-buttons">
        <button type="button" className="one-update-primary" data-result={checkedEmpty ? 'no-update' : undefined} aria-busy={data?.status === 'downloading' || undefined} disabled={busy || (!!data && !supported)} onClick={() => { if (primary) onAction(primary); else if (!data) onRefresh() }}>
          {data?.status === 'downloading'
            ? <span className="one-update-progress" role="progressbar" aria-label="更新下载进度" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress}>
              <svg className="one-update-download-icon" viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" aria-hidden="true">
                <path className="one-update-download-arrow" d="M8 2v8M4.5 7 8 10.5 11.5 7" /><path d="M3 13h10" />
              </svg>
              <span className="num">{progress}%</span>
            </span>
            : checkedEmpty ? <span className="one-update-checked" role="status">
              暂无更新
            </span> : checking ? '检查中' : primary ? actionLabels[primary] : data?.status === 'unsupported' ? '安装不可用' : busy ? title : '重试读取'}
        </button>
      </div>
    </footer>
    {feedback && <div className="one-update-notice" role="status">{feedback}</div>}
  </section>
}
