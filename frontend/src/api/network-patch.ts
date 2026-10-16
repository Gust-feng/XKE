import type { NetworkSettingsDto } from './dto'

export function networkPatch(draft: Partial<NetworkSettingsDto>, provider: string, account: string | null, password: string) {
  const patch: Record<string, string | number | boolean | string[]> = { ...draft }
  if (account !== null && account.trim()) patch[`${provider}_account`] = account.trim()
  if (password) patch[`${provider}_password`] = password
  return patch
}
