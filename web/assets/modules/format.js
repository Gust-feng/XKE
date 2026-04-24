import { CN_NUMS, SCHEDULE_WEEKDAYS } from './constants.js'

/** Escape arbitrary text before injecting it into HTML strings. */
export function esc (value) {
  if (value == null) return ''
  const node = document.createElement('div')
  node.textContent = String(value)
  return node.innerHTML
}

export function toChineseNum (value) {
  return value >= 0 && value < CN_NUMS.length ? CN_NUMS[value] : String(value)
}

export function formatWeekLabel (raw) {
  if (!raw) return ''
  return raw.replace(/(\d+)/g, (match) => toChineseNum(parseInt(match, 10)))
}

function parseChineseNumber (value) {
  const cleaned = String(value || '').trim()
  if (!cleaned) return null
  const digitMap = {
    零: 0,
    〇: 0,
    一: 1,
    二: 2,
    两: 2,
    三: 3,
    四: 4,
    五: 5,
    六: 6,
    七: 7,
    八: 8,
    九: 9,
  }
  const unitMap = { 十: 10, 百: 100 }
  let total = 0
  let current = 0
  for (const char of cleaned) {
    if (Object.prototype.hasOwnProperty.call(digitMap, char)) {
      current = digitMap[char]
      continue
    }
    if (Object.prototype.hasOwnProperty.call(unitMap, char)) {
      total += (current || 1) * unitMap[char]
      current = 0
      continue
    }
    return null
  }
  total += current
  return total > 0 ? total : null
}

export function parseWeekNumberFromLabel (label) {
  const cleaned = String(label || '').replace(/\s+/g, '')
  if (!cleaned) return null
  const digitMatch = cleaned.match(/第?(\d{1,2})(?:[-~]\d{1,2})?周?/)
  if (digitMatch) return Number(digitMatch[1])
  const chineseMatch = cleaned.match(/第?([零〇一二两三四五六七八九十百]+)周/)
  if (!chineseMatch) return null
  const found = CN_NUMS.findIndex((item) => item === chineseMatch[1])
  if (found > 0) return found
  return parseChineseNumber(chineseMatch[1])
}

export function resolveCurrentWeekLabelFromAvailableWeeks (availableWeeks, now = new Date()) {
  if (!Array.isArray(availableWeeks) || !availableWeeks.length) return ''
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const datedWeeks = availableWeeks
    .map((item, index) => {
      const rawValue = item && item.value ? String(item.value).trim() : ''
      const match = rawValue.match(/\d{4}-\d{2}-\d{2}/)
      if (!match) return null
      const stamp = new Date(`${match[0]}T00:00:00`).getTime()
      if (Number.isNaN(stamp)) return null
      return { index, stamp }
    })
    .filter(Boolean)

  if (datedWeeks.length) {
    let currentIndex = datedWeeks[0].index
    for (const item of datedWeeks) {
      if (today >= item.stamp) currentIndex = item.index
      else break
    }
    return String((availableWeeks[currentIndex] && availableWeeks[currentIndex].label) || '').trim()
  }

  const selected = availableWeeks.find((item) => item && item.selected)
  return String((selected && selected.label) || '').trim()
}

export function normalizeWeekdayLabel (value) {
  const raw = String(value || '').trim()
  const mapping = {
    '周一': '星期一',
    '周二': '星期二',
    '周三': '星期三',
    '周四': '星期四',
    '周五': '星期五',
    '周六': '星期六',
    '周日': '星期日',
    '周天': '星期日',
    '星期天': '星期日',
  }
  return mapping[raw] || raw
}

export function resolveWeekdayIndex (value) {
  const raw = String(value || '').trim()
  if (/^\d+$/.test(raw)) {
    const number = Number(raw)
    if (number >= 1 && number <= 7) return number
    if (number === 0) return 7
  }
  const digitMatch = raw.match(/[周星期]?\s*([1-7])\s*$/)
  if (digitMatch) return Number(digitMatch[1])
  const normalized = normalizeWeekdayLabel(value)
  const index = SCHEDULE_WEEKDAYS.indexOf(normalized)
  return index === -1 ? null : index + 1
}

export function parseClockToMinutes (value) {
  if (!value) return null
  const match = String(value).match(/(\d{1,2}):(\d{2})/)
  if (!match) return null
  const hours = Number(match[1])
  const minutes = Number(match[2])
  if (Number.isNaN(hours) || Number.isNaN(minutes)) return null
  return hours * 60 + minutes
}

export function formatMinutesText (minutes) {
  if (minutes <= 0) return '即将开始'
  if (minutes < 60) return `${minutes} 分钟`
  const hours = Math.floor(minutes / 60)
  const remain = minutes % 60
  return remain ? `${hours} 小时 ${remain} 分钟` : `${hours} 小时`
}

export function formatMinutesCompact (minutes) {
  if (minutes <= 0) return '0分'
  if (minutes < 60) return `${minutes}分`
  const hours = Math.floor(minutes / 60)
  const remain = minutes % 60
  return remain ? `${hours}小时${remain}分` : `${hours}小时`
}

export function formatSecondsCompact (seconds) {
  const safe = Math.max(0, Math.round(seconds))
  if (safe < 60) return `${safe}秒`
  const minutes = Math.floor(safe / 60)
  const remain = safe % 60
  return `${minutes}分${String(remain).padStart(2, '0')}秒`
}
