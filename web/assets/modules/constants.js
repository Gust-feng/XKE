/**
 * Shared frontend constants.
 *
 * This module intentionally stays data-only so it can be reused by
 * business modules without creating import cycles.
 */
export const API = '/api'

export const SCHEDULE_WEEKDAYS = ['星期一', '星期二', '星期三', '星期四', '星期五', '星期六', '星期日']

export const DEFAULT_SCHEDULE_BLOCKS = [
  { index: 1, label: '第一大节', sectionRange: '1-2节', blockTime: '08:00-09:40', small1: '08:00-08:45', small2: '08:55-09:40', breakMinutes: 10 },
  { index: 2, label: '第二大节', sectionRange: '3-4节', blockTime: '10:00-11:40', small1: '10:00-10:45', small2: '10:55-11:40', breakMinutes: 10 },
  { index: 3, label: '第三大节', sectionRange: '5-6节', blockTime: '14:00-15:40', small1: '14:00-14:45', small2: '14:55-15:40', breakMinutes: 10 },
  { index: 4, label: '第四大节', sectionRange: '7-8节', blockTime: '16:00-17:40', small1: '16:00-16:45', small2: '16:55-17:40', breakMinutes: 10 },
  { index: 5, label: '第五大节', sectionRange: '9-10节', blockTime: '19:00-20:40', small1: '19:00-19:45', small2: '19:55-20:40', breakMinutes: 10 },
]

export const SCHEDULE_ROW_HEIGHT = 132
export const SCHEDULE_DIFF_ANIMATION_MS = 220

export const REMEMBER_KEY = 'xke-remembered-login'
export const TRUSTED_ACCOUNTS_KEY = 'xke-trusted-accounts'
export const LAST_ACCOUNT_KEY = 'xke-last-account'
export const SESSION_KEY = 'xke-session-id'
export const AVATAR_KEY = 'xke-avatar-url'
export const THEME_FALLBACK_KEY = 'xke-theme-fallback'
export const THEME_ACCOUNT_KEY_PREFIX = 'xke-theme-account'
export const DEFAULT_THEME_ID = 'campus-dawn'
export const PRELOGIN_MODE_OPTIONS = [
  {
    value: 'capture',
    label: '录包重放',
    description: '移动教务验证优先走本地兼容链路，传统教务验证码登录后续接入。',
  },
  {
    value: 'coded',
    label: '自动登录',
    description: '首次无本地账号时会用移动教务加密登录完成初始化；后续验证也可直接使用。',
  },
]
export const SCHEDULE_SOURCE_OPTIONS = [
  { value: 'preferred', label: '自动选择' },
  { value: 'mobile', label: '移动教务' },
  {
    value: 'cookie',
    label: '教务系统',
    description: '开发态可用：先走传统教务预登录和人工验证码，再刷新 Cookie 课表。',
  },
]
export const SCHEDULE_WEEK_MODE_OPTIONS = [
  { value: 'current', label: '本周' },
  { value: 'next', label: '下周' },
  { value: 'manual', label: '指定周次' },
]
export const SCHEDULE_AUTO_REFRESH_MODE_OPTIONS = [
  { value: 'daily', label: '每天定时' },
  { value: 'interval', label: '每隔数小时' },
]
export const SCHEDULE_AUTO_REFRESH_INTERVAL_OPTIONS = [3, 4, 5, 6]
export const SCHEDULE_REMINDER_MINUTE_OPTIONS = [5, 10, 15, 20, 30]
export const NETWORK_PROVIDER_OPTIONS = [
  {
    value: 'campus',
    label: '校园账号',
    description: '直接使用当前学号与本地密码发起校园网认证。',
  },
  {
    value: 'telecom',
    label: '中国电信',
    description: '单独保存电信手机号与密码到学号目录下的 .env。',
  },
  {
    value: 'unicom',
    label: '中国联通',
    description: '单独保存联通手机号与密码到学号目录下的 .env。',
  },
  {
    value: 'mobile',
    label: '中国移动',
    description: '单独保存移动手机号与密码到学号目录下的 .env。',
  },
]
export const NETWORK_HEARTBEAT_INTERVAL_OPTIONS = [3, 5, 10, 15]
export const WEEKEND_IGNORE_DAY_OPTIONS = [
  { key: 'saturday', label: '周六' },
  { key: 'sunday', label: '周日' },
]
export const DEV_MOCK_DAY_OPTIONS = [
  { value: '', label: '关闭模拟' },
  { value: '1', label: '周一' },
  { value: '2', label: '周二' },
  { value: '3', label: '周三' },
  { value: '4', label: '周四' },
  { value: '5', label: '周五' },
  { value: '6', label: '周六' },
  { value: '7', label: '周日' },
]

export const THEME_OPTIONS = [
  {
    id: 'campus-dawn',
    label: '校园晨雾',
    description: '清亮轻盈，如晨雾初散',
    badge: '清爽',
    colorScheme: 'light',
    preview: 'linear-gradient(135deg, #eef5ff 0%, #ffffff 52%, #dbeafe 100%)',
    swatches: ['#2563eb', '#f8fbff', '#dbeafe'],
  },
  {
    id: 'forest-notes',
    label: '林间纸页',
    description: '温润青绿，如林间纸页般舒适',
    badge: '轻盈',
    colorScheme: 'light',
    preview: 'linear-gradient(135deg, #edf6ed 0%, #fbfdf8 50%, #d6ead8 100%)',
    swatches: ['#1f7a5b', '#f4f7f1', '#d6ead8'],
  },
  {
    id: 'graphite-night',
    label: '石墨夜航',
    description: '深邃沉稳，如舷窗暖光般静谧',
    badge: '夜色',
    colorScheme: 'dark',
    preview: 'linear-gradient(135deg, #0d131c 0%, #182335 52%, #7fd2c5 82%, #f0bf92 100%)',
    swatches: ['#7fd2c5', '#101823', '#f0bf92'],
  },
]

export const CN_NUMS = [
  '零', '一', '二', '三', '四', '五', '六', '七', '八', '九', '十',
  '十一', '十二', '十三', '十四', '十五', '十六', '十七', '十八', '十九', '二十',
  '二十一', '二十二', '二十三', '二十四', '二十五',
]
