import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listBranches } from '../services/branches'
import { getEmployee, listJobPositions } from '../services/employees'
import EmployeeDetailPage from './EmployeeDetailPage.vue'
import JobPositionsPage from './JobPositionsPage.vue'

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { id: 'employee-1' }, query: { branchId: 'branch-1' } }),
}))

vi.mock('../services/branches', () => ({
  listBranches: vi.fn(),
}))

vi.mock('../services/employees', () => ({
  getEmployee: vi.fn(),
  listJobPositions: vi.fn(),
  createJobPosition: vi.fn(),
  updateJobPosition: vi.fn(),
  changeJobPositionStatus: vi.fn(),
  assignEmployee: vi.fn(),
  endEmployeeAssignment: vi.fn(),
  endEmploymentRelationship: vi.fn(),
  makePrimaryEmployeeAssignment: vi.fn(),
  transitionEmployeeAssignment: vi.fn(),
}))

const tenantLayout = { template: '<div><slot /></div>' }

describe('employment lifecycle pages', () => {
  beforeEach(() => {
    vi.mocked(listJobPositions).mockResolvedValue([
      { id: 'position-1', name: 'Administrador', status: 'ACTIVE' },
    ])
    vi.mocked(listBranches).mockResolvedValue({
      items: [{
        id: 'branch-1',
        name: 'Centro',
        contactEmail: null,
        phone: null,
        address: 'Calle 1',
        municipalityCode: '11001',
        municipalityName: 'Bogotá, D.C.',
        departmentCode: '11',
        departmentName: 'Bogotá, D.C.',
        status: 'ACTIVE',
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: '2026-08-01T00:00:00Z',
      }],
      page: 1,
      pageSize: 100,
      totalItems: 1,
      totalPages: 1,
    })
    vi.mocked(getEmployee).mockResolvedValue({
      id: 'employee-1',
      documentType: 'CC',
      documentNumber: '1030123456',
      firstName: 'María',
      lastName: 'Trabajadora',
      administrativeAccess: null,
      employmentRelationships: [{
        id: 'relationship-1',
        startedOn: '2026-08-01',
        endedOn: null,
        status: 'ACTIVE',
        assignments: [{
          id: 'assignment-1',
          employmentRelationshipId: 'relationship-1',
          branchId: 'branch-1',
          branchName: 'Centro',
          jobPositionId: 'position-1',
          jobPositionName: 'Administrador',
          isPrimary: true,
          startedOn: '2026-08-01',
          endedOn: null,
          status: 'ACTIVE',
        }],
      }],
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    })
  })

  it('loads the full job position catalog for management', async () => {
    const wrapper = mount(JobPositionsPage, {
      global: {
        stubs: {
          TenantLayout: tenantLayout,
          AppDataTable: { props: ['rows'], template: '<div><span v-for="row in rows" :key="row.id">{{ row.name }}</span></div>' },
        },
      },
    })
    await flushPromises()

    expect(listJobPositions).toHaveBeenCalledWith('ALL')
    expect(wrapper.text()).toContain('Administrador')
  })

  it('renders the active relationship and assignment history', async () => {
    const wrapper = mount(EmployeeDetailPage, {
      global: { stubs: { TenantLayout: tenantLayout } },
    })
    await flushPromises()

    expect(getEmployee).toHaveBeenCalledWith('employee-1')
    expect(wrapper.text()).toContain('María Trabajadora')
    expect(wrapper.text()).toContain('Administrador')
    expect(wrapper.text()).toContain('Centro')
    expect(wrapper.text()).toContain('Finalizar relación')
  })
})
