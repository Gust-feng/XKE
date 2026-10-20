import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('../src/editions/one/one.css', import.meta.url), 'utf8')

describe('v22 stylesheet coverage', () => {
  it.each(['one-cap', 'one-cap-btn', 'one-cap-sync', 'one-pop', 'one-pop-terms', 'one-pop-sync',
    'toast-act', 'one-pop-c', 'one-nr', 'one-id', 'one-sf', 'nc-save', 'nc-undo'])('contains active styling for %s', (name) => {
    expect(css).toMatch(new RegExp(`\\.${name}[ .:{]`))
  })
  it('retains completed scan effects for rapid refresh continuation without replaying tick entrances', () => {
    expect(css).toMatch(/\.one-ticks\.is-syncing \.one-tick i\s*\{[^}]*animation: one-scan[^;]*infinite both;/s)
    expect(css).toMatch(/\.one-ticks\.has-refreshed:not\(\.is-syncing\) \.one-tick i\s*\{[^}]*animation: none;/s)
    expect(css).toMatch(/@keyframes one-scan\s*\{\s*0%, 100%\s*\{ opacity: 1;/s)
  })
  it('positions the semester menu independently of the ruler and removes browser numbering', () => {
    expect(css).toMatch(/\.one-pop\s*\{[^}]*position: absolute/s)
    expect(css).toMatch(/\.one-pop-terms\s*\{[^}]*list-style: none/s)
    expect(css).toContain('.one-ticks.is-syncing')
  })
})


describe('returned optimized design', () => {
  it('gives native dragging to the shell, not its navigation or window buttons', () => {
    const runtime = readFileSync(new URL('../src/styles/runtime.css', import.meta.url), 'utf8')
    expect(runtime).toMatch(/\.runtime-drag\s*\{[^}]*app-region: drag/s)
    expect(runtime).toMatch(/\.runtime-window\s*\{[^}]*app-region: no-drag/s)
    expect(runtime).toMatch(/\.one-toolbar\.is-desktop \.one-nav\s*\{[^}]*app-region: no-drag/s)
    expect(runtime).toContain('.runtime-window ~ .sheet .x')
  })
  it('keeps the navigation on the right without affecting the host window actions', () => {
    const runtime = readFileSync(new URL('../src/styles/runtime.css', import.meta.url), 'utf8')
    expect(runtime).toMatch(/\.one-toolbar \.one-nav\s*\{[^}]*margin-left: auto/s)
  })
  it('preserves solid lesson surfaces and separates elapsed time from the text', () => {
    expect(css).toMatch(/\.sw-cell\.is-past\s*\{[^}]*opacity: 1/s)
    expect(css).toContain('.sw-elapsed::after')
    expect(css).toContain('[data-pop] { visibility: hidden; }')
    expect(css).toContain('.one-pop-c dt,')
  })
})


describe('timetable header separation', () => {
  it('separates the time axis from the header without moving individual course times', () => {
    expect(css).toMatch(/\.sw-col\s*\{[^}]*margin-top: 12px/s)
    expect(css).not.toContain('.sw-col::before')
    expect(css).toMatch(/\.sw-week-rule\s*\{[^}]*top: 48px/s)
  })
  it('overlaps rule segments through the gutters and reveals them without scaled-edge seams', () => {
    expect(css).toContain('width: calc(100% + var(--g) + 1px)')
    expect(css).toMatch(/\.sw-week-rule i:last-child\s*\{[^}]*width: 100%/s)
    expect(css).toMatch(/@keyframes sw-rule-reveal[^\n]*clip-path: inset/s)
  })
})


describe('timetable edge outlines', () => {
  it('extends the outer clipping edge without widening or shifting the day grid', () => {
    expect(css).toMatch(/\.sw-week \.sw-days\s*\{[^}]*--sw-edge-bleed: 6px;[^}]*margin-right: calc\(var\(--sw-edge-bleed\) \* -1\)/s)
    expect(css).toMatch(/\.sw-week \.sw-days-in\s*\{[^}]*inset: 0 var\(--sw-edge-bleed\) 0 36px/s)
    expect(css).toMatch(/\.sw-days\s*\{[^}]*overflow: hidden/s)
  })
})


describe('whole-line lesson titles', () => {
  it('reserves the actual metadata height before sizing the title', () => {
    expect(css).toMatch(/\.sw-cell\s*\{[^}]*grid-template-rows: minmax\(0, 1fr\) auto/s)
    expect(css).toMatch(/\.sw-cell-title\s*\{[^}]*container-name: lesson-title/s)
  })
  it('clamps two or one full 20px lines to the title space rather than the card height', () => {
    expect(css).toMatch(/@container lesson-title \(height < 60px\)[\s\S]*?-webkit-line-clamp: 2/)
    expect(css).toMatch(/@container lesson-title \(height < 40px\)[\s\S]*?-webkit-line-clamp: 1/)
  })
})


describe('stable inline network settings', () => {
  it('reserves a discreet scrollbar gutter rather than changing selector width on overflow', () => {
    expect(css).toMatch(/\.one-nr\s*\{[^}]*scrollbar-gutter: stable;[^}]*scrollbar-color: var\(--mute\) transparent/s)
    expect(css).toMatch(/\.one-nr::-webkit-scrollbar-button\s*\{[^}]*display: none/s)
  })
  it('keeps the final four provider tiles fixed-height with visible keyboard focus', () => {
    expect(css).toMatch(/\.one-network-pop \.nc-seg\s*\{[^}]*grid-template-columns: repeat\(4, minmax\(0, 1fr\)\);[^}]*grid-auto-rows: 64px/s)
    expect(css).toMatch(/\.nc-seg button\s*\{[^}]*white-space: nowrap/s)
    expect(css).toMatch(/\.one-network-pop \.nc-seg button:focus-visible\s*\{[^}]*outline: 2px solid var\(--red\)/s)
  })
})


describe('network account refresh control', () => {
  it('centres the vector icon in a stable accessible target with inset focus', () => {
    expect(css).toMatch(/\.one-nr-refresh\s*\{[^}]*width: 44px; height: 44px;[^}]*align-items: center; justify-content: center/s)
    expect(css).toMatch(/\.one-nr-refresh:focus-visible\s*\{[^}]*outline-offset: -3px/s)
    expect(css).not.toContain('.one-nr-period')
  })
})


describe('large network status words', () => {
  it('sizes the word to its content region, keeping unknown visually distinct from offline', () => {
    expect(css).toMatch(/\.one-net\s*\{[^}]*container-type: inline-size/s)
    expect(css).toMatch(/\.one-net \.sw-word\s*\{[^}]*font-size: min\(19cqw, 26vh\)[^}]*min-height: 1em/s)
    expect(css).toMatch(/\.one-net.is-off \.sw-word\s*\{[^}]*-webkit-text-stroke: 2px/s)
    expect(css).toMatch(/\.one-net.is-unknown \.sw-word\s*\{[^}]*color: var\(--mute\);[^}]*-webkit-text-stroke: 0;[^}]*font-stretch: 70%/s)
  })
  it('allows full glyph advance and a stroke gutter instead of clipping outlined letters', () => {
    expect(css).toMatch(/\.sw-word \.tk-c\s*\{[^}]*height: 1em;[^}]*letter-spacing: 0;[^}]*padding: 0 2px/s)
  })
})


describe('final connection card controls', () => {
  it('uses one bottom focus rule rather than the superseded full input outline', () => {
    expect(css).toMatch(/\.nc-in:focus-visible\s*\{[^}]*outline: none;[^}]*box-shadow: inset 0 -2px 0 var\(--ink\)/s)
    expect(css).toMatch(/\.one-network-pop \.nc-credentials \.nc-in:focus\s*\{[^}]*box-shadow: inset 0 -3px 0 var\(--ink\)/s)
    expect(css).not.toMatch(/\.nc-in:focus-visible\s*\{[^}]*outline: 2px/s)
  })
  it('keeps custom stepping and makes room for the four-digit upper bound', () => {
    expect(css).toMatch(/\.one-network-pop \.nc-stepper\s*\{[^}]*grid-template-columns: 32px auto auto 32px/s)
    expect(css).toContain('appearance: textfield')
    expect(css).toMatch(/::-webkit-outer-spin-button\s*\{[^}]*-webkit-appearance: none/s)
    expect(css).toMatch(/\.one-network-pop \.nc-stepper \.nc-in\s*\{[^}]*width: calc\(4ch \+ 4px\)/s)
  })
  it('keeps form content bounded inside the card', () => {
    expect(css).toMatch(/\.one-network-body\s*\{[^}]*min-height: 0;[^}]*overflow-y: auto/s)
    expect(css).toMatch(/\.one-network-pop \.one-pop-in\s*\{[^}]*flex-direction: column;[^}]*overflow: hidden/s)
    expect(css).toContain('width: min(560px, calc(100vw - 48px))')
  })
  it('has no inactive rail entry or intermediate account-footer styling', () => {
    expect(css).not.toContain('.one-nr-cfg')
    expect(css).not.toContain('.one-nr-config')
    expect(css).not.toContain('.st-id')
    expect(css).toContain('.st-c { grid-column: 9 / span 4; grid-row: 1; }')
  })
})


describe('native dialog navigation boundary', () => {
  it('returns before global numeric navigation for events inside an open dialog', () => {
    const layout = readFileSync(new URL('../src/editions/one/OneLayout.tsx', import.meta.url), 'utf8')
    const guard = layout.indexOf(".closest('input, select, textarea, dialog[open]')) return")
    expect(guard).toBeGreaterThanOrEqual(0)
    expect(guard).toBeLessThan(layout.indexOf('const i = Number(e.key) - 1'))
    expect(layout).toContain("addEventListener('keydown', onKey, true)")
  })
})

describe('product identity', () => {
  it('uses the product name as the browser title', () => {
    const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8')
    expect(html).toContain('<title>XKE</title>')
  })
})
