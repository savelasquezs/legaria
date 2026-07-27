import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../services/api'
import { useAuthStore } from './auth'
import type { AuthenticationResponse } from '../types/auth'

const response: AuthenticationResponse = {
  accessToken: 'access-token-only-in-memory',
  expiresAt: '2026-07-26T12:10:00Z',
  account: {
    id: '18ceac86-4b5d-4916-a139-e0e278399463',
    accountType: 'PLATFORM',
    email: 'owner@legaria.test',
    firstName: 'Propietario',
    lastName: 'Legaria',
    roles: ['OWNER'],
    organizationId: null,
    employeeId: null,
  },
}

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('coordinates concurrent refresh requests', async () => {
    let release!: () => void
    const pending = new Promise<void>((resolve) => {
      release = resolve
    })
    const post = vi.spyOn(api, 'post').mockImplementation(async () => {
      await pending
      return { data: response }
    })
    const store = useAuthStore()

    const first = store.refresh()
    const second = store.refresh()
    release()

    expect(await first).toBe(true)
    expect(await second).toBe(true)
    expect(post).toHaveBeenCalledTimes(1)
    expect(store.account?.accountType).toBe('PLATFORM')
  })

  it('keeps the access token in memory and clears it when refresh fails', async () => {
    const storageSpy = vi.spyOn(Storage.prototype, 'setItem')
    vi.spyOn(api, 'post')
      .mockResolvedValueOnce({ data: response })
      .mockRejectedValueOnce(new Error('network'))
    const store = useAuthStore()

    expect(await store.refresh()).toBe(true)
    expect(store.accessToken).toBe('access-token-only-in-memory')
    store.$patch({ initialized: false })
    expect(await store.refresh()).toBe(false)

    expect(store.accessToken).toBeNull()
    expect(store.account).toBeNull()
    expect(storageSpy).not.toHaveBeenCalled()
  })
})
