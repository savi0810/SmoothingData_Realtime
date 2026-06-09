<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { usePricesHub } from '@/composables/usePricesHub'
import { useAuth } from '@/composables/useAuth'
import { SYMBOLS, SMOOTHING_METHODS, type SmoothingMethod } from '@/config/constants'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Alert } from '@/components/ui/alert'
import { AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Select } from '@/components/ui/select'
import { SelectTrigger } from '@/components/ui/select'
import { SelectValue } from '@/components/ui/select'
import { SelectContent } from '@/components/ui/select'
import { SelectItem } from '@/components/ui/select'
import StatsGrid from '@/components/StatsGrid.vue'
import PriceChart from '@/components/PriceChart.vue'

const { username, logout } = useAuth()

function loadUserPrefs() {
  try {
    const raw = localStorage.getItem(`prefs_${username.value}`)
    if (raw) return JSON.parse(raw) as { symbol: string; method: SmoothingMethod }
  } catch {}
  return null
}

const prefs = loadUserPrefs()
const isNewUserSession = prefs === null
let firstWatchFired = false
const selectedSymbol = ref(prefs?.symbol ?? 'BTCUSDT')
const selectedMethod = ref<SmoothingMethod>(prefs?.method ?? 'ema')

const sessionStartTime = isNewUserSession ? new Date() : undefined

const { 
  chartPoints, 
  status, 
  connectionError,
  isPaused,
  subscribe,
  pause,
  resume,
} = usePricesHub(sessionStartTime)

watch([selectedSymbol, selectedMethod], ([symbol, method]) => {
  const isInitialFire = !firstWatchFired
  firstWatchFired = true

  if (!isNewUserSession || !isInitialFire) {
    localStorage.setItem(`prefs_${username.value}`, JSON.stringify({ symbol, method }))
  }

  subscribe({ symbol, method })
}, { immediate: true })

const selectedMethodLabel = computed(() => 
  SMOOTHING_METHODS.find(m => m.value === selectedMethod.value)?.label ?? ''
)

const statusLabel = computed(() => {
  switch (status.value) {
    case 'connected': return 'подключено'
    case 'connecting': return 'подключение...'
    case 'error': return 'ошибка'
    default: return 'отключено'
  }
})
</script>

<template>
  <div class="h-screen bg-background text-foreground p-4 flex flex-col gap-3 overflow-hidden">
    <div class="flex items-center justify-between shrink-0">

      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2 rounded-lg border bg-card px-3 py-1.5 shadow-sm">
          <div class="flex h-7 w-7 items-center justify-center rounded-full bg-primary text-primary-foreground text-xs font-bold select-none">
            {{ username?.charAt(0).toUpperCase() }}
          </div>
          <span class="text-sm font-medium">{{ username }}</span>
          <div class="w-px h-4 bg-border" />
          <Button size="sm" variant="ghost" class="h-6 px-2 text-xs text-muted-foreground hover:text-destructive" @click="logout">
            Выйти
          </Button>
        </div>
        <div>
          <h1 class="text-xl font-semibold tracking-tight">Мониторинг цен + сглаживание</h1>
          <p class="text-muted-foreground text-xs mt-0.5">Данные Binance в реальном времени со сглаживанием</p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <Button
          v-if="status === 'connected'"
          size="sm"
          variant="outline"
          @click="isPaused ? resume() : pause()"
        >
          {{ isPaused ? 'Возобновить' : 'Пауза' }}
        </Button>

        <div class="flex items-center gap-2">
          <Badge
            :variant="status === 'connected' ? 'success' : status === 'connecting' ? 'warning' : 'destructive'"
          >
            {{ statusLabel }}
          </Badge>
          <Badge v-if="isPaused" variant="warning">пауза</Badge>
        </div>
      </div>
    </div>

    <Alert v-if="connectionError" variant="destructive" class="shrink-0">
      <AlertDescription>{{ connectionError }}</AlertDescription>
    </Alert>

    <Card class="shrink-0">
      <CardContent class="py-3 px-4">
        <div class="flex flex-wrap gap-4 items-end">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium">Валютная пара</label>
            <Select v-model="selectedSymbol">
              <SelectTrigger class="w-36"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem v-for="s in SYMBOLS" :key="s" :value="s">{{ s }}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium">Метод сглаживания</label>
            <Select v-model="selectedMethod">
              <SelectTrigger class="w-48"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem v-for="m in SMOOTHING_METHODS" :key="m.value" :value="m.value">
                  {{ m.label }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </CardContent>
    </Card>

    <StatsGrid
      :symbol="selectedSymbol"
      :chart-points="chartPoints"
      :method-label="selectedMethodLabel"
    />

    <PriceChart
      :symbol="selectedSymbol"
      :method-label="selectedMethodLabel"
      :chart-points="chartPoints"
      :status="status"
    />
  </div>
</template>
