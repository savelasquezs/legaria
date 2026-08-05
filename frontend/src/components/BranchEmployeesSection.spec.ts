import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listEmployees, listJobPositions } from '../services/employees'
import { listBranches } from '../services/branches'
import BranchEmployeesSection from './BranchEmployeesSection.vue'

vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('../services/employees', () => ({
  listEmployees: vi.fn(),
  listJobPositions: vi.fn(),
  createEmployee: vi.fn(),
  assignEmployee: vi.fn(),
  createJobPosition: vi.fn(),
  grantEmployeeAdministrativeAccess: vi.fn(),
}))

vi.mock('../services/branches', () => ({
  listBranches: vi.fn(),
  updateBranchAdministratorAssignments: vi.fn(),
  resendBranchAdministratorInvitation: vi.fn(),
  changeBranchAdministratorStatus: vi.fn(),
}))

describe('branch employees section', () => {
  beforeEach(() => {
    vi.mocked(listEmployees).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      totalItems: 0,
      totalPages: 0,
    })
    vi.mocked(listJobPositions).mockResolvedValue([])
    vi.mocked(listBranches).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 100,
      totalItems: 0,
      totalPages: 0,
    })
  })

  it('loads employees scoped to the current branch', async () => {
    mount(BranchEmployeesSection, {
      props: { branchId: 'branch-1', branchActive: true },
    })
    await flushPromises()

    expect(listEmployees).toHaveBeenCalledWith(expect.objectContaining({ branchId: 'branch-1' }))
  })

  it('does not offer new assignments when the branch is inactive', async () => {
    const wrapper = mount(BranchEmployeesSection, {
      props: { branchId: 'branch-1', branchActive: false },
    })
    await flushPromises()

    const buttonLabels = wrapper.findAll('button').map((button) => button.text())
    expect(buttonLabels).not.toContain('Nuevo trabajador')
    expect(buttonLabels).not.toContain('Asignar existente')
  })
})
