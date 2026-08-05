import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createDocumentCategory, listDocumentCategories, listDocumentTypes } from '../services/documentCatalog'
import { useAuthStore } from '../stores/auth'
import AppDialog from '../components/AppDialog.vue'
import DocumentCatalogPage from './DocumentCatalogPage.vue'

vi.mock('../services/documentCatalog', () => ({
  listDocumentCategories: vi.fn(),
  listDocumentTypes: vi.fn(),
  createDocumentCategory: vi.fn(),
  updateDocumentCategory: vi.fn(),
  changeDocumentCategoryStatus: vi.fn(),
  createDocumentType: vi.fn(),
  updateDocumentType: vi.fn(),
  changeDocumentTypeStatus: vi.fn(),
}))

const category = {
  id: 'category-1',
  name: 'Seguridad Social',
  description: 'Afiliaciones del trabajador',
  scope: 'EMPLOYEE' as const,
  status: 'ACTIVE' as const,
  documentTypeCount: 1,
  createdAt: '2026-08-05T00:00:00Z',
  updatedAt: '2026-08-05T00:00:00Z',
}
const documentType = {
  id: 'type-1',
  categoryId: category.id,
  categoryName: category.name,
  scope: category.scope,
  name: 'Afiliación EPS',
  description: null,
  status: 'ACTIVE' as const,
  isAvailable: true,
  isRequiredByDefault: true,
  issueDateMode: 'OPTIONAL' as const,
  expirationDateMode: 'NEVER' as const,
  allowsMultipleActiveVersions: false,
  allowsMultipleEvidenceItems: true,
  allowedEvidenceKinds: ['PDF', 'LINK'] as const,
  createdAt: '2026-08-05T00:00:00Z',
  updatedAt: '2026-08-05T00:00:00Z',
}
const globalOptions = {
  stubs: {
    TenantLayout: { template: '<div><slot /></div>' },
    AppDataTable: {
      props: ['rows'],
      template: '<div><div v-for="row in rows" :key="row.id">{{ row.name }} {{ row.isRequiredByDefault ? "Obligatorio por defecto" : "" }} {{ row.allowedEvidenceKinds.join(" ") }}</div></div>',
    },
  },
}

describe('document catalog page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
    useAuthStore().account = {
      id: 'account-1', accountType: 'TENANT', email: 'admin@tenant.test', firstName: 'Ana', lastName: 'Admin',
      roles: ['SUPER_ADMIN'], organizationId: 'organization-1', employeeId: null,
    }
    vi.mocked(listDocumentCategories).mockResolvedValue([category])
    vi.mocked(listDocumentTypes).mockResolvedValue([{ ...documentType, allowedEvidenceKinds: [...documentType.allowedEvidenceKinds] }])
  })

  it('renders categories and types with their configuration', async () => {
    const wrapper = mount(DocumentCatalogPage, {
      global: globalOptions,
    })
    await flushPromises()

    expect(listDocumentCategories).toHaveBeenCalledWith(expect.objectContaining({ scope: 'EMPLOYEE' }))
    expect(listDocumentTypes).toHaveBeenCalledWith(expect.objectContaining({ categoryId: category.id }))
    expect(wrapper.text()).toContain('Seguridad Social')
    expect(wrapper.text()).toContain('Afiliación EPS')
    expect(wrapper.text()).toContain('Obligatorio por defecto')
    expect(wrapper.text()).toContain('PDF')
  })

  it('keeps employee catalog read-only for a branch administrator', async () => {
    useAuthStore().account = { ...useAuthStore().account!, roles: ['BRANCH_ADMIN'], employeeId: 'employee-1' }
    const wrapper = mount(DocumentCatalogPage, {
      global: globalOptions,
    })
    await flushPromises()

    expect(wrapper.find('[aria-label="Nueva categoría"]').exists()).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text() === 'Nuevo tipo')).toBe(false)
    expect(wrapper.text()).toContain('Afiliación EPS')
  })

  it('shows a retryable error when categories cannot load', async () => {
    vi.mocked(listDocumentCategories).mockRejectedValue(new Error('network'))
    const wrapper = mount(DocumentCatalogPage, {
      global: globalOptions,
    })
    await flushPromises()

    expect(wrapper.text()).toContain('No fue posible cargar las categorías.')
    expect(wrapper.text()).toContain('Reintentar')
  })

  it('preserves category form data when the server rejects it', async () => {
    vi.mocked(createDocumentCategory).mockRejectedValue({ response: { data: { detail: 'Ya existe una categoría con ese nombre.' } } })
    const wrapper = mount(DocumentCatalogPage, { global: globalOptions })
    await flushPromises()
    await wrapper.find('[aria-label="Nueva categoría"]').trigger('click')
    await flushPromises()
    const categoryDialog = wrapper.findAllComponents(AppDialog)[0]!
    const nameInput = categoryDialog.find('input')
    await nameInput.setValue('Seguridad Social')
    await categoryDialog.find('form').trigger('submit')
    await flushPromises()

    expect(nameInput.element.value).toBe('Seguridad Social')
    expect(wrapper.text()).toContain('No fue posible guardar la categoría.')
  })
})
