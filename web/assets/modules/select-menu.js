/**
 * Shared dropdown/combobox controller used by multiple settings panels.
 *
 * The controller keeps trigger text, option rendering, active state and
 * outside-click closing behavior in one place so feature modules only provide
 * their own option list and selection behavior.
 */
export function createSelectMenuController ({
  combobox,
  trigger,
  menu,
  getOptions,
  getValue,
  getTriggerLabel,
  onSelect,
  renderOptionContent,
  isDisabled = () => false,
  optionClassName = 'round-option',
}) {
  function close () {
    if (!menu || !trigger) return
    menu.classList.add('hidden')
    trigger.setAttribute('aria-expanded', 'false')
  }

  function open () {
    if (!menu || !trigger || isDisabled()) return
    menu.classList.remove('hidden')
    trigger.setAttribute('aria-expanded', 'true')
  }

  function syncTrigger () {
    if (!trigger) return
    trigger.textContent = getTriggerLabel(getValue(), getOptions())
    trigger.disabled = Boolean(isDisabled())
  }

  function render () {
    if (!menu) return
    const currentValue = getValue()
    menu.innerHTML = ''

    getOptions().forEach((option) => {
      const button = document.createElement('button')
      button.type = 'button'
      button.className = optionClassName
      button.dataset.value = option.value
      button.disabled = Boolean(option.disabled)
      button.classList.toggle('is-active', option.value === currentValue)
      button.classList.toggle('is-disabled', Boolean(option.disabled))
      button.setAttribute('aria-pressed', option.value === currentValue ? 'true' : 'false')

      if (typeof renderOptionContent === 'function') {
        button.innerHTML = renderOptionContent(option, currentValue)
      } else {
        button.textContent = option.label || option.value || ''
      }

      button.addEventListener('click', () => {
        if (option.disabled) return
        onSelect(option)
        render()
        syncTrigger()
        close()
      })

      menu.appendChild(button)
    })
  }

  function bind () {
    if (trigger) {
      trigger.addEventListener('click', () => {
        if (!menu) return
        if (menu.classList.contains('hidden')) open()
        else close()
      })
    }

    document.addEventListener('click', (event) => {
      if (!combobox || !menu || menu.classList.contains('hidden')) return
      if (combobox.contains(event.target)) return
      close()
    })
  }

  function refresh () {
    render()
    syncTrigger()
  }

  return {
    bind,
    close,
    open,
    refresh,
    render,
    syncTrigger,
  }
}
