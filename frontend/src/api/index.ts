import { RuntimeApi } from './runtime'
import { HttpClient } from './http'
import type { Api } from './types'

/** Explicit build-time composition. Runtime never falls back to synthetic data. */
export const api: Api = __XKE_PREVIEW__
  ? (await import('./mock')).mockApi
  : new RuntimeApi(new HttpClient(import.meta.env.VITE_XKE_API_BASE_URL || ''))

export type * from './types'
