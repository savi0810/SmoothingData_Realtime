<script setup lang="ts">
import { ref } from 'vue'
import { useAuth } from '@/composables/useAuth'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Alert } from '@/components/ui/alert'
import { AlertDescription } from '@/components/ui/alert'

const { login, register } = useAuth()

const tab = ref<'login' | 'register'>('login')
const error = ref<string | null>(null)
const loading = ref(false)

const loginUsername = ref('')
const loginPassword = ref('')

const regUsername = ref('')
const regEmail = ref('')
const regPassword = ref('')
const regPasswordConfirm = ref('')

const successMessage = ref<string | null>(null)

function switchTab(t: 'login' | 'register') {
  tab.value = t
  error.value = null
  successMessage.value = null
}

function validateLoginForm(): string | null {
  if (!loginUsername.value)
    return 'Имя пользователя обязательно для заполнения.'
  if (!loginPassword.value)
    return 'Пароль обязателен для заполнения.'
  return null
}

function validateRegisterForm(): string | null {
  if (!regUsername.value)
    return 'Имя пользователя обязательно для заполнения.'
  if (regUsername.value.length < 3)
    return 'Имя пользователя должно содержать не менее 3 символов.'
  if (regUsername.value.length > 50)
    return 'Имя пользователя не должно превышать 50 символов.'
  if (!regEmail.value)
    return 'Электронная почта обязательна для заполнения.'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(regEmail.value))
    return 'Неверный формат электронной почты.'
  if (!regPassword.value)
    return 'Пароль обязателен для заполнения.'
  if (regPassword.value.length < 6)
    return 'Пароль должен содержать не менее 6 символов.'
  if (regPassword.value !== regPasswordConfirm.value)
    return 'Пароли не совпадают.'
  return null
}

async function handleLogin() {
  const validationError = validateLoginForm()
  if (validationError) {
    error.value = validationError
    return
  }
  loading.value = true
  error.value = null
  try {
    await login(loginUsername.value, loginPassword.value)
    window.location.reload()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Ошибка входа'
  } finally {
    loading.value = false
  }
}

async function handleRegister() {
  const validationError = validateRegisterForm()
  if (validationError) {
    error.value = validationError
    return
  }
  loading.value = true
  error.value = null
  try {
    await register(regUsername.value, regEmail.value, regPassword.value)
    loginUsername.value = regUsername.value
    regUsername.value = ''
    regEmail.value = ''
    regPassword.value = ''
    regPasswordConfirm.value = ''
    tab.value = 'login'
    successMessage.value = 'Аккаунт создан! Войдите, чтобы продолжить.'
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Ошибка регистрации'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-background flex items-center justify-center p-4">
    <div class="w-full max-w-sm">
      <div class="text-center mb-6">
        <h1 class="text-2xl font-semibold tracking-tight">Мониторинг цен + сглаживание</h1>
        <p class="text-muted-foreground text-sm mt-1">Binance · Реальное время</p>
      </div>

      <Card>
        <div class="flex border-b">
          <button
            class="flex-1 py-2.5 text-sm font-medium transition-colors"
            :class="tab === 'login'
              ? 'text-foreground border-b-2 border-primary -mb-px'
              : 'text-muted-foreground hover:text-foreground'"
            @click="switchTab('login')"
          >
            Вход
          </button>
          <button
            class="flex-1 py-2.5 text-sm font-medium transition-colors"
            :class="tab === 'register'
              ? 'text-foreground border-b-2 border-primary -mb-px'
              : 'text-muted-foreground hover:text-foreground'"
            @click="switchTab('register')"
          >
            Регистрация
          </button>
        </div>

        <template v-if="tab === 'login'">
          <CardHeader class="pb-3">
            <CardTitle class="text-base">Войти в аккаунт</CardTitle>
            <CardDescription>Введите имя пользователя и пароль</CardDescription>
          </CardHeader>
          <CardContent class="flex flex-col gap-4">
            <Alert v-if="successMessage" variant="default" class="border-green-500 text-green-700">
              <AlertDescription>{{ successMessage }}</AlertDescription>
            </Alert>
            <Alert v-if="error" variant="destructive">
              <AlertDescription>{{ error }}</AlertDescription>
            </Alert>
            <div class="flex flex-col gap-1.5">
              <Label for="login-username">Имя пользователя</Label>
              <Input
                id="login-username"
                v-model="loginUsername"
                placeholder="username"
                @keydown.enter="handleLogin"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <Label for="login-password">Пароль</Label>
              <Input
                id="login-password"
                v-model="loginPassword"
                type="password"
                placeholder="••••••"
                @keydown.enter="handleLogin"
              />
            </div>
          </CardContent>
          <CardFooter>
            <Button class="w-full" :disabled="loading" @click="handleLogin">
              {{ loading ? 'Входим...' : 'Войти' }}
            </Button>
          </CardFooter>
        </template>

        <template v-else>
          <CardHeader class="pb-3">
            <CardTitle class="text-base">Создать аккаунт</CardTitle>
            <CardDescription>Минимум 3 символа для имени, 6 для пароля</CardDescription>
          </CardHeader>
          <CardContent class="flex flex-col gap-4">
            <Alert v-if="error" variant="destructive">
              <AlertDescription>{{ error }}</AlertDescription>
            </Alert>
            <div class="flex flex-col gap-1.5">
              <Label for="reg-username">Имя пользователя</Label>
              <Input
                id="reg-username"
                v-model="regUsername"
                placeholder="username"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <Label for="reg-email">Email</Label>
              <Input
                id="reg-email"
                v-model="regEmail"
                type="email"
                placeholder="you@example.com"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <Label for="reg-password">Пароль</Label>
              <Input
                id="reg-password"
                v-model="regPassword"
                type="password"
                placeholder="••••••"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <Label for="reg-password-confirm">Повторите пароль</Label>
              <Input
                id="reg-password-confirm"
                v-model="regPasswordConfirm"
                type="password"
                placeholder="••••••"
                @keydown.enter="handleRegister"
              />
            </div>
          </CardContent>
          <CardFooter>
            <Button class="w-full" :disabled="loading" @click="handleRegister">
              {{ loading ? 'Регистрируем...' : 'Зарегистрироваться' }}
            </Button>
          </CardFooter>
        </template>
      </Card>
    </div>
  </div>
</template>
