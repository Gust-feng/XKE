import { useCallback, useEffect, useRef, useState } from 'react'
import { useStore } from '../lib/store-context'
import { useNow } from '../lib/time'
import '../editions/one/one.css'
import '../styles/runtime.css'

/** Restore established workspaces; otherwise require the initial authentication. */
export function AuthPanel() {
  const { phase, authOpen, captcha } = useStore()
  if (phase === 'loading') return <div className="auth-surface"><span className="auth-spinner" role="status" aria-label="正在恢复工作区" /></div>
  return phase === 'anonymous' || authOpen || captcha ? <AuthForm initial={phase === 'anonymous'} /> : null
}

/** Credentials disappear when the panel closes; both login and continuation are cancellable. */
function AuthForm({ initial }: { initial: boolean }) {
  const { captcha, authBusy, authError, login, connect, submitCaptcha, cancelAuth, user } = useStore()
  const [account, setAccount] = useState(user?.student.student_id || '')
  const [password, setPassword] = useState('')
  const [entry, setEntry] = useState({ id: captcha?.id, code: '' })
  const code = entry.id === captcha?.id ? entry.code : ''
  const input = useRef<HTMLInputElement>(null)
  const form = useRef<HTMLFormElement>(null)
  const now = useNow(1000)
  const seconds = captcha ? Math.max(0, Math.ceil((captcha.expiresAt - now.getTime()) / 1000)) : 0
  const cancel = useCallback(() => {
    setPassword(''); setEntry({ id: undefined, code: '' }); cancelAuth()
  }, [cancelAuth])
  useEffect(() => {
    const previous = document.activeElement
    return () => { if (previous instanceof HTMLElement && previous.isConnected) previous.focus() }
  }, [])
  useEffect(() => {
    input.current?.focus()
  }, [captcha?.id])
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); cancel() }
      if (e.key !== 'Tab') return
      const controls = form.current?.querySelectorAll<HTMLElement>('input:not(:disabled), button:not(:disabled)')
      const first = controls?.[0], last = controls?.[controls.length - 1]
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last?.focus() }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first?.focus() }
    }
    addEventListener('keydown', key)
    return () => removeEventListener('keydown', key)
  }, [cancel])
  return (
    <div className={initial ? 'auth-surface' : 'auth-overlay'} onClick={(e) => { if (!initial && e.target === e.currentTarget) cancel() }}>
      <form ref={form} className="auth-panel" role={initial ? undefined : 'dialog'} aria-modal={initial ? undefined : true}
        aria-labelledby="auth-title"
        onSubmit={(e) => {
          e.preventDefault()
          if (captcha) { if (seconds > 0) void submitCaptcha(code.trim()) }
          else { const value = password; setPassword(''); void login(account.trim(), value) }
        }}>
        <h1 id="auth-title">{captcha ? '验证码' : initial ? '教务认证' : '连接教务'}</h1>
        {captcha ? (
          <>
            <img src={captcha.image} alt="教务验证码" className="auth-captcha" />
            <label>验证码<input ref={input} name="captcha" value={code} onChange={(e) => setEntry({ id: captcha.id, code: e.target.value })} autoComplete="off" required disabled={authBusy || seconds === 0} /></label>
            <p className="mute" role="status">{seconds ? `${seconds}s` : '已过期'}</p>
          </>
        ) : (
          <>
            <label>学号<input ref={input} name="username" value={account} onChange={(e) => setAccount(e.target.value)} autoComplete="username" required disabled={authBusy} /></label>
            <label>密码<input name="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="off" required disabled={authBusy} /></label>
            {user && <button type="button" className="sw-btn" onClick={() => { setPassword(''); void connect() }} disabled={authBusy}>使用已存账号</button>}
          </>
        )}
        <div className="runtime-actions auth-actions">
          <button type="submit" className="sw-btn" disabled={authBusy || !!captcha && (!code.trim() || seconds === 0)}>{authBusy ? '处理中…' : captcha ? '确认' : initial ? '认证' : '连接'}</button>
          {(!initial || captcha || authBusy) && <button type="button" className="sw-btn" onClick={cancel}>{captcha && seconds === 0 ? '重试' : '取消'}</button>}
          {authError && <span className="auth-feedback" role="alert">{authError}</span>}
        </div>
      </form>
    </div>
  )
}
