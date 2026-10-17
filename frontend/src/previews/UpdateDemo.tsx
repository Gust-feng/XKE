import { useEffect, useRef, useState } from 'react'
import App from '../App'
import { AppUpdateContent } from '../editions/one/AppUpdatePanel'
import type { AppUpdateDto } from '../api/dto'
import type { UpdateAction, UpdateObservation } from '../lib/app-updates'

type Scene = 'idle' | 'available' | 'downloading' | 'ready'
const scenes: { id: Scene; label: string }[] = [
  { id: 'idle', label: '当前版本' }, { id: 'available', label: '发现更新' }, { id: 'downloading', label: '下载中' }, { id: 'ready', label: '下载完成' },
]
const initialScene = (): Scene => {
  const scene = new URLSearchParams(location.search).get('scene')
  return scene === 'idle' || scene === 'downloading' || scene === 'ready' ? scene : 'available'
}
const stateFor = (scene: Scene): AppUpdateDto => ({
  current_version: '1.0.0', latest_version: scene === 'idle' ? null : '1.0.1', status: scene, progress: scene === 'downloading' ? 42 : scene === 'ready' ? 100 : 0,
  repository_url: 'https://github.com/Gust-feng/XKE', release_url: 'https://github.com/Gust-feng/XKE/releases',
  error: '', checked_at: null,
})

export function UpdateDemo() {
  const [data, setData] = useState(() => stateFor(initialScene()))
  const [noUpdate, setNoUpdate] = useState(false)
  useEffect(() => {
    if (data.status !== 'checking') return
    const timer = setTimeout(() => {
      setData({ ...stateFor('idle'), status: 'up_to_date' })
      setNoUpdate(true)
    }, 600)
    return () => clearTimeout(timer)
  }, [data.status])
  useEffect(() => {
    if (!noUpdate) return
    const timer = setTimeout(() => setNoUpdate(false), 2000)
    return () => clearTimeout(timer)
  }, [noUpdate])
  const [autoUpdate, setAutoUpdate] = useState(true)
  const [open, setOpen] = useState(true)
  const [playing, setPlaying] = useState(false)
  const dialog = useRef<HTMLDialogElement>(null)
  const state: UpdateObservation = { data, pending: false, action: null, error: '' }
  useEffect(() => {
    const node = dialog.current
    if (open && node && !node.open) node.showModal()
  }, [open])
  useEffect(() => {
    if (!playing) return
    const timer = setInterval(() => setData((previous) => previous.status !== 'downloading' ? previous
      : previous.progress >= 95 ? { ...previous, status: 'ready', progress: 100 }
        : { ...previous, progress: previous.progress + 5 }), 220)
    return () => clearInterval(timer)
  }, [playing])
  const chooseScene = (scene: Scene) => { setPlaying(false); setNoUpdate(false); setData(stateFor(scene)) }
  const act = (action: UpdateAction) => {
    if (action === 'check') { if (!data.latest_version) { setNoUpdate(false); setData({ ...stateFor('idle'), status: 'checking' }) } else chooseScene('available') }
    if (action === 'download') { setData({ ...stateFor('downloading'), progress: 0 }); setPlaying(true) }
    if (action === 'apply') { setPlaying(false); setData({ ...stateFor('available'), current_version: '1.0.1', latest_version: null, status: 'up_to_date' }) }
  }
  return <>
    <App />
    {!open && <button className="update-demo-reopen" onClick={() => { chooseScene('available'); setOpen(true) }}>查看更新演示</button>}
    {open && <dialog className="one-update-dialog update-demo-dialog" ref={dialog} aria-label="应用更新交互演示"
      onCancel={(event) => { event.preventDefault(); setOpen(false); setPlaying(false) }}>
      <AppUpdateContent state={state} noUpdate={noUpdate} autoUpdate={autoUpdate} desktopSupported preview
        onAction={act} onRefresh={() => chooseScene('available')} onAutoChange={async (value) => setAutoUpdate(value)}
        onClose={() => { setPlaying(false); setOpen(false) }} />
      <nav className="update-demo-scenes" aria-label="切换演示状态">
      {scenes.map((scene) => <button type="button" key={scene.id} aria-pressed={data.status === scene.id}
        onClick={() => { chooseScene(scene.id); setOpen(true) }}>{scene.label}</button>)}
      </nav>
    </dialog>}
  </>
}
