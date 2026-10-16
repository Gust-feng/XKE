import { NetworkConfigBody } from '../../components/NetworkConfig'
import { useStore } from '../../lib/store'
import { CardPopover } from './CardPopover'

export function NetworkConfigPop() {
  const { sheet, openSheet } = useStore()
  const open = sheet?.type === 'network'
  return <CardPopover open={open} identity="network-config" label="连接设置" source="button[data-network-config-trigger]"
    popupId="network-config-panel" className="one-network-pop" preferredWidth={560} dynamic manageFocus
    onClose={() => openSheet(null)}>
    <h2 data-course-text="title">连接设置</h2>
    <div className="one-network-body" data-pop-detail><NetworkConfigBody /></div>
    <button className="one-pop-c-x" onClick={() => openSheet(null)} aria-label="关闭连接设置">✕</button>
  </CardPopover>
}
