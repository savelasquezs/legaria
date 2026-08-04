import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../services/api'
import ForgotPasswordPage from './ForgotPasswordPage.vue'
import ResetPasswordPage from './ResetPasswordPage.vue'
import VerifyEmailPage from './VerifyEmailPage.vue'
import AcceptInvitationPage from './AcceptInvitationPage.vue'

let routeQuery: Record<string, string> = {}

vi.mock('vue-router', () => ({
  useRoute: () => ({ query: routeQuery }),
  useRouter: () => ({ replace: vi.fn() }),
  RouterLink: {
    props: ['to'],
    template: '<a><slot /></a>',
  },
}))

describe('authentication pages', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    routeQuery = {}
    vi.restoreAllMocks()
  })

  it('shows the generic recovery confirmation', async () => {
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const wrapper = mount(ForgotPasswordPage, {
      global: { stubs: { RouterLink: true } },
    })
    await wrapper.find('input[type="email"]').setValue('persona@legaria.test')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain(
      'Si existe una cuenta asociada al correo, recibirás las instrucciones.',
    )
  })

  it('reports a reset link without token as invalid', () => {
    const wrapper = mount(ResetPasswordPage, {
      global: { stubs: { RouterLink: true } },
    })

    expect(wrapper.text()).toContain('El enlace no contiene un token válido.')
  })

  it('shows the verified state after the API accepts the token', async () => {
    routeQuery = { token: 'verification-token' }
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const wrapper = mount(VerifyEmailPage, {
      global: { stubs: { RouterLink: true } },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Tu correo quedó verificado.')
  })

  it('requires an invitation token before choosing a password', () => {
    const wrapper = mount(AcceptInvitationPage)

    expect(wrapper.text()).toContain('El enlace no contiene una invitación válida.')
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it('activates an invited tenant account', async () => {
    routeQuery = { token: 'tenant-invitation' }
    const request = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const wrapper = mount(AcceptInvitationPage)
    const inputs = wrapper.findAll('input[type="password"]')
    await inputs[0]!.setValue('nueva-clave-123')
    await inputs[1]!.setValue('nueva-clave-123')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(request).toHaveBeenCalledWith('/api/auth/accept-invitation', {
      token: 'tenant-invitation',
      newPassword: 'nueva-clave-123',
    })
    expect(wrapper.text()).toContain('Cuenta activada')
  })
})
