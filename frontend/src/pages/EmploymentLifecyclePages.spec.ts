import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listBranches } from '../services/branches'
import { listDocumentCategories, listDocumentTypes } from '../services/documentCatalog'
import { getEmployeeDocumentSummary } from '../services/employeeDocuments'
import {
  getEmployee,
  getJobPositionDocumentRequirements,
  listJobPositions,
  updateJobPositionDocumentRequirements,
} from '../services/employees'
import EmployeeDetailPage from './EmployeeDetailPage.vue'
import JobPositionsPage from './JobPositionsPage.vue'
import { useAuthStore } from '../stores/auth'

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { id: 'employee-1' }, query: { branchId: 'branch-1' } }),
}))

vi.mock('../services/branches', () => ({
  listBranches: vi.fn(),
}))

vi.mock('../services/documentCatalog', () => ({
  listDocumentCategories: vi.fn(),
  listDocumentTypes: vi.fn(),
}))

vi.mock('../services/employeeDocuments', () => ({
  getEmployeeDocumentSummary: vi.fn(),
  uploadEmployeeDocument: vi.fn(),
}))

vi.mock('../services/employees', () => ({
  getEmployee: vi.fn(),
  listJobPositions: vi.fn(),
  createJobPosition: vi.fn(),
  updateJobPosition: vi.fn(),
  changeJobPositionStatus: vi.fn(),
  getJobPositionDocumentRequirements: vi.fn(),
  updateJobPositionDocumentRequirements: vi.fn(),
  assignEmployee: vi.fn(),
  endEmployeeAssignment: vi.fn(),
  endEmploymentRelationship: vi.fn(),
  makePrimaryEmployeeAssignment: vi.fn(),
  transitionEmployeeAssignment: vi.fn(),
}))

const tenantLayout = { template: '<div><slot /></div>' }

describe('employment lifecycle pages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
    useAuthStore().account = {
      id: 'account-1',
      accountType: 'TENANT',
      email: 'admin@tenant.test',
      firstName: 'Ana',
      lastName: 'Admin',
      roles: ['SUPER_ADMIN'],
      organizationId: 'organization-1',
      employeeId: null,
    }
    vi.mocked(listJobPositions).mockResolvedValue([
      { id: 'position-1', name: 'Administrador', status: 'ACTIVE', requiredDocumentCount: 0 },
    ])
    vi.mocked(listDocumentCategories).mockResolvedValue([{
      id: 'category-1',
      name: 'Identidad',
      description: null,
      scope: 'EMPLOYEE',
      status: 'ACTIVE',
      documentTypeCount: 1,
      createdAt: '2026-08-05T00:00:00Z',
      updatedAt: '2026-08-05T00:00:00Z',
    }])
    vi.mocked(listDocumentTypes).mockResolvedValue([{
      id: 'type-1',
      categoryId: 'category-1',
      categoryName: 'Identidad',
      scope: 'EMPLOYEE',
      name: 'Cédula',
      description: null,
      status: 'ACTIVE',
      isAvailable: true,
      isRequiredByDefault: false,
      issueDateMode: 'NEVER',
      expirationDateMode: 'NEVER',
      allowsMultipleActiveVersions: false,
      allowsMultipleEvidenceItems: false,
      allowedEvidenceKinds: ['PDF'],
      createdAt: '2026-08-05T00:00:00Z',
      updatedAt: '2026-08-05T00:00:00Z',
    }])
    vi.mocked(getJobPositionDocumentRequirements).mockResolvedValue({
      jobPositionId: 'position-1',
      documentTypeIds: ['type-1'],
    })
    vi.mocked(updateJobPositionDocumentRequirements).mockResolvedValue({
      jobPositionId: 'position-1',
      documentTypeIds: ['type-1'],
    })
    vi.mocked(getEmployeeDocumentSummary).mockResolvedValue({
      requiredCount: 1,
      missingCount: 1,
      missingDocuments: [{ documentTypeId: 'type-1', name: 'Cédula', categoryId: 'category-1', categoryName: 'Identidad' }],
      upcomingExpirations: [],
      categories: [{
        id: 'category-1', name: 'Identidad', missingCount: 1,
        documentTypes: [{ id: 'type-1', name: 'Cédula', isMissing: true, issueDateMode: 'NEVER', expirationDateMode: 'NEVER', allowsMultipleEvidenceItems: false, allowedEvidenceKinds: ['PDF'] }],
      }],
    })
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
          AppDataTable: { props: ['rows'], template: '<div><div v-for="row in rows" :key="row.id"><span>{{ row.name }}</span><slot name="body-cell-actions" :row="row" /></div></div>' },
        },
      },
    })
    await flushPromises()

    expect(listJobPositions).toHaveBeenCalledWith('ALL')
    expect(wrapper.text()).toContain('Administrador')
  })

  it('loads employee document categories and current requirements from a job position', async () => {
    const wrapper = mount(JobPositionsPage, {
      global: {
        stubs: {
          TenantLayout: tenantLayout,
          AppDataTable: { props: ['rows'], template: '<div><div v-for="row in rows" :key="row.id"><slot name="body-cell-actions" :row="row" /></div></div>' },
          QExpansionItem: { props: ['label'], template: '<section><span>{{ label }}</span><slot /></section>' },
        },
      },
    })
    await flushPromises()
    await wrapper.find('[aria-label="Configurar documentos de Administrador"]').trigger('click')
    await flushPromises()

    expect(listDocumentCategories).toHaveBeenCalledWith({ scope: 'EMPLOYEE', status: 'ACTIVE' })
    expect(listDocumentTypes).toHaveBeenCalledWith({ scope: 'EMPLOYEE', status: 'ACTIVE' })
    expect(getJobPositionDocumentRequirements).toHaveBeenCalledWith('position-1')
    expect(wrapper.text()).toContain('Identidad')
    expect(wrapper.text()).toContain('Cédula')
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
    expect(wrapper.text()).toContain('Obligatorios pendientes')
    expect(wrapper.text()).toContain('Cédula')
    expect(wrapper.text()).toContain('Identidad · 1')
    expect(wrapper.text()).toContain('Próximos vencimientos')
  })

  it('renders employee details without mutation actions for a branch administrator', async () => {
    useAuthStore().account = {
      ...useAuthStore().account!,
      roles: ['BRANCH_ADMIN'],
      employeeId: 'administrator-employee',
    }
    const wrapper = mount(EmployeeDetailPage, {
      global: { stubs: { TenantLayout: tenantLayout } },
    })
    await flushPromises()

    expect(getEmployee).toHaveBeenCalledWith('employee-1')
    expect(listJobPositions).not.toHaveBeenCalled()
    expect(listBranches).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('María Trabajadora')
    expect(wrapper.text()).not.toContain('Finalizar relación')
    expect(wrapper.text()).not.toContain('Nueva asignación')
  })
})
