// @vitest-environment happy-dom
import { act, createElement } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ScheduleRuler } from '../src/editions/one/ScheduleRuler'

function scan(time: number, delay = 0) {
  let finish!: () => void
  const finished = new Promise<void>(resolve => { finish = resolve })
  return {
    animationName: 'one-scan', currentTime: time, finished, finish, play: vi.fn(),
    effect: { getComputedTiming: () => ({ delay, duration: 1100 }), updateTiming: vi.fn() },
  }
}
let root: Root
let container: HTMLDivElement
let scans: ReturnType<typeof scan>[]
const render = (refreshing: boolean, reducedMotion = false, weeks = 20) => act(async () => {
  root.render(createElement(ScheduleRuler, { refreshing, reducedMotion, weeks }, createElement('i')))
})
const ruler = () => container.querySelector('.one-ticks')!
beforeEach(() => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true)
  container = document.createElement('div')
  document.body.append(container)
  root = createRoot(container)
  scans = [scan(20), scan(20, 855)]
  vi.spyOn(Element.prototype, 'getAnimations').mockImplementation(() => scans as unknown as Animation[])
})
afterEach(async () => {
  await act(async () => root.unmount())
  container.remove()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('schedule refresh scan lifecycle', () => {
  it('finishes every staggered tick even when the request returns immediately', async () => {
    await render(true)
    expect(ruler().classList.contains('is-syncing')).toBe(true)
    await render(false)
    for (const item of scans) expect(item.effect.updateTiming).toHaveBeenLastCalledWith({ iterations: 1 })
    expect(ruler().classList.contains('is-syncing')).toBe(true)
    await act(async () => scans[0].finish())
    expect(ruler().classList.contains('is-syncing')).toBe(true)
    await act(async () => scans[1].finish())
    expect(ruler().classList.contains('is-syncing')).toBe(false)
    expect(ruler().classList.contains('has-refreshed')).toBe(true)
  })

  it('finishes at the next full cycle after a long request, not a fixed minimum timeout', async () => {
    await render(true)
    scans[0].currentTime = 2400
    scans[1].currentTime = 2400
    await render(false)
    expect(scans[0].effect.updateTiming).toHaveBeenLastCalledWith({ iterations: 3 })
    expect(scans[1].effect.updateTiming).toHaveBeenLastCalledWith({ iterations: 2 })
  })

  it('continues a new refresh without resetting progress or accepting the previous finish callback', async () => {
    await render(true)
    await render(false)
    scans[0].currentTime = 400
    await render(true)
    for (const item of scans) {
      expect(item.effect.updateTiming).toHaveBeenLastCalledWith({ iterations: Infinity })
      expect(item.play).toHaveBeenCalledTimes(2)
    }
    expect(scans[0].currentTime).toBe(400)
    await act(async () => scans.forEach(item => item.finish()))
    expect(ruler().classList.contains('is-syncing')).toBe(true)
  })

  it('does not wait for disabled animations under reduced motion', async () => {
    await render(true, true)
    expect(ruler().classList.contains('is-syncing')).toBe(false)
    expect(Element.prototype.getAnimations).not.toHaveBeenCalled()
    await render(false, true)
    expect(ruler().classList.contains('is-syncing')).toBe(false)
  })

  it('releases the scan immediately when reduced motion is enabled mid-cycle', async () => {
    await render(true)
    await render(false)
    await render(false, true)
    expect(ruler().classList.contains('is-syncing')).toBe(false)
  })

  it('ignores completion after the ruler has unmounted', async () => {
    await render(true)
    await render(false)
    const previous = ruler()
    await act(async () => root.render(null))
    await act(async () => scans.forEach(item => item.finish()))
    expect(previous.classList.contains('is-syncing')).toBe(true)
    expect(container.childElementCount).toBe(0)
  })
})
