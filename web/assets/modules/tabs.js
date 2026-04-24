/**
 * Minimal tab-panel controller.
 *
 * Missing panels are treated as reserved placeholders so unfinished modules
 * can stay visible in the nav without breaking the current page.
 */
export function createTabsController ({
  tabs,
  panels,
  resolvePanel,
  onChange,
  onMissingPanel,
}) {
  function activate (tab) {
    const panel = resolvePanel(tab)
    if (!panel) {
      if (typeof onMissingPanel === 'function') onMissingPanel(tab)
      return
    }

    tabs.forEach((item) => item.classList.remove('active'))
    panels.forEach((item) => item.classList.remove('active'))

    tab.classList.add('active')
    panel.classList.add('no-anim')
    panel.classList.add('active')
    requestAnimationFrame(() => requestAnimationFrame(() => panel.classList.remove('no-anim')))

    if (typeof onChange === 'function') onChange(tab, panel)
  }

  function bind () {
    tabs.forEach((tab) => {
      tab.addEventListener('click', () => activate(tab))
    })
  }

  return {
    activate,
    bind,
  }
}
