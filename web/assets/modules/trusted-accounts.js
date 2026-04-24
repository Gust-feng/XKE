function normalizeTrustedAccountCandidate (entry = {}) {
  if (!entry || typeof entry !== 'object') return null
  const user = String(entry.user || '').trim()
  const trustedLoginToken = String(entry.trusted_login_token || '').trim()
  if (!user || !trustedLoginToken) return null
  return {
    user,
    trusted_login_token: trustedLoginToken,
    prelogin_mode: String(entry.prelogin_mode || 'capture').trim() || 'capture',
    saved_at: String(entry.saved_at || '').trim(),
    last_used_at: String(entry.last_used_at || '').trim(),
  }
}

export function normalizeTrustedAccounts (accounts = []) {
  if (!Array.isArray(accounts)) return []
  return accounts
    .map((entry) => normalizeTrustedAccountCandidate(entry))
    .filter(Boolean)
}

export function resolveTrustedBootstrap (options = {}) {
  const sessionId = String(options.sessionId || '').trim()
  const accounts = normalizeTrustedAccounts(options.accounts || [])
  if (sessionId) {
    return {
      mode: 'session',
      account: null,
      accounts,
    }
  }

  if (accounts.length === 1) {
    return {
      mode: 'auto',
      account: accounts[0],
      accounts,
    }
  }

  if (accounts.length > 1) {
    return {
      mode: 'chooser',
      account: null,
      accounts,
    }
  }

  return {
    mode: 'manual',
    account: null,
    accounts,
  }
}

export function resolveAuthBootFallbackState (options = {}) {
  const accounts = normalizeTrustedAccounts(options.accounts || [])
  const lastAccount = String(options.lastAccount || '').trim()
  const repairUser = String(options.repairUser || '').trim()

  if (options.restored) {
    return { view: 'dashboard', mode: 'authenticated', account: null, accounts }
  }

  if (options.blocked) {
    return { view: 'blocked', mode: 'blocked', account: null, accounts }
  }

  if (options.localInitializationIncomplete) {
    return {
      view: 'login',
      mode: 'repair',
      account: null,
      accounts,
      prefillUser: repairUser || lastAccount,
    }
  }

  const bootstrap = resolveTrustedBootstrap({ sessionId: '', accounts })
  if (bootstrap.mode === 'chooser') {
    return { view: 'login', mode: 'chooser', account: null, accounts: bootstrap.accounts }
  }

  return {
    view: 'login',
    mode: 'manual',
    account: null,
    accounts: bootstrap.accounts,
    prefillUser: lastAccount,
  }
}

export function shouldPromptToTrustAccount (options = {}) {
  const user = String(options.user || '').trim()
  const trustedLoginToken = String(options.trustedLoginToken || '').trim()
  if (!user || !trustedLoginToken) return false
  const accounts = normalizeTrustedAccounts(options.accounts || [])
  return !accounts.some(
    (entry) =>
      entry.user === user &&
      entry.trusted_login_token === trustedLoginToken,
  )
}
