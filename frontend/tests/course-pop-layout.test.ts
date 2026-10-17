import { describe, expect, it } from 'vitest'
import { cardBounds, popupBounds, popupWidth } from '../src/editions/one/course-pop-layout'

describe('course popup geometry', () => {
  it('supports the network form width without changing the course default or overflowing a narrow viewport', () => {
    const source = { left: 40, top: 580, width: 260, height: 48 }
    expect(popupWidth(source, { width: 1100, height: 656 })).toBe(360)
    expect(popupBounds(source, { width: 1100, height: 656 }, 500, 420)).toEqual({ left: 40, top: 132, width: 420, height: 500 })
    expect(popupWidth(source, { width: 400, height: 656 }, 420)).toBe(352)
  })
  const viewport = { width: 2560, height: 1406 }
  it('retains both horizontal corners of a sidebar card wider than the detail default', () => {
    const source = { left: 86, top: 411, width: 565, height: 302 }
    const target = popupBounds(source, viewport, 340)
    expect(target.left).toBe(source.left)
    expect(target.width).toBe(source.width)
    expect(target.left + target.width).toBe(source.left + source.width)
    expect(target.top).toBe(source.top)
  })
  it('expands a narrow timetable cell with a stable left corner when space allows', () => {
    const source = { left: 740, top: 360, width: 180, height: 100 }
    expect(popupBounds(source, viewport, 340)).toEqual({ left: 740, top: 360, width: 360, height: 340 })
  })
  it('anchors a rightmost cell at its right edge instead of overflowing the window', () => {
    const source = { left: 2310, top: 360, width: 200, height: 100 }
    const target = popupBounds(source, viewport, 340)
    expect(target.left + target.width).toBe(2510)
    expect(target.left).toBe(2150)
  })
  it('moves a bottom cell only as far as the actual viewport requires', () => {
    expect(popupBounds({ left: 100, top: 1280, width: 400, height: 80 }, viewport, 320).top).toBe(1062)
  })
  it('limits a wide source to the available viewport without negative width', () => {
    const source = { left: 20, top: 80, width: 565, height: 200 }
    expect(popupWidth(source, { width: 420, height: 720 })).toBe(372)
    expect(popupBounds(source, { width: 420, height: 720 }, 320).left).toBe(24)
  })
  it('starts and ends the morph with actual painted bounds, even when the detail is narrower', () => {
    const source = { left: 40, top: 40, width: 600, height: 260 }
    expect(cardBounds({ ...source })).toEqual(source)
    // All four edges, not negative insets on a smaller drawing surface.
    expect(popupBounds(source, { width: 520, height: 720 }, 300)).toEqual({ left: 24, top: 40, width: 472, height: 300 })
  })
  it('covers a Sunday card inside the usual right margin', () => {
    const source = { left: 1014, top: 120, width: 68, height: 140 }
    const bounds = popupBounds(source, { width: 1084, height: 720 }, 250)
    expect(bounds.left + bounds.width).toBe(source.left + source.width)
    expect(bounds.left).toBeLessThanOrEqual(source.left)
  })
  it('covers a source flush with either viewport edge', () => {
    for (const left of [0, 1016]) {
      const source = { left, top: 120, width: 68, height: 140 }
      const bounds = popupBounds(source, { width: 1084, height: 720 }, 250)
      expect(bounds.left).toBeLessThanOrEqual(left)
      expect(bounds.left + bounds.width).toBeGreaterThanOrEqual(left + source.width)
      expect(bounds.left + bounds.width).toBeLessThanOrEqual(1084)
    }
  })

})
