import { esc } from './format.js'

export function createHitokotoController ({
  card,
  content,
  requestTimeoutMs = 2800,
  refreshCooldownMs = 1600,
  bufferTarget = 1,
}) {
  let cache = null
  let queue = []
  let prefetchPromise = null
  let refreshing = false
  let stableHeight = 88
  let cooldownUntil = 0
  let cooldownTimer = null
  let blockedTimer = null

  function getNow () {
    if (window.performance && typeof window.performance.now === 'function') return window.performance.now()
    return Date.now()
  }

  async function fetchData () {
    const controller = typeof AbortController === 'function' ? new AbortController() : null
    const timeoutId = controller
      ? window.setTimeout(() => controller.abort(), requestTimeoutMs)
      : null
    try {
      const response = await fetch('https://v1.hitokoto.cn/?encode=json', controller ? { signal: controller.signal } : undefined)
      if (!response.ok) throw new Error(`Hitokoto request failed: ${response.status}`)
      const data = await response.json()
      return normalizeData({
        sentence: data.hitokoto || '',
        author: data.from || data.from_who || '',
      })
    } catch {
      return null
    } finally {
      if (timeoutId != null) window.clearTimeout(timeoutId)
    }
  }

  function normalizeData (data) {
    const sentence = String(data?.sentence || '').trim()
    const author = String(data?.author || '').trim()
    if (!sentence) return null
    return { sentence, author }
  }

  function buildMarkup (data) {
    const normalized = normalizeData(data)
    if (!normalized) return ''
    const { sentence, author } = normalized
    const sentenceLength = sentence.length
    let fontSize = 92
    if (sentenceLength <= 15) fontSize = 108
    else if (sentenceLength <= 25) fontSize = 100
    else if (sentenceLength <= 35) fontSize = 92
    else if (sentenceLength <= 45) fontSize = 86
    else if (sentenceLength <= 55) fontSize = 80
    else fontSize = 72

    let html = `<div class="quote-content"><div class="quote-sentence" style="font-size: ${fontSize}%">"${esc(sentence)}"</div>`
    html += author
      ? `<div class="quote-author">—— ${esc(author)}</div>`
      : '<div class="quote-author is-empty">&nbsp;</div>'
    html += '</div>'
    return html
  }

  function getSignature (data) {
    if (!data) return ''
    return `${data.sentence || ''}::${data.author || ''}`
  }

  function collectExclusions () {
    const signatures = new Set(queue.map((item) => getSignature(item)).filter(Boolean))
    const currentSignature = getSignature(cache)
    if (currentSignature) signatures.add(currentSignature)
    return signatures
  }

  async function render (data, { animate = true } = {}) {
    if (!content) return
    const html = buildMarkup(data)
    if (!html) return
    const current = content.firstElementChild
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

    if (!animate || !current || typeof current.animate !== 'function' || prefersReducedMotion) {
      content.innerHTML = html
      stableHeight = Math.max(stableHeight, content.offsetHeight, 88)
      content.style.minHeight = `${stableHeight}px`
      content.style.height = ''
      return
    }

    const lockedHeight = Math.max(stableHeight, content.offsetHeight, 88)
    content.style.minHeight = `${lockedHeight}px`
    content.style.height = `${lockedHeight}px`

    try {
      await current.animate([
        { opacity: 1, transform: 'translate3d(0, 0, 0)', filter: 'blur(0px)' },
        { opacity: 0, transform: 'translate3d(0, -8px, 0)', filter: 'blur(3px)' },
      ], {
        duration: 140,
        easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
        fill: 'forwards',
      }).finished
    } catch {
      /* ignore */
    }

    content.innerHTML = html
    const next = content.firstElementChild
    if (!next || typeof next.animate !== 'function') {
      content.style.height = ''
      return
    }

    const targetHeight = Math.max(content.offsetHeight, next.offsetHeight, 88)
    stableHeight = Math.max(stableHeight, lockedHeight, targetHeight)
    content.style.minHeight = `${stableHeight}px`
    content.style.height = `${stableHeight}px`

    try {
      await next.animate([
        { opacity: 0, transform: 'translate3d(0, 8px, 0)', filter: 'blur(4px)' },
        { opacity: 1, transform: 'translate3d(0, 0, 0)', filter: 'blur(0px)' },
      ], {
        duration: 220,
        easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
        fill: 'both',
      }).finished
    } catch {
      /* ignore */
    } finally {
      content.style.height = ''
    }
  }

  function setState (state) {
    if (!card) return
    card.classList.toggle('is-refreshing', state === 'refreshing')
    card.setAttribute('aria-busy', state === 'refreshing' ? 'true' : 'false')
  }

  function clearCooldownVisual () {
    if (!card) return
    card.classList.remove('is-cooling')
    card.style.removeProperty('--quote-cooldown-duration')
  }

  function restartCooldownVisual (duration) {
    if (!card || duration <= 0) return
    card.style.setProperty('--quote-cooldown-duration', `${Math.round(duration)}ms`)
    card.classList.remove('is-cooling')
    void card.offsetWidth
    card.classList.add('is-cooling')
  }

  function beginCooldown (duration = refreshCooldownMs) {
    const safeDuration = Math.max(0, Math.round(duration))
    if (cooldownTimer) {
      window.clearTimeout(cooldownTimer)
      cooldownTimer = null
    }
    if (!safeDuration) {
      cooldownUntil = 0
      clearCooldownVisual()
      return
    }

    cooldownUntil = getNow() + safeDuration
    restartCooldownVisual(safeDuration)
    cooldownTimer = window.setTimeout(() => {
      cooldownUntil = 0
      cooldownTimer = null
      clearCooldownVisual()
    }, safeDuration)
  }

  function flashCooldownHint () {
    if (!card) return
    const remaining = Math.max(0, cooldownUntil - getNow())
    if (remaining > 0) restartCooldownVisual(remaining)
    card.classList.remove('is-blocked')
    void card.offsetWidth
    card.classList.add('is-blocked')
    if (blockedTimer) window.clearTimeout(blockedTimer)
    blockedTimer = window.setTimeout(() => {
      card.classList.remove('is-blocked')
      blockedTimer = null
    }, 320)
  }

  async function prefetch (target = bufferTarget) {
    if (queue.length >= target) return queue
    if (prefetchPromise) return prefetchPromise
    prefetchPromise = (async () => {
      const excluded = collectExclusions()
      let attempts = 0
      while (queue.length < target && attempts < target * 4) {
        attempts += 1
        const candidate = await fetchData()
        const signature = getSignature(candidate)
        if (!signature || excluded.has(signature)) continue
        excluded.add(signature)
        queue.push(candidate)
      }
      return queue
    })().finally(() => {
      prefetchPromise = null
    })
    return prefetchPromise
  }

  async function takeNextQuote (preferQueue = true) {
    if (preferQueue) {
      if (!queue.length) await prefetch(1)
      const next = queue.shift()
      void prefetch()
      if (next) return next
    }

    const excluded = collectExclusions()
    let attempts = 0
    while (attempts < 2) {
      attempts += 1
      const data = await fetchData()
      const signature = getSignature(data)
      if (!signature || excluded.has(signature)) continue
      return data
    }
    return null
  }

  async function fetchNext (usePrefetch = false) {
    if (!content) return
    if (refreshing || getNow() < cooldownUntil) {
      flashCooldownHint()
      return
    }

    refreshing = true
    setState('refreshing')
    try {
      const nextQuotePromise = takeNextQuote(usePrefetch || queue.length > 0)
      const [, nextQuote] = await Promise.all([
        new Promise((resolve) => setTimeout(resolve, 160)),
        nextQuotePromise,
      ])

      if (nextQuote) {
        cache = nextQuote
        await render(cache, { animate: Boolean(content.firstElementChild) })
      }
    } finally {
      beginCooldown()
      void prefetch()
      setState('idle')
      refreshing = false
    }
  }

  function bind () {
    if (!card) return
    card.addEventListener('click', () => {
      fetchNext(true)
    })
  }

  return {
    bind,
    fetch: fetchNext,
    prefetch,
  }
}
