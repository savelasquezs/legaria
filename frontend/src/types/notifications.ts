export type ChannelStatus = 'ACTIVE' | 'INACTIVE'
export type ConnectionStatus = 'UNVERIFIED' | 'CONNECTED' | 'ERROR'
export type NotificationPriority = 'LOW' | 'NORMAL' | 'HIGH' | 'CRITICAL'
export type NotificationRecipient = 'EMPLOYEE' | 'BRANCH_ADMIN' | 'SUPER_ADMIN'
export type ScheduleUnit = 'DAY' | 'WEEK' | 'MONTH'

export interface WhatsAppChannel {
  id: string; name: string; phoneNumberId: string; businessAccountId: string
  displayPhoneNumber: string | null; status: ChannelStatus; connectionStatus: ConnectionStatus
  accessTokenConfigured: boolean; webhookVerifyTokenConfigured: boolean; appSecretConfigured: boolean
  lastVerifiedAt: string | null; lastSynchronizedAt: string | null; lastError: string | null
}
export interface WhatsAppChannelData {
  name: string; phoneNumberId: string; businessAccountId: string
  accessToken: string | null; webhookVerifyToken: string | null; appSecret: string | null
}
export interface WhatsAppTemplate {
  id: string; whatsAppChannelId: string; name: string; category: string; language: string; status: string
  componentsJson: string; variables: string[]; buttonsJson: string; isAvailable: boolean
  lastSynchronizedAt: string; lastChangedAt: string | null
}
export interface NotificationSchedule { id?: string; amount: number; unit: ScheduleUnit }
export interface NotificationRule {
  id: string; name: string; eventCode: 'DOCUMENT_EXPIRING'; documentTypeId: string; documentTypeName: string
  whatsAppChannelId: string; channelName: string; whatsAppTemplateId: string; templateName: string
  priority: NotificationPriority; status: ChannelStatus; isBlocked: boolean; blockedReason: string | null
  recipients: NotificationRecipient[]; variableMappings: Record<string, string>; schedules: NotificationSchedule[]
}
export interface NotificationRuleData {
  name: string; documentTypeId: string; whatsAppChannelId: string; whatsAppTemplateId: string
  priority: NotificationPriority; recipients: NotificationRecipient[]
  variableMappings: Record<string, string>; schedules: Array<Omit<NotificationSchedule, 'id'>>
}
export interface NotificationAttempt { attemptNumber: number; outcome: string; errorCode: string | null; requestJson: string; responseJson: string | null; startedAt: string; finishedAt: string }
export interface NotificationQueueItem {
  id: string; eventCode: string; ruleName: string; documentName: string; recipientType: string
  destination: string | null; priority: string; status: string; deliveryStatus: string | null
  attemptCount: number; createdAt: string; sentAt: string | null; lastError: string | null; payloadJson: string; attempts: NotificationAttempt[]
}
export interface NotificationSettings { timeZoneId: string; notificationTime: string }
export interface NotificationContact { mobilePhone: string | null; whatsAppConsentAt: string | null }
