import { useEffect, useState } from 'react'
import { useDesktopBridge } from '../../lib/desktop'
import { Modal } from '../../components/Modal'
import { Row, SwToggle } from './SwissSettings'

/**
 * 开发者预览：桌面课前提醒卡的免打扰开关与外观预览。
 * 不直接出现在设置列表；连续点击系统分组版本号七次后解锁显示。
 */
export function DesktopNotificationSettings() {
  const { bridge } = useDesktopBridge()
  const [dnd, setDnd] = useState(false)
  const [supported, setSupported] = useState(false)
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  const [feedback, setFeedback] = useState('')
  useEffect(() => {
    let active = true
    void bridge?.get_notification_settings?.().then((result) => { if (active) { setDnd(result.dnd); setSupported(result.supported) } }).catch(() => {})
    return () => { active = false }
  }, [bridge])
  if (!supported) return null
  const change = async (value: boolean) => {
    if (busy) return
    setBusy(true); setFeedback('')
    try { const result = await bridge?.set_notification_settings?.(String(value)); if (result) setDnd(result.dnd) }
    catch (cause) { setFeedback(cause instanceof Error ? cause.message : '免打扰设置未保存') }
    finally { setBusy(false) }
  }
  const preview = async () => {
    if (busy) return
    setBusy(true); setFeedback('')
    try {
      const result = await bridge?.preview_notification?.()
      if (!result?.ok) setFeedback(result?.reason || (dnd ? '免打扰已开启' : '当前系统状态暂不显示提醒'))
    } catch (cause) { setFeedback(cause instanceof Error ? cause.message : '提醒预览失败') }
    finally { setBusy(false) }
  }
  return <>
    <Row i={9.6} label="桌面提醒" onClick={() => { setOpen(true); setFeedback('') }}>
      {dnd ? <span>免打扰</span> : <span aria-hidden="true">↗</span>}
    </Row>
    {open && <Modal label="桌面提醒" onClose={() => setOpen(false)} className="notification-modal">
      {/* 示意卡镜像真实 WPF 提醒卡：纸面 + 墨边 + 红方块小标 + 红色大数字 + 课程/教室行 */}
      <div className="notification-sample" aria-label="提醒外观示意">
        <span className="notification-sample-k"><i /> 课前提醒</span>
        <b>15<i> 分钟</i></b>
        <span className="notification-sample-course">数据结构</span>
        <span className="notification-sample-meta">S2-207(新)<b>查看课表</b></span>
      </div>
      <div className="notification-options"><span className="notification-options-l">免打扰</span><SwToggle on={dnd} onChange={(value) => { void change(value) }} label="提醒免打扰" disabled={busy} /></div>
      <div className="notification-actions"><button type="button" className="notification-preview" disabled={busy} onClick={() => { void preview() }}>预览提醒 ↗</button></div>
      {feedback && <p className="notification-feedback" role="status">{feedback}</p>}
    </Modal>}
  </>
}
