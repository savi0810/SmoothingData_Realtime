<script setup lang="ts">
import { computed, ref, onErrorCaptured } from 'vue'
import { VisXYContainer, VisLine, VisAxis } from '@unovis/vue'
import { CurveType } from '@unovis/ts'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card'
import {
  ChartContainer,
  ChartCrosshair,
  ChartTooltip,
  ChartTooltipContent,
  componentToString,
  type ChartConfig,
} from '@/components/ui/chart'
import { Activity } from 'lucide-vue-next'
import type { ChartPoint, ConnectionStatus } from '@/types/prices'

const props = defineProps<{
  symbol: string
  methodLabel: string
  chartPoints: readonly ChartPoint[]
  status: ConnectionStatus
}>()

const chartError = ref<string | null>(null)

onErrorCaptured((error) => {
  chartError.value = error instanceof Error ? error.message : String(error)
  console.error('Chart error:', error)
  return false
})

const chartConfig = {
  raw: {
    label: 'Цена',
    color: 'var(--chart-1)',
  },
  smoothed: {
    label: 'Фильтр',
    color: 'var(--chart-2)',
  },
} satisfies ChartConfig

const xAccessor = (d: ChartPoint) => d.timestamp.getTime()
const yRaw = (d: ChartPoint) => d.raw
const ySmoothed = (d: ChartPoint) => d.smoothed

const xTickFormat = (d: number | Date) => {
  const date = typeof d === 'number' ? new Date(d) : d
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

const crosshairTemplate = componentToString(chartConfig, ChartTooltipContent, { labelFormatter: xTickFormat })

const emptyStateMessage = computed(() => {
  if (props.status === 'connecting') return 'Подключение к серверу...'
  if (props.status === 'connected') return 'Ожидание данных...'
  if (props.status === 'error') return 'Ошибка соединения. Повторная попытка...'
  return 'Не подключено'
})

const hasData = computed(() => props.chartPoints.length > 0)

const ZOOM_OUT_FACTOR = 0.05

const yDomain = computed<[number, number] | undefined>(() => {
  if (props.chartPoints.length === 0) return undefined

  let min = Infinity
  let max = -Infinity
  for (const p of props.chartPoints) {
    if (p.raw < min) min = p.raw
    if (p.raw > max) max = p.raw
    if (p.smoothed < min) min = p.smoothed
    if (p.smoothed > max) max = p.smoothed
  }

  const padding = (max - min || min) * ZOOM_OUT_FACTOR
  return [min - padding, max + padding]
})

const lineCurveType = CurveType.MonotoneX
</script>

<template>
  <Card class="flex flex-col flex-1 min-h-0">
    <CardHeader class="py-3 px-4 shrink-0">
      <CardTitle class="text-base">График цен</CardTitle>
      <CardDescription>
        {{ symbol }} &mdash; {{ methodLabel }} сглаживание
      </CardDescription>
    </CardHeader>
    
    <CardContent class="flex-1 min-h-0 px-4 pb-3">

      <div
        v-if="chartError"
        class="flex items-center justify-center h-full"
      >
        <div class="text-center">
          <p class="text-destructive text-sm mb-2">Ошибка графика</p>
          <p class="text-xs text-muted-foreground">{{ chartError }}</p>
        </div>
      </div>

      <div
        v-else-if="!hasData"
        class="flex items-center justify-center h-full text-muted-foreground text-sm"
      >
        {{ emptyStateMessage }}
      </div>

      <ChartContainer v-else :config="chartConfig" class="h-full">
        <VisXYContainer
          :data="chartPoints"
          :y-domain="yDomain"
          :margin="{ left: 60, right: 10, top: 8, bottom: 8 }"
        >
          <VisLine
            :x="xAccessor"
            :y="yRaw"
            color="var(--color-raw)"
            :curve-type="lineCurveType"
          />
          <VisLine
            :x="xAccessor"
            :y="ySmoothed"
            color="var(--color-smoothed)"
            :curve-type="lineCurveType"
          />
          <VisAxis
            type="x"
            :x="xAccessor"
            :tick-format="xTickFormat"
            :num-ticks="6"
            :tick-line="false"
            :domain-line="false"
            :grid-line="false"
          />
          <VisAxis
            type="y"
            :num-ticks="5"
            :tick-line="false"
            :domain-line="false"
          />
          <ChartTooltip />
          <ChartCrosshair
            :template="crosshairTemplate"
            color="var(--color-raw)"
          />
        </VisXYContainer>
      </ChartContainer>
    </CardContent>
    
    <CardFooter class="py-2 px-4 shrink-0 flex-col items-start gap-1 text-xs">
      <div class="flex gap-2 font-medium leading-none">
        Данные обновляются в реальном времени
        <Activity class="h-3.5 w-3.5" />
      </div>
      <div class="leading-none text-muted-foreground">
        Показано {{ chartPoints.length }} точек данных в реальном времени
      </div>
    </CardFooter>
  </Card>
</template>
