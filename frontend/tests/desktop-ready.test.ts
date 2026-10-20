// @vitest-environment happy-dom
import { act, createElement, StrictMode } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useDesktopBridge, type DesktopBridge } from '../src/lib/desktop'

let root: Root
let container: HTMLDivElement
let bridge: DesktopBridge
function Probe({ ready }: { ready: boolean }) {
  useDesktopBridge(ready)
  return createElement('main', { 'data-ready': ready }, ready ? '工作区' : '恢复中')
}
const render = (ready: boolean) => act(async () => {
  root.render(createElement(StrictMode, null, createElement(Probe, { ready })))
})
beforeEach(() => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true)
  container = document.createElement('div'); document.body.append(container)
  root = createRoot(container)
  bridge = {
    show_window: vi.fn(), minimize_window: vi.fn(), toggle_maximize_window: vi.fn(), close_window: vi.fn(),
    get_window_state: vi.fn().mockResolvedValue({ maximized: false }),
    content_ready: vi.fn().mockImplementation(async () => {
      expect(container.querySelector('main[data-ready="true"]')).not.toBeNull()
    }),
  }
  window.pywebview = { api: bridge }
})
afterEach(async () => {
  await act(async () => root.unmount())
  container.remove(); delete window.pywebview; vi.unstubAllGlobals()
})

describe('desktop content readiness', () => {
  it('does not present the empty shell while account restoration is pending', async () => {
    await render(false)
    expect(bridge.content_ready).not.toHaveBeenCalled()
    await render(true)
    expect(bridge.content_ready).toHaveBeenCalledTimes(1)
  })
  it('reports only once per document, including StrictMode remounts and later logout', async () => {
    await render(true)
    await render(false)
    await render(true)
    expect(bridge.content_ready).toHaveBeenCalledTimes(1)
  })
  it('reports when a late bridge becomes available after the content commit', async () => {
    delete window.pywebview
    await render(true)
    window.pywebview = { api: bridge }
    await act(async () => { window.dispatchEvent(new Event('pywebviewready')) })
    expect(bridge.content_ready).toHaveBeenCalledTimes(1)
  })
  it('does not require a native bridge in a plain browser', async () => {
    delete window.pywebview
    await render(true)
    expect(container.textContent).toBe('工作区')
    expect(bridge.content_ready).not.toHaveBeenCalled()
  })
  it('keeps older desktop bridges operable without the optional readiness method', async () => {
    delete bridge.content_ready
    await render(true)
    expect(container.textContent).toBe('工作区')
    expect(bridge.get_window_state).toHaveBeenCalled()
  })
  it('leaves failed acknowledgements to the native timeout without creating a retry loop', async () => {
    vi.mocked(bridge.content_ready!).mockRejectedValue(new Error('synthetic bridge failure'))
    await render(true)
    await render(true)
    expect(bridge.content_ready).toHaveBeenCalledTimes(1)
  })
})
