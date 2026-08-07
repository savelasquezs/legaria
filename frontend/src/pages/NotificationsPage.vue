<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import AppAlert from '../components/AppAlert.vue'
import AppDialog from '../components/AppDialog.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingSkeleton from '../components/LoadingSkeleton.vue'
import PageHeader from '../components/PageHeader.vue'
import StatusChip from '../components/StatusChip.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import { listDocumentTypes } from '../services/documentCatalog'
import {
  changeNotificationRuleStatus, changeWhatsAppChannelStatus, createNotificationRule,
  createWhatsAppChannel, getMyNotificationContact, getNotificationSettings, listNotificationQueue,
  listNotificationRules, listWhatsAppChannels, listWhatsAppTemplates, syncWhatsAppTemplates,
  testWhatsAppChannel, updateMyNotificationContact, updateNotificationRule, updateNotificationSettings,
  updateWhatsAppChannel,
} from '../services/notifications'
import type { DocumentType } from '../types/documentCatalog'
import type { NotificationQueueItem, NotificationRule, NotificationRuleData, ScheduleUnit, WhatsAppChannel, WhatsAppTemplate } from '../types/notifications'

const tab = ref('channels')
const loading = ref(true)
const saving = ref(false)
const errorMessage = ref('')
const dialogError = ref('')
const channels = ref<WhatsAppChannel[]>([])
const templates = ref<WhatsAppTemplate[]>([])
const rules = ref<NotificationRule[]>([])
const queue = ref<NotificationQueueItem[]>([])
const documentTypes = ref<DocumentType[]>([])
const channelDialog = ref(false)
const ruleDialog = ref(false)
const previewDialog = ref(false)
const queueDialog = ref(false)
const selectedChannel = ref<WhatsAppChannel | null>(null)
const selectedRule = ref<NotificationRule | null>(null)
const selectedTemplate = ref<WhatsAppTemplate | null>(null)
const selectedQueueItem = ref<NotificationQueueItem | null>(null)
const statusDialog = ref(false)
const statusTarget = ref<{ kind: 'channel'; item: WhatsAppChannel } | { kind: 'rule'; item: NotificationRule } | null>(null)
const templateSearch = ref('')
const templateChannel = ref('')
const queueStatus = ref('')
const notificationTime = ref('08:00')
const timeZoneId = ref('America/Bogota')
const myPhone = ref('')
const myConsent = ref(false)
const eventVariables = ['employeeName', 'documentName', 'expirationDate', 'daysUntilExpiration', 'branchName', 'organizationName']
const recipientOptions = [
  { label: 'Trabajador', value: 'EMPLOYEE' }, { label: 'Administrador de sucursal', value: 'BRANCH_ADMIN' },
  { label: 'Superadministrador', value: 'SUPER_ADMIN' },
]
const channelForm = reactive({ name: '', phoneNumberId: '', businessAccountId: '', accessToken: '', webhookVerifyToken: '', appSecret: '' })
const ruleForm = reactive<NotificationRuleData>({
  name: '', documentTypeId: '', whatsAppChannelId: '', whatsAppTemplateId: '', priority: 'NORMAL',
  recipients: ['EMPLOYEE'], variableMappings: {}, schedules: [{ amount: 30, unit: 'DAY' }],
})

const availableDocumentTypes = computed(() => documentTypes.value.filter((item) => item.isAvailable && item.expirationDateMode !== 'NEVER'))
const channelTemplates = computed(() => templates.value.filter((item) => item.whatsAppChannelId === ruleForm.whatsAppChannelId && item.isAvailable && item.status === 'APPROVED'))
const filteredTemplates = computed(() => templates.value.filter((item) =>
  (!templateChannel.value || item.whatsAppChannelId === templateChannel.value) &&
  (!templateSearch.value || item.name.toLowerCase().includes(templateSearch.value.toLowerCase()))))
const currentRuleTemplate = computed(() => templates.value.find((item) => item.id === ruleForm.whatsAppTemplateId) ?? null)
const previewComponents = computed(() => {
  try {
    const components = JSON.parse(selectedTemplate.value?.componentsJson ?? '[]') as Array<{ type?: string; text?: string; buttons?: Array<{ type?: string; text?: string; url?: string }> }>
    return components.map((item) => ({ type: item.type ?? '', text: item.text ?? '', buttons: item.buttons ?? [] }))
  } catch { return [] }
})

watch(currentRuleTemplate, (value) => {
  const previous = ruleForm.variableMappings
  ruleForm.variableMappings = Object.fromEntries((value?.variables ?? []).map((key) => [key, previous[key] ?? '']))
})

async function load(): Promise<void> {
  loading.value = true; errorMessage.value = ''
  try {
    const [loadedChannels, loadedTemplates, loadedRules, loadedQueue, loadedTypes, settings, contact] = await Promise.all([
      listWhatsAppChannels(), listWhatsAppTemplates(), listNotificationRules(), listNotificationQueue(),
      listDocumentTypes({ scope: 'EMPLOYEE', status: 'ALL' }), getNotificationSettings(), getMyNotificationContact(),
    ])
    channels.value = loadedChannels; templates.value = loadedTemplates; rules.value = loadedRules
    queue.value = loadedQueue; documentTypes.value = loadedTypes
    timeZoneId.value = settings.timeZoneId; notificationTime.value = settings.notificationTime.slice(0, 5)
    myPhone.value = contact.mobilePhone ?? ''; myConsent.value = Boolean(contact.whatsAppConsentAt)
  } catch (error) { errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar las notificaciones.' }
  finally { loading.value = false }
}

function openChannel(channel: WhatsAppChannel | null = null): void {
  selectedChannel.value = channel
  Object.assign(channelForm, { name: channel?.name ?? '', phoneNumberId: channel?.phoneNumberId ?? '', businessAccountId: channel?.businessAccountId ?? '', accessToken: '', webhookVerifyToken: '', appSecret: '' })
  dialogError.value = ''; channelDialog.value = true
}

async function saveChannel(): Promise<void> {
  if (saving.value) return; saving.value = true; dialogError.value = ''
  try {
    const input = { ...channelForm, accessToken: channelForm.accessToken || null, webhookVerifyToken: channelForm.webhookVerifyToken || null, appSecret: channelForm.appSecret || null }
    if (selectedChannel.value) await updateWhatsAppChannel(selectedChannel.value.id, input)
    else await createWhatsAppChannel(input)
    channelDialog.value = false; await load(); Notify.create({ type: 'positive', message: 'Canal guardado.' })
  } catch (error) { dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar el canal.' }
  finally { saving.value = false }
}

async function testChannel(channel: WhatsAppChannel): Promise<void> {
  saving.value = true
  try { const result = await testWhatsAppChannel(channel.id); await load(); Notify.create({ type: result.success ? 'positive' : 'negative', message: result.success ? 'Conexión verificada.' : result.error ?? 'Credenciales inválidas.' }) }
  catch (error) { Notify.create({ type: 'negative', message: getProblem(error)?.detail ?? 'No fue posible probar la conexión.' }) }
  finally { saving.value = false }
}

async function syncTemplates(channel: WhatsAppChannel): Promise<void> {
  saving.value = true
  try { const result = await syncWhatsAppTemplates(channel.id); await load(); Notify.create({ type: 'positive', message: `${result.created} nuevas, ${result.updated} actualizadas.` }) }
  catch (error) { Notify.create({ type: 'negative', message: getProblem(error)?.detail ?? 'No fue posible sincronizar.' }) }
  finally { saving.value = false }
}

async function toggleChannel(channel: WhatsAppChannel): Promise<void> {
  saving.value = true
  try { await changeWhatsAppChannelStatus(channel.id, channel.status === 'ACTIVE' ? 'deactivate' : 'activate'); await load() }
  catch (error) { Notify.create({ type: 'negative', message: getProblem(error)?.detail ?? 'No fue posible cambiar el estado.' }) }
  finally { saving.value = false }
}

function requestChannelStatus(channel: WhatsAppChannel): void {
  statusTarget.value = { kind: 'channel', item: channel }; statusDialog.value = true
}

function openRule(rule: NotificationRule | null = null): void {
  selectedRule.value = rule
  Object.assign(ruleForm, rule ? {
    name: rule.name, documentTypeId: rule.documentTypeId, whatsAppChannelId: rule.whatsAppChannelId,
    whatsAppTemplateId: rule.whatsAppTemplateId, priority: rule.priority, recipients: [...rule.recipients],
    variableMappings: { ...rule.variableMappings }, schedules: rule.schedules.map(({ amount, unit }) => ({ amount, unit })),
  } : {
    name: '', documentTypeId: availableDocumentTypes.value[0]?.id ?? '', whatsAppChannelId: channels.value.find((item) => item.connectionStatus === 'CONNECTED')?.id ?? '',
    whatsAppTemplateId: '', priority: 'NORMAL', recipients: ['EMPLOYEE'], variableMappings: {}, schedules: [{ amount: 30, unit: 'DAY' }],
  })
  dialogError.value = ''; ruleDialog.value = true
}

function addSchedule(): void { if (ruleForm.schedules.length < 3) ruleForm.schedules.push({ amount: 1, unit: 'DAY' }) }
function removeSchedule(index: number): void { if (ruleForm.schedules.length > 1) ruleForm.schedules.splice(index, 1) }

async function saveRule(): Promise<void> {
  if (saving.value) return; saving.value = true; dialogError.value = ''
  try {
    if (selectedRule.value) await updateNotificationRule(selectedRule.value.id, ruleForm)
    else await createNotificationRule(ruleForm)
    ruleDialog.value = false; await load(); Notify.create({ type: 'positive', message: 'Alerta guardada.' })
  } catch (error) { dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar la alerta.' }
  finally { saving.value = false }
}

async function toggleRule(rule: NotificationRule): Promise<void> {
  saving.value = true
  try { await changeNotificationRuleStatus(rule.id, rule.status === 'ACTIVE' ? 'deactivate' : 'activate'); await load() }
  catch (error) { Notify.create({ type: 'negative', message: getProblem(error)?.detail ?? 'No fue posible cambiar la alerta.' }) }
  finally { saving.value = false }
}

function requestRuleStatus(rule: NotificationRule): void {
  statusTarget.value = { kind: 'rule', item: rule }; statusDialog.value = true
}

async function confirmStatus(): Promise<void> {
  if (!statusTarget.value) return
  if (statusTarget.value.kind === 'channel') await toggleChannel(statusTarget.value.item)
  else await toggleRule(statusTarget.value.item)
  statusDialog.value = false
}

async function savePreferences(): Promise<void> {
  saving.value = true
  try {
    await Promise.all([updateNotificationSettings({ timeZoneId: timeZoneId.value, notificationTime: `${notificationTime.value}:00` }), updateMyNotificationContact({ mobilePhone: myPhone.value || null, whatsAppConsent: myConsent.value })])
    Notify.create({ type: 'positive', message: 'Preferencias actualizadas.' })
  } catch (error) { Notify.create({ type: 'negative', message: getProblem(error)?.detail ?? 'No fue posible guardar las preferencias.' }) }
  finally { saving.value = false }
}

async function filterQueue(): Promise<void> { queue.value = await listNotificationQueue(queueStatus.value || undefined) }
function showPreview(template: WhatsAppTemplate): void { selectedTemplate.value = template; previewDialog.value = true }
function showQueueItem(item: NotificationQueueItem): void { selectedQueueItem.value = item; queueDialog.value = true }
function formatDate(value: string | null): string { return value ? new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—' }
function statusTone(status: string): 'success' | 'warning' | 'danger' | 'info' { return ['ACTIVE', 'CONNECTED', 'APPROVED', 'SENT', 'DELIVERED', 'READ'].includes(status) ? 'success' : ['ERROR', 'FAILED'].includes(status) ? 'danger' : ['PENDING', 'PROCESSING', 'UNVERIFIED'].includes(status) ? 'warning' : 'info' }

onMounted(load)
</script>

<template>
  <TenantLayout>
    <PageHeader context="Consola de organización" title="Notificaciones" description="Canales, plantillas aprobadas y alertas documentales en un solo lugar." />
    <AppAlert v-if="errorMessage" tone="danger">{{ errorMessage }}</AppAlert>
    <LoadingSkeleton v-else-if="loading" :rows="5" />
    <template v-else>
      <section class="notification-preferences">
        <q-input v-model="timeZoneId" dense outlined label="Zona horaria" />
        <q-input v-model="notificationTime" dense outlined type="time" label="Hora de envío" />
        <q-input v-model="myPhone" dense outlined label="Mi WhatsApp" hint="Formato +573001234567" />
        <q-checkbox v-model="myConsent" dense label="Autorizo avisos por WhatsApp" />
        <q-btn flat no-caps color="primary" label="Guardar preferencias" :loading="saving" @click="savePreferences" />
      </section>

      <q-tabs v-model="tab" dense align="left" narrow-indicator class="notification-tabs">
        <q-tab name="channels" label="Canales" /><q-tab name="templates" label="Plantillas" />
        <q-tab name="rules" label="Alertas" /><q-tab name="queue" label="Cola" />
      </q-tabs>
      <q-tab-panels v-model="tab" animated class="notification-panels">
        <q-tab-panel name="channels">
          <div class="notification-toolbar"><span>{{ channels.length }} canales</span><q-btn unelevated no-caps color="primary" :icon="icons.addBusiness" label="Nuevo canal" @click="openChannel()" /></div>
          <EmptyState v-if="!channels.length" title="Sin canales" description="Configura el primer número de WhatsApp Business." />
          <div v-else class="compact-list">
            <article v-for="channel in channels" :key="channel.id" class="compact-row">
              <div><strong>{{ channel.name }}</strong><span>{{ channel.displayPhoneNumber || channel.phoneNumberId }}</span></div>
              <StatusChip :tone="statusTone(channel.connectionStatus)" :label="channel.connectionStatus" />
              <span class="compact-row__meta">Sync {{ formatDate(channel.lastSynchronizedAt) }}</span>
              <div class="compact-actions">
                <q-btn flat dense no-caps label="Editar" @click="openChannel(channel)" />
                <q-btn flat dense no-caps label="Probar" :disable="saving" @click="testChannel(channel)" />
                <q-btn flat dense no-caps label="Sincronizar" :disable="channel.connectionStatus !== 'CONNECTED' || saving" @click="syncTemplates(channel)" />
                <q-btn flat dense no-caps :color="channel.status === 'ACTIVE' ? 'negative' : 'positive'" :label="channel.status === 'ACTIVE' ? 'Desactivar' : 'Activar'" @click="requestChannelStatus(channel)" />
              </div>
            </article>
          </div>
        </q-tab-panel>

        <q-tab-panel name="templates">
          <div class="notification-toolbar notification-toolbar--filters">
            <q-input v-model="templateSearch" dense outlined clearable label="Buscar plantilla" />
            <q-select v-model="templateChannel" dense outlined emit-value map-options clearable label="Canal" :options="channels.map((item) => ({ label: item.name, value: item.id }))" />
          </div>
          <EmptyState v-if="!filteredTemplates.length" title="Sin plantillas" description="Conecta y sincroniza un canal para consultar Meta." />
          <div v-else class="compact-list">
            <button v-for="template in filteredTemplates" :key="template.id" class="compact-row compact-row--button" @click="showPreview(template)">
              <div><strong>{{ template.name }}</strong><span>{{ template.category }} · {{ template.language }}</span></div>
              <StatusChip :tone="statusTone(template.status)" :label="template.isAvailable ? template.status : 'NO DISPONIBLE'" />
              <span>{{ template.variables.length }} variables</span><q-icon :name="icons.visibility" />
            </button>
          </div>
        </q-tab-panel>

        <q-tab-panel name="rules">
          <div class="notification-toolbar"><span>{{ rules.length }} alertas</span><q-btn unelevated no-caps color="primary" :icon="icons.notifications" label="Nueva alerta" :disable="!templates.some((item) => item.status === 'APPROVED')" @click="openRule()" /></div>
          <EmptyState v-if="!rules.length" title="Sin alertas" description="Crea una alerta sobre un tipo documental con vencimiento." />
          <div v-else class="compact-list">
            <article v-for="rule in rules" :key="rule.id" class="compact-row">
              <div><strong>{{ rule.name }}</strong><span>{{ rule.documentTypeName }} · {{ rule.templateName }}</span></div>
              <StatusChip :tone="rule.isBlocked ? 'danger' : statusTone(rule.status)" :label="rule.isBlocked ? 'BLOQUEADA' : rule.status" />
              <span>{{ rule.schedules.map((item) => `${item.amount} ${item.unit}`).join(' · ') }}</span>
              <div class="compact-actions"><q-btn flat dense no-caps label="Editar" @click="openRule(rule)" /><q-btn flat dense no-caps :label="rule.status === 'ACTIVE' ? 'Desactivar' : 'Activar'" @click="requestRuleStatus(rule)" /></div>
            </article>
          </div>
        </q-tab-panel>

        <q-tab-panel name="queue">
          <div class="notification-toolbar notification-toolbar--filters">
            <q-select v-model="queueStatus" dense outlined emit-value map-options clearable label="Estado" :options="['PENDING','PROCESSING','SENT','FAILED','CANCELLED'].map((value) => ({ label: value, value }))" @update:model-value="filterQueue" />
            <q-btn flat round dense :icon="icons.refresh" aria-label="Actualizar cola" @click="filterQueue"><q-tooltip>Actualizar cola</q-tooltip></q-btn>
          </div>
          <EmptyState v-if="!queue.length" title="Cola vacía" description="Los envíos y omisiones aparecerán aquí." />
          <div v-else class="compact-list">
            <button v-for="item in queue" :key="item.id" type="button" class="compact-row compact-row--button" @click="showQueueItem(item)">
              <div><strong>{{ item.ruleName }} · {{ item.documentName }}</strong><span>{{ item.recipientType }} · {{ item.destination || 'Sin destino' }}</span></div>
              <StatusChip :tone="statusTone(item.status)" :label="item.deliveryStatus || item.status" />
              <span>{{ formatDate(item.createdAt) }} · {{ item.attemptCount }} intentos</span>
              <q-icon v-if="item.lastError" :name="icons.info"><q-tooltip>{{ item.lastError }}</q-tooltip></q-icon>
            </button>
          </div>
        </q-tab-panel>
      </q-tab-panels>
    </template>

    <AppDialog v-model="channelDialog" :title="selectedChannel ? 'Editar canal' : 'Nuevo canal'" description="Los secretos quedan cifrados y nunca vuelven a mostrarse." :loading="saving">
      <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
      <div class="dialog-grid"><q-input v-model="channelForm.name" dense outlined label="Nombre" /><q-input v-model="channelForm.phoneNumberId" dense outlined label="Phone Number ID" /><q-input v-model="channelForm.businessAccountId" dense outlined label="Business Account ID" /><q-input v-model="channelForm.accessToken" dense outlined type="password" :label="selectedChannel ? 'Nuevo Access Token (opcional)' : 'Access Token'" /><q-input v-model="channelForm.webhookVerifyToken" dense outlined type="password" :label="selectedChannel ? 'Nuevo Verify Token (opcional)' : 'Webhook Verify Token'" /><q-input v-model="channelForm.appSecret" dense outlined type="password" :label="selectedChannel ? 'Nuevo App Secret (opcional)' : 'App Secret'" /></div>
      <template #actions><q-btn flat no-caps label="Cancelar" @click="channelDialog = false" /><q-btn unelevated no-caps color="primary" label="Guardar" @click="saveChannel" /></template>
    </AppDialog>

    <AppDialog v-model="ruleDialog" :title="selectedRule ? 'Editar alerta' : 'Nueva alerta'" description="Documento próximo a vencer" size="lg" :loading="saving">
      <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
      <div class="dialog-grid"><q-input v-model="ruleForm.name" dense outlined label="Nombre" /><q-select v-model="ruleForm.documentTypeId" dense outlined emit-value map-options label="Documento" :options="availableDocumentTypes.map((item) => ({ label: item.name, value: item.id }))" /><q-select v-model="ruleForm.whatsAppChannelId" dense outlined emit-value map-options label="Canal" :options="channels.map((item) => ({ label: item.name, value: item.id }))" /><q-select v-model="ruleForm.whatsAppTemplateId" dense outlined emit-value map-options label="Plantilla aprobada" :options="channelTemplates.map((item) => ({ label: `${item.name} · ${item.language}`, value: item.id }))" /><q-select v-model="ruleForm.priority" dense outlined label="Prioridad" :options="['LOW','NORMAL','HIGH','CRITICAL']" /><q-select v-model="ruleForm.recipients" dense outlined multiple emit-value map-options label="Destinatarios" :options="recipientOptions" /></div>
      <h3 class="dialog-subtitle">Anticipaciones</h3>
      <div v-for="(schedule, index) in ruleForm.schedules" :key="index" class="schedule-row"><q-input v-model.number="schedule.amount" dense outlined type="number" min="1" max="3650" label="Cantidad" /><q-select v-model="schedule.unit" dense outlined :options="(['DAY','WEEK','MONTH'] as ScheduleUnit[])" label="Unidad" /><q-btn flat round dense :icon="icons.close" aria-label="Quitar anticipación" :disable="ruleForm.schedules.length === 1" @click="removeSchedule(index)" /></div>
      <q-btn v-if="ruleForm.schedules.length < 3" flat dense no-caps :icon="icons.addBusiness" label="Agregar aviso" @click="addSchedule" />
      <template v-if="currentRuleTemplate?.variables.length"><h3 class="dialog-subtitle">Variables de Meta</h3><div class="dialog-grid"><q-select v-for="variable in currentRuleTemplate.variables" :key="variable" v-model="ruleForm.variableMappings[variable]" dense outlined :label="variable" :options="eventVariables" /></div></template>
      <template #actions><q-btn flat no-caps label="Cancelar" @click="ruleDialog = false" /><q-btn unelevated no-caps color="primary" label="Guardar" @click="saveRule" /></template>
    </AppDialog>

    <AppDialog v-model="previewDialog" :title="selectedTemplate?.name ?? 'Plantilla'" :description="selectedTemplate ? `${selectedTemplate.category} · ${selectedTemplate.language}` : ''" size="lg">
      <div class="template-preview"><section v-for="(component, index) in previewComponents" :key="index"><small>{{ component.type }}</small><p v-if="component.text">{{ component.text }}</p><q-btn v-for="(button, buttonIndex) in component.buttons" :key="buttonIndex" outline no-caps disable :label="button.text || button.type" /></section></div>
      <div class="template-variables"><StatusChip v-for="variable in selectedTemplate?.variables" :key="variable" tone="info" :label="variable" /></div>
    </AppDialog>

    <AppDialog v-model="queueDialog" :title="selectedQueueItem?.ruleName ?? 'Envío'" :description="selectedQueueItem ? `${selectedQueueItem.documentName} · ${selectedQueueItem.recipientType}` : ''" size="lg">
      <AppAlert v-if="selectedQueueItem?.lastError" tone="danger">{{ selectedQueueItem.lastError }}</AppAlert>
      <h3 class="dialog-subtitle">Valores resueltos</h3><pre class="template-preview">{{ selectedQueueItem?.payloadJson }}</pre>
      <h3 class="dialog-subtitle">Intentos</h3>
      <EmptyState v-if="!selectedQueueItem?.attempts.length" title="Sin intentos" description="El envío todavía no ha sido procesado." />
      <article v-for="attempt in selectedQueueItem?.attempts" :key="attempt.attemptNumber" class="attempt-detail"><strong>Intento {{ attempt.attemptNumber }} · {{ attempt.outcome }}</strong><span>{{ formatDate(attempt.finishedAt) }} · {{ attempt.errorCode || 'Sin error' }}</span><details><summary>Solicitud y respuesta sanitizadas</summary><pre>{{ attempt.requestJson }}</pre><pre v-if="attempt.responseJson">{{ attempt.responseJson }}</pre></details></article>
    </AppDialog>
    <ConfirmDialog v-model="statusDialog" :title="statusTarget?.item.status === 'ACTIVE' ? 'Desactivar configuración' : 'Activar configuración'" :message="statusTarget?.item.status === 'ACTIVE' ? 'Los envíos pendientes que dejen de ser válidos serán cancelados.' : 'La configuración volverá a participar en nuevos envíos.'" :tone="statusTarget?.item.status === 'ACTIVE' ? 'danger' : 'acceptance'" confirm-label="Confirmar" :loading="saving" @confirm="confirmStatus" />
  </TenantLayout>
</template>

<style scoped lang="scss">
.notification-preferences { display: grid; grid-template-columns: 1.4fr 140px 1.2fr auto auto; align-items: center; gap: var(--space-1); padding: var(--space-1-5); border: 1px solid var(--color-border); border-radius: var(--radius); background: var(--color-surface); }
.notification-tabs { margin-top: var(--space-2); border-bottom: 1px solid var(--color-border); }
.notification-panels { background: transparent; }
.notification-panels :deep(.q-tab-panel) { padding: var(--space-2) 0; }
.notification-toolbar { display: flex; min-height: var(--control-height); align-items: center; justify-content: space-between; gap: var(--space-1); margin-bottom: var(--space-1); color: var(--color-text-secondary); }
.notification-toolbar--filters { justify-content: flex-start; }.notification-toolbar--filters > * { min-width: 200px; }
.compact-list { overflow: hidden; border: 1px solid var(--color-border); border-radius: var(--radius); background: var(--color-surface); }
.compact-row { display: grid; grid-template-columns: minmax(220px, 1.6fr) auto minmax(160px, 1fr) auto; min-height: 52px; align-items: center; gap: var(--space-1); padding: var(--space-1) var(--space-1-5); border-bottom: 1px solid var(--color-border); }.compact-row:last-child { border-bottom: 0; }.compact-row strong,.compact-row span { display: block; }.compact-row span { color: var(--color-text-secondary); font-size: var(--font-small); }.compact-row--button { width: 100%; border: 0; border-bottom: 1px solid var(--color-border); color: inherit; text-align: left; cursor: pointer; }.compact-row--button:hover { background: var(--color-surface-hover); }.compact-actions { display: flex; gap: var(--space-0-5); }
.dialog-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-1); }.dialog-subtitle { margin: var(--space-2) 0 var(--space-1); font-size: var(--font-body); }.schedule-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: var(--space-1); margin-bottom: var(--space-1); }.template-preview { overflow: auto; max-height: 360px; margin: 0; padding: var(--space-2); border: 1px solid var(--color-border); border-radius: var(--radius); background: var(--color-canvas); color: var(--color-text-secondary); white-space: pre-wrap; }.template-variables { display: flex; flex-wrap: wrap; gap: var(--space-0-5); margin-top: var(--space-1); }
.template-preview section + section,.attempt-detail + .attempt-detail { margin-top: var(--space-1); padding-top: var(--space-1); border-top: 1px solid var(--color-border); }.template-preview p { margin: var(--space-0-5) 0; }.attempt-detail span { display: block; color: var(--color-text-secondary); font-size: var(--font-small); }.attempt-detail pre { overflow: auto; white-space: pre-wrap; }
@media (max-width: 900px) { .notification-preferences { grid-template-columns: 1fr 1fr; }.compact-row { grid-template-columns: 1fr auto; }.compact-row__meta { display: none !important; }.compact-actions { grid-column: 1 / -1; flex-wrap: wrap; }.dialog-grid { grid-template-columns: 1fr; } }
@media (max-width: 560px) { .notification-preferences { grid-template-columns: 1fr; }.notification-toolbar--filters { align-items: stretch; flex-direction: column; }.notification-toolbar--filters > * { width: 100%; }.compact-row { grid-template-columns: 1fr; }.schedule-row { grid-template-columns: 1fr 1fr auto; } }
</style>
