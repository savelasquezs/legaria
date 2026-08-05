import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { icons } from '../design-system/icons'
import { useAuthStore } from '../stores/auth'
import AppShell from './AppShell.vue'

const replace = vi.fn()
vi.mock('vue-router', () => ({
  useRoute: () => ({ path: '/platform' }),
  useRouter: () => ({ replace }),
}))

describe('AppShell', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    const auth = useAuthStore()
    auth.account = {
      id: '1', accountType: 'PLATFORM', email: 'owner@legaria.test', firstName: 'Ana', lastName: 'Admin',
      roles: ['OWNER'], organizationId: null, employeeId: null,
    }
  })

  it('renders typed navigation, account context and mobile menu control', () => {
    const wrapper = mount(AppShell, {
      props: {
        homeTo: '/platform', contextLabel: 'Plataforma', roleLabel: 'Propietario',
        navigation: [{ label: 'Organizaciones', icon: icons.apartment, to: '/platform' }],
      },
      global: { stubs: { RouterLink: { props: ['to'], template: '<a><slot /></a>' } } },
    })

    expect(wrapper.text()).toContain('Organizaciones')
    expect(wrapper.text()).toContain('Ana Admin')
    expect(wrapper.find('[aria-label="Abrir navegación"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Cerrar sesión"]').exists()).toBe(true)
  })
})
