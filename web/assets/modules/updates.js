import { esc } from './format.js'

export function formatNoticeTimestamp (value) {
  const raw = String(value || '').trim()
  if (!raw) return ''
  const normalized = raw.includes('T') ? raw : raw.replace(' ', 'T')
  const parsed = new Date(normalized)
  if (Number.isNaN(parsed.getTime())) return raw
  const year = parsed.getFullYear()
  const month = String(parsed.getMonth() + 1).padStart(2, '0')
  const day = String(parsed.getDate()).padStart(2, '0')
  const hour = String(parsed.getHours()).padStart(2, '0')
  const minute = String(parsed.getMinutes()).padStart(2, '0')
  return `${year}-${month}-${day} ${hour}:${minute}`
}

function pickText (payload = {}, keys = []) {
  for (const key of keys) {
    if (!Object.prototype.hasOwnProperty.call(payload, key)) continue
    const cleaned = String(payload[key] || '').trim()
    if (cleaned) return cleaned
  }
  return ''
}

function pickBoolean (payload = {}, keys = []) {
  for (const key of keys) {
    if (!Object.prototype.hasOwnProperty.call(payload, key)) continue
    const value = payload[key]
    if (typeof value === 'boolean') return value
    const cleaned = String(value || '').trim().toLowerCase()
    if (['1', 'true', 'yes', 'on'].includes(cleaned)) return true
    if (['0', 'false', 'no', 'off', ''].includes(cleaned)) return false
    return Boolean(value)
  }
  return false
}

function pickInteger (payload = {}, keys = [], fallback = 0) {
  for (const key of keys) {
    if (!Object.prototype.hasOwnProperty.call(payload, key)) continue
    const value = Number.parseInt(String(payload[key] ?? ''), 10)
    if (Number.isInteger(value)) return value
  }
  return fallback
}

function pickList (payload = {}, keys = [], { lowercase = false } = {}) {
  const values = []
  for (const key of keys) {
    if (!Object.prototype.hasOwnProperty.call(payload, key)) continue
    const rawValue = payload[key]
    let rawItems = []
    if (Array.isArray(rawValue)) rawItems = rawValue
    else if (rawValue != null) {
      const cleaned = String(rawValue || '').trim()
      if (!cleaned) continue
      rawItems = cleaned.includes(',') ? cleaned.split(',') : [cleaned]
    }
    for (const rawItem of rawItems) {
      const cleaned = String(rawItem || '').trim()
      if (!cleaned) continue
      const normalized = lowercase ? cleaned.toLowerCase() : cleaned
      if (!values.includes(normalized)) values.push(normalized)
    }
  }
  return values
}

function resolveKind (value) {
  const normalized = String(value || '').trim().toLowerCase()
  if (!normalized) return ''
  if (['announcement', 'maintenance', 'update', 'operation'].includes(normalized)) {
    return normalized
  }
  return ''
}

function resolveDisplayMode (value) {
  const normalized = String(value || 'panel').trim().toLowerCase()
  if (normalized === 'toast') return 'reminder'
  if (normalized === 'dialog') return 'modal'
  if (['panel', 'reminder', 'modal'].includes(normalized)) return normalized
  return 'panel'
}

function resolveSeverity (value) {
  const normalized = String(value || '').trim().toLowerCase()
  if (!normalized) return ''
  if (['critical', 'error', 'fatal', 'urgent'].includes(normalized)) return 'critical'
  if (['important', 'warning', 'warn', 'medium', 'high'].includes(normalized)) return 'important'
  if (normalized === 'info') return 'info'
  return ''
}

function resolveSummary (payload = {}) {
  return pickText(payload, ['summary', 'subtitle', 'description', 'desc', 'excerpt', 'brief', 'notice_text', 'noticeText'])
}

function normalizeContentFormat (payload = {}, contentMarkdown = '', contentPath = '') {
  const explicit = pickText(payload, ['content_format', 'contentFormat']).toLowerCase()
  if (['markdown', 'text'].includes(explicit)) return explicit
  return contentMarkdown || contentPath ? 'markdown' : 'text'
}

function renderInlineMarkdown (value = '') {
  let raw = String(value || '')
  const placeholders = []
  const createToken = (html) => {
    const token = `@@UPDATESINLINE${placeholders.length}@@`
    placeholders.push({ token, html })
    return token
  }

  raw = raw.replace(/`([^`\n]+)`/g, (_match, code) => createToken(`<code>${esc(code)}</code>`))
  raw = raw.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/gi, (_match, label, url) => {
    return createToken(`<a href="${esc(url)}" target="_blank" rel="noopener noreferrer">${esc(label)}</a>`)
  })
  raw = raw.replace(/<(https?:\/\/[^<>\s]+)>/gi, (_match, url) => {
    return createToken(`<a href="${esc(url)}" target="_blank" rel="noopener noreferrer">${esc(url)}</a>`)
  })
  raw = raw.replace(/<([A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})>/gi, (_match, email) => {
    const normalizedEmail = String(email || '').trim()
    return createToken(`<a href="mailto:${esc(normalizedEmail)}">${esc(normalizedEmail)}</a>`)
  })

  let html = esc(raw)
    .replace(/~~([^~\n]+)~~/g, '<del>$1</del>')
    .replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
    .replace(/__([^_\n]+)__/g, '<strong>$1</strong>')
    .replace(/\*([^*\n]+)\*/g, '<em>$1</em>')
    .replace(/_([^_\n]+)_/g, '<em>$1</em>')

  for (const placeholder of placeholders) {
    html = html.replace(placeholder.token, placeholder.html)
  }
  return html
}

function parseMarkdownTableCells (line = '') {
  let normalized = String(line || '').trim()
  if (normalized.startsWith('|')) normalized = normalized.slice(1)
  if (normalized.endsWith('|')) normalized = normalized.slice(0, -1)

  const cells = []
  let current = ''
  let insideCodeSpan = false

  for (let index = 0; index < normalized.length; index += 1) {
    const char = normalized[index]
    const nextChar = normalized[index + 1]

    if (char === '\\' && nextChar === '|') {
      current += '|'
      index += 1
      continue
    }

    if (char === '`') {
      insideCodeSpan = !insideCodeSpan
      current += char
      continue
    }

    if (char === '|' && !insideCodeSpan) {
      cells.push(current.trim())
      current = ''
      continue
    }

    current += char
  }

  cells.push(current.trim())
  return cells
}

function isMarkdownTableRow (line = '') {
  const normalized = String(line || '').trim()
  if (!normalized || !normalized.includes('|')) return false
  return parseMarkdownTableCells(normalized).length >= 2
}

function parseMarkdownTableAlignments (line = '') {
  return parseMarkdownTableCells(line).map((cell) => {
    const normalized = String(cell || '').replace(/\s/g, '')
    if (!/^:?-{3,}:?$/.test(normalized)) return ''
    if (normalized.startsWith(':') && normalized.endsWith(':')) return 'center'
    if (normalized.endsWith(':')) return 'right'
    if (normalized.startsWith(':')) return 'left'
    return ''
  })
}

function isMarkdownTableSeparator (line = '') {
  const cells = parseMarkdownTableCells(line)
  if (cells.length < 2) return false
  return cells.every((cell) => /^:?-{3,}:?$/.test(String(cell || '').replace(/\s/g, '')))
}

function normalizeMarkdownTableRow (cells = [], columnCount = 0) {
  const normalized = Array.isArray(cells) ? cells.slice(0, columnCount) : []
  while (normalized.length < columnCount) normalized.push('')
  return normalized
}

function renderMarkdownTable (headerLine = '', separatorLine = '', bodyLines = []) {
  const headerCells = parseMarkdownTableCells(headerLine)
  const alignments = parseMarkdownTableAlignments(separatorLine)
  const bodyRows = (Array.isArray(bodyLines) ? bodyLines : []).map((line) => parseMarkdownTableCells(line))
  const columnCount = Math.max(
    headerCells.length,
    alignments.length,
    ...bodyRows.map((row) => row.length),
  )
  if (columnCount < 2) return ''

  const renderCell = (tag, value, index) => {
    const alignment = alignments[index] || ''
    const alignmentAttr = alignment ? ` data-align="${esc(alignment)}"` : ''
    return `<${tag}${alignmentAttr}>${renderInlineMarkdown(value)}</${tag}>`
  }

  const normalizedHeader = normalizeMarkdownTableRow(headerCells, columnCount)
  const renderedBody = bodyRows.length
    ? `<tbody>${bodyRows.map((row) => {
        const normalizedRow = normalizeMarkdownTableRow(row, columnCount)
        return `<tr>${normalizedRow.map((cell, index) => renderCell('td', cell, index)).join('')}</tr>`
      }).join('')}</tbody>`
    : ''

  return `
    <div class="updates-md-table-wrap">
      <table class="updates-md-table">
        <thead>
          <tr>${normalizedHeader.map((cell, index) => renderCell('th', cell, index)).join('')}</tr>
        </thead>
        ${renderedBody}
      </table>
    </div>
  `.trim()
}

export function looksLikeMarkdownContent (value = '') {
  const normalized = String(value || '').replace(/\r/g, '').trim()
  if (!normalized) return false
  if (/(^|\n)\s*(#{1,6}\s|>\s|[-*+]\s|\d+\.\s|```|~~[^~\n]+~~)/.test(normalized)) {
    return true
  }

  const lines = normalized.split('\n')
  for (let index = 0; index < lines.length - 1; index += 1) {
    const current = String(lines[index] || '').trim()
    const next = String(lines[index + 1] || '').trim()
    if (isMarkdownTableRow(current) && isMarkdownTableSeparator(next)) return true
  }
  return false
}

export function renderPlainTextContent (value = '') {
  const normalized = String(value || '').replace(/\r/g, '').trim()
  if (!normalized) return ''
  return normalized
    .split(/\n{2,}/)
    .map((block) => block.trim())
    .filter(Boolean)
    .map((block) => `<p>${esc(block).replace(/\n/g, '<br>')}</p>`)
    .join('')
}

export function renderMarkdownContent (value = '') {
  const normalized = String(value || '').replace(/\r/g, '').trim()
  if (!normalized) return ''

  const blockMap = new Map()
  let tokenIndex = 0
  const withCodeBlocks = normalized.replace(/```([^\n`]*)\n?([\s\S]*?)```/g, (_match, language, code) => {
    const token = `@@UPDATESBLOCK${tokenIndex}@@`
    tokenIndex += 1
    const lang = String(language || '').trim().toLowerCase().replace(/[^a-z0-9_-]/g, '')
    const langAttr = lang ? ` data-lang="${esc(lang)}"` : ''
    blockMap.set(token, `<pre class="updates-md-pre"><code${langAttr}>${esc(String(code || '').replace(/\n+$/, ''))}</code></pre>`)
    return `\n${token}\n`
  })

  const blocks = []
  const lines = withCodeBlocks.split('\n')
  let paragraph = []
  let listType = ''
  let listItems = []
  let quoteLines = []

  const flushParagraph = () => {
    if (!paragraph.length) return
    blocks.push(`<p>${renderInlineMarkdown(paragraph.join(' ').trim())}</p>`)
    paragraph = []
  }
  const flushList = () => {
    if (!listItems.length) return
    const tag = listType === 'ol' ? 'ol' : 'ul'
    blocks.push(`<${tag}>${listItems.map((item) => `<li>${renderInlineMarkdown(item)}</li>`).join('')}</${tag}>`)
    listItems = []
    listType = ''
  }
  const flushQuote = () => {
    if (!quoteLines.length) return
    blocks.push(`<blockquote><p>${renderInlineMarkdown(quoteLines.join(' ').trim())}</p></blockquote>`)
    quoteLines = []
  }
  const flushStructuredBlocks = () => {
    flushParagraph()
    flushList()
    flushQuote()
  }

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index]
    const trimmed = String(line || '').trim()
    if (!trimmed) {
      flushStructuredBlocks()
      continue
    }
    if (blockMap.has(trimmed)) {
      flushStructuredBlocks()
      blocks.push(blockMap.get(trimmed))
      continue
    }

    const nextTrimmed = String(lines[index + 1] || '').trim()
    if (isMarkdownTableRow(trimmed) && isMarkdownTableSeparator(nextTrimmed)) {
      flushStructuredBlocks()
      const bodyLines = []
      let cursor = index + 2
      while (cursor < lines.length) {
        const rowTrimmed = String(lines[cursor] || '').trim()
        if (!rowTrimmed || !isMarkdownTableRow(rowTrimmed) || isMarkdownTableSeparator(rowTrimmed)) break
        bodyLines.push(rowTrimmed)
        cursor += 1
      }
      blocks.push(renderMarkdownTable(trimmed, nextTrimmed, bodyLines))
      index = cursor - 1
      continue
    }

    const headingMatch = trimmed.match(/^(#{1,6})\s+(.*)$/)
    if (headingMatch) {
      flushStructuredBlocks()
      const level = Math.min(6, headingMatch[1].length)
      blocks.push(`<h${level}>${renderInlineMarkdown(headingMatch[2].trim())}</h${level}>`)
      continue
    }

    const quoteMatch = trimmed.match(/^>\s?(.*)$/)
    if (quoteMatch) {
      flushParagraph()
      flushList()
      quoteLines.push(quoteMatch[1].trim())
      continue
    }
    flushQuote()

    const orderedMatch = trimmed.match(/^\d+\.\s+(.*)$/)
    if (orderedMatch) {
      flushParagraph()
      if (listType && listType !== 'ol') flushList()
      listType = 'ol'
      listItems.push(orderedMatch[1].trim())
      continue
    }

    const unorderedMatch = trimmed.match(/^[-*+]\s+(.*)$/)
    if (unorderedMatch) {
      flushParagraph()
      if (listType && listType !== 'ul') flushList()
      listType = 'ul'
      listItems.push(unorderedMatch[1].trim())
      continue
    }

    flushList()
    paragraph.push(trimmed)
  }

  flushStructuredBlocks()
  return blocks.length ? blocks.join('') : ''
}

function getSortTime (item) {
  const raw = String(item.updatedAt || item.createdAt || '').trim()
  if (!raw) return 0
  const normalized = raw.includes('T') ? raw : raw.replace(' ', 'T')
  const parsed = new Date(normalized)
  return Number.isNaN(parsed.getTime()) ? 0 : parsed.getTime()
}

export function resolveNoticeTone (item = {}) {
  if (item.severity === 'critical') return 'critical'
  if (item.severity === 'important') return 'important'
  if (item.kind === 'update') return 'accent'
  return 'neutral'
}

export function resolveNoticeKindLabel (item = {}) {
  const labels = {
    announcement: '公告',
    update: '更新',
    maintenance: '维护',
    operation: '运营',
  }
  const kind = String(item.kind || '').trim().toLowerCase()
  return labels[kind] || ''
}

function resolveSeverityLabel (item = {}) {
  if (item.severity === 'critical') return '紧急'
  if (item.severity === 'important') return '重要'
  if (item.severity === 'info') return '普通'
  return ''
}

export function normalizeNoticeItem (raw = {}) {
  const payload = raw && typeof raw === 'object' ? raw : {}
  const createdAt = pickText(payload, ['created_at', 'createdAt', 'published_at', 'publishedAt'])
  const updatedAt = pickText(payload, ['updated_at', 'updatedAt', 'modified_at', 'modifiedAt', 'published_at', 'publishedAt']) || createdAt
  const channels = pickList(payload, ['channels', 'channel', 'channel_list'], { lowercase: true })
  const contentMarkdown = pickText(payload, ['content_markdown', 'contentMarkdown', 'markdown'])
  const contentPath = pickText(payload, ['content_path', 'contentPath', 'body_path', 'bodyPath'])
  const content = pickText(payload, ['content', 'body', 'message', 'text', 'detail', 'details']) || contentMarkdown
  return {
    id: pickText(payload, ['id', 'notice_id', 'update_id', 'key', 'slug']),
    kind: resolveKind(pickText(payload, ['kind', 'type', 'category'])),
    title: pickText(payload, ['title', 'name', 'subject', 'headline']),
    summary: resolveSummary(payload),
    content,
    contentMarkdown,
    contentPath,
    contentResolvedPath: pickText(payload, ['content_resolved_path', 'contentResolvedPath']),
    contentFormat: normalizeContentFormat(payload, contentMarkdown, contentPath),
    displayMode: resolveDisplayMode(pickText(payload, ['display_mode', 'displayMode', 'mode'])),
    severity: resolveSeverity(pickText(payload, ['severity', 'reminder_level', 'reminderLevel', 'level', 'importance'])),
    pinned: pickBoolean(payload, ['pinned', 'is_pinned', 'isPinned', 'sticky', 'top']),
    channels,
    targetBuilds: pickList(payload, ['target_builds', 'targetBuilds', 'builds', 'target_build']),
    activeFrom: pickText(payload, ['active_from', 'activeFrom', 'start_at', 'startAt', 'active_at', 'activeAt']),
    activeUntil: pickText(payload, ['active_until', 'activeUntil', 'end_at', 'endAt', 'expire_at', 'expireAt', 'expires_at', 'expiresAt']),
    createdAt,
    updatedAt,
  }
}

export function sortNoticeItems (items = []) {
  return (Array.isArray(items) ? items : [])
    .map((item, index) => ({
      index,
      raw: item && typeof item === 'object' ? item : {},
      normalized: normalizeNoticeItem(item),
    }))
    .sort((left, right) => {
      if (left.normalized.pinned !== right.normalized.pinned) return left.normalized.pinned ? -1 : 1

      const timeDiff = getSortTime(right.normalized) - getSortTime(left.normalized)
      if (timeDiff !== 0) return timeDiff

      const bundleOrderDiff = pickInteger(left.raw, ['bundle_order', 'bundleOrder'], -1)
        - pickInteger(right.raw, ['bundle_order', 'bundleOrder'], -1)
      if (bundleOrderDiff !== 0) return bundleOrderDiff

      const itemOrderDiff = pickInteger(left.raw, ['item_order', 'itemOrder'], 1000)
        - pickInteger(right.raw, ['item_order', 'itemOrder'], 1000)
      if (itemOrderDiff !== 0) return itemOrderDiff

      const bundlePriorityDiff = pickInteger(left.raw, ['bundle_priority', 'bundlePriority'], 100)
        - pickInteger(right.raw, ['bundle_priority', 'bundlePriority'], 100)
      if (bundlePriorityDiff !== 0) return bundlePriorityDiff

      const rankDiff = pickInteger(left.raw, ['rank'], 1000) - pickInteger(right.raw, ['rank'], 1000)
      if (rankDiff !== 0) return rankDiff

      const titleDiff = String(left.normalized.title || '').localeCompare(String(right.normalized.title || ''))
      if (titleDiff !== 0) return titleDiff

      return left.index - right.index
    })
    .map((entry) => entry.normalized)
}

function renderToneBadge (tone, label, extraClass = '') {
  return `<span class="updates-badge tone-${esc(tone)}${extraClass ? ` ${esc(extraClass)}` : ''}">${esc(label)}</span>`
}

function shouldShowKindBadge (item = {}) {
  const kind = String(item.kind || '').trim().toLowerCase()
  return Boolean(kind) && kind !== 'announcement'
}

export function renderUpdatesList (container, items = [], options = {}) {
  if (!container) return
  const activeId = String(options.activeId || '').trim()
  const seenIds = options.seenIds && typeof options.seenIds === 'object' ? options.seenIds : {}
  container.innerHTML = items.map((item) => {
    const normalized = normalizeNoticeItem(item)
    const tone = resolveNoticeTone(normalized)
    const isActive = Boolean(activeId && activeId === normalized.id)
    const isRead = Boolean(normalized.id && seenIds[normalized.id])
    const summary = normalized.summary
    const timeLabel = formatNoticeTimestamp(normalized.updatedAt || normalized.createdAt)
    const kindBadge = shouldShowKindBadge(normalized)
      ? renderToneBadge(tone, resolveNoticeKindLabel(normalized), 'is-quiet')
      : ''
    const stateBadge = isActive
      ? `<span class="updates-list-state${isRead ? ' is-read' : ' is-unread'}">${isRead ? '已读' : '未读'}</span>`
      : ''
    const metaRow = kindBadge || stateBadge
      ? `<span class="updates-list-meta">${kindBadge}${stateBadge}</span>`
      : ''
    const unreadFlag = !isActive && !isRead
      ? '<span class="updates-list-flag" aria-hidden="true"></span>'
      : ''
    const expandedBody = isActive && (summary || timeLabel)
      ? `
          <span class="updates-list-body">
            ${summary ? `<span class="updates-list-summary">${esc(summary)}</span>` : ''}
            ${timeLabel ? `<span class="updates-list-stamp"><span class="updates-list-stamp-label">更新时间</span><time datetime="${esc(normalized.updatedAt || normalized.createdAt || timeLabel)}">${esc(timeLabel)}</time></span>` : ''}
          </span>
        `
      : ''
    return `
      <button
        class="updates-list-item${isActive ? ' is-active' : ''}${isRead ? '' : ' is-unread'}"
        type="button"
        data-notice-id="${esc(normalized.id)}"
        data-tone="${esc(tone)}"
        aria-pressed="${isActive ? 'true' : 'false'}"
      >
        <span class="updates-list-accent" aria-hidden="true"></span>
        <span class="updates-list-copy">
          ${metaRow}
          <span class="updates-list-heading">
            <strong>${esc(normalized.title)}</strong>
            ${unreadFlag}
          </span>
          ${expandedBody}
        </span>
      </button>
    `
  }).join('')
}

export function renderUpdatesDetail (container, item) {
  if (!container) return
  if (!item) {
    container.innerHTML = `
      <div class="updates-detail-empty">
        <h3>选择一条公告</h3>
        <p>从左侧公告列表中选择一条内容，在这里阅读完整正文。</p>
      </div>
    `
    return
  }

  const normalized = normalizeNoticeItem(item)
  const detailBody = normalized.contentMarkdown || normalized.content || normalized.summary || ''
  const looksLikeMarkdown = looksLikeMarkdownContent(detailBody)
  const detailHtml = normalized.contentFormat === 'markdown' || looksLikeMarkdown
    ? renderMarkdownContent(detailBody)
    : renderPlainTextContent(detailBody)

  container.innerHTML = `
    <div class="updates-detail-shell">
      <article class="updates-detail-content">${detailHtml || ''}</article>
    </div>
  `
}
