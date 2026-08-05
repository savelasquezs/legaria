import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listBranches } from '../services/branches'
import { useAuthStore } from '../stores/auth'
import type { AuthenticatedAccount } from '../types/auth'
import TenantBranchesPage from './TenantBranchesPage.vue'

vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('../services/branches', () => ({
  listBranches: vi.fn(),
}))

const tenantAccount: AuthenticatedAccount = {
  id: '595a2bf6-7779-41f6-96ea-c0080fd7e35a',
  accountType: 'TENANT',
  email: 'admin@tenant.test',
  firstName: 'Ana',
  lastName: 'Admin',
  roles: ['SUPER_ADMIN'],
  organizationId: '077c60cd-1c0b-496b-83df-d1cfa89e397c',
  employeeId: null,
}

describe('tenant pages', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(listBranches).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalItems: 0,
      totalPages: 0,
    })
  })

  it('shows branch creation only to the superadministrator', async () => {
    const auth = useAuthStore()
    auth.account = tenantAccount
    const superView = mount(TenantBranchesPage, {
      global: { stubs: { TenantLayout: { template: '<div><slot /></div>' } } },
    })
    await flushPromises()
    expect(superView.text()).toContain('Nueva sucursal')

    auth.account = { ...tenantAccount, roles: ['BRANCH_ADMIN'] }
    const branchView = mount(TenantBranchesPage, {
      global: { stubs: { TenantLayout: { template: '<div><slot /></div>' } } },
    })
    await flushPromises()
    expect(branchView.text()).not.toContain('Nueva sucursal')
    expect(branchView.text()).toContain('Consulta las sucursales que tienes asignadas.')
  })

  it('renders an honest empty tenant state after loading', async () => {
    const auth = useAuthStore()
    auth.account = tenantAccount
    const wrapper = mount(TenantBranchesPage, {
      global: {
        stubs: {
          TenantLayout: { template: '<div><slot /></div>' },
          AppDataTable: {
            props: ['rows', 'emptyTitle'],
            template: '<div>{{ rows.length === 0 ? emptyTitle : "" }}</div>',
          },
        },
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('No hay sucursales')
  })
})
