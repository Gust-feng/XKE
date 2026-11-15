import { useEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useOverlay } from '../lib/use-overlay'
import '../editions/one/notification.css'

export function Modal({ label, onClose, children, className = '' }: { label: string; onClose(): void; children?: ReactNode; className?: string }) {
  const ref = useRef<HTMLDialogElement>(null)
  useOverlay(true, () => ref.current, onClose)
  useEffect(() => { if (ref.current && !ref.current.open) ref.current.showModal() }, [])
  return createPortal(<dialog ref={ref} className={`xke-modal ${className}`} aria-label={label}
    onCancel={(event) => { event.preventDefault(); onClose() }} onClick={(event) => { if (event.target === event.currentTarget) onClose() }}>
    <div className="xke-modal-body"><header className="xke-modal-head"><h2>{label}</h2><button type="button" className="xke-close" onClick={onClose} aria-label={`关闭${label}`}>×</button></header>{children}</div>
  </dialog>, document.body)
}
