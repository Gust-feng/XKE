import { DEFAULT_THEME_ID, THEME_OPTIONS } from './constants.js'
import { getThemeFallback, saveThemeFallback } from './storage.js'

const FALLBACK_THEME = {
  id: DEFAULT_THEME_ID,
  label: '默认主题',
  description: '使用内置基础主题。',
  badge: '基础',
  colorScheme: 'light',
  preview: 'linear-gradient(135deg, #eef5ff 0%, #ffffff 52%, #dbeafe 100%)',
  swatches: ['#2563eb', '#f8fbff', '#dbeafe'],
}

function getThemeOptions () {
  return Array.isArray(THEME_OPTIONS) && THEME_OPTIONS.length ? THEME_OPTIONS : [FALLBACK_THEME]
}

function findTheme (themeId) {
  const options = getThemeOptions()
  return options.find((item) => item.id === themeId) || options[0]
}

function prefersReducedMotion () {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export function resolveThemeId (themeId) {
  return findTheme(themeId || DEFAULT_THEME_ID).id
}

export function createThemeController ({ root, grid, summaryName, summaryScope, summaryMeta }) {
  let activeThemeId = resolveThemeId(root.dataset.theme || getThemeFallback() || DEFAULT_THEME_ID)
  let transitionTimer = null

  function runThemeTransition (theme, options = {}) {
    const { animate = false } = options
    if (!animate || prefersReducedMotion()) return
    document.body.classList.remove('theme-switching')
    const activeButton = grid ? grid.querySelector(`[data-theme-option="${theme.id}"]`) : null
    if (activeButton) activeButton.classList.remove('is-committing')
    void document.body.offsetWidth
    document.body.classList.add('theme-switching')
    if (activeButton) {
      void activeButton.offsetWidth
      activeButton.classList.add('is-committing')
    }

    if (transitionTimer) clearTimeout(transitionTimer)
    transitionTimer = window.setTimeout(() => {
      document.body.classList.remove('theme-switching')
      if (activeButton) activeButton.classList.remove('is-committing')
    }, 320)
  }

  function syncSummary (scopeText = '') {
    const theme = findTheme(activeThemeId)
    if (summaryName) summaryName.textContent = theme.label
    if (summaryMeta) summaryMeta.textContent = theme.description
    if (summaryScope && scopeText) summaryScope.textContent = scopeText
  }

  function markSelectedState () {
    if (!grid) return
    grid.querySelectorAll('[data-theme-option]').forEach((button) => {
      const selected = button.dataset.themeOption === activeThemeId
      button.classList.toggle('is-active', selected)
      button.setAttribute('aria-pressed', selected ? 'true' : 'false')
    })
  }

  function applyTheme (themeId, options = {}) {
    const { persist = true, scopeText = '', animate = false } = options
    const theme = findTheme(themeId)
    const shouldAnimate = animate && theme.id !== activeThemeId
    activeThemeId = theme.id
    root.dataset.theme = theme.id
    root.style.colorScheme = theme.colorScheme || 'light'
    if (persist) saveThemeFallback(theme.id)
    markSelectedState()
    syncSummary(scopeText)
    runThemeTransition(theme, { animate: shouldAnimate })
    return theme
  }

  function render (onSelect) {
    if (!grid) return
    grid.innerHTML = ''
    getThemeOptions().forEach((theme) => {
      const button = document.createElement('button')
      button.type = 'button'
      button.className = 'theme-option'
      button.dataset.themeOption = theme.id
      button.setAttribute('aria-pressed', 'false')
      button.style.setProperty('--theme-accent', theme.swatches[0] || 'var(--primary)')

      const preview = document.createElement('div')
      preview.className = 'theme-option-preview'
      if (theme.preview) preview.style.background = theme.preview

      if (theme.badge) {
        const badge = document.createElement('span')
        badge.className = 'theme-option-badge'
        badge.textContent = theme.badge
        preview.appendChild(badge)
      }

      theme.swatches.forEach((color) => {
        const swatch = document.createElement('span')
        swatch.className = 'theme-option-swatch'
        swatch.style.background = color
        preview.appendChild(swatch)
      })

      const title = document.createElement('div')
      title.className = 'theme-option-title'
      title.textContent = theme.label

      const description = document.createElement('div')
      description.className = 'theme-option-description'
      description.textContent = theme.description

      button.append(preview, title, description)
      button.addEventListener('click', () => onSelect(theme.id))
      grid.appendChild(button)
    })
    markSelectedState()
    syncSummary()
  }

  function getActiveThemeId () {
    return activeThemeId
  }

  return {
    applyTheme,
    render,
    getActiveThemeId,
    getActiveTheme: () => findTheme(activeThemeId),
  }
}
