import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../api'
import { emptyUpdateObservation, observeAppUpdates, type UpdateAction } from './app-updates'

/** Only an open, visible panel polls. One initial local read supplies the compact version entry. */
export function useAppUpdates() {
  const [open, setOpen] = useState(false)
  const [state, setState] = useState(emptyUpdateObservation)
  const [noUpdate, setNoUpdate] = useState(false)
  const resultTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const clearResult = useCallback(() => {
    clearTimeout(resultTimer.current)
    setNoUpdate(false)
  }, [])
  const observer = useRef<ReturnType<typeof observeAppUpdates> | null>(null)
  const opened = useRef(open)
  useEffect(() => {
    const current = observeAppUpdates({ api, onChange: setState, onNoUpdate: () => {
      clearTimeout(resultTimer.current)
      setNoUpdate(true)
      resultTimer.current = setTimeout(() => setNoUpdate(false), 2000)
    } })
    observer.current = current
    const visibility = () => {
      const visible = opened.current && document.visibilityState === 'visible'
      if (!visible) clearResult()
      current.setVisible(visible)
    }
    document.addEventListener('visibilitychange', visibility)
    if (document.visibilityState === 'visible') void current.refresh()
    visibility()
    return () => {
      document.removeEventListener('visibilitychange', visibility)
      clearTimeout(resultTimer.current)
      current.stop()
      if (observer.current === current) observer.current = null
    }
  }, [clearResult])
  const openPanel = useCallback(() => {
    opened.current = true
    clearResult(); setOpen(true)
    observer.current?.setVisible(document.visibilityState === 'visible')
  }, [clearResult])
  const closePanel = useCallback(() => {
    opened.current = false
    observer.current?.setVisible(false)
    clearResult(); setOpen(false)
  }, [clearResult])
  return { ...state, open, openPanel, closePanel, noUpdate, run: (action: UpdateAction) => { clearResult(); return observer.current?.run(action) }, refresh: () => observer.current?.refresh() }
}
