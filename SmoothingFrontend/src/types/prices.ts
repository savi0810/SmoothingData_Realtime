export interface ChartPoint {
  timestamp: Date
  raw: number
  smoothed: number
}

export interface SubscriptionRequest {
  symbol: string
  method: string
}

export interface SmoothedPoint {
  timestamp: string
  raw: number
  smoothed: number
}

export interface SnapshotPayload {
  symbol: string
  algorithm: string
  points: SmoothedPoint[]
}

export type PriceUpdatedPayload = SmoothedPoint

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error'

export function isValidNumber(value: unknown): value is number {
  return typeof value === 'number' && !isNaN(value) && isFinite(value)
}

export function isValidTimestamp(value: unknown): value is string {
  if (typeof value !== 'string') return false
  const date = new Date(value)
  return !isNaN(date.getTime())
}


export function validateSnapshotPayload(data: unknown): data is SnapshotPayload {
  if (!data || typeof data !== 'object') return false
  const payload = data as Partial<SnapshotPayload>
  return (
    typeof payload.symbol === 'string' &&
    typeof payload.algorithm === 'string' &&
    Array.isArray(payload.points)
  )
}

export function validatePriceUpdatedPayload(data: unknown): data is PriceUpdatedPayload {
  if (!data || typeof data !== 'object') return false
  const point = data as Partial<SmoothedPoint>
  return isValidTimestamp(point.timestamp) && isValidNumber(point.raw) && isValidNumber(point.smoothed)
}
