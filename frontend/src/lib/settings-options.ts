/** Keep custom lead times; old short reminders get a useful minimum without enabling them. */
export const REMINDER_OPTIONS = [0, 15, 30, 45] as const
export function reminderMinutes(minutes: number): number {
  return minutes > 0 ? Math.max(15, minutes) : 0
}
export function reminderOptions(minutes: number): number[] {
  const current = reminderMinutes(minutes)
  return [...new Set<number>([...REMINDER_OPTIONS, current])].sort((a, b) => a - b)
}

export function validRefreshInput(kind: 'daily' | 'interval', value: string): boolean {
  return kind === 'daily' ? /^([01]\d|2[0-3]):[0-5]\d$/.test(value)
    : /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= 8760
}
