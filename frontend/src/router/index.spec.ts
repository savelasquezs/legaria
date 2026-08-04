import { beforeEach, describe, expect, it } from 'vitest'
import { router } from './index'
import { pinia } from '../stores'
import { useAuthStore } from '../stores/auth'
import type { AuthenticatedAccount } from '../types/auth'

const platformAccount: AuthenticatedAccount = {
  id: '4cef9d70-11a5-4be3-a5bf-8f96ecbe1715',
  accountType: 'PLATFORM',
  email: 'owner@legaria.test',
  firstName: 'Propietario',
  lastName: 'Legaria',
  roles: ['OWNER'],
  organizationId: null,
  employeeId: null,
}

const tenantAccount: AuthenticatedAccount = {
  ...platformAccount,
  id: '595a2bf6-7779-41f6-96ea-c0080fd7e35a',
  accountType: 'TENANT',
  email: 'admin@tenant.test',
  roles: ['SUPER_ADMIN'],
  organizationId: '077c60cd-1c0b-496b-83df-d1cfa89e397c',
}

describe('authentication route guards', () => {
  beforeEach(async () => {
    const auth = useAuthStore(pinia)
    auth.clearSession()
    auth.initialized = true
    await router.replace('/')
  })

  it('redirects an unauthenticated visitor to login', async () => {
    await router.push('/platform')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/platform')
  })

  it('keeps platform and tenant accounts in their corresponding area', async () => {
    const auth = useAuthStore(pinia)
    auth.account = tenantAccount
    await router.push('/platform')
    expect(router.currentRoute.value.path).toBe('/app')

    auth.account = platformAccount
    await router.push('/forgot-password')
    expect(router.currentRoute.value.path).toBe('/platform')
  })

  it('keeps invitation acceptance public and protects organization details', async () => {
    await router.push('/accept-invitation?token=invitation')
    expect(router.currentRoute.value.name).toBe('accept-invitation')

    await router.push('/platform/organizations/4cef9d70-11a5-4be3-a5bf-8f96ecbe1715')
    expect(router.currentRoute.value.name).toBe('login')

    const auth = useAuthStore(pinia)
    auth.account = platformAccount
    await router.push('/platform/organizations/4cef9d70-11a5-4be3-a5bf-8f96ecbe1715')
    expect(router.currentRoute.value.name).toBe('organization-detail')
  })
})
