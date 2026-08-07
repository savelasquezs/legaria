import { api } from './api'
import type { NotificationContact, NotificationQueueItem, NotificationRule, NotificationRuleData, NotificationSettings, WhatsAppChannel, WhatsAppChannelData, WhatsAppTemplate } from '../types/notifications'

export const listWhatsAppChannels = async (): Promise<WhatsAppChannel[]> => (await api.get('/api/tenant/whatsapp-channels')).data
export const createWhatsAppChannel = async (input: WhatsAppChannelData): Promise<WhatsAppChannel> => (await api.post('/api/tenant/whatsapp-channels', input)).data
export const updateWhatsAppChannel = async (id: string, input: WhatsAppChannelData): Promise<WhatsAppChannel> => (await api.put(`/api/tenant/whatsapp-channels/${id}`, input)).data
export const changeWhatsAppChannelStatus = async (id: string, action: 'activate' | 'deactivate'): Promise<WhatsAppChannel> => (await api.post(`/api/tenant/whatsapp-channels/${id}/${action}`)).data
export const testWhatsAppChannel = async (id: string): Promise<{ success: boolean; displayPhoneNumber: string | null; error: string | null }> => (await api.post(`/api/tenant/whatsapp-channels/${id}/test`)).data
export const syncWhatsAppTemplates = async (id: string): Promise<{ created: number; updated: number; unavailable: number }> => (await api.post(`/api/tenant/whatsapp-channels/${id}/sync-templates`)).data
export const listWhatsAppTemplates = async (channelId?: string): Promise<WhatsAppTemplate[]> => (await api.get('/api/tenant/whatsapp-templates', { params: { channelId } })).data
export const listNotificationRules = async (): Promise<NotificationRule[]> => (await api.get('/api/tenant/notification-rules')).data
export const createNotificationRule = async (input: NotificationRuleData): Promise<NotificationRule> => (await api.post('/api/tenant/notification-rules', input)).data
export const updateNotificationRule = async (id: string, input: NotificationRuleData): Promise<NotificationRule> => (await api.put(`/api/tenant/notification-rules/${id}`, input)).data
export const changeNotificationRuleStatus = async (id: string, action: 'activate' | 'deactivate'): Promise<NotificationRule> => (await api.post(`/api/tenant/notification-rules/${id}/${action}`)).data
export const listNotificationQueue = async (status?: string): Promise<NotificationQueueItem[]> => (await api.get('/api/tenant/notification-queue', { params: { status, limit: 100 } })).data
export const getNotificationSettings = async (): Promise<NotificationSettings> => (await api.get('/api/tenant/notification-settings')).data
export const updateNotificationSettings = async (input: NotificationSettings): Promise<NotificationSettings> => (await api.put('/api/tenant/notification-settings', input)).data
export const getMyNotificationContact = async (): Promise<NotificationContact> => (await api.get('/api/tenant/me/notification-contact')).data
export const updateMyNotificationContact = async (input: { mobilePhone: string | null; whatsAppConsent: boolean }): Promise<NotificationContact> => (await api.put('/api/tenant/me/notification-contact', input)).data
