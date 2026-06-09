import { ref, shallowRef, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { MAX_CHART_POINTS, HUB_CONFIG } from '@/config/constants'
import { useAuth } from '@/composables/useAuth'
import type {
  ChartPoint,
  SubscriptionRequest,
  ConnectionStatus,
  SnapshotPayload,
} from '@/types/prices'
import {
  isValidNumber,
  isValidTimestamp,
  validateSnapshotPayload,
  validatePriceUpdatedPayload,
} from '@/types/prices'

const POINT_ABS_EPSILON = 1e-2
const POINT_REL_EPSILON = 1e-7

function normalizeSymbol(symbol: string): string {
  return symbol.trim().toUpperCase()
}

function normalizeMethod(method: string): string {
  const value = method.trim().toLowerCase()
  if (value === 'alpha-beta' || value === 'alpha_beta') return 'alphabeta'
  return value
}

function areSamePointValues(a: ChartPoint, b: ChartPoint): boolean {
  const eq = (x: number, y: number) =>
    Math.abs(x - y) <= Math.max(POINT_ABS_EPSILON, Math.max(Math.abs(x), Math.abs(y)) * POINT_REL_EPSILON)
  return eq(a.raw, b.raw) && eq(a.smoothed, b.smoothed)
}

const requestKey = (symbol: string, method: string) =>
  `${normalizeSymbol(symbol)}:${normalizeMethod(method)}`

function isSnapshotForRequest(payload: SnapshotPayload, request: SubscriptionRequest): boolean {
  return requestKey(payload.symbol, payload.algorithm) === requestKey(request.symbol, request.method)
}

function sameRequest(a: SubscriptionRequest | null, b: SubscriptionRequest): boolean {
  return !!a && requestKey(a.symbol, a.method) === requestKey(b.symbol, b.method)
}

export function usePricesHub(sessionStartTime?: Date) {
  const { token } = useAuth()
  const chartPoints = shallowRef<ChartPoint[]>([])
  const status = ref<ConnectionStatus>('disconnected')
  const connectionError = ref<string | null>(null)
  const isPaused = ref(false)

  let connection: signalR.HubConnection | null = null
  let pendingRequest: SubscriptionRequest | null = null
  let isCleanedUp = false
  let subscriptionInProgress = false
  let queuedRequest: SubscriptionRequest | null = null

  let rafId: number | null = null
  let pendingPoints: ChartPoint[] | null = null

  function scheduleRender(points: ChartPoint[]) {
    pendingPoints = points
    if (rafId === null) {
      rafId = requestAnimationFrame(() => {
        rafId = null
        if (pendingPoints !== null && !isPaused.value && !isCleanedUp) {
          chartPoints.value = pendingPoints
          pendingPoints = null
        }
      })
    }
  }

  function cancelRender() {
    if (rafId !== null) {
      cancelAnimationFrame(rafId)
      rafId = null
    }
    pendingPoints = null
  }

  function createConnection(): signalR.HubConnection {
    const builder = new signalR.HubConnectionBuilder()
      .withUrl(HUB_CONFIG.url, {
        accessTokenFactory: () => token.value ?? '',
      })
      .configureLogging(
        HUB_CONFIG.reconnect.enabled
          ? signalR.LogLevel.Information
          : signalR.LogLevel.Warning,
      )

    if (HUB_CONFIG.reconnect.enabled) {
      builder.withAutomaticReconnect([...HUB_CONFIG.reconnect.delays])
    }

    const conn = builder.build()
    setupConnectionHandlers(conn)
    return conn
  }

  function setupConnectionHandlers(conn: signalR.HubConnection) {
    conn.on('snapshotLoaded', handleSnapshotLoaded)
    conn.on('priceUpdated', handlePriceUpdated)
    conn.on('streamError', handleStreamError)

    conn.onreconnecting(() => {
      status.value = 'connecting'
    })

    conn.onreconnected(async () => {
      status.value = 'connected'
      if (pendingRequest && !isCleanedUp && !subscriptionInProgress) {
        try {
          await conn.invoke('Subscribe', pendingRequest)
        } catch (error) {
          console.warn('Failed to resubscribe after reconnect:', error)
        }
      }
    })

    conn.onclose((error) => {
      status.value = 'disconnected'
      if (error) {
        console.warn('Connection closed with error:', error)
      }
    })
  }

  function handleSnapshotLoaded(data: unknown) {
    if (isCleanedUp) return

    if (!validateSnapshotPayload(data)) {
      console.warn('Invalid snapshot payload received:', data)
      return
    }

    if (!pendingRequest || !isSnapshotForRequest(data, pendingRequest)) {
      return
    }

    const validPoints = data.points
      .slice(-MAX_CHART_POINTS)
      .map(mapToChartPoint)
      .filter((p): p is ChartPoint => p !== null)
      .filter((p) => !sessionStartTime || p.timestamp >= sessionStartTime)

    cancelRender()
    chartPoints.value = validPoints
  }

  function handlePriceUpdated(data: unknown) {
    if (isPaused.value || isCleanedUp) return

    if (!validatePriceUpdatedPayload(data)) {
      console.warn('Invalid price update payload received:', data)
      return
    }

    const point: ChartPoint = {
      timestamp: new Date(data.timestamp),
      raw: Number(data.raw),
      smoothed: Number(data.smoothed),
    }

    const base = pendingPoints ?? chartPoints.value
    const next = buildNextPoints(base, point)
    if (!next) return

    scheduleRender(next)
  }

  function buildNextPoints(current: readonly ChartPoint[], point: ChartPoint): ChartPoint[] | null {
    if (current.length === 0) {
      return [point]
    }

    const last = current[current.length - 1]

    if (last.timestamp.getTime() === point.timestamp.getTime()) {
      const next = current.slice()
      next[next.length - 1] = point
      return next
    }

    if (areSamePointValues(last, point)) {
      return null
    }

    if (current.length >= MAX_CHART_POINTS) {
      const newArray = new Array<ChartPoint>(MAX_CHART_POINTS)
      for (let i = 0; i < MAX_CHART_POINTS - 1; i++) {
        newArray[i] = current[i + 1]
      }
      newArray[MAX_CHART_POINTS - 1] = point
      return newArray
    }

    return [...current, point]
  }

  function mapToChartPoint(p: { timestamp: string; raw: number; smoothed: number }): ChartPoint | null {
    if (!isValidTimestamp(p.timestamp)) return null

    const raw = Number(p.raw)
    const smoothed = Number(p.smoothed)

    if (!isValidNumber(raw) || !isValidNumber(smoothed)) return null

    return { timestamp: new Date(p.timestamp), raw, smoothed }
  }

  async function ensureConnected(): Promise<boolean> {
    if (isCleanedUp) return false

    if (!connection) {
      connection = createConnection()
    }

    if (connection.state === signalR.HubConnectionState.Connected) {
      return true
    }

    if (connection.state !== signalR.HubConnectionState.Disconnected) {
      return false
    }

    status.value = 'connecting'

    try {
      await connection.start()
      status.value = 'connected'
      connectionError.value = null
      return true
    } catch {
      status.value = 'error'
      connectionError.value = 'Не удалось подключиться к серверу'
      return false
    }
  }

  async function subscribe(request: SubscriptionRequest) {
    if (isCleanedUp) return

    if (sameRequest(pendingRequest, request)) return

    if (subscriptionInProgress) {
      queuedRequest = request
      return
    }

    subscriptionInProgress = true

    try {
      const connected = await ensureConnected()
      if (!connected || !connection) return

      if (pendingRequest) {
        try {
          await connection.invoke('Unsubscribe', pendingRequest)
        } catch (error) {
          console.warn('Failed to unsubscribe:', error)
        }
      }

      pendingRequest = request
      cancelRender()
      chartPoints.value = []

      try {
        await connection.invoke('Subscribe', request)
        connectionError.value = null
      } catch {
        connectionError.value = 'Ошибка при подписке на обновления цен'
        pendingRequest = null
      }
    } finally {
      subscriptionInProgress = false
      if (queuedRequest) {
        const next = queuedRequest
        queuedRequest = null
        await subscribe(next)
      }
    }
  }

  function pause() {
    isPaused.value = true
  }

  function resume() {
    isPaused.value = false
  }

  function handleStreamError(message: string) {
    if (isCleanedUp) return
    connectionError.value = message
  }

  async function cleanup() {
    if (isCleanedUp) return
    isCleanedUp = true
    cancelRender()
    pendingRequest = null
    queuedRequest = null

    if (connection) {
      connection.off('snapshotLoaded', handleSnapshotLoaded)
      connection.off('priceUpdated', handlePriceUpdated)
      connection.off('streamError', handleStreamError)
      const conn = connection
      connection = null
      try {
        await conn.stop()
      } catch {}
    }
  }

  onUnmounted(() => {
    cleanup()
  })

  return {
    chartPoints,
    status,
    connectionError,
    isPaused,
    subscribe,
    pause,
    resume,
  }
}
