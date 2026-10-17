import { useEffect, useState } from 'react'

export interface DesktopBridge {
  show_window(): Promise<unknown>; minimize_window(): Promise<unknown>
  toggle_maximize_window(): Promise<unknown>; close_window(): Promise<unknown>
  get_window_state(): Promise<{ maximized?: boolean }>
}
declare global { interface Window { pywebview?: { api?: DesktopBridge } } }

export function useDesktopBridge() {
  const [bridge, setBridge] = useState(() => window.pywebview?.api)
  const [maximized, setMaximized] = useState(false)
  useEffect(() => {
    const ready = () => setBridge(window.pywebview?.api)
    const state = (event: Event) => setMaximized(!!(event as CustomEvent<{ maximized?: boolean }>).detail.maximized)
    addEventListener('pywebviewready', ready); addEventListener('xke-window-state', state)
    return () => { removeEventListener('pywebviewready', ready); removeEventListener('xke-window-state', state) }
  }, [])
  useEffect(() => { bridge?.get_window_state().then((s) => setMaximized(!!s.maximized)).catch(() => {}) }, [bridge])
  return { bridge, maximized }
}
