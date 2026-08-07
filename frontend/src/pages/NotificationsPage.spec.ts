import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AppDialog from '../components/AppDialog.vue'
import { createWhatsAppChannel, listNotificationQueue, listNotificationRules, listWhatsAppChannels, listWhatsAppTemplates } from '../services/notifications'
import NotificationsPage from './NotificationsPage.vue'

vi.mock('../services/documentCatalog', () => ({
  listDocumentTypes: vi.fn().mockResolvedValue([]),
}))

vi.mock('../services/notifications', () => ({
  listWhatsAppChannels: vi.fn(), listWhatsAppTemplates: vi.fn(), listNotificationRules: vi.fn(), listNotificationQueue: vi.fn(),
  getNotificationSettings: vi.fn().mockResolvedValue({ timeZoneId: 'America/Bogota', notificationTime: '08:00:00' }),
  getMyNotificationContact: vi.fn().mockResolvedValue({ mobilePhone: null, whatsAppConsentAt: null }),
  createWhatsAppChannel: vi.fn(), updateWhatsAppChannel: vi.fn(), changeWhatsAppChannelStatus: vi.fn(),
  testWhatsAppChannel: vi.fn(), syncWhatsAppTemplates: vi.fn(), createNotificationRule: vi.fn(),
  updateNotificationRule: vi.fn(), changeNotificationRuleStatus: vi.fn(), updateNotificationSettings: vi.fn(),
  updateMyNotificationContact: vi.fn(),
}))

const channel = {
  id: 'channel-1', name: 'Principal', phoneNumberId: '123', businessAccountId: '456', displayPhoneNumber: '+573001234567',
  status: 'ACTIVE' as const, connectionStatus: 'CONNECTED' as const, accessTokenConfigured: true,
  webhookVerifyTokenConfigured: true, appSecretConfigured: true, lastVerifiedAt: '2026-08-05T13:00:00Z',
  lastSynchronizedAt: '2026-08-05T13:00:00Z', lastError: null,
}
const globalOptions = {
  stubs: {
    TenantLayout: { template: '<div><slot /></div>' },
    QTabs: { template: '<div><slot /></div>' },
    QTabPanels: { template: '<div><slot /></div>' },
    QTabPanel: { template: '<section><slot /></section>' },
    QTab: { props: ['label'], template: '<span>{{ label }}</span>' },
  },
}

describe('notifications page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(listWhatsAppChannels).mockResolvedValue([channel])
    vi.mocked(listWhatsAppTemplates).mockResolvedValue([])
    vi.mocked(listNotificationRules).mockResolvedValue([])
    vi.mocked(listNotificationQueue).mockResolvedValue([])
  })

  it('loads the notification console states', async () => {
    const wrapper = mount(NotificationsPage, { global: globalOptions })
    await flushPromises()

    expect(wrapper.text()).toContain('Notificaciones')
    expect(wrapper.text()).toContain('Principal')
    expect(wrapper.text()).toContain('CONNECTED')
  })

  it('keeps channel form data when the server rejects it', async () => {
    vi.mocked(createWhatsAppChannel).mockRejectedValue(new Error('network'))
    const wrapper = mount(NotificationsPage, { global: globalOptions })
    await flushPromises()
    await wrapper.findAll('button').find((button) => button.text().includes('Nuevo canal'))!.trigger('click')
    const dialog = wrapper.findAllComponents(AppDialog)[0]!
    const name = dialog.find('input')
    await name.setValue('Canal alterno')
    await dialog.findAll('button').find((button) => button.text() === 'Guardar')!.trigger('click')
    await flushPromises()

    expect(name.element.value).toBe('Canal alterno')
    expect(wrapper.text()).toContain('No fue posible guardar el canal.')
  })
})
