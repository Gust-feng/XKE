import { API } from './constants.js'

/**
 * Small API client facade used by the main app.
 *
 * We keep fetch wiring here so session/query parameter composition is not
 * duplicated across schedule/course/auth flows.
 */
export const SESSION_HEADER_NAME = 'X-XKE-Session'

export const API_TIMEOUTS = Object.freeze({
  default: 45_000,
  authInitialization: 120_000,
  scheduleRefresh: 90_000,
})

export function buildSessionHeaders (sessionId = '') {
  const normalized = String(sessionId || '').trim()
  return normalized ? { [SESSION_HEADER_NAME]: normalized } : {}
}

function resolveTimeoutMs (value) {
  const numeric = Number(value)
  return Number.isFinite(numeric) && numeric > 0 ? numeric : API_TIMEOUTS.default
}

function createTimeoutController (timeoutMs) {
  const controller = new AbortController()
  const timer = setTimeout(() => {
    controller.abort()
  }, timeoutMs)
  return {
    signal: controller.signal,
    dispose: () => clearTimeout(timer),
  }
}

function normalizeTransportError (error, timeoutMs) {
  if (error && (error.name === 'AbortError' || error.name === 'TimeoutError')) {
    const timeoutSeconds = Math.max(1, Math.round(timeoutMs / 1000))
    const timeoutError = new Error(`请求超时（${timeoutSeconds} 秒），移动教务或本地服务暂未响应，请稍后重试`)
    timeoutError.status = 0
    timeoutError.reason = 'timeout'
    throw timeoutError
  }

  if (error && error.message === 'Failed to fetch') {
    const backendError = new Error('服务似乎错误运行，建议重启客户端')
    backendError.status = 0
    throw backendError
  }

  throw error
}

export function createApiClient (getSessionId) {
  function formatValidationEntry (detail) {
    if (!detail || typeof detail !== 'object') return String(detail || '').trim()
    const message = String(detail.msg || detail.message || '').trim()
    const location = Array.isArray(detail.loc)
      ? detail.loc.filter((part) => part && part !== 'body').join('.')
      : ''
    if (!location) return message
    const genericPatterns = ['Field required', 'Input should', 'Invalid']
    if (genericPatterns.some((pattern) => message.includes(pattern))) {
      return `${location}: ${message}`
    }
    return message || `${location}: 请求参数无效`
  }

  function formatApiErrorDetail (detail) {
    if (Array.isArray(detail)) {
      const messages = detail
        .map((entry) => formatValidationEntry(entry))
        .filter(Boolean)
      if (!messages.length) return ''
      return messages.length === 1 ? messages[0] : messages.join('；')
    }
    if (detail && typeof detail === 'object') {
      return String(detail.message || detail.detail || '').trim()
    }
    return String(detail || '').trim()
  }

  async function parseJson (response) {
    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      const error = new Error(formatApiErrorDetail(data.detail) || `HTTP ${response.status}`)
      error.status = response.status
      error.detail = data.detail
      error.reason = data.reason
      error.data = data.data
      throw error
    }
    return data
  }

  async function post (path, body = {}, options = {}) {
    const timeoutMs = resolveTimeoutMs(options.timeoutMs)
    const timeout = createTimeoutController(timeoutMs)
    try {
      const response = await fetch(`${API}${path}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...buildSessionHeaders(options.sessionId),
          ...(options.headers || {}),
        },
        body: JSON.stringify(body),
        signal: timeout.signal,
      })
      return parseJson(response)
    } catch (error) {
      normalizeTransportError(error, timeoutMs)
    } finally {
      timeout.dispose()
    }
  }

  async function get (path, options = {}) {
    const timeoutMs = resolveTimeoutMs(options.timeoutMs)
    const timeout = createTimeoutController(timeoutMs)
    try {
      const response = await fetch(`${API}${path}`, {
        headers: {
          ...buildSessionHeaders(options.sessionId),
          ...(options.headers || {}),
        },
        signal: timeout.signal,
      })
      return parseJson(response)
    } catch (error) {
      normalizeTransportError(error, timeoutMs)
    } finally {
      timeout.dispose()
    }
  }

  async function postWithSession (path, body = {}, sessionId = '', options = {}) {
    if (sessionId && typeof sessionId === 'object') {
      options = sessionId
      sessionId = ''
    }
    return post(path, body, { ...options, sessionId: sessionId || getSessionId() })
  }

  async function getWithSession (path, sessionId = '', options = {}) {
    if (sessionId && typeof sessionId === 'object') {
      options = sessionId
      sessionId = ''
    }
    return get(path, { ...options, sessionId: sessionId || getSessionId() })
  }

  return { post, get, postWithSession, getWithSession }
}