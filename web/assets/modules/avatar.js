import { API } from './constants.js'
import { buildSessionHeaders } from './http.js'
import { clearCachedAvatarUrl, getCachedAvatarUrl, setCachedAvatarUrl } from './storage.js'

/**
 * Encapsulates avatar rendering + upload state.
 *
 * The main app only coordinates when to load/update profile data; the file
 * preview lifecycle and local persistence stay isolated here.
 */
export function createAvatarController ({ elements, getSessionId, log }) {
  const {
    profileAvatar,
    profileAvatarImg,
    profileAvatarIcon,
    btnAvatarUpload,
    inputAvatar,
  } = elements

  let currentAvatarUrl = ''
  let localAvatarObjectUrl = ''
  let remoteAvatarObjectUrl = ''
  let remoteAvatarRequestToken = 0

  function revokeLocalAvatarObjectUrl () {
    if (!localAvatarObjectUrl) return
    try {
      URL.revokeObjectURL(localAvatarObjectUrl)
    } catch {
      /* ignore */
    }
    localAvatarObjectUrl = ''
  }

  function revokeRemoteAvatarObjectUrl () {
    if (!remoteAvatarObjectUrl) return
    try {
      URL.revokeObjectURL(remoteAvatarObjectUrl)
    } catch {
      /* ignore */
    }
    remoteAvatarObjectUrl = ''
  }

  function showAvatarSource (src) {
    if (!profileAvatarImg || !profileAvatarIcon) return
    profileAvatarImg.onload = null
    profileAvatarImg.onerror = null
    profileAvatarImg.src = src
    profileAvatarImg.classList.remove('hidden')
    profileAvatarIcon.classList.add('hidden')
    profileAvatar.classList.add('has-image')
  }

  async function loadRemoteAvatar (url, fallbackUrl = '') {
    const sessionId = getSessionId()
    if (!sessionId) return
    const requestToken = remoteAvatarRequestToken + 1
    remoteAvatarRequestToken = requestToken
    try {
      const response = await fetch(url, {
        headers: buildSessionHeaders(sessionId),
      })
      if (!response.ok) throw new Error(`HTTP ${response.status}`)
      const blob = await response.blob()
      const objectUrl = URL.createObjectURL(blob)
      if (requestToken !== remoteAvatarRequestToken) {
        URL.revokeObjectURL(objectUrl)
        return
      }
      revokeRemoteAvatarObjectUrl()
      remoteAvatarObjectUrl = objectUrl
      revokeLocalAvatarObjectUrl()
      showAvatarSource(remoteAvatarObjectUrl)
    } catch {
      if (fallbackUrl && fallbackUrl !== url) {
        applyAvatar(fallbackUrl, {
          ephemeral: /^blob:|^data:/i.test(fallbackUrl),
        })
        return
      }
      applyAvatar('')
    }
  }

  function applyAvatar (url = '', options = {}) {
    const previousAvatarUrl = currentAvatarUrl
    currentAvatarUrl = url || ''
    if (currentAvatarUrl && !/^blob:|^data:/i.test(currentAvatarUrl)) {
      setCachedAvatarUrl(currentAvatarUrl)
    }
    if (!currentAvatarUrl) {
      clearCachedAvatarUrl()
    }
    if (!profileAvatarImg || !profileAvatarIcon) return
    if (currentAvatarUrl) {
      const isEphemeral = Boolean(options.ephemeral) || /^blob:|^data:/i.test(currentAvatarUrl)
      const fallbackUrl = String(options.fallbackUrl || previousAvatarUrl || '').trim()
      if (isEphemeral) {
        revokeRemoteAvatarObjectUrl()
        showAvatarSource(currentAvatarUrl)
      } else {
        void loadRemoteAvatar(currentAvatarUrl, fallbackUrl)
      }
      return
    }
    remoteAvatarRequestToken += 1
    profileAvatarImg.onload = null
    profileAvatarImg.onerror = null
    profileAvatarImg.removeAttribute('src')
    profileAvatarImg.classList.add('hidden')
    profileAvatarIcon.classList.remove('hidden')
    profileAvatar.classList.remove('has-image')
    revokeLocalAvatarObjectUrl()
    revokeRemoteAvatarObjectUrl()
  }

  async function uploadAvatarFile (file) {
    const sessionId = getSessionId()
    if (!sessionId || !file) return
    const previousAvatarUrl = currentAvatarUrl
    if (!/^image\//i.test(file.type || '')) {
      log('头像上传失败: 请选择图片文件')
      return
    }
    revokeLocalAvatarObjectUrl()
    localAvatarObjectUrl = URL.createObjectURL(file)
    applyAvatar(localAvatarObjectUrl, { ephemeral: true })
    const body = new FormData()
    body.append('file', file)
    btnAvatarUpload.disabled = true
    try {
      const response = await fetch(`${API}/auth/avatar`, {
        method: 'POST',
        headers: buildSessionHeaders(sessionId),
        body,
      })
      const data = await response.json().catch(() => ({}))
      if (!response.ok) throw new Error(data.detail || `HTTP ${response.status}`)
      applyAvatar(data.data && data.data.avatar_url ? data.data.avatar_url : '', {
        fallbackUrl: localAvatarObjectUrl || previousAvatarUrl,
      })
      log('头像已更新')
    } catch (error) {
      if (previousAvatarUrl) applyAvatar(previousAvatarUrl)
      else applyAvatar('')
      log(`头像上传失败: ${error.message}`)
    } finally {
      btnAvatarUpload.disabled = false
      inputAvatar.value = ''
    }
  }

  function bind () {
    if (!btnAvatarUpload || !inputAvatar) return
    btnAvatarUpload.addEventListener('click', () => {
      if (!getSessionId()) return
      inputAvatar.click()
    })
    inputAvatar.addEventListener('change', () => {
      const [file] = inputAvatar.files || []
      if (file) uploadAvatarFile(file)
    })
  }

  function restoreCachedAvatar () {
    const cachedUrl = getCachedAvatarUrl()
    if (cachedUrl && getSessionId()) applyAvatar(cachedUrl)
  }

  return {
    bind,
    applyAvatar,
    restoreCachedAvatar,
    getCurrentAvatarUrl: () => currentAvatarUrl,
  }
}
