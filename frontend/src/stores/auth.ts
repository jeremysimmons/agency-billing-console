import { defineStore } from 'pinia'
import { ref } from 'vue'
import { http, ensureCsrf } from '../api/http'
import type { AuthUser } from '../api/types'

/** Client-side auth state. Server-state lives in Pinia Colada queries. */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null)
  const ready = ref(false)

  async function loadCurrent(): Promise<void> {
    try {
      const { data } = await http.get<AuthUser>('/auth/me')
      user.value = data
    } catch {
      user.value = null
    } finally {
      ready.value = true
    }
  }

  async function loginPassword(usernameOrEmail: string, password: string): Promise<void> {
    await ensureCsrf()
    const { data } = await http.post<AuthUser>('/auth/login', { usernameOrEmail, password })
    user.value = data
  }

  async function requestMagicLink(email: string): Promise<void> {
    await ensureCsrf()
    await http.post('/auth/magic-link/request', { email })
  }

  async function consumeMagicLink(token: string): Promise<void> {
    await ensureCsrf()
    const { data } = await http.post<AuthUser>('/auth/magic-link/consume', { token })
    user.value = data
  }

  async function loginGoogle(idToken: string): Promise<void> {
    await ensureCsrf()
    const { data } = await http.post<AuthUser>('/auth/google', { idToken })
    user.value = data
  }

  async function logout(): Promise<void> {
    await ensureCsrf()
    await http.post('/auth/logout')
    user.value = null
  }

  return { user, ready, loadCurrent, loginPassword, requestMagicLink, consumeMagicLink, loginGoogle, logout }
})
