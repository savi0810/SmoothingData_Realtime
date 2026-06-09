<script setup lang="ts">
import { computed } from 'vue'
import { Card, CardContent } from '@/components/ui/card'
import type { ChartPoint } from '@/types/prices'

const props = defineProps<{
  symbol: string
  chartPoints: readonly ChartPoint[]
  methodLabel: string
}>()

const stats = computed(() => {
  const last = props.chartPoints.at(-1)
  if (!last) return { priceDisplay: '-', smoothedDisplay: '-', pointsCount: 0, differenceFormatted: null }
  const diff = last.smoothed - last.raw
  return {
    priceDisplay: last.raw.toFixed(2),
    smoothedDisplay: last.smoothed.toFixed(2),
    pointsCount: props.chartPoints.length,
    differenceFormatted: last.raw !== 0
      ? `${diff >= 0 ? '+' : ''}${diff.toFixed(2)}`
      : null,
  }
})
</script>

<template>
  <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
    <Card>
      <CardContent class="py-3 px-4">
        <p class="text-muted-foreground text-xs uppercase tracking-wider mb-0.5">Валюта</p>
        <p class="text-lg font-semibold">{{ symbol }}</p>
      </CardContent>
    </Card>
    
    <Card>
      <CardContent class="py-3 px-4">
        <p class="text-muted-foreground text-xs uppercase tracking-wider mb-0.5">Цена</p>
        <p class="text-lg font-semibold tabular-nums text-chart-1">{{ stats.priceDisplay }}</p>
      </CardContent>
    </Card>
    
    <Card>
      <CardContent class="py-3 px-4">
        <p class="text-muted-foreground text-xs uppercase tracking-wider mb-0.5">Сглаженная</p>
        <p class="text-lg font-semibold tabular-nums text-chart-2">{{ stats.smoothedDisplay }}</p>
        <p v-if="stats.differenceFormatted" class="text-xs text-muted-foreground mt-0.5">
          {{ stats.differenceFormatted }}
        </p>
      </CardContent>
    </Card>
    
    <Card>
      <CardContent class="py-3 px-4">
        <p class="text-muted-foreground text-xs uppercase tracking-wider mb-0.5">Точек данных</p>
        <p class="text-lg font-semibold tabular-nums">{{ stats.pointsCount }}</p>
      </CardContent>
    </Card>
  </div>
</template>
