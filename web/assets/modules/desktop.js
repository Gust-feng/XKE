const APP_DISPLAY_NAME = 'XKE·软大课务'

function wait(ms) {
  return new Promise((resolve) => window.setTimeout(resolve, ms))
}

async function getPywebviewApi(timeoutMs = 2200) {
  const startedAt = Date.now()
  while (Date.now() - startedAt < timeoutMs) {
    if (window.pywebview && window.pywebview.api) return window.pywebview.api
    await wait(60)
  }
  return null
}

export function createDesktopRuntime({ log } = {}) {
  async function notify({ title = APP_DISPLAY_NAME, message = '' } = {}) {
    const api = await getPywebviewApi()
    if (api && typeof api.notify === 'function') {
      try {
        await api.notify(String(title || APP_DISPLAY_NAME), String(message || ''))
        return 'desktop'
      } catch (error) {
        if (typeof log === 'function') log(`桌面通知发送失败: ${error.message || error}`)
      }
    }

    if (!('Notification' in window)) return 'unsupported'
    if (Notification.permission === 'default') {
      try {
        await Notification.requestPermission()
      } catch {
        return 'unsupported'
      }
    }
    if (Notification.permission !== 'granted') return 'denied'
    new Notification(String(title || APP_DISPLAY_NAME), { body: String(message || '') })
    return 'browser'
  }

  async function ensureNotificationPermission() {
    const api = await getPywebviewApi(250)
    if (api && typeof api.notify === 'function') return true
    if (!('Notification' in window)) return false
    if (Notification.permission === 'granted') return true
    if (Notification.permission === 'denied') return false
    try {
      return (await Notification.requestPermission()) === 'granted'
    } catch {
      return false
    }
  }

  async function showWindow() {
    const api = await getPywebviewApi(250)
    if (api && typeof api.show_window === 'function') {
      await api.show_window()
      return true
    }
    return false
  }

  return {
    notify,
    ensureNotificationPermission,
    showWindow,
    isDesktopShellAvailable: () => Boolean(window.pywebview && window.pywebview.api),
  }
}
