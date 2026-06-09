import { ref, computed } from 'vue'

const AUTH_API = (import.meta.env.VITE_AUTH_URL as string) || 'http://localhost:5212'
const TOKEN_KEY = 'auth_token'
const USERNAME_KEY = 'auth_username'

function isTokenExpired(jwt: string): boolean {
  try {
    const payload = JSON.parse(atob(jwt.split('.')[1]))
    return typeof payload.exp === 'number' && payload.exp * 1000 < Date.now()
  } catch {
    return true
  }
}

function loadStoredToken(): string | null {
  const jwt = localStorage.getItem(TOKEN_KEY)
  if (!jwt || isTokenExpired(jwt)) {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USERNAME_KEY)
    return null
  }
  return jwt
}

const token = ref<string | null>(loadStoredToken())
const username = ref<string | null>(token.value ? localStorage.getItem(USERNAME_KEY) : null)

export function useAuth() {
  const isAuthenticated = computed(() => {
    if (!token.value) return false
    if (isTokenExpired(token.value)) {
      token.value = null
      username.value = null
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(USERNAME_KEY)
      return false
    }
    return true
  })

  async function login(usernameInput: string, password: string): Promise<void> {
    const res = await fetch(`${AUTH_API}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: usernameInput, password }),
    })

    if (!res.ok) {
      const msg = await res.text()
      throw new Error(msg || 'Неверный логин или пароль')
    }

    const jwt = await res.text()
    token.value = jwt
    username.value = usernameInput
    localStorage.setItem(TOKEN_KEY, jwt)
    localStorage.setItem(USERNAME_KEY, usernameInput)
  }

  async function register(usernameInput: string, email: string, password: string): Promise<void> {
    const res = await fetch(`${AUTH_API}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: usernameInput, email, password }),
    })

    if (!res.ok) {
      const msg = await res.text()
      throw new Error(msg || 'Ошибка регистрации')
    }
  }

  function logout(): void {
    token.value = null
    username.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USERNAME_KEY)
  }

  return { isAuthenticated, token, username, login, register, logout }
}
