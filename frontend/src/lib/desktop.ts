import { useEffect, useRef, useState } from 'react'

export interface DesktopBridge {
  show_window(): Promise<unknown>; minimize_window(): Promise<unknown>
  toggle_maximize_window(): Promise<unknown>; close_window(): Promise<unknown>
  get_window_state(): Promise<{ maximized?: boolean }>
  content_ready?(): Promise<unknown>
}
declare global { interface Window { pywebview?: { api?: DesktopBridge } } }

export function useDesktopBridge(contentReady = false) {
  const reported = useRef(false)
  const [bridge, setBridge] = useState(() => window.pywebview?.api)
  const [maximized, setMaximized] = useState(false)
  useEffect(() => {
    const ready = () => setBridge(window.pywebview?.api)
    const state = (event: Event) => setMaximized(!!(event as CustomEvent<{ maximized?: boolean }>).detail.maximized)
    addEventListener('pywebviewready', ready); addEventListener('xke-window-state', state)
    return () => { removeEventListener('pywebviewready', ready); removeEventListener('xke-window-state', state) }
  }, [])
  useEffect(() => { bridge?.get_window_state().then((s) => setMaximized(!!s.maximized)).catch(() => {}) }, [bridge])
  useEffect(() => {
    if (!contentReady || !bridge?.content_ready || reported.current) return
    reported.current = true
    // The native timeout owns the retry/error surface if this acknowledgement fails.
    void bridge.content_ready().catch(() => {})
  }, [bridge, contentReady])
  return { bridge, maximized }
}
