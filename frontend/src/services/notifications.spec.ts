import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'
import { createNotificationRule, listNotificationQueue, syncWhatsAppTemplates, updateMyNotificationContact } from './notifications'

vi.mock('./api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }))

describe('notification service', () => {
  beforeEach(() => vi.clearAllMocks())

  it('uses tenant endpoints and preserves the rule variable mapping', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} })
    vi.mocked(api.get).mockResolvedValue({ data: [] })
    vi.mocked(api.put).mockResolvedValue({ data: {} })
    const rule = {
      name: 'SOAT próximo a vencer', documentTypeId: 'type-1', whatsAppChannelId: 'channel-1',
      whatsAppTemplateId: 'template-1', priority: 'HIGH' as const,
      recipients: ['EMPLOYEE', 'BRANCH_ADMIN'] as const,
      variableMappings: { '$[0].text:1': 'employeeName' }, schedules: [{ amount: 1, unit: 'MONTH' as const }],
    }

    await createNotificationRule({ ...rule, recipients: [...rule.recipients] })
    await syncWhatsAppTemplates('channel-1')
    await listNotificationQueue('FAILED')
    await updateMyNotificationContact({ mobilePhone: '+573001234567', whatsAppConsent: true })

    expect(api.post).toHaveBeenCalledWith('/api/tenant/notification-rules', { ...rule, recipients: ['EMPLOYEE', 'BRANCH_ADMIN'] })
    expect(api.post).toHaveBeenCalledWith('/api/tenant/whatsapp-channels/channel-1/sync-templates')
    expect(api.get).toHaveBeenCalledWith('/api/tenant/notification-queue', { params: { status: 'FAILED', limit: 100 } })
    expect(api.put).toHaveBeenCalledWith('/api/tenant/me/notification-contact', { mobilePhone: '+573001234567', whatsAppConsent: true })
  })
})
