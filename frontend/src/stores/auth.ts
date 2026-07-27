import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '../services/api'
import type { AuthenticatedAccount, AuthenticationResponse } from '../types/auth'

export const useAuthStore = defineStore('auth', () => {
  const account = ref<AuthenticatedAccount | null>(null)
  const accessToken = ref<string | null>(null)
  const initialized = ref(false)
  let activeRefresh: Promise<boolean> | null = null

  function applySession(response: AuthenticationResponse): void {
    account.value = response.account
    accessToken.value = response.accessToken
  }

  function clearSession(): void {
    account.value = null
    accessToken.value = null
  }

  async function login(email: string, password: string): Promise<void> {
    const { data } = await api.post<AuthenticationResponse>('/api/auth/login', {
      email,
      password,
    })
    applySession(data)
    initialized.value = true
  }

  async function refresh(): Promise<boolean> {
    if (activeRefresh) {
      return activeRefresh
    }

    activeRefresh = (async () => {
      try {
        const { data } = await api.post<AuthenticationResponse>('/api/auth/refresh')
        applySession(data)
        return true
      } catch {
        clearSession()
        return false
      } finally {
        initialized.value = true
        activeRefresh = null
      }
    })()
    return activeRefresh
  }

  async function restore(): Promise<void> {
    if (!initialized.value) {
      await refresh()
    }
  }

  async function logout(): Promise<void> {
    try {
      await api.post('/api/auth/logout')
    } finally {
      clearSession()
      initialized.value = true
    }
  }

  async function logoutAll(): Promise<void> {
    try {
      await api.post('/api/auth/logout-all')
    } finally {
      clearSession()
      initialized.value = true
    }
  }

  return {
    account,
    accessToken,
    initialized,
    login,
    refresh,
    restore,
    logout,
    logoutAll,
    clearSession,
  }
})
