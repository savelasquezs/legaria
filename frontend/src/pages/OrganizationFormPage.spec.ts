import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createInitialBranch,
  createOrganization,
  getDepartments,
  getMunicipalities,
  getOrganization,
} from '../services/organizations'
import { useAuthStore } from '../stores/auth'
import type { Organization } from '../types/organizations'
import OrganizationFormPage from './OrganizationFormPage.vue'

const routerReplace = vi.fn()
const route = { params: {} as Record<string, string> }

vi.mock('quasar', async (importOriginal) => {
  const actual = await importOriginal<typeof import('quasar')>()
  return { ...actual, Notify: { create: vi.fn() } }
})

vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => ({ replace: routerReplace, push: vi.fn() }),
}))

vi.mock('../services/organizations', () => ({
  changeOrganizationStatus: vi.fn(),
  createInitialBranch: vi.fn(),
  createOrganization: vi.fn(),
  getDepartments: vi.fn(),
  getMunicipalities: vi.fn(),
  getOrganization: vi.fn(),
  resendInvitation: vi.fn(),
  updateInitialAdmin: vi.fn(),
  updateOrganization: vi.fn(),
}))

const organization: Organization = {
  id: '8437d74f-d1da-4af5-9020-f3b6ac3d224a',
  tradeName: 'Empresa Demo',
  legalName: 'Empresa Demo S.A.S.',
  nit: '900373913',
  verificationDigit: 4,
  contactEmail: 'contacto@empresa.test',
  phone: '+573001112233',
  address: 'Carrera 7 # 10-20',
  municipalityCode: '11001',
  municipalityName: 'Bogotá, D.C.',
  departmentCode: '11',
  departmentName: 'Bogotá, D.C.',
  status: 'ACTIVE',
  createdAt: '2026-08-05T00:00:00Z',
  updatedAt: '2026-08-05T00:00:00Z',
  hasBranches: false,
  initialAdmin: {
    id: '0230f0eb-c328-4886-b4e6-a4fef76005e5',
    firstName: 'Ana',
    lastName: 'Prueba',
    email: 'admin@empresa.test',
    invitationStatus: 'SENT',
    invitationExpiresAt: '2026-08-06T00:00:00Z',
  },
}

const QFormStub = defineComponent({
  emits: ['submit'],
  setup(_, { emit, expose, slots }) {
    expose({ validate: async () => true })
    return () => h('form', {
      onSubmit: (event: Event) => {
        event.preventDefault()
        emit('submit', event)
      },
    }, slots.default?.())
  },
})

const dialogStubs = {
  PlatformLayout: { template: '<div><slot /></div>' },
  QForm: QFormStub,
  ConfirmDialog: {
    props: ['modelValue', 'title', 'confirmLabel', 'cancelLabel'],
    emits: ['update:modelValue', 'confirm'],
    template: `<div v-if="modelValue" class="confirm-dialog-stub">
      <span>{{ title }}</span>
      <button @click="$emit('update:modelValue', false)">{{ cancelLabel }}</button>
      <button @click="$emit('confirm')">{{ confirmLabel }}</button>
    </div>`,
  },
  AppDialog: {
    props: ['modelValue', 'title'],
    emits: ['update:modelValue'],
    template: '<div v-if="modelValue" class="app-dialog-stub"><h2>{{ title }}</h2><slot /><slot name="actions" /></div>',
  },
}

describe('organization provisioning', () => {
  beforeEach(() => {
    route.params = {}
    routerReplace.mockReset()
    setActivePinia(createPinia())
    useAuthStore().account = {
      id: '9aed595a-ae85-40a4-a866-dd2873d76076',
      accountType: 'PLATFORM',
      email: 'owner@legaria.test',
      firstName: 'Owner',
      lastName: 'Legaria',
      roles: ['OWNER'],
      organizationId: null,
      employeeId: null,
    }
    vi.mocked(getDepartments).mockResolvedValue([{ code: '11', name: 'Bogotá, D.C.' }])
    vi.mocked(getMunicipalities).mockResolvedValue([{ code: '11001', name: 'Bogotá, D.C.', type: 'Distrito' }])
    vi.mocked(getOrganization).mockResolvedValue({ ...organization })
    vi.mocked(createOrganization).mockResolvedValue({ ...organization })
    vi.mocked(createInitialBranch).mockReset()
  })

  it('creates after the administrator fields and then suggests the first branch', async () => {
    const wrapper = mount(OrganizationFormPage, { global: { stubs: dialogStubs } })
    await flushPromises()
    const vm = wrapper.vm as unknown as {
      company: Record<string, unknown>
      admin: Record<string, unknown>
    }
    Object.assign(vm.company, {
      tradeName: organization.tradeName,
      legalName: organization.legalName,
      nit: organization.nit,
      verificationDigit: organization.verificationDigit,
      contactEmail: organization.contactEmail,
      phone: organization.phone,
      address: organization.address,
      municipalityCode: organization.municipalityCode,
    })
    Object.assign(vm.admin, {
      firstName: organization.initialAdmin.firstName,
      lastName: organization.initialAdmin.lastName,
      email: organization.initialAdmin.email,
    })

    expect(wrapper.text().lastIndexOf('Crear organización')).toBeGreaterThan(
      wrapper.text().lastIndexOf('Superadministrador inicial'),
    )
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createOrganization).toHaveBeenCalledWith(expect.objectContaining({
      initialAdmin: expect.objectContaining({ email: 'admin@empresa.test' }),
    }))
    expect(routerReplace).toHaveBeenCalledWith(`/platform/organizations/${organization.id}`)
    expect(wrapper.find('.confirm-dialog-stub').text()).toContain('Organización creada')
    await wrapper.findAll('.confirm-dialog-stub button').find((button) => button.text() === 'Ahora no')!.trigger('click')
    expect(wrapper.find('.confirm-dialog-stub').exists()).toBe(false)
    expect(createInitialBranch).not.toHaveBeenCalled()
  })

  it('prefills, preserves errors and hides the guided action after creating the branch', async () => {
    route.params = { id: organization.id }
    vi.mocked(createInitialBranch).mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce({
        id: 'b78ff306-5481-49b4-b6c3-93beab506e6f',
        name: 'Sede principal',
        contactEmail: organization.contactEmail,
        phone: organization.phone,
        address: organization.address,
        municipalityCode: organization.municipalityCode,
        municipalityName: organization.municipalityName,
        departmentCode: organization.departmentCode,
        departmentName: organization.departmentName,
        status: 'ACTIVE',
        createdAt: organization.createdAt,
        updatedAt: organization.updatedAt,
      })
    const wrapper = mount(OrganizationFormPage, { global: { stubs: dialogStubs } })
    await flushPromises()

    await wrapper.findAll('button').find((button) => button.text() === 'Crear primera sucursal')!.trigger('click')
    await flushPromises()
    expect(wrapper.find('.app-dialog-stub').exists()).toBe(true)
    expect(wrapper.find('input[value="Sede principal"]').exists()).toBe(true)
    expect(wrapper.find(`input[value="${organization.contactEmail}"]`).exists()).toBe(true)

    await wrapper.findAll('.app-dialog-stub button').find((button) => button.text() === 'Crear sucursal')!.trigger('click')
    await flushPromises()
    expect(wrapper.find('.app-dialog-stub').text()).toContain('No fue posible crear la sucursal.')

    await wrapper.findAll('.app-dialog-stub button').find((button) => button.text() === 'Crear sucursal')!.trigger('click')
    await flushPromises()
    expect(createInitialBranch).toHaveBeenLastCalledWith(
      organization.id,
      expect.objectContaining({ name: 'Sede principal', municipalityCode: '11001' }),
    )
    expect(wrapper.find('.app-dialog-stub').exists()).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text() === 'Crear primera sucursal')).toBe(false)
  })
})
