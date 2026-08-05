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

const branchAdministratorAccount: AuthenticatedAccount = {
  ...tenantAccount,
  id: '619608b5-55b5-42eb-8ebc-9287002a9f2a',
  email: 'branch@tenant.test',
  roles: ['BRANCH_ADMIN'],
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
    expect(router.currentRoute.value.path).toBe('/app/branches')

    auth.account = platformAccount
    await router.push('/forgot-password')
    expect(router.currentRoute.value.path).toBe('/platform')
  })

  it('keeps a branch administrator out of tenant administration routes', async () => {
    const auth = useAuthStore(pinia)
    auth.account = branchAdministratorAccount

    await router.push('/app/administrators')
    expect(router.currentRoute.value.path).toBe('/app/branches')

    await router.push('/app/branches/new')
    expect(router.currentRoute.value.path).toBe('/app/branches')

    await router.push('/app/branches/4cef9d70-11a5-4be3-a5bf-8f96ecbe1715')
    expect(router.currentRoute.value.name).toBe('tenant-branch-detail')
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
