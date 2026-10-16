import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { useStore } from '../src/lib/store'
import { adaptSemester, adaptSettings, adaptWeek } from '../src/api/adapters'
import { account, auth, row, semester, week } from './fixtures'
import { courseBlocks } from '../src/lib/course-blocks'
import { combineSchedule } from '../src/lib/schedule-state'
import type { DesktopBridge } from '../src/lib/desktop'

const state = vi.hoisted(() => {
  vi.stubGlobal('__XKE_PREVIEW__', false)
  return { current: {} as ReturnType<typeof useStore>, desktop: { bridge: undefined as DesktopBridge | undefined, maximized: false } }
})
vi.mock('../src/lib/store', () => ({ useStore: () => state.current }))
// SSR has no desktop window; exercise the actual edition and account components.
vi.mock('../src/lib/desktop', () => ({ useDesktopBridge: () => state.desktop }))
import App from '../src/App'
import { TermCap } from '../src/editions/one/SwissWeek'

const emptyStore = (): ReturnType<typeof useStore> => ({
  preview: false, phase: 'anonymous', user: null, authOpen: false, authBusy: false, authError: '', captcha: null,
  schedule: null, get semesterSchedule() { return this.schedule?.scope === 'semester' ? this.schedule : null }, get currentSchedule() { return this.schedule }, browsing: false, settings: null, net: null, netBusy: false, netError: '', syncing: false, refreshing: false, scheduleError: '', selectedWeek: 1,
  weekLoaded: false, jobs: null, jobsError: '', networkConfig: null, networkSummary: null, summaryBusy: false, summaryError: '', view: 'week', sheet: null, toast: null,
  setView: vi.fn(), openSheet: vi.fn(), notify: vi.fn(), login: vi.fn(), connect: vi.fn(), submitCaptcha: vi.fn(),
  openAuth: vi.fn(), cancelAuth: vi.fn(), logout: vi.fn(), sync: vi.fn(), selectWeek: vi.fn(), selectTerm: vi.fn(),
  refreshNet: vi.fn(), toggleNet: vi.fn(), kick: vi.fn(), updateSettings: vi.fn(), loadNetworkConfig: vi.fn(),
  saveNetworkConfig: vi.fn(), refreshJobs: vi.fn(), loadNetworkSummary: vi.fn(),
})
const render = () => renderToStaticMarkup(createElement(App))
const workspace = () => ({ student: auth().student, connected: false, settings: adaptSettings(auth().settings, auth().student.student_id), schedule: adaptWeek(week()) })

beforeEach(() => { state.current = emptyStore(); state.desktop = { bridge: undefined, maximized: false } })
afterEach(() => { vi.useRealTimers() })

describe('personal app entry', () => {
  it('restores the workspace without briefly exposing the app or asking for credentials', () => {
    state.current.phase = 'loading'
    const html = render()
    expect(html).toContain('role="status"')
    expect(html).toContain('auth-spinner')
    expect(html).not.toContain('class="sw one"')
    expect(html).not.toContain('name="password"')
  })

  it.each(['week', 'settings', 'net'] as const)('requires initial authentication before accessing %s', (view) => {
    state.current.view = view
    const html = render()
    expect(html).toContain('auth-surface')
    expect(html).toContain('name="username"')
    expect(html).toContain('name="password"')
    expect(html).toContain('>认证</button>')
    expect(html).not.toContain('class="sw one"')
    expect(html).not.toContain('aria-label="视图"')
    expect(html).not.toContain('首次登录完成')
    expect(html).not.toContain('可离线恢复')
    expect(html).not.toContain('前往设置')
    expect(html).not.toContain('注销')
  })

  it('initial captcha remains part of authentication and can be cancelled without exposing the app', () => {
    state.current.captcha = { kind: 'captcha', purpose: 'login', id: 'synthetic-captcha', image: 'data:image/png;base64,AAAA', expiresAt: Date.now() + 60_000 }
    const html = render()
    expect(html).toContain('name="captcha"')
    expect(html).toContain('>取消</button>')
    expect(html).not.toContain('class="sw one"')
    state.current.captcha = null
    expect(render()).toContain('name="password"')
    expect(render()).not.toContain('class="sw one"')
  })

  it('expired captcha provides a retry action and disables confirmation', () => {
    state.current.captcha = { kind: 'captcha', purpose: 'login', id: 'expired-captcha', image: 'data:image/png;base64,AAAA', expiresAt: 0 }
    const html = render()
    expect(html).toContain('已过期')
    expect(html).toContain('>重试</button>')
    expect(html).toMatch(/<button type="submit"[^>]*disabled=""/)
    expect(html).not.toContain('请重新发起')
  })

  it('shows authentication errors without adding instructional text', () => {
    state.current.authError = '账号或密码错误'
    const html = render()
    expect(html).toContain('role="alert"')
    expect(html).toContain('账号或密码错误')
    expect(html).not.toContain('首次登录完成')
  })

  it('a connected workspace does not open account controls automatically; settings contains connection and logout', () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', view: 'settings', user, settings: user.settings, schedule: user.schedule })
    const html = render()
    const nav = html.slice(html.indexOf('<nav'), html.indexOf('</nav>'))
    expect(html).toContain('注销')
    expect(nav).not.toContain('注销')
    expect(nav).not.toContain('重新连接')
    expect(html).not.toContain('role="dialog"')
    expect(html).not.toContain('连接后读取')
    expect(nav).not.toContain('本地工作区')
    expect(nav).not.toContain('教务已连接')
    state.current.authOpen = true
    expect(render()).toContain('使用已存账号')
  })

  it('a continuation captcha renders over the main edition with cancellation available', () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule })
    state.current.captcha = { kind: 'captcha', purpose: 'schedule', id: 'synthetic-captcha', image: 'data:image/png;base64,AAAA', expiresAt: Date.now() + 60_000 }
    const html = render()
    expect(html).toContain('class="sw one"')
    expect(html).toContain('role="dialog"')
    expect(html).toContain('name="captcha"')
    expect(html).toContain('>取消</button>')
  })

  it('cancelling a connection preserves the authenticated workspace; logout returns to authentication', () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule })
    state.current.view = 'settings'; state.current.authOpen = true
    expect(render()).toContain('role="dialog"')
    state.current.authOpen = false
    const html = render()
    expect(html).toContain('class="sw one"')
    expect(html).not.toContain('role="dialog"')
    state.current = emptyStore()
    expect(render()).toContain('name="password"')
    expect(render()).not.toContain('class="sw one"')
  })

  it('a restored workspace without a schedule uses its action button rather than instructions', () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings })
    const html = render()
    expect(html).toContain('class="sw one"')
    expect(html).toContain('>获取课表</button>')
    expect(html).not.toContain('在设置中连接')
    expect(html).not.toContain('name="password"')
  })

  it('explicit preview keeps its existing settings and avoids runtime account controls', () => {
    const user = workspace()
    Object.assign(state.current, { preview: true, phase: 'ready', view: 'settings', user, settings: user.settings, schedule: user.schedule })
    const html = render()
    expect(html).toContain('class="sw one"')
    expect(html).toContain('role="switch"')
    expect(html).not.toContain('注销')
    expect(html).not.toContain('role="dialog"')
  })
})

describe('ended courses in the week view', () => {
  function viewWeek(selectedWeek: number, mode: 'dim' | 'hide', monday: string) {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 27, 16)) // Sunday of week 3.
    const user = workspace()
    Object.assign(state.current, {
      phase: 'ready', user, settings: { ...user.settings, inactiveCourseMode: mode },
      schedule: adaptWeek(week(selectedWeek, { week_dates: { 星期一: monday } })), selectedWeek, weekLoaded: true,
    })
    return render()
  }

  it('keeps ended courses visible in the current week with dim selected', () => {
    const html = viewWeek(3, 'dim', '2026-09-21')
    expect(html).toMatch(/class="sw-cell [^"]*is-past/)
    expect(html).not.toContain('本周无课')
  })

  it('historical weeks remain readable even when hiding ended courses is selected', () => {
    const html = viewWeek(1, 'hide', '2026-09-07')
    expect(html).toMatch(/class="sw-cell [^"]*is-past/)
    expect(html).not.toContain('本周课程已结束')
  })

  it('distinguishes hidden ended classes from a week with no classes', () => {
    const html = viewWeek(3, 'hide', '2026-09-21')
    expect(html).not.toContain('class="sw-cell ')
    expect(html).toContain('本周课程已结束')
    expect(html).not.toContain('本周无课')
  })

  it('hiding ended courses leaves future weeks visible', () => {
    const html = viewWeek(4, 'hide', '2026-09-28')
    expect(html).toContain('class="sw-cell ')
    expect(html).not.toMatch(/class="sw-cell [^"]*is-past/)
  })
})

describe('focused runtime surfaces', () => {
  const ready = () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule })
    return user
  }

  it('puts refresh beside the semester context without redundant timetable labels or hover text', () => {
    ready()
    const html = render()
    expect(html).toContain('class="one-toolbar"')
    const cap = renderToStaticMarkup(createElement(TermCap))
    expect(cap).toContain('大一上学期')
    expect(cap).toContain('class="one-cap"')
    expect(html).not.toContain('one-context')
    expect(html).not.toContain('<select')
    expect(html).not.toContain('周课表')
    expect(html).not.toContain('本地缓存')
    expect(html).not.toContain('刷新本周')
    expect(html).not.toContain(' title=')
  })

  it('keeps only personal terms in settings', () => {
    ready()
    Object.assign(state.current, { view: 'settings', schedule: adaptSemester(semester({ available_terms: [
      { value: '2026-2027-1', label: '秋', selected: true },
      { value: '2025-2026-2', label: '春', selected: false },
      { value: '2016-2017-2', label: '旧', selected: false },
    ] })) })
    state.current.user!.student.enrollment_date = '2025-09-01'
    const html = render()
    expect(html).toContain('aria-label="查看学期"')
    expect(html).toContain('大一下学期')
    expect(html).not.toContain('2016-2017-2')
    expect(html).not.toContain(' title=')
  })

  it.each(['unknown', 'updating', 'offline', 'online', 'refreshing'] as const)('network %s keeps its controls and content surface', (mode) => {
    ready()
    state.current.view = 'net'
    state.current.netBusy = mode === 'updating' || mode === 'refreshing'
    if (mode === 'online' || mode === 'offline' || mode === 'refreshing') state.current.net = {
      online: mode !== 'offline', account: 'synthetic-account', ip: '192.0.2.2', since: null, usedGB: null,
      devices: [], campusConnected: true, devicesSupported: false, disconnectSupported: false,
    }
    const html = render()
    expect(html).toContain('class="sw-net sw-grid')
    expect(html).toContain('class="one-rock sw-in"')
    expect(html).toContain('class="sw-word sw-giant" aria-hidden="true"')
    expect(html).not.toContain('aria-label="—"')
    if (mode === 'online') expect(html).toContain('aria-label="ONLINE"')
    if (mode === 'offline') expect(html).toContain('aria-label="OFFLINE"')
    if (mode === 'unknown') expect(html).toContain('aria-label="CHECKING"')
    expect(html).not.toContain('class="sw-word sw-giant" aria-hidden="true"></div>')
    expect(html).not.toContain('<span class="tk"></span>')
    expect(html).toContain('class="sr-only" role="status" aria-live="polite"')
    expect(html).not.toContain('class="one-nr-cfg"')
    expect(html).toContain('>内网')
    expect(html).toContain('>外网')
    expect(html).not.toContain('runtime-placeholder')
    expect(html).not.toContain('检测校园网')
    expect(html).not.toContain('重新检测')
    expect(html).not.toContain('手动保活')
    expect(html).not.toContain('流量暂不可用')
    expect(html).not.toContain('设备暂不支持')
    expect(html).not.toContain(' title=')
    if (mode === 'online') expect(html).toMatch(/class="one-rock sw-in"[^>]*disabled=""/)
  })

  it('a network request failure remains inline on the same surface with configuration available', () => {
    ready()
    Object.assign(state.current, { view: 'net', netError: '连接失败，请重试' })
    const html = render()
    expect(html).toContain('class="sw-net sw-grid')
    expect(html).not.toContain('class="one-nr-cfg"')
    expect(html).toContain('role="alert">连接失败，请重试')
  })

  it('explicit preview retains its supported device action and connection switch', () => {
    ready()
    Object.assign(state.current, { preview: true, view: 'net', net: {
      online: true, account: 'DEMO', ip: '192.0.2.2', since: null, usedGB: 18.4,
      devices: [{ id: 'demo', name: '演示电脑', ip: '192.0.2.2', since: '', current: true }],
    } })
    const html = render()
    expect(html).toContain('role="switch"')
    expect(html).toContain('在线设备')
    expect(html).not.toContain('本月已用')
    expect(html).not.toContain('18<small>.4</small><em>GB')
    expect(html).toContain('<dt>IP</dt><dd class="num lat">192.0.2.2')
    expect(html).not.toContain('连接设置')
    expect(html).not.toContain('>配置</button>')
  })
})

describe('lesson blocks and classroom cues', () => {
  function readyAt(hour: number, minute: number, room = 'A101') {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 21, hour, minute))
    const user = workspace()
    const schedule = adaptWeek(week(3, { rows: [
      row({ teacher: '合成教师', room: 'A101', section_numbers: [5, 6], time_segments: ['14:00-15:40'] }),
      row({ teacher: '合成教师', room, section_numbers: [7, 8], time_segments: ['16:00-17:40'], hours_text: '另一段学时' }),
    ] }))
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule, selectedWeek: 3, weekLoaded: true })
    return schedule
  }

  it('keeps tomorrow’s start time prominent without repeating its range in the footer', () => {
    const schedule = readyAt(18, 0)
    schedule.courses = schedule.courses.map((course) => ({ ...course, weekday: 1 }))
    const html = render()
    expect(html).toContain('明天')
    expect(html).toContain('aria-label="14:00"')
    const footer = html.match(/<span class="one-card-m num lat">([\s\S]*?)<\/span><\/span>/)![1]
    expect(footer).toContain('A101')
    expect(footer).not.toContain('data-course-text="time"')
    expect(footer).not.toContain('15:40')
  })

  it('retains today’s time range alongside its countdown', () => {
    readyAt(14, 30)
    expect(render()).toContain('data-course-text="time">14:00–15:40')
  })

  it.each([true, false])('draws one separate header-rule segment per visible day (weekend=%s)', (weekend) => {
    const schedule = readyAt(7, 30)
    schedule.courses = [{ ...schedule.courses[0], start: '08:00', end: '09:40' }]
    state.current.settings!.showWeekend = weekend
    const html = render()
    const rule = html.match(/<div class="sw-week-rule"[^>]*>(.*?)<\/div>/)![1]
    expect(rule.match(/<i /g)).toHaveLength(weekend ? 7 : 5)
    const columns = `grid-template-columns:repeat(${weekend ? 7 : 5}, minmax(0, 1fr))`
    expect(html.split(columns)).toHaveLength(3)
    expect(html).not.toContain('2fr')
    // Separation is structural padding, not a falsified start/duration for early lessons.
    expect(html).toMatch(/class="sw-cell [^"]*"[^>]*style="top:0%;height:calc\(/)
  })

  it('renders one block for a same-room lesson and retains the target emphasis', () => {
    readyAt(14, 30)
    const html = render()
    expect(html.match(/class="sw-cell /g)).toHaveLength(1)
    expect(html).toMatch(/class="sw-cell [^"]*is-todaycell[^"]*is-target[^"]*is-live/)
    expect(html).not.toContain('>换教室</em>')
    expect(html).not.toContain(' title=')
  })

  it('during the gap the card counts down to continuation instead of claiming teaching is live', () => {
    readyAt(15, 50)
    const html = render()
    expect(html).toContain('课间 · 距续课')
    expect(html).not.toContain('此刻 · 距下课')
    expect(html).toContain('aria-label="10:00"')
    expect(html).not.toMatch(/class="sw-cell [^"]*is-live/)
  })

  it('represents the live lesson with an elapsed surface instead of a competing red time line', () => {
    readyAt(14, 30)
    const html = render()
    expect(html).toContain('class="sw-elapsed"')
    expect(html).not.toContain('class="sw-nowline"')
    expect(html).not.toContain('transform:scaleY')
  })

  it('restores the time line between the actual segments of a merged lesson', () => {
    readyAt(15, 50)
    const html = render()
    expect(html).toContain('课间 · 距续课')
    expect(html).not.toContain('class="sw-elapsed"')
    expect(html).toContain('class="sw-nowline"')
  })

  it('keeps a room change split and shows the destination in advance', () => {
    readyAt(14, 30, 'B202')
    const html = render()
    expect(html.match(/class="sw-cell /g)).toHaveLength(2)
    expect(html).toContain('16:00 换到 B202')
    expect(html).toContain('class="sw-move">换教室</em>')
    expect(html).toMatch(/class="sw-cell [^"]*is-move/)
  })

  it('details show exact teaching segments without week rules, periods or a false continuous span', () => {
    const schedule = readyAt(14, 30)
    state.current.sheet = { type: 'course', course: courseBlocks(schedule.courses, 3)[0] }
    vi.stubGlobal('document', { documentElement: { dataset: { edition: 'one' } } })
    try {
      const html = render()
      const detail = html.slice(html.indexOf('class="one-pop-c-static"'))
      expect(detail).toMatch(/class="one-pop-times\b/)
      expect(html).toContain('14:00–15:40')
      expect(html).toContain('16:00–17:40')
      expect(detail).not.toContain('第 5、6 节')
      expect(detail).not.toContain('第 7、8 节')
      expect(detail).not.toContain('14:00–17:40')
      expect(detail).not.toContain('周一')
      expect(detail).toContain('<dt>地点</dt>')
      expect(detail).toContain('<dt>教师</dt>')
      expect(html).not.toContain('另一段学时')
      expect(html).not.toContain('理实一体:64')
      expect(html).not.toContain('复制')
      expect(html).not.toContain('相关安排：')
      expect(schedule.courses[1].source?.hours_text).toBe('另一段学时')
    } finally {
      vi.unstubAllGlobals()
      vi.stubGlobal('__XKE_PREVIEW__', false)
    }
  })
})

describe('shared desktop chrome', () => {
  const bridge = () => ({ show_window: vi.fn(), minimize_window: vi.fn(), toggle_maximize_window: vi.fn(), close_window: vi.fn(), get_window_state: vi.fn() })
  it.each(['loading', 'anonymous', 'ready'] as const)('has one control set in %s, outside modal/inert content', (phase) => {
    state.current.phase = phase
    state.desktop.bridge = bridge()
    const html = render()
    expect(html.match(/aria-label="最小化"/g)).toHaveLength(1)
    expect(html.match(/aria-label="最大化"/g)).toHaveLength(1)
    expect(html.match(/aria-label="关闭窗口"/g)).toHaveLength(1)
    expect(html).toContain('class="runtime-drag"')
    expect(html.indexOf('class="runtime-window"')).toBeLessThan(html.indexOf(phase === 'ready' ? 'class="sw one"' : 'class="auth-surface"'))
  })
  it('does not invent window controls in a browser or the standalone preview', () => {
    expect(render()).not.toContain('runtime-window')
    expect(render()).not.toContain('runtime-drag')
  })
  it('uses a restore icon and reserves navigation space only in the desktop shell', () => {
    state.current.phase = 'ready'; state.desktop.bridge = bridge(); state.desktop.maximized = true
    const html = render()
    expect(html).toContain('aria-label="还原窗口"')
    expect(html).toContain('class="one-toolbar is-desktop"')
    expect(html).not.toContain(' title=')
  })
})

describe('date hierarchy and concise course details', () => {
  it('groups month and weekday on one baseline without duplicating the semester week', () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date(2026, 9, 13, 13, 30))
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule })
    const html = render()
    const date = html.slice(html.indexOf('class="one-date"'), html.indexOf('</div>', html.indexOf('class="one-date"')))
    expect(date).toContain('class="one-date-meta t5"><span>十月</span><span>周二</span>')
    expect(date).toContain('>13</b>')
    expect(date).not.toContain('第 ')
    expect(date).not.toContain('13:30')
  })
  it('keeps week rules in source data while omitting them from the compact lesson detail', () => {
    const schedule = adaptSemester(semester())
    const course = courseBlocks(schedule.courses, 3)[0]
    state.current.sheet = { type: 'course', course }
    state.current.phase = 'ready'
    vi.stubGlobal('document', { documentElement: { dataset: { edition: 'one' } } })
    try {
      const html = render()
      const detail = html.slice(html.indexOf('class="one-pop-c-static"'))
      expect(detail).toContain('10:10–12:00')
      expect(detail).not.toContain('上课周')
      expect(detail).not.toContain('节次')
      expect(course.parts[0].weeks.length).toBeGreaterThan(1)
      expect(html).not.toContain('相关安排：')
      expect(html).not.toContain('学时：')
      expect(html).not.toContain('学分：')
    } finally { vi.unstubAllGlobals(); vi.stubGlobal('__XKE_PREVIEW__', false) }
  })
  it('omits absent fields instead of creating empty detail rows', () => {
    state.current.phase = 'ready'
    state.current.sheet = { type: 'course', course: { id: 'empty', name: '合成课程', weekday: 0,
      kind: 'unknown', start: '', end: '', periods: '', weeks: [], teacher: '', room: '' } }
    vi.stubGlobal('document', { documentElement: { dataset: { edition: 'one' } } })
    try {
      const html = render()
      expect(html).toContain('aria-label="合成课程"')
      expect(html).not.toContain('未提供')
      expect(html).not.toContain('<dt>')
      expect(html).not.toContain('复制')
    } finally { vi.unstubAllGlobals(); vi.stubGlobal('__XKE_PREVIEW__', false) }
  })
})

describe('pending connection detent', () => {
  it.each([false, true])('waits midway during an actual connection operation (previous online=%s)', (online) => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', view: 'net', user, settings: user.settings, netBusy: true,
      net: { online, account: '', ip: null, since: null, usedGB: null, devices: [] } })
    expect(render()).toMatch(/class="one-rock-cap"[^>]*style="left:25%"/)
    state.current.netBusy = false
    expect(render()).toMatch(new RegExp(`class="one-rock-cap"[^>]*style="left:${online ? '50%' : '0%'}"`))
  })
})


describe('archive browsing', () => {
  it('keeps the current-term next lesson while showing an undated archive', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 21, 9, 0))
    const user = workspace()
    const current = adaptWeek(week(3, { rows: [row({ course_name: '本学期课程' })] }))
    const history = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史学期' }, rows: [row({ course_name: '历史课程', week_numbers: [1] })] }))
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: history,
      browsing: true, selectedWeek: 1 })
    Object.defineProperty(state.current, 'currentSchedule', { value: current, configurable: true })
    const html = render()
    expect(html).toContain('本学期课程')
    expect(html).toContain('历史课程')
    expect(html).not.toContain('学期日期不可用')
    expect(html).not.toContain('已有课表保留')
  })

  it('offers all enrolled terms, including the active term, when browsing an older semester', () => {
    const user = workspace()
    const history = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: '历史' } }))
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: history,
      browsing: true })
    Object.defineProperty(state.current, 'currentSchedule', { value: user.schedule, configurable: true })
    const cap = renderToStaticMarkup(createElement(TermCap))
    expect(cap).toContain('class="one-cap"')
    expect(cap).toContain('aria-controls="term-options"')
    expect(cap).not.toContain('<ol') // Closed selector never emits a raw list.
  })
})


describe('week browsing versus explicit refresh', () => {
  function ready() {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule, syncing: true })
  }
  it('does not announce ordinary week loading as a refresh', () => {
    ready()
    expect(renderToStaticMarkup(createElement(TermCap))).not.toContain('更新中')
  })
  it('announces only an explicit refresh', () => {
    ready(); state.current.refreshing = true
    expect(renderToStaticMarkup(createElement(TermCap))).toContain('class="one-cap-sync" role="status">更新中')
  })
})


describe('quiet network information hierarchy', () => {
  function ready() {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', view: 'net', user, settings: user.settings, schedule: user.schedule,
      net: { online: true, account: 'ACCOUNT-MUST-NOT-RENDER', ip: '192.0.2.75', since: '2026-10-13T09:00:00+08:00',
        usedGB: 18.4, devices: [], devicesSupported: false, campusConnected: true, disconnectSupported: false } })
  }
  it('keeps the matrix and real connection controls but removes unrelated numeric metadata', () => {
    ready()
    const html = render()
    expect(html).toContain('class="one-field"')
    expect(html).toContain('class="one-status-note sw-in"')
    expect(html).toContain('class="one-nr-account"')
    expect(html).not.toContain('>连接设置</span>')
    expect(html).not.toContain('aria-label="自动保持连接"')
    expect(html).not.toContain('ACCOUNT-MUST-NOT-RENDER')
    expect(html).toContain('<dt>IP</dt><dd class="num lat">192.0.2.75')
    expect(html).not.toContain('未分配 IP')
    expect(html).not.toContain('本月已用')
    expect(html).not.toContain('class="one-meta')
    expect(html).not.toContain('暂无设备信息')
  })
  it('has exactly one connection settings entry, in settings, not in the network rail', () => {
    ready()
    expect(render()).not.toContain('data-network-config-trigger')
    state.current.view = 'settings'
    const html = render()
    expect(html.match(/data-network-config-trigger/g)).toHaveLength(1)
    const start = html.indexOf('data-network-config-trigger')
    const row = html.slice(start, html.indexOf('</button>', start))
    expect(row).toContain('aria-haspopup="dialog" aria-expanded="false" aria-controls="network-config-panel"')
    expect(row).toContain('data-course-text="title">连接设置</span>')
    expect(row).toContain('M4 10h12M11 5l5 5-5 5')
    expect(html).not.toContain('class="nc"')
  })
  it('settings rail shows personal info; the portal login is one entry in the timetable column', () => {
    ready()
    state.current.view = 'settings'
    const html = render()
    const rail = html.slice(html.indexOf('<aside class="one-rail"'), html.indexOf('</aside>'))
    expect(rail).toContain('class="one-id"')
    expect(rail).not.toContain('one-card')
    const col = html.slice(html.indexOf('st-col st-a'), html.indexOf('st-col st-b'))
    expect(col).toContain('教务登录')
  })
  it('uses self-service account facts without filling the rail with connection settings', () => {
    ready()
    state.current.networkSummary = { configured: true, available: true, cached: false, updated_at: null, message: '',
      account_status: '正常', plan_name: '合成校园套餐', billing_method: '按月', period_start: '2026-10-01', period_end: '2026-10-31' }
    const html = render()
    expect(html).toContain('合成校园套餐')
    expect(html).toContain('正常')
    expect(html).toContain('按月')
    expect(html).not.toContain('2026.10.01')
    expect(html).not.toContain('2026.10.31')
    expect(html).not.toContain('<dt>周期</dt>')
    expect(html).toContain('<dt>IP</dt>')
    expect(html).not.toContain('余额')
    expect(html).not.toContain('class="nc"')
    expect(html).not.toContain('>认证门户<')
  })
  it('retains the missing actual-IP state without any static-IP configuration', () => {
    ready()
    state.current.net!.ip = null
    state.current.networkConfig = { settings: account().network, campus_account: 'SYNTHETIC', credentials: {} }
    const html = render()
    expect(html).toContain('<dt>IP</dt><dd class="num lat">未获取')
    expect(html).not.toContain('手动 IP')
    expect(html).not.toContain('子网前缀')
    expect(html).not.toContain('>网关<')
    expect(html).not.toContain('>DNS<')
  })

  it.each([false, true])('renders an accessible vector refresh control and reflects summary loading (%s)', (busy) => {
    ready()
    state.current.summaryBusy = busy
    const html = render()
    expect(html).toContain('aria-label="刷新校园网信息"')
    expect(html).toContain('viewBox="0 0 24 24"')
    expect(html).not.toContain('↻')
    if (busy) expect(html).toMatch(/class="one-nr-refresh is-loading"[^>]*disabled=""/)
    else expect(html).toMatch(/class="one-nr-refresh"[^>]*aria-busy="false"/)
  })

  it('opens connection settings as a card dialog instead of extending the rail', () => {
    ready()
    state.current.sheet = { type: 'network' }
    state.current.view = 'settings'
    state.current.networkConfig = { settings: account().network, campus_account: 'SYNTHETIC', credentials: {} }
    const html = render()
    expect(html).toContain('aria-expanded="true"')
    expect(html).toContain('id="network-config-panel" class="one-pop-c-static one-network-pop" role="dialog" aria-label="连接设置"')
    expect(html).toContain('aria-haspopup="dialog"')
    expect(html).toContain('class="one-network-body"')
    expect(html.indexOf('id="network-config-panel"')).toBeGreaterThan(html.indexOf('</main>'))
    expect(html).toContain('aria-label="关闭连接设置"')
    expect(html).toContain('<details class="nc-advanced">')
    expect(html).not.toContain('手动 IP')
    expect(html).not.toContain('IP 地址')
    expect(html).not.toContain('子网前缀')
    expect(html).not.toContain('>接入方式</span>')
    expect(html).toContain('role="radiogroup" aria-label="接入方式"')
    expect(html).not.toContain('class="sheet"')
    expect(html).not.toContain('class="scrim"')
    expect(html).not.toContain('复制')
  })
  it.each(['campus', 'telecom', 'unicom', 'mobile'] as const)('keeps four provider choices with exactly one selected (%s)', (provider) => {
    ready()
    state.current.sheet = { type: 'network' }
    state.current.networkConfig = { settings: { ...account().network, provider }, campus_account: 'SYNTHETIC', credentials: {} }
    const html = render()
    expect(html.match(/role="radio"/g)).toHaveLength(4)
    expect(html.match(/aria-checked="true"[^>]*class="on"/g)).toHaveLength(1)
    expect(html).toContain('role="radiogroup" aria-label="接入方式"')
    expect(html).toContain('<label>账号<input')
    expect(html).toContain('<label>密码<input')
    expect(html).not.toContain('运营商账号')
    expect(html).not.toContain('运营商密码')
    expect(html).toContain('心跳间隔<span class="sr-only">（分钟）</span></label>')
    expect(html).toContain('class="nc-unit" aria-hidden="true">分钟</span>')
    expect(html).toMatch(/type="number" inputMode="numeric" min="1" max="1440" step="1" required="" value="5"/)
  })

  it.each([1, 1440])('bounds the custom heartbeat stepper at %s minutes', (minutes) => {
    ready()
    state.current.sheet = { type: 'network' }
    state.current.networkConfig = { settings: { ...account().network, heartbeat_interval_minutes: minutes }, campus_account: '', credentials: {} }
    const html = render()
    const action = minutes === 1 ? '减少心跳间隔' : '增加心跳间隔'
    expect(html).toMatch(new RegExp('aria-label="' + action + '" disabled=""'))
    expect(html).toContain('class="nc-stepper"')
  })

  it('retains supported device names and actions without displaying their addresses or counts', () => {
    ready()
    state.current.net!.devicesSupported = true
    state.current.net!.devices = [{ id: 'synthetic-device', name: '演示手机', ip: '192.0.2.84', since: '', current: false }]
    const html = render()
    expect(html).toContain('在线设备')
    expect(html).toContain('演示手机')
    expect(html).toContain('>下线</button>')
    expect(html).not.toContain('192.0.2.84')
    expect(html).not.toContain('在线设备<span')
  })
  it('does not place another clock beside the month on any main page', () => {
    ready()
    for (const view of ['net', 'week', 'settings'] as const) {
      state.current.view = view
      const html = render()
      const start = html.indexOf('class="one-date"')
      const date = html.slice(start, html.indexOf('</div>', start))
      expect(date).not.toContain('<em')
      expect(date).not.toMatch(/\d{2}:\d{2}/)
    }
  })
})


describe('latest-term settings summary', () => {
  it('keeps the identity card and period dots on the latest workspace term while browsing history', () => {
    const user = workspace()
    const latest = adaptSemester(semester(), user.schedule)
    const history = adaptSemester(semester({ term: { academic_term: '2025-2026-2', label: 'HISTORICAL-TERM' }, rows: [] }))
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: history, view: 'settings', browsing: true })
    Object.defineProperty(state.current, 'currentSchedule', { value: latest, configurable: true })
    const html = render()
    expect(html).toContain('class="one-id"')
    expect(html).toContain('<dt>学期</dt><dd>大一上学期</dd>')
    expect(html).not.toContain('HISTORICAL-TERM')
    expect(html.match(/class="one-sf-d"/g)).toHaveLength(15)
  })
  it('withholds the semester dot total when only a week is available', () => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule: user.schedule, view: 'settings' })
    expect(render()).not.toContain('class="one-sf-d"')
  })
})


describe('network status word fallbacks', () => {
  it.each([
    { online: null, error: '', word: 'CHECKING', status: '正在读取网络状态' },
    { online: null, error: '网络状态暂时无法读取', word: 'UNKNOWN', status: '网络状态读取失败' },
    { online: false, error: '', word: 'OFFLINE', status: '校园网未连接' },
    { online: true, error: '认证被服务器拒绝', word: 'ONLINE', status: '校园网已连接' },
  ])('renders $word without inventing offline state or hiding errors', ({ online, error, word, status }) => {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, view: 'net', netError: error,
      net: online === null ? null : { online, account: '', ip: null, since: null, usedGB: null, devices: [], devicesSupported: false } })
    const html = render()
    expect(html).toContain('aria-label="' + word + '"')
    expect(html).toContain('>' + status + '</span>')
    if (error) expect(html).toContain('role="alert">' + error)
    if (online === null) {
      expect(html).toContain('is-unknown')
      expect(html).not.toContain('aria-label="OFFLINE"')
    }
  })
})


describe('relogin calendar rendering', () => {
  it('restores dates, next-course card and current-day emphasis with fresh courses and a same-term calendar anchor', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-21T09:00:00'))
    const user = workspace()
    const anchor = adaptWeek(week(1, { updated_at: '2026-09-01T00:00:00Z' }))
    const fresh = adaptWeek(week(3, { week_dates: {}, available_weeks: [{ value: '3', label: '第3周', selected: true }], updated_at: '2026-09-21T01:00:00Z' }))
    const schedule = combineSchedule(adaptSemester(semester()), [anchor, fresh], 3)!
    Object.assign(state.current, { phase: 'ready', user, settings: user.settings, schedule, view: 'week', selectedWeek: 3 })
    const html = render()
    expect(html).toContain('class="one-card is-next"')
    expect(html).toContain('class="sw-day is-today')
    expect(html).toContain('is-target')
    expect(html).toContain('class="num">21</b>')
    expect(html).not.toContain('class="num">—</b>')
  })
})


describe('reminder and automatic schedule settings', () => {
  function ready() {
    const user = workspace()
    Object.assign(state.current, { phase: 'ready', user, settings: { ...user.settings, desktopSupported: true, remindBefore: 0 }, schedule: user.schedule, view: 'settings' })
  }
  it('keeps the three-column settings and offers a useful lead time without enabling reminders', () => {
    ready()
    const html = render()
    expect(html).toContain('st-col st-a')
    expect(html).toContain('st-col st-b')
    expect(html).toContain('st-col st-c')
    expect(html).toContain('st-reminder')
    expect(html).toMatch(/aria-checked="true"[^>]*>.*?<span>关<\/span>/s)
    expect(html).toContain('<span>15</span>')
    expect(html).toContain('<span>30</span>')
    expect(html).toContain('<span>45</span>')
    expect(html).not.toContain('<span>5</span>')
    expect(state.current.updateSettings).not.toHaveBeenCalled()
  })
  it('selects custom minutes or the legacy floor rather than showing an empty selection', () => {
    ready(); state.current.settings!.remindBefore = 18
    expect(render()).toMatch(/aria-checked="true"[^>]*>.*?<span>18<\/span>/s)
    state.current.settings!.remindBefore = 5
    expect(render()).toContain('上课前 15 分钟通知')
    expect(state.current.updateSettings).not.toHaveBeenCalled()
  })
  it('disables desktop notifications without changing the saved preference when native capability is absent', () => {
    ready(); state.current.settings!.desktopSupported = false; state.current.settings!.remindBefore = 30
    const html = render()
    expect(html).toContain('桌面通知不可用')
    const group = html.match(/aria-label="课前提醒">(.*?)<\/span><\/span><\/div>/s)?.[1] || ''
    expect(group.match(/disabled=""/g)).toHaveLength(4)
    expect(state.current.settings!.remindBefore).toBe(30)
    expect(state.current.updateSettings).not.toHaveBeenCalled()
  })
  it('shows daily/interval fields only when automatic refresh is enabled', () => {
    ready(); state.current.settings!.autoRefresh = false
    expect(render()).toContain('aria-label="自动更新课表"')
    expect(render()).not.toContain('aria-label="更新频率"')
    state.current.settings!.autoRefresh = true; state.current.settings!.refreshMode = 'daily'; state.current.settings!.refreshTime = '07:30'
    expect(render()).toContain('aria-label="课表每日更新时间"')
    expect(render()).toContain('value="07:30"')
    state.current.settings!.refreshMode = 'interval'; state.current.settings!.refreshHours = 6
    expect(render()).toContain('aria-label="课表更新间隔小时"')
    expect(render()).toContain('value="6"')
  })
  it('reports reconnect/failure in the existing sync location but not on a historical semester', () => {
    ready()
    const scheduleJob = { enabled: true, next_run_at: null, last_run_at: null, last_error: '教务登录已过期', requires_login: true }
    state.current.jobs = { network: scheduleJob, schedule: scheduleJob }
    expect(renderToStaticMarkup(createElement(TermCap))).toContain('需连接教务')
    state.current.jobs.schedule = { ...scheduleJob, requires_login: false, last_error: '教务暂不可用' }
    expect(renderToStaticMarkup(createElement(TermCap))).toContain('自动更新失败')
    state.current.browsing = true
    expect(renderToStaticMarkup(createElement(TermCap))).not.toContain('自动更新失败')
  })
})
