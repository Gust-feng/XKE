import { afterEach, describe, expect, it, vi } from 'vitest'

vi.hoisted(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance', 'Date'] })
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => setTimeout(() => callback(performance.now()), 8))
  vi.stubGlobal('cancelAnimationFrame', clearTimeout)
})

import { createSemesterMotion } from '../src/editions/one/semester-motion'
import type { SemesterMotion } from '../src/editions/one/semester-motion'

const controllers: SemesterMotion[] = []
function setup(delays = [0, 240, 480]) {
  const positions = delays.map(() => 0)
  const state = vi.fn()
  const render = vi.fn()
  const controller = createSemesterMotion(delays.map((delay, i) => ({ delay, render(value) { positions[i] = value; render(i, value) } })), state)
  controllers.push(controller)
  return { controller, positions, state, render }
}
const advance = (ms: number) => vi.advanceTimersByTimeAsync(ms)
afterEach(async () => {
  controllers.splice(0).forEach(controller => controller.destroy())
  await advance(32)
})

describe('semester dot controlled spring handoff', () => {
  it('keeps entrance stagger and reads the complete glyph before automatically restoring', async () => {
    const { controller, positions, state } = setup()
    controller.toggle()
    await advance(160)
    expect(positions[0]).toBeGreaterThan(0)
    expect(positions.slice(1)).toEqual([0, 0])
    await advance(1440)
    expect(positions).toEqual([1, 1, 1])
    await advance(1800)
    expect(state).toHaveBeenLastCalledWith('in')
    await advance(2500)
    expect(positions).toEqual([0, 0, 0])
    expect(state).toHaveBeenLastCalledWith(null)
  })

  it('does not turn halfway; joins only near 88% with continuous position and incoming velocity', async () => {
    const { controller, positions, state } = setup([0])
    controller.toggle()
    await advance(160)
    const halfway = positions[0]
    expect(halfway).toBeLessThan(0.6)
    controller.toggle()
    expect(state).toHaveBeenCalledTimes(1)
    expect(positions[0]).toBe(halfway)
    await advance(120)
    expect(state).toHaveBeenLastCalledWith('in')
    for (let n = 0; n < 80 && state.mock.calls.length === 1; n++) await advance(8)
    expect(state).toHaveBeenLastCalledWith('out')
    const atJoin = positions[0]
    expect(atJoin).toBeGreaterThanOrEqual(0.88)
    expect(atJoin).toBeLessThan(0.92)
    await advance(8)
    expect(positions[0]).toBeGreaterThan(atJoin) // no abrupt velocity reversal
    expect(positions[0] - atJoin).toBeLessThan(0.03)
    await advance(1600)
    expect(positions[0]).toBe(0)
    expect(state).toHaveBeenLastCalledWith(null)
  })

  it('coalesces middle-leg bursts into exactly one pending switch, without restarting departures', async () => {
    const { controller, positions, state } = setup()
    controller.toggle()
    await advance(120)
    for (let i = 0; i < 20; i++) {
      controller.toggle()
      expect(state).toHaveBeenCalledTimes(1)
      await advance(8)
    }
    expect(positions[1]).toBeGreaterThan(0) // original 240ms departure was not postponed by clicks
    expect(positions[2]).toBe(0)
    await advance(300)
    expect(state).toHaveBeenLastCalledWith('in') // last staggered dot still mid-flight
    await advance(400)
    expect(state.mock.calls.map(([value]) => value)).toEqual(['in', 'out'])
    await advance(1800)
    expect(positions).toEqual([0, 0, 0])
    expect(state).toHaveBeenLastCalledWith(null)
    const count = state.mock.calls.length
    await advance(6000)
    expect(state).toHaveBeenCalledTimes(count) // no queued/infinite ping-pong
  })

  it('also protects the return and joins reentry near its end', async () => {
    const { controller, positions, state } = setup([0])
    controller.toggle()
    await advance(1500)
    controller.toggle()
    await advance(160)
    const halfway = positions[0]
    for (let n = 0; n < 10; n++) controller.toggle()
    expect(state).toHaveBeenLastCalledWith('out')
    expect(positions[0]).toBe(halfway)
    for (let n = 0; n < 80 && state.mock.calls.length === 2; n++) await advance(8)
    expect(state).toHaveBeenLastCalledWith('in')
    expect(positions[0]).toBeLessThanOrEqual(0.12)
    const atJoin = positions[0]
    await advance(8)
    expect(positions[0]).toBeLessThan(atJoin)
    await advance(1600)
    expect(positions[0]).toBe(1)
  })

  it('same-frame burst requests one controlled next leg, not parity cancellation', async () => {
    const { controller, positions, state } = setup([0])
    for (let i = 0; i < 30; i++) controller.toggle()
    expect(state.mock.calls).toEqual([['in']])
    await advance(2000)
    expect(positions[0]).toBe(0)
    expect(state.mock.calls.map(([value]) => value)).toEqual(['in', 'out', null])
  })

  it('continuous clicking starts bounded legs and settles after clicks stop', async () => {
    const { controller, positions, state } = setup([0])
    for (let i = 0; i < 100; i++) { controller.toggle(); await advance(20) }
    expect(state.mock.calls.length).toBeLessThan(10)
    await advance(8000)
    expect(positions[0]).toBe(0)
    expect(state).toHaveBeenLastCalledWith(null)
    const count = state.mock.calls.length
    await advance(6000)
    expect(state).toHaveBeenCalledTimes(count)
  })

  it('Escape overrides the gate, clears pending reentry, and never restarts an existing return', async () => {
    const { controller, positions, state } = setup([0])
    controller.toggle()
    await advance(120)
    controller.restore()
    expect(state).toHaveBeenLastCalledWith('out')
    controller.toggle()
    controller.restore()
    controller.restore()
    expect(state.mock.calls.map(([value]) => value)).toEqual(['in', 'out'])
    await advance(6000)
    expect(positions[0]).toBe(0)
    expect(state.mock.calls.map(([value]) => value)).toEqual(['in', 'out', null])
  })

  it('reset cancels pending switch, departures and reading timer, then allows a fresh stagger', async () => {
    const { controller, positions, state } = setup()
    controller.toggle()
    await advance(120)
    controller.toggle()
    controller.reset()
    expect(positions).toEqual([0, 0, 0])
    expect(state).toHaveBeenLastCalledWith(null)
    await advance(6000)
    expect(state.mock.calls.map(([value]) => value)).toEqual(['in', null])
    controller.toggle()
    await advance(160)
    expect(positions[0]).toBeGreaterThan(0)
    expect(positions.slice(1)).toEqual([0, 0])
  })

  it('destroy prevents late render/state callbacks and interactions with a pending switch', async () => {
    const { controller, state, render } = setup()
    controller.toggle()
    await advance(120)
    controller.toggle()
    controller.destroy()
    state.mockClear()
    render.mockClear()
    controller.toggle()
    controller.restore()
    controller.reset()
    await advance(6000)
    expect(render).not.toHaveBeenCalled()
    expect(state).not.toHaveBeenCalled()
  })
})
