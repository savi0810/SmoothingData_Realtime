<script setup lang="ts">
import { useAuth } from '@/composables/useAuth'
import PricesDashboard from './components/PricesDashboard.vue'
import AuthPage from './components/auth/AuthPage.vue'

const { isAuthenticated, username } = useAuth()
</script>

<template>
  <AuthPage v-if="!isAuthenticated" />
  <PricesDashboard v-else :username="username" />
</template>
