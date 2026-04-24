export function createDesktopChromeController ({
  shell,
  chrome,
  handle,
  btnMinimize,
  btnMaximize,
  btnClose,
}) {
  if (!shell || !chrome) {
    return {
      setConnectionState () {},
      setPanelLabel () {},
      syncWindowState () {},
    }
  }

  let bridge = null
  let available = false
  let shellEnabled = false

  function prefersDragRegion () {
    return Boolean(window.pywebview && window.pywebview.platform === 'edgechromium')
  }

  function setShellEnabled (enabled) {
    const nextEnabled = Boolean(enabled)
    if (shellEnabled === nextEnabled) return
    shellEnabled = nextEnabled
    document.body.classList.toggle('desktop-shell-enabled', nextEnabled)
    shell.classList.toggle('hidden', !nextEnabled)
    shell.setAttribute('aria-hidden', nextEnabled ? 'false' : 'true')
  }

  function applyWindowState (state = {}) {
    const frameless = state.frameless == null ? true : Boolean(state.frameless)
    setShellEnabled(frameless)
    const maximized = Boolean(state.maximized)
    const minimized = Boolean(state.minimized)
    chrome.classList.toggle('is-maximized', maximized)
    chrome.dataset.windowState = minimized ? 'minimized' : maximized ? 'maximized' : 'normal'
    if (btnMaximize) {
      btnMaximize.dataset.mode = maximized ? 'restore' : 'maximize'
      const label = maximized ? '还原窗口' : '放大窗口'
      btnMaximize.setAttribute('aria-label', label)
    }
  }

  async function syncWindowState () {
    if (!bridge || typeof bridge.get_window_state !== 'function') return
    try {
      const state = await bridge.get_window_state()
      applyWindowState(state || {})
    } catch (error) {
      console.error('同步窗口状态失败', error)
    }
  }

  async function minimizeWindow () {
    if (!bridge || typeof bridge.minimize_window !== 'function') return
    try {
      await bridge.minimize_window()
    } catch (error) {
      console.error('最小化窗口失败', error)
    }
  }

  async function toggleMaximizeWindow () {
    if (!bridge || typeof bridge.toggle_maximize_window !== 'function') return
    try {
      const state = await bridge.toggle_maximize_window()
      applyWindowState(state || {})
    } catch (error) {
      console.error('切换窗口缩放状态失败', error)
    }
  }

  async function closeWindow () {
    if (!bridge || typeof bridge.close_window !== 'function') return
    try {
      await bridge.close_window()
    } catch (error) {
      console.error('关闭窗口失败', error)
    }
  }

  async function startWindowDrag () {
    if (!bridge || typeof bridge.start_window_drag !== 'function') return
    try {
      await bridge.start_window_drag()
    } catch (error) {
      console.error('启动窗口拖拽失败', error)
    }
  }

  function activateDesktopShell () {
    if (available || !(window.pywebview && window.pywebview.api)) return
    bridge = window.pywebview.api
    available = true
    void syncWindowState()
  }

  window.addEventListener('pywebviewready', () => {
    activateDesktopShell()
  })

  window.addEventListener('xke-window-state', (event) => {
    applyWindowState((event && event.detail) || {})
  })

  if (handle) {
    handle.addEventListener('mousedown', (event) => {
      if (prefersDragRegion()) return
      if (event.button !== 0 || event.detail > 1) return
      event.preventDefault()
      void startWindowDrag()
    })
    handle.addEventListener('dblclick', (event) => {
      event.preventDefault()
      void toggleMaximizeWindow()
    })
  }

  if (btnMinimize) {
    btnMinimize.addEventListener('click', (event) => {
      event.preventDefault()
      event.stopPropagation()
      void minimizeWindow()
    })
  }

  if (btnMaximize) {
    btnMaximize.addEventListener('click', (event) => {
      event.preventDefault()
      event.stopPropagation()
      void toggleMaximizeWindow()
    })
  }

  if (btnClose) {
    btnClose.addEventListener('click', (event) => {
      event.preventDefault()
      event.stopPropagation()
      void closeWindow()
    })
  }

  activateDesktopShell()

  return {
    setConnectionState () {},
    setPanelLabel () {},
    syncWindowState,
  }
}
