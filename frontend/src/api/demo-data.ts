import fixture from './demo-data.json'
import type { NetStatus, Schedule, Settings } from './types'
import type { NetworkConfigDto, NetworkSummaryDto } from './dto'

interface DemoData {
  schedule: Schedule
  net: NetStatus
  networkConfig: NetworkConfigDto
  networkSummary: NetworkSummaryDto
  settings: Settings
  connectionIp: string
}

/** One synthetic fixture; JSON string literals widen enum fields on import. */
export const demoData = fixture as DemoData
