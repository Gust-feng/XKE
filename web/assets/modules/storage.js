import {
  AVATAR_KEY,
  LAST_ACCOUNT_KEY,
  REMEMBER_KEY,
  SESSION_KEY,
  THEME_ACCOUNT_KEY_PREFIX,
  THEME_FALLBACK_KEY,
  TRUSTED_ACCOUNTS_KEY,
} from './constants.js'

const NOTICE_STATE_KEY_PREFIX = 'xke-notice-state'
const DEV_TOOLS_STATE_KEY = 'xke-dev-tools-state'

/**
 * Local storage helpers are centralized here so persistence rules stay
 * consistent across login/session/avatar flows.
 */
export function getRememberedLoginState () {
  try {
    const raw = localStorage.getItem(REMEMBER_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    localStorage.removeItem(REMEMBER_KEY)
    return null
  }
}

export function saveRememberedLoginState (payload) {
  localStorage.setItem(REMEMBER_KEY, JSON.stringify(payload))
}

export function clearRememberedLoginState () {
  localStorage.removeItem(REMEMBER_KEY)
}

function normalizeTrustedAccountEntry (entry = {}) {
  if (!entry || typeof entry !== 'object') return null
  const user = String(entry.user || '').trim()
  const trustedLoginToken = String(entry.trusted_login_token || '').trim()
  if (!user || !trustedLoginToken) return null
  return {
    user,
    trusted_login_token: trustedLoginToken,
    prelogin_mode: String(entry.prelogin_mode || 'capture').trim() || 'capture',
    saved_at: String(entry.saved_at || '').trim() || new Date().toISOString(),
    last_used_at: String(entry.last_used_at || '').trim() || '',
  }
}

function sortTrustedAccounts (accounts = []) {
  return [...accounts].sort((left, right) => {
    const leftLastUsedAt = Date.parse(left.last_used_at || left.saved_at || 0) || 0
    const rightLastUsedAt = Date.parse(right.last_used_at || right.saved_at || 0) || 0
    if (leftLastUsedAt !== rightLastUsedAt) return rightLastUsedAt - leftLastUsedAt
    return String(left.user || '').localeCompare(String(right.user || ''), 'zh-CN')
  })
}

export function getTrustedAccounts () {
  try {
    const raw = localStorage.getItem(TRUSTED_ACCOUNTS_KEY)
    const parsed = raw ? JSON.parse(raw) : []
    if (!Array.isArray(parsed)) {
      localStorage.removeItem(TRUSTED_ACCOUNTS_KEY)
      return []
    }
    return sortTrustedAccounts(
      parsed
        .map((entry) => normalizeTrustedAccountEntry(entry))
        .filter(Boolean),
    )
  } catch {
    localStorage.removeItem(TRUSTED_ACCOUNTS_KEY)
    return []
  }
}

export function saveTrustedAccounts (accounts = []) {
  const normalized = sortTrustedAccounts(
    Array.isArray(accounts)
      ? accounts
        .map((entry) => normalizeTrustedAccountEntry(entry))
        .filter(Boolean)
      : [],
  )
  localStorage.setItem(TRUSTED_ACCOUNTS_KEY, JSON.stringify(normalized))
  return normalized
}

export function upsertTrustedAccount (payload = {}) {
  const nextEntry = normalizeTrustedAccountEntry({
    ...payload,
    saved_at: payload.saved_at || new Date().toISOString(),
    last_used_at: payload.last_used_at || payload.saved_at || new Date().toISOString(),
  })
  if (!nextEntry) return getTrustedAccounts()
  const current = getTrustedAccounts().filter((entry) => entry.user !== nextEntry.user)
  current.push(nextEntry)
  return saveTrustedAccounts(current)
}

export function removeTrustedAccount (user = '') {
  const normalizedUser = String(user || '').trim()
  if (!normalizedUser) return getTrustedAccounts()
  const nextAccounts = getTrustedAccounts().filter((entry) => entry.user !== normalizedUser)
  saveTrustedAccounts(nextAccounts)
  if (getLastAccount() === normalizedUser) {
    clearLastAccount()
  }
  return nextAccounts
}

export function findTrustedAccount (user = '') {
  const normalizedUser = String(user || '').trim()
  if (!normalizedUser) return null
  return getTrustedAccounts().find((entry) => entry.user === normalizedUser) || null
}

export function touchTrustedAccount (user = '', changes = {}) {
  const existing = findTrustedAccount(user)
  if (!existing) return getTrustedAccounts()
  return upsertTrustedAccount({
    ...existing,
    ...changes,
    user: existing.user,
    trusted_login_token: String(
      changes.trusted_login_token || existing.trusted_login_token || '',
    ).trim(),
    prelogin_mode: String(changes.prelogin_mode || existing.prelogin_mode || 'capture').trim() || 'capture',
    saved_at: existing.saved_at || new Date().toISOString(),
    last_used_at: changes.last_used_at || new Date().toISOString(),
  })
}

export function getLastAccount () {
  return String(localStorage.getItem(LAST_ACCOUNT_KEY) || '').trim()
}

export function saveLastAccount (user = '') {
  const normalizedUser = String(user || '').trim()
  if (!normalizedUser) return
  localStorage.setItem(LAST_ACCOUNT_KEY, normalizedUser)
}

export function clearLastAccount () {
  localStorage.removeItem(LAST_ACCOUNT_KEY)
}

export function migrateRememberedLoginToTrustedAccounts () {
  const remembered = getRememberedLoginState()
  if (!remembered) return { migrated: false, accounts: getTrustedAccounts() }

  const user = String(remembered.user || '').trim()
  const trustedLoginToken = String(remembered.trusted_login_token || '').trim()
  if (user) {
    saveLastAccount(user)
  }

  if (!user || !trustedLoginToken) {
    clearRememberedLoginState()
    return { migrated: false, accounts: getTrustedAccounts() }
  }

  const accounts = upsertTrustedAccount({
    user,
    trusted_login_token: trustedLoginToken,
    prelogin_mode: String(remembered.prelogin_mode || 'capture').trim() || 'capture',
  })
  clearRememberedLoginState()
  return { migrated: true, accounts }
}

export function getSessionId () {
  return localStorage.getItem(SESSION_KEY)
}

export function saveSessionId (sessionId) {
  if (!sessionId) return
  localStorage.setItem(SESSION_KEY, sessionId)
}

export function clearSessionId () {
  localStorage.removeItem(SESSION_KEY)
}

export function getCachedAvatarUrl () {
  return localStorage.getItem(AVATAR_KEY) || ''
}

export function setCachedAvatarUrl (url) {
  if (!url) return
  localStorage.setItem(AVATAR_KEY, url)
}

export function clearCachedAvatarUrl () {
  localStorage.removeItem(AVATAR_KEY)
}

export function getThemeFallback () {
  return localStorage.getItem(THEME_FALLBACK_KEY) || ''
}

export function saveThemeFallback (themeId) {
  if (!themeId) return
  localStorage.setItem(THEME_FALLBACK_KEY, themeId)
}

function getThemeAccountKey (studentId) {
  return `${THEME_ACCOUNT_KEY_PREFIX}:${studentId}`
}

export function getThemeForStudent (studentId) {
  if (!studentId) return ''
  return localStorage.getItem(getThemeAccountKey(studentId)) || ''
}

export function saveThemeForStudent (studentId, themeId) {
  if (!studentId || !themeId) return
  localStorage.setItem(getThemeAccountKey(studentId), themeId)
}

function getNoticeStateKey (studentId = '') {
  return `${NOTICE_STATE_KEY_PREFIX}:${studentId || 'guest'}`
}

export function getNoticeState (studentId = '') {
  try {
    const raw = localStorage.getItem(getNoticeStateKey(studentId))
    return raw ? JSON.parse(raw) : {}
  } catch {
    localStorage.removeItem(getNoticeStateKey(studentId))
    return {}
  }
}

export function saveNoticeState (studentId = '', payload = {}) {
  localStorage.setItem(getNoticeStateKey(studentId), JSON.stringify(payload || {}))
}

export function clearNoticeState (studentId = '') {
  localStorage.removeItem(getNoticeStateKey(studentId))
}

export function getDevToolsState () {
  try {
    const raw = localStorage.getItem(DEV_TOOLS_STATE_KEY)
    return raw ? JSON.parse(raw) : {}
  } catch {
    localStorage.removeItem(DEV_TOOLS_STATE_KEY)
    return {}
  }
}

export function saveDevToolsState (payload) {
  localStorage.setItem(DEV_TOOLS_STATE_KEY, JSON.stringify(payload || {}))
}

export function clearFrontendClientState (options = {}) {
  const { preserveTheme = false, preserveDevTools = true } = options
  clearRememberedLoginState()
  saveTrustedAccounts([])
  clearLastAccount()
  clearSessionId()
  clearCachedAvatarUrl()
  clearNoticeState()
  if (!preserveDevTools) {
    localStorage.removeItem(DEV_TOOLS_STATE_KEY)
  }
  const noticeKeysToRemove = []
  for (let index = 0; index < localStorage.length; index += 1) {
    const key = localStorage.key(index)
    if (!key) continue
    if (key.startsWith(`${NOTICE_STATE_KEY_PREFIX}:`)) noticeKeysToRemove.push(key)
  }
  noticeKeysToRemove.forEach((key) => localStorage.removeItem(key))
  if (!preserveTheme) {
    localStorage.removeItem(THEME_FALLBACK_KEY)
    const keysToRemove = []
    for (let index = 0; index < localStorage.length; index += 1) {
      const key = localStorage.key(index)
      if (!key) continue
      if (key.startsWith(`${THEME_ACCOUNT_KEY_PREFIX}:`)) keysToRemove.push(key)
    }
    keysToRemove.forEach((key) => localStorage.removeItem(key))
  }
}
