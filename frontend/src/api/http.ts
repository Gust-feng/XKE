export class ApiError extends Error {
  readonly status: number
  readonly authenticated: boolean
  constructor(status: number, message: string, authenticated = false) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.authenticated = authenticated
  }
}

/** Session stays in memory and is never added to a URL or browser storage. */
export class HttpClient {
  private session = ''
  private generation = 0
  private readonly base: string
  private readonly transport: typeof fetch
  constructor(base: string, transport: typeof fetch = globalThis.fetch.bind(globalThis)) { this.base = base; this.transport = transport }
  setSession(session: string) { this.session = session; this.generation++ }
  clearSession() { this.setSession('') }
  async request<T>(path: string, body?: unknown, raw = false, anonymous = false): Promise<T> {
    const session = anonymous ? '' : this.session
    const generation = this.generation
    const headers: Record<string, string> = { Accept: 'application/json' }
    if (session) headers['X-XKE-Session'] = session
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    let response: Response
    try {
      response = await this.transport(`${this.base.replace(/\/$/, '')}${path}`, {
        method: body === undefined ? 'GET' : 'POST', headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(90_000),
      })
    } catch (error) {
      throw new ApiError(0, error instanceof DOMException && error.name === 'TimeoutError'
        ? '连接超时' : '暂时无法连接', !!session)
    }
    if (generation !== this.generation) throw new ApiError(409, '会话已切换，已忽略旧请求')
    let payload: unknown
    try { payload = await response.json() } catch {
      throw new ApiError(response.status, '响应异常', !!session)
    }
    const envelope = payload as { ok?: boolean; msg?: string; data?: T }
    if (!response.ok || (!raw && envelope.ok !== true)) {
      throw new ApiError(response.status, envelope.msg || '操作未完成', !!session)
    }
    return raw ? payload as T : envelope.data as T
  }
}
