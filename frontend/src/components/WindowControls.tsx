import type { DesktopBridge } from '../lib/desktop'

/** Kept outside modal/inert content so the desktop remains operable during authentication. */
export function WindowControls({ bridge, maximized, onError }: {
  bridge: DesktopBridge; maximized: boolean; onError(message: string): void
}) {
  const run = (action: () => Promise<unknown>) => void action().catch((error: Error) => onError(error.message))
  return <>
    <div className="runtime-drag" aria-hidden="true" />
    <div className="runtime-window" role="group" aria-label="窗口">
      <button aria-label="最小化" onClick={() => run(() => bridge.minimize_window())}>
        <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3 8h10" /></svg>
      </button>
      <button aria-label={maximized ? '还原窗口' : '最大化'} onClick={() => run(() => bridge.toggle_maximize_window())}>
        <svg viewBox="0 0 16 16" aria-hidden="true">{maximized
          ? <><path d="M6 3h7v7" /><rect x="3" y="6" width="7" height="7" /></>
          : <rect x="3" y="3" width="10" height="10" />}</svg>
      </button>
      <button className="runtime-window-close" aria-label="关闭窗口" onClick={() => run(() => bridge.close_window())}>
        <svg viewBox="0 0 16 16" aria-hidden="true"><path d="m4 4 8 8m0-8-8 8" /></svg>
      </button>
    </div>
  </>
}
