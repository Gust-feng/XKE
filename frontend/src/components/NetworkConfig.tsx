import { useEffect, useId, useState } from 'react'
import type { NetworkConfigDto, NetworkSettingsDto } from '../api/dto'
import { networkPatch } from '../api/network-patch'
import { useStore } from '../lib/store'
import { SwToggle } from '../editions/one/SwissSettings'

const PROVIDERS: { value: NetworkSettingsDto['provider']; label: string }[] = [
  { value: 'campus', label: '校园网' }, { value: 'telecom', label: '电信' },
  { value: 'unicom', label: '联通' }, { value: 'mobile', label: '移动' },
]

/** Network form for the source-card popover; low-frequency fields remain under advanced settings. */
export function NetworkConfigBody() {
  const { networkConfig, loadNetworkConfig } = useStore()
  const [loading, setLoading] = useState(!networkConfig)
  useEffect(() => { void loadNetworkConfig().finally(() => setLoading(false)) }, [loadNetworkConfig])
  if (!networkConfig) return <div className="nc-empty" role="status">{loading ? '读取中' : <button onClick={() => {
    setLoading(true); void loadNetworkConfig().finally(() => setLoading(false))
  }}>重新读取</button>}</div>
  return <NetworkConfigEditor key={JSON.stringify(networkConfig)} config={networkConfig} />
}

function NetworkConfigEditor({ config }: { config: NetworkConfigDto }) {
  const { saveNetworkConfig } = useStore()
  const intervalId = useId()
  const saved = config.settings
  const [draft, setDraft] = useState<Partial<NetworkSettingsDto>>({})
  const [account, setAccount] = useState<string | null>(null)
  const [password, setPassword] = useState('')
  const [probe, setProbe] = useState('')
  const [busy, setBusy] = useState(false)
  const current = { ...saved, ...draft }
  const credential = config.credentials[current.provider]
  const hasSavedPassword = !!credential?.password && (account === null || account.trim() === credential.account)
  const dirty = Object.keys(draft).length > 0 || account !== null && account !== (credential?.account || '') || password.length > 0
  const set = <K extends keyof NetworkSettingsDto>(key: K, value: NetworkSettingsDto[K]) => setDraft((old) => {
    const next = { ...old }
    if (JSON.stringify(value) === JSON.stringify(saved[key])) delete next[key]
    else next[key] = value
    return next
  })
  const changeInterval = (step: number) => set('heartbeat_interval_minutes', Math.min(1440, Math.max(1, Math.round(current.heartbeat_interval_minutes) + step)))
  const addProbe = () => {
    const value = probe.trim()
    if (value && !current.connectivity_check_urls.includes(value)) set('connectivity_check_urls', [...current.connectivity_check_urls, value])
    setProbe('')
  }
  const save = async () => {
    setBusy(true)
    try {
      const success = await saveNetworkConfig(networkPatch(draft, current.provider, account, password))
      if (success) { setDraft({}); setAccount(null); setPassword('') }
    } finally { setBusy(false) }
  }
  const undo = () => {
    setDraft({}); setAccount(null); setPassword(''); setProbe('')
  }

  return <form className="nc" onSubmit={(event) => { event.preventDefault(); if (dirty && !busy) void save() }}>
    <fieldset disabled={busy}>
      <div className="nc-sec nc-block">
        <div className="nc-seg" role="radiogroup" aria-label="接入方式">
          {PROVIDERS.map((provider) => <button type="button" key={provider.value} role="radio" aria-checked={current.provider === provider.value}
            className={current.provider === provider.value ? 'on' : ''} onClick={() => { set('provider', provider.value); setAccount(null); setPassword('') }}>{provider.label}</button>)}
        </div>
      </div>
      <div className="nc-sec nc-block nc-credentials">
        <label>账号<input className="nc-in lat" required={current.heartbeat_enabled || account !== null || password.length > 0} value={account ?? credential?.account ?? ''} onChange={(event) => setAccount(event.target.value)} /></label>
        <label>密码<input className="nc-in lat" type="password" autoComplete="new-password" required={current.heartbeat_enabled && !hasSavedPassword} placeholder={hasSavedPassword ? '已保存' : ''} value={password} onChange={(event) => setPassword(event.target.value)} /></label>
      </div>
      <div className="nc-sec nc-keep"><span>自动保持连接</span><SwToggle label="自动保持连接" on={current.heartbeat_enabled} onChange={(value) => set('heartbeat_enabled', value)} disabled={busy} /></div>
      <div className="nc-sec nc-interval">
        <label htmlFor={intervalId}>心跳间隔<span className="sr-only">（分钟）</span></label>
        <div className="nc-stepper">
          <button type="button" aria-label="减少心跳间隔" disabled={current.heartbeat_interval_minutes <= 1} onClick={() => changeInterval(-1)}>−</button>
          <input id={intervalId} className="nc-in num lat" type="number" inputMode="numeric" min="1" max="1440" step="1" required value={current.heartbeat_interval_minutes} onChange={(event) => set('heartbeat_interval_minutes', Number(event.target.value))} />
          <span className="nc-unit" aria-hidden="true">分钟</span>
          <button type="button" aria-label="增加心跳间隔" disabled={current.heartbeat_interval_minutes >= 1440} onClick={() => changeInterval(1)}>+</button>
        </div>
      </div>
      <details className="nc-advanced">
        <summary>高级设置<span aria-hidden="true" /></summary>
        <div className="nc-advanced-body">
          <label className="nc-sec">认证门户<input className="nc-in lat" value={current.portal_url} onChange={(event) => set('portal_url', event.target.value)} spellCheck={false} /></label>
          <div className="nc-sec">
            <span className="nc-label">联网检测地址</span>
            <ul className="nc-list">{current.connectivity_check_urls.map((url) => <li key={url}>
              <span className="lat">{url.replace(/^https?:\/\//, '').replace(/\/$/, '')}</span>
              <button type="button" aria-label={`移除 ${url}`} onClick={() => set('connectivity_check_urls', current.connectivity_check_urls.filter((item) => item !== url))}>×</button>
            </li>)}</ul>
            <div className="nc-add"><input aria-label="新增检测地址" className="nc-in lat" placeholder="https://…" value={probe} onChange={(event) => setProbe(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); addProbe() } }} /><button type="button" onClick={addProbe} disabled={!probe.trim()} aria-label="添加检测地址">+</button></div>
          </div>

        </div>
      </details>
    </fieldset>
    {dirty && <div className="nc-foot"><button className="nc-save" type="submit" disabled={busy}>{busy ? '保存中' : '保存'}</button><button className="nc-undo" type="button" onClick={undo} disabled={busy}>撤销</button></div>}
  </form>
}
