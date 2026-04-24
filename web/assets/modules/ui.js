/**
 * Generic UI helpers shared across panels.
 */
export function showMsg (element, type, text) {
  element.className = `msg ${type}`
  element.textContent = text
  element.style.display = 'block'
}

export function hideMsg (element) {
  element.style.display = 'none'
  element.className = 'msg'
}

export function setLoading (button, loading) {
  if (loading) {
    button.classList.add('loading')
    button.disabled = true
    return
  }
  button.classList.remove('loading')
  button.disabled = false
}

export function createLogger (logArea) {
  return function log (message) {
    const ts = new Date().toLocaleTimeString('zh-CN', { hour12: false })
    logArea.textContent += `[${ts}] ${message}\n`
    logArea.scrollTop = logArea.scrollHeight
  }
}
