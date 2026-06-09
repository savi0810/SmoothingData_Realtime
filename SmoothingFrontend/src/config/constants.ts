export const MAX_CHART_POINTS = 300 as const

export const HUB_CONFIG = {
  url: (import.meta.env.VITE_HUB_URL as string) || 'http://localhost:5186/hubs/prices',
  reconnect: {
    enabled: true,
    delays: [0, 2000, 10000, 30000],
  },
  logLevel: 'Warning' as const,
} as const

export const SYMBOLS = [
  'BTCUSDT',
  'ETHUSDT',
  'BNBUSDT',
  'SOLUSDT',
  'XRPUSDT',
  'ADAUSDT',
] as const

export type SmoothingMethod = 'ema' | 'alphabeta' | 'kalman'

export const SMOOTHING_METHODS: readonly { value: SmoothingMethod; label: string }[] = [
  { value: 'ema', label: 'EMA' },
  { value: 'alphabeta', label: 'Alpha-Beta' },
  { value: 'kalman', label: 'Kalman' },
] as const
