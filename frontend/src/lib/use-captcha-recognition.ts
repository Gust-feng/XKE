import { useEffect, useState } from 'react'
import { api } from '../api'
import type { CaptchaRecognitionDto } from '../api/dto'

/** Local status only; never causes a school request or an OCR attempt. */
export function useCaptchaRecognition(account: string | undefined, enabled: boolean, busy: boolean) {
  const [result, setResult] = useState<{ account: string; data: CaptchaRecognitionDto } | null>(null)
  useEffect(() => {
    if (!account) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined
    let pending = false
    const read = async () => {
      if (controller.signal.aborted || document.hidden || pending) return
      pending = true
      let active = false
      try {
        const data = await api.getCaptchaRecognition(controller.signal)
        if (!controller.signal.aborted) {
          setResult({ account, data })
          active = data.state === 'recognizing'
        }
      } catch {
        if (!controller.signal.aborted) setResult({ account, data: {
          supported: false, trusted: true, enabled, state: 'unavailable', attempt: 0, message: '识别状态暂不可用',
        } })
      } finally {
        pending = false
        if (!controller.signal.aborted && !document.hidden) timer = setTimeout(() => { void read() }, busy || active ? 500 : 2000)
      }
    }
    const visibility = () => { clearTimeout(timer); if (!document.hidden) void read() }
    document.addEventListener('visibilitychange', visibility)
    void read()
    return () => { controller.abort(); clearTimeout(timer); document.removeEventListener('visibilitychange', visibility) }
  }, [account, enabled, busy])
  return result && result.account === account ? result.data : null
}
