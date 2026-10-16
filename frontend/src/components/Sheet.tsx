import { AnimatePresence, motion } from 'motion/react'
import { useEffect } from 'react'
import { useStore } from '../lib/store'
import { sinceLabel, useNow } from '../lib/time'

function DevicesBody() {
  const { net, kick, settings, updateSettings } = useStore()
  const t = useNow(30_000)
  if (!net || !settings) return null
  if (net.devicesSupported === false) return <><div className="kind">校园网</div><h2>在线设备</h2><p>暂无设备信息</p></>
  return (
    <>
      <div className="kind" style={{ color: 'var(--accent)' }}>校园网</div>
      <h2>在线设备</h2>
      <AnimatePresence initial={false}>
        {net.devices.map((d) => (
          <motion.div key={d.id} className="dev" layout exit={{ opacity: 0, x: 40, height: 0, paddingTop: 0, paddingBottom: 0 }}>
            <div>
              <b>{d.name}</b>
              <span className="num">{d.ip} · {d.current ? '本机' : `${sinceLabel(d.since, t)}上线`}</span>
            </div>
            {!d.current && <button className="textbtn" onClick={() => kick(d.id)}>下线</button>}
          </motion.div>
        ))}
      </AnimatePresence>
      <div className="row" style={{ marginTop: 20 }}>
        <div className="l">开机自动登录</div>
        <button className={`toggle ${settings.netAutoLogin ? 'on' : ''}`} onClick={() => updateSettings({ netAutoLogin: !settings.netAutoLogin })}>
          <motion.i layout style={{ left: settings.netAutoLogin ? 21 : 3 }} transition={{ type: 'spring', stiffness: 600, damping: 34 }} />
        </button>
      </div>
    </>
  )
}

export function Sheet() {
  const { sheet, openSheet } = useStore()
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && openSheet(null)
    addEventListener('keydown', onKey)
    return () => removeEventListener('keydown', onKey)
  }, [openSheet])

  if (sheet?.type === 'course' || sheet?.type === 'network') return null

  return (
    <AnimatePresence>
      {sheet && (
        <>
          <motion.div className="scrim" onClick={() => openSheet(null)} initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} />
          <motion.aside
            className="sheet"
            initial={{ x: '100%' }} animate={{ x: 0 }} exit={{ x: '100%' }}
            transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
          >
            <button className="chip x" onClick={() => openSheet(null)} aria-label="关闭">✕</button>
            <DevicesBody />
          </motion.aside>
        </>
      )}
    </AnimatePresence>
  )
}

export function Toast() {
  const { toast } = useStore()
  return (
    <AnimatePresence>
      {toast && (
        <motion.div
          key={toast.id}
          className="toast" role="status"
          initial={{ opacity: 0, y: 16, scale: 0.96 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: 8 }}
          transition={{ type: 'spring', stiffness: 500, damping: 34 }}
        >
          <i className="dot" />
          <span>{toast.text}</span>
          {toast.action && <button className="toast-act" onClick={toast.action.run}>{toast.action.label}</button>}
        </motion.div>
      )}
    </AnimatePresence>
  )
}
