import { motion } from 'motion/react'
import { useState, type CSSProperties, type ReactNode } from 'react'
import { useStore } from '../../lib/store-context'
import { sinceLabel, useNow } from '../../lib/time'
import { personalTerms, termLabel } from '../../lib/schedule-view'
import { SemesterField } from './SemesterField'
import { ProfileName } from './ProfileName'
import { AppUpdatePanel } from './AppUpdatePanel'
import { useAppUpdates } from '../../lib/use-app-updates'
import { reminderMinutes, reminderOptions, validRefreshInput } from '../../lib/settings-options'

const d = (i: number) => ({ '--i': i }) as CSSProperties
const EASE = [0.22, 1, 0.36, 1] as const

export function SwToggle({ on, onChange, label, disabled = false }: { on: boolean; onChange(v: boolean): void; label?: string; disabled?: boolean }) {
  return <button type="button" className={`st-sw ${on ? 'on' : ''}`} role="switch" aria-checked={on} aria-label={label} onClick={() => onChange(!on)} disabled={disabled}>
    <motion.b initial={false} animate={{ x: on ? 20 : 0 }} transition={{ duration: 0.24, ease: EASE }} />
  </button>
}

function Row({ label, children, i, onClick, networkPopup = false }: { label: string; children?: ReactNode; i: number; onClick?(): void; networkPopup?: boolean }) {
  const { sheet } = useStore()
  const Tag = onClick ? 'button' : 'div'
  return <Tag className={`st-row sw-in${onClick ? ' is-link' : ''}`} style={d(i)} onClick={onClick} data-network-config-trigger={networkPopup ? '' : undefined} aria-haspopup={networkPopup ? 'dialog' : undefined} aria-expanded={networkPopup ? sheet?.type === 'network' : undefined} aria-controls={networkPopup ? 'network-config-panel' : undefined}>
    <span className="st-l"><span data-course-text={networkPopup ? 'title' : undefined}>{label}</span></span>
    <span className="st-r">{children}</span>
  </Tag>
}

/** 设置保持为低频操作：账号、课表显示、校园网入口和系统启动。 */
export function SwissSettings() {
  const { settings, schedule, user, updateSettings, openSheet, openAuth, preview } = useStore()
  const updates = useAppUpdates()
  if (!settings) return <div className="st sw-grid"><section className="st-col"><h2 className="st-h sw-in" style={d(0)}>课表</h2><button className="st-btn" onClick={openAuth}>连接教务</button></section></div>

  const reminder = reminderMinutes(settings.remindBefore)
  const term = schedule?.term
  const termOptions = schedule ? personalTerms(schedule, user?.student.enrollment_date || '', term?.id || term?.name) : []
  return <div className="st sw-grid">
    <section className="st-col st-a">
      <h2 className="st-h sw-in" style={d(0)}>课表</h2>
      {termOptions.length > 1 && <span className="sr-only" aria-label="查看学期">{termOptions.map((option) => option.label).join('、')}</span>}
      {/* 教务登录：换学号、密码改了之后重新认证，都走这一个入口 */}
      <Row i={2} label="教务登录" onClick={openAuth}>
        <svg className="one-network-open" viewBox="0 0 20 20" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" aria-hidden="true"><path d="M4 10h12M11 5l5 5-5 5" /></svg>
      </Row>
      <Row i={3} label="显示周末">
        <SwToggle label="显示周末" on={settings.showWeekend} onChange={(value) => updateSettings({ showWeekend: value })} />
      </Row>
      <Row i={4} label="课前提醒">
        <span className="st-seg st-reminder" role="radiogroup" aria-label="课前提醒">
          {reminderOptions(reminder).map((minutes) => <button key={minutes} role="radio" aria-label={minutes ? `上课前 ${minutes} 分钟提醒` : '关闭课前提醒'} disabled={settings.desktopSupported === false} aria-checked={reminder === minutes} className={reminder === minutes ? 'on' : ''} onClick={() => updateSettings({ remindBefore: minutes })}>
            {reminder === minutes && <motion.i layoutId="st-seg" transition={{ duration: 0.26, ease: EASE }} />}
            <span>{minutes ? minutes : '关'}</span>
          </button>)}
        </span>
      </Row>

    </section>

    <section className="st-col st-b">
      <h2 className="st-h sw-in" style={d(5)}>校园网</h2>
      <Row i={6} label="自动保持连接">
        <SwToggle label="自动保持连接" on={settings.netAutoLogin} onChange={(value) => updateSettings({ netAutoLogin: value })} />
      </Row>
      <Row i={7} label="连接设置" networkPopup onClick={() => openSheet({ type: 'network' })}>
        <svg className="one-network-open" viewBox="0 0 20 20" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" aria-hidden="true"><path d="M4 10h12M11 5l5 5-5 5" /></svg>
      </Row>

    </section>

    <section className="st-col st-c">
      <h2 className="st-h st-system-heading sw-in" style={d(8)}><span>系统</span><button type="button" className="st-version num" aria-label="应用版本与更新" aria-haspopup="dialog" aria-expanded={updates.open} data-ready={updates.data?.status === 'ready' || updates.data?.status === 'available'} onClick={updates.openPanel}>{updates.data?.current_version ? `v${updates.data.current_version.replace(/^v/i, '')}` : '版本'}</button></h2>
      <Row i={9} label="开机启动">
        <SwToggle label="开机启动" on={settings.launchAtLogin} onChange={(value) => updateSettings({ launchAtLogin: value })} disabled={settings.desktopSupported === false} />
      </Row>
      <Row i={10} label="自动更新课表">
        <SwToggle label="自动更新课表" on={settings.autoRefresh === true} onChange={(value) => updateSettings({ autoRefresh: value })} />
      </Row>
      {settings.autoRefresh && <>
        <Row i={11} label="更新频率">
          <span className="st-seg" role="radiogroup" aria-label="更新频率">
            {(['daily', 'interval'] as const).map((mode) => <button key={mode} role="radio" aria-checked={(settings.refreshMode || 'daily') === mode} className={(settings.refreshMode || 'daily') === mode ? 'on' : ''} onClick={() => updateSettings({ refreshMode: mode })}>
              {(settings.refreshMode || 'daily') === mode && <motion.i layoutId="st-refresh-mode" transition={{ duration: 0.26, ease: EASE }} />}
              <span>{mode === 'daily' ? '每天' : '间隔'}</span>
            </button>)}
          </span>
        </Row>
        {settings.refreshMode === 'interval'
          ? <Row i={12} label="间隔小时"><RefreshInput key={settings.refreshHours ?? 4} kind="interval" value={String(settings.refreshHours ?? 4)} save={(value) => updateSettings({ refreshHours: Number(value) })} /></Row>
          : <Row i={12} label="更新时间"><RefreshInput key={settings.refreshTime || '20:00'} kind="daily" value={settings.refreshTime || '20:00'} save={(value) => updateSettings({ refreshTime: value })} /></Row>}
      </>}
    </section>

    <SemesterField />
    {updates.open && <AppUpdatePanel state={updates} noUpdate={updates.noUpdate} autoUpdate={settings.autoUpdate} desktopSupported={settings.desktopSupported === true} preview={preview}
      onAction={(action) => { void updates.run(action) }} onRefresh={() => { void updates.refresh() }}
      onAutoChange={(value) => updateSettings({ autoUpdate: value }, 'inline')} onClose={updates.closePanel} />}
  </div>
}

function RefreshInput({ kind, value, save }: { kind: 'daily' | 'interval'; value: string; save(value: string): Promise<void> }) {
  const [draft, setDraft] = useState(value)
  const [saving, setSaving] = useState(false)
  const commit = () => {
    if (saving) return
    if (!validRefreshInput(kind, draft)) { setDraft(value); return }
    if (draft !== value) {
      setSaving(true)
      void save(draft).finally(() => { setDraft(value); setSaving(false) })
    }
  }
  return <input className="st-input num" disabled={saving} aria-label={kind === 'daily' ? '课表每日更新时间' : '课表更新间隔小时'} type={kind === 'daily' ? 'time' : 'number'} min={kind === 'daily' ? undefined : 1} max={kind === 'daily' ? undefined : 8760} step={kind === 'daily' ? 60 : 1} value={draft} onChange={(event) => setDraft(event.target.value)} onBlur={commit} onKeyDown={(event) => { if (event.key === 'Enter') event.currentTarget.blur(); if (event.key === 'Escape') { event.preventDefault(); setDraft(value) } }} />
}

/** Personal information keeps the display name as its only primary identity. */
export function IdCard() {
  const { settings, currentSchedule, user, logout, preview } = useStore()
  const now = useNow(30_000)
  if (!settings) return null
  const term = currentSchedule?.term
  const termName = term ? termLabel(term.id || term.name, user?.student.enrollment_date || '') : '未加载'
  const student = user?.student
  const major = [student?.major_name, student?.class_name].filter(Boolean).join(' · ')
  return <section className="one-id" aria-label="个人信息">
    <h3><span className="one-k"><i className="one-k-mark" aria-hidden="true" />个人信息</span></h3>
    {settings.portalBound && student ? <ProfileName key={student.student_id} student={student} /> : <b className="one-id-name">{settings.portalBound ? '已登录' : '未登录'}</b>}
    <dl className="one-id-facts">
      {major && <div className="is-wide"><dt>专业</dt><dd>{major}</dd></div>}
      <div><dt>学期</dt><dd>{termName}</dd></div>
      <div><dt>同步</dt><dd>{sinceLabel(currentSchedule?.syncedAt || null, now)}</dd></div>
    </dl>
    {!preview && settings.portalBound && <button className="one-id-out" onClick={logout}>注销</button>}
  </section>
}
