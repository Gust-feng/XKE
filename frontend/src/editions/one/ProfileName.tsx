import { useRef, useState } from 'react'
import type { StudentDto } from '../../api/dto'
import { useStore } from '../../lib/store-context'
import './profile-name.css'

export function ProfileName({ student }: { student: StudentDto }) {
  const { updateNickname } = useStore()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const pending = useRef(false)
  const cancelled = useRef(false)
  const button = useRef<HTMLButtonElement>(null)
  const current = student.use_nickname ? student.nickname : ''
  const displayed = current || student.name || '已登录'
  const save = async () => {
    if (cancelled.current || pending.current) return
    const value = draft.trim() === student.name.trim() ? '' : draft.trim()
    if (value === current) { setEditing(false); return }
    pending.current = true; setSaving(true); setError('')
    try { await updateNickname(value); setEditing(false) }
    catch (cause) { setError(cause instanceof Error ? cause.message : '昵称未保存') }
    finally { pending.current = false; setSaving(false) }
  }
  return <div className="one-profile-name">
    {editing ? <input className="one-id-name one-profile-input" aria-label="昵称" aria-busy={saving || undefined}
      aria-invalid={!!error} value={draft} placeholder={student.name} readOnly={saving} autoFocus
      onFocus={(event) => event.currentTarget.select()} onChange={(event) => { setDraft(event.target.value); setError('') }}
      onBlur={() => { void save() }} onKeyDown={(event) => {
        if (event.nativeEvent.isComposing) return
        if (event.key === 'Enter') { event.preventDefault(); event.currentTarget.blur() }
        if (event.key === 'Escape' && !pending.current) {
          event.preventDefault(); event.stopPropagation(); cancelled.current = true; setEditing(false); setError('')
          requestAnimationFrame(() => button.current?.focus())
        }
      }} />
      : <button ref={button} type="button" className="one-id-name one-profile-edit" aria-label={`修改昵称，当前${displayed}`}
        onClick={() => { cancelled.current = false; setDraft(displayed); setEditing(true); setError('') }}>{displayed}</button>}
    {error && <span className="one-profile-error" role="alert">{error}</span>}
  </div>
}
