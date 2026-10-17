import { AnimatePresence, motion } from 'motion/react'
import type { CSSProperties } from 'react'
import { useStore } from '../../lib/store-context'

/** 校园网左栏：一张账户卡 + 每台在线设备一张卡。连接设置只在「设置」里有入口。 */
export function NetworkRail() {
  const { net, kick, refreshNet, netBusy, networkSummary: summary, summaryBusy, summaryError, loadNetworkSummary } = useStore()
  const hasFacts = !!(summary?.plan_name || summary?.account_status || summary?.billing_method)
  const devices = net && net.devicesSupported !== false ? net.devices : []
  const on = !!net?.online && !netBusy
  return <div className="one-nr">
    <section className="one-nr-account" data-on={on || undefined} aria-busy={summaryBusy}>
      <h3><span className="one-k"><i className="one-k-mark" aria-hidden="true" />校园网账户</span><button className={`one-nr-refresh${summaryBusy ? ' is-loading' : ''}`} aria-label="刷新校园网信息" aria-busy={summaryBusy} disabled={summaryBusy || netBusy} onClick={() => { void loadNetworkSummary(); void refreshNet() }}>
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
          <path d="M20 11a8 8 0 1 0-2 6" /><path d="M20 5v6h-6" />
        </svg>
      </button></h3>
      {summary?.plan_name && <p className="one-nr-plan">{summary.plan_name}</p>}
      {!hasFacts && <p className="one-nr-empty" role="status">{summaryBusy ? '读取中' : summaryError || '暂无套餐信息'}</p>}
      <dl className="one-nr-facts">
        {summary?.account_status && <div><dt>状态</dt><dd>{summary.account_status}</dd></div>}
        {summary?.billing_method && <div><dt>计费</dt><dd>{summary.billing_method}</dd></div>}
        <div className="is-wide"><dt>IP</dt><dd className="num lat">{net ? net.ip || '未获取' : '读取中'}</dd></div>
      </dl>
      {hasFacts && summaryError && <p className="one-nr-empty" role="status">暂未更新</p>}
    </section>
    {devices.length > 0 && <section className="one-nr-dev">
      <h3><span className="one-k">在线设备</span></h3>
      <ul><AnimatePresence initial={false}>{devices.map((device, i) => <motion.li key={device.id} layout exit={{ opacity: 0, x: -24 }} className={device.current ? 'is-me' : undefined} style={{ '--i': i } as CSSProperties}>
        <span className="n">{device.name}</span>
        {device.current ? <span className="me">本机</span> : <button onClick={() => void kick(device.id)}>下线</button>}
      </motion.li>)}</AnimatePresence></ul>
    </section>}
  </div>
}
