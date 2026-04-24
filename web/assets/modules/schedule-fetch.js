export function buildMobileFetchOverridesForMissingWeek (targetWeekNumber, currentWeekNumber = null) {
  const normalizedTarget = Number.parseInt(String(targetWeekNumber ?? ''), 10)
  const normalizedCurrent = Number.parseInt(String(currentWeekNumber ?? ''), 10)
  if (!Number.isInteger(normalizedTarget) || normalizedTarget <= 0) {
    return null
  }

  if (Number.isInteger(normalizedCurrent) && normalizedTarget === normalizedCurrent) {
    return {
      mode: 'current',
      manual_week: '',
      cookie_rq: null,
      sourceOverride: 'mobile',
    }
  }

  return {
    mode: 'manual',
    manual_week: String(normalizedTarget),
    cookie_rq: null,
    sourceOverride: 'mobile',
  }
}

export function createScheduleFetchGate ({ onReuse } = {}) {
  let inFlight = null

  return {
    get active () {
      return Boolean(inFlight)
    },
    run (taskFactory, options = {}) {
      if (typeof taskFactory !== 'function') {
        return Promise.reject(new TypeError('taskFactory must be a function'))
      }

      if (options.bypass) return (async () => taskFactory())()

      if (inFlight) {
        if (typeof onReuse === 'function') onReuse()
        return inFlight
      }

      inFlight = (async () => taskFactory())().finally(() => {
        inFlight = null
      })
      return inFlight
    },
  }
}

export function createRemoteFailureCooldown ({ durationMs = 30000, now = () => Date.now() } = {}) {
  let cooldownUntil = 0
  let cooldownReason = ''

  function snapshot () {
    const remainingMs = Math.max(0, cooldownUntil - now())
    return {
      active: remainingMs > 0,
      remaining_ms: remainingMs,
      reason: cooldownReason,
    }
  }

  return {
    snapshot,
    mark (reason = '') {
      cooldownUntil = now() + Math.max(0, Number(durationMs) || 0)
      cooldownReason = String(reason || '').trim()
      return snapshot()
    },
    clear () {
      cooldownUntil = 0
      cooldownReason = ''
      return snapshot()
    },
  }
}

export function shouldMarkRemoteFailureForAuthError (error) {
  const status = Number(error && error.status)
  const message = String((error && error.message) || error || '').trim()
  if (status === 409 && message.includes('本地初始化数据不完整')) return false
  return ![
    '本地账号或密码不正确',
    '本地凭据不存在',
    '本地初始化数据不完整',
    '可信登录环境不可用',
    '当前设备尚未建立可信登录环境',
    '会话不存在或已过期',
  ].some((localOnlyMessage) => message.includes(localOnlyMessage))
}
