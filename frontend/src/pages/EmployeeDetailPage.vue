<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppAlert from '../components/AppAlert.vue'
import AssignmentFields from '../components/AssignmentFields.vue'
import AppDialog from '../components/AppDialog.vue'
import LoadingSkeleton from '../components/LoadingSkeleton.vue'
import PageHeader from '../components/PageHeader.vue'
import StatusChip from '../components/StatusChip.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import { listBranches } from '../services/branches'
import { getEmployeeDocumentSummary, uploadEmployeeDocument } from '../services/employeeDocuments'
import {
  assignEmployee,
  endEmployeeAssignment,
  endEmploymentRelationship,
  getEmployee,
  listJobPositions,
  makePrimaryEmployeeAssignment,
  transitionEmployeeAssignment,
  updateEmployeeNotificationContact,
} from '../services/employees'
import type { Branch } from '../types/branches'
import type { EmployeeAssignment, EmployeeDetail, JobPosition } from '../types/employees'
import type { EmployeeDocumentCategory, EmployeeDocumentSummary, EmployeeDocumentTypeOption } from '../types/employeeDocuments'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const auth = useAuthStore()
const isSuperAdmin = computed(() => auth.account?.roles.includes('SUPER_ADMIN') === true)
const employeeId = computed(() => route.params.id as string)
const backTo = computed(() => typeof route.query.branchId === 'string' ? `/app/branches/${route.query.branchId}` : '/app/branches')
const today = new Date().toISOString().slice(0, 10)
const loading = ref(true)
const saving = ref(false)
const errorMessage = ref('')
const dialogError = ref('')
const employee = ref<EmployeeDetail | null>(null)
const documentSummary = ref<EmployeeDocumentSummary | null>(null)
const positions = ref<JobPosition[]>([])
const branches = ref<Branch[]>([])
const assignmentDialog = ref(false)
const contactDialog = ref(false)
const endDialog = ref(false)
const endRelationshipDialog = ref(false)
const selectedAssignment = ref<EmployeeAssignment | null>(null)
const selectedDocumentCategory = ref<EmployeeDocumentCategory | null>(null)
const documentDialog = ref(false)
const assignmentMode = ref<'add' | 'transition'>('add')
const endDate = ref(today)
const form = reactive({ branchId: '', jobPositionId: '', effectiveOn: today, isPrimary: false })
const documentForm = reactive({ documentTypeId: '', issuedOn: '', expiresOn: '', files: [] as InstanceType<typeof globalThis.File>[], link: '' })
const contactForm = reactive({ mobilePhone: '', contactEmail: '', whatsAppConsent: false })

const activeRelationship = computed(() => employee.value?.employmentRelationships.find((item) => item.status === 'ACTIVE') ?? null)
const activeAssignments = computed(() => activeRelationship.value?.assignments.filter((item) => item.status === 'ACTIVE') ?? [])
const canSaveAssignment = computed(() => Boolean(form.branchId && form.jobPositionId && form.effectiveOn))
const selectedDocumentType = computed<EmployeeDocumentTypeOption | null>(() =>
  selectedDocumentCategory.value?.documentTypes.find((item) => item.id === documentForm.documentTypeId) ?? null)
const documentFileAccept = computed(() => {
  const kinds = selectedDocumentType.value?.allowedEvidenceKinds ?? []
  return [kinds.includes('PDF') ? 'application/pdf' : '', kinds.includes('IMAGE') ? 'image/*' : '', kinds.includes('VIDEO') ? 'video/mp4,video/webm' : ''].filter(Boolean).join(',')
})
const documentAllowsLink = computed(() => selectedDocumentType.value?.allowedEvidenceKinds.includes('LINK') === true)
const canSaveDocument = computed(() => {
  const evidenceCount = documentForm.files.length + (documentForm.link.trim() ? 1 : 0)
  return Boolean(documentForm.documentTypeId && evidenceCount && (selectedDocumentType.value?.allowsMultipleEvidenceItems || evidenceCount === 1))
})

function formatDate(value: string | null): string {
  if (!value) return 'Actualidad'
  return new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`))
}

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    if (isSuperAdmin.value) {
      const [detail, loadedPositions, loadedBranches, documents] = await Promise.all([
        getEmployee(employeeId.value),
        listJobPositions('ACTIVE'),
        listBranches({ page: 1, pageSize: 100, status: 'ACTIVE' }),
        getEmployeeDocumentSummary(employeeId.value),
      ])
      employee.value = detail
      positions.value = loadedPositions
      branches.value = loadedBranches.items
      documentSummary.value = documents
    } else {
      const [detail, documents] = await Promise.all([getEmployee(employeeId.value), getEmployeeDocumentSummary(employeeId.value)])
      employee.value = detail
      documentSummary.value = documents
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar el trabajador.'
  } finally {
    loading.value = false
  }
}

function openDocumentCategory(category: EmployeeDocumentCategory): void {
  selectedDocumentCategory.value = category
  const initial = category.documentTypes.find((item) => item.isMissing) ?? category.documentTypes[0]
  Object.assign(documentForm, { documentTypeId: initial?.id ?? '', issuedOn: '', expiresOn: '', files: [], link: '' })
  dialogError.value = ''
  documentDialog.value = true
}

function resetDocumentEvidence(): void {
  Object.assign(documentForm, { issuedOn: '', expiresOn: '', files: [], link: '' })
}

async function saveDocument(): Promise<void> {
  if (!canSaveDocument.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    documentSummary.value = await uploadEmployeeDocument(employeeId.value, {
      documentTypeId: documentForm.documentTypeId,
      issuedOn: documentForm.issuedOn,
      expiresOn: documentForm.expiresOn,
      files: documentForm.files,
      links: documentForm.link.trim() ? [documentForm.link.trim()] : [],
    })
    documentDialog.value = false
    Notify.create({ type: 'positive', message: 'Documento cargado.' })
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible cargar el documento.'
  } finally {
    saving.value = false
  }
}

function openContact(): void {
  Object.assign(contactForm, {
    mobilePhone: employee.value?.mobilePhone ?? '',
    contactEmail: employee.value?.contactEmail ?? '',
    whatsAppConsent: Boolean(employee.value?.whatsAppConsentAt),
  })
  dialogError.value = ''
  contactDialog.value = true
}

async function saveContact(): Promise<void> {
  if (saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    employee.value = await updateEmployeeNotificationContact(employeeId.value, {
      mobilePhone: contactForm.mobilePhone || null,
      contactEmail: contactForm.contactEmail || null,
      whatsAppConsent: contactForm.whatsAppConsent,
    })
    contactDialog.value = false
    Notify.create({ type: 'positive', message: 'Contacto de notificaciones actualizado.' })
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible actualizar el contacto.'
  } finally {
    saving.value = false
  }
}

function openAddAssignment(): void {
  assignmentMode.value = 'add'
  selectedAssignment.value = null
  Object.assign(form, {
    branchId: typeof route.query.branchId === 'string' ? route.query.branchId : branches.value[0]?.id ?? '',
    jobPositionId: positions.value[0]?.id ?? '',
    effectiveOn: today,
    isPrimary: activeAssignments.value.every((item) => !item.isPrimary),
  })
  dialogError.value = ''
  assignmentDialog.value = true
}

function openTransition(assignment: EmployeeAssignment): void {
  assignmentMode.value = 'transition'
  selectedAssignment.value = assignment
  Object.assign(form, {
    branchId: assignment.branchId,
    jobPositionId: assignment.jobPositionId,
    effectiveOn: today,
    isPrimary: assignment.isPrimary,
  })
  dialogError.value = ''
  assignmentDialog.value = true
}

async function saveAssignment(): Promise<void> {
  if (!canSaveAssignment.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    if (assignmentMode.value === 'transition' && selectedAssignment.value) {
      employee.value = await transitionEmployeeAssignment(employeeId.value, selectedAssignment.value.id, {
        branchId: form.branchId,
        jobPositionId: form.jobPositionId,
        effectiveOn: form.effectiveOn,
      })
      Notify.create({ type: 'positive', message: 'Asignación actualizada conservando el historial.' })
    } else {
      employee.value = await assignEmployee(form.branchId, employeeId.value, {
        jobPositionId: form.jobPositionId,
        startedOn: form.effectiveOn,
        isPrimary: form.isPrimary,
        administrativeAccess: null,
      })
      Notify.create({ type: 'positive', message: activeRelationship.value ? 'Asignación creada.' : 'Trabajador recontratado y asignado.' })
    }
    assignmentDialog.value = false
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar la asignación.'
  } finally {
    saving.value = false
  }
}

function requestEndAssignment(assignment: EmployeeAssignment): void {
  selectedAssignment.value = assignment
  endDate.value = today
  dialogError.value = ''
  endDialog.value = true
}

async function confirmEndAssignment(): Promise<void> {
  if (!selectedAssignment.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    employee.value = await endEmployeeAssignment(employeeId.value, selectedAssignment.value.id, endDate.value)
    endDialog.value = false
    Notify.create({ type: 'positive', message: 'Asignación finalizada.' })
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible finalizar la asignación.'
  } finally {
    saving.value = false
  }
}

async function makePrimary(assignment: EmployeeAssignment): Promise<void> {
  if (saving.value) return
  saving.value = true
  errorMessage.value = ''
  try {
    employee.value = await makePrimaryEmployeeAssignment(employeeId.value, assignment.id)
    Notify.create({ type: 'positive', message: 'Asignación principal actualizada.' })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cambiar la asignación principal.'
  } finally {
    saving.value = false
  }
}

async function confirmEndRelationship(): Promise<void> {
  if (!activeRelationship.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    employee.value = await endEmploymentRelationship(employeeId.value, activeRelationship.value.id, endDate.value)
    endRelationshipDialog.value = false
    Notify.create({ type: 'positive', message: 'Relación laboral finalizada y acceso administrativo suspendido.' })
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible finalizar la relación laboral.'
  } finally {
    saving.value = false
  }
}

function requestEndRelationship(): void {
  endDate.value = today
  dialogError.value = ''
  endRelationshipDialog.value = true
}

onMounted(load)
</script>

<template>
  <TenantLayout>
    <main class="platform-content employee-detail-page">
      <PageHeader context="Trabajadores" :title="employee ? `${employee.firstName} ${employee.lastName}` : 'Detalle del trabajador'" :description="employee ? `${employee.documentType} ${employee.documentNumber}` : 'Relación laboral y asignaciones.'" :back-to="backTo" back-label="Volver">
        <template v-if="employee && isSuperAdmin" #actions><q-btn flat no-caps :icon="icons.notifications" label="Contacto" @click="openContact" /><q-btn unelevated no-caps color="primary" :icon="icons.personAdd" :label="activeRelationship ? 'Nueva asignación' : 'Recontratar y asignar'" @click="openAddAssignment" /></template>
      </PageHeader>
      <LoadingSkeleton v-if="loading" variant="form" :rows="6" />
      <AppAlert v-else-if="errorMessage && !employee" tone="danger">{{ errorMessage }}<template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="load" /></template></AppAlert>
      <template v-else-if="employee">
        <AppAlert v-if="errorMessage" tone="danger">{{ errorMessage }}</AppAlert>
        <div class="employee-overview-grid">
          <q-card flat bordered class="employee-mini-card">
            <q-card-section>
              <div class="employee-mini-card__heading"><span>Relación laboral</span><StatusChip :tone="activeRelationship ? 'success' : 'neutral'" :label="activeRelationship ? 'Activa' : 'Finalizada'" /></div>
              <strong>{{ activeRelationship ? `Desde ${formatDate(activeRelationship.startedOn)}` : 'Sin relación activa' }}</strong>
              <q-btn v-if="activeRelationship && isSuperAdmin" flat dense no-caps color="negative" :icon="icons.block" label="Finalizar" @click="requestEndRelationship" />
            </q-card-section>
          </q-card>
          <q-card flat bordered class="employee-mini-card">
            <q-card-section>
              <div class="employee-mini-card__heading"><span>Obligatorios pendientes</span><strong>{{ documentSummary?.missingCount ?? 0 }}</strong></div>
              <div class="compact-document-list">
                <span v-for="item in documentSummary?.missingDocuments.slice(0, 3)" :key="item.documentTypeId">{{ item.name }}</span>
                <small v-if="(documentSummary?.missingCount ?? 0) > 3">+{{ (documentSummary?.missingCount ?? 0) - 3 }} más</small>
                <small v-if="documentSummary?.missingCount === 0">Documentación obligatoria completa</small>
              </div>
            </q-card-section>
          </q-card>
          <q-card flat bordered class="employee-mini-card">
            <q-card-section>
              <div class="employee-mini-card__heading"><span>Próximos vencimientos</span><strong>{{ documentSummary?.upcomingExpirations.length ?? 0 }}</strong></div>
              <div class="compact-document-list">
                <span v-for="item in documentSummary?.upcomingExpirations.slice(0, 3)" :key="item.employeeDocumentId">{{ item.name }} · {{ formatDate(item.expiresOn) }}</span>
                <small v-if="documentSummary?.upcomingExpirations.length === 0">Sin vencimientos en los próximos 2 meses</small>
              </div>
            </q-card-section>
          </q-card>
        </div>

        <section v-if="documentSummary?.categories.length" class="employee-document-actions" aria-label="Cargar documentos por categoría">
          <q-btn v-for="category in documentSummary.categories" :key="category.id" outline dense no-caps color="primary" :icon="icons.upload" :label="`${category.name}${category.missingCount ? ` · ${category.missingCount}` : ''}`" @click="openDocumentCategory(category)" />
        </section>

        <q-card flat bordered class="employee-assignments-panel">
          <q-card-section class="employee-panel-heading"><strong>Asignaciones actuales</strong><span>{{ activeAssignments.length }}</span></q-card-section>
          <q-list separator>
            <q-item v-for="assignment in activeAssignments" :key="assignment.id" class="employee-assignment-row">
              <q-item-section><q-item-label><strong>{{ assignment.jobPositionName }}</strong> · {{ assignment.branchName }}</q-item-label><q-item-label caption>Desde {{ formatDate(assignment.startedOn) }}</q-item-label></q-item-section>
              <q-item-section side><StatusChip v-if="assignment.isPrimary" tone="info" label="Principal" /></q-item-section>
              <q-item-section v-if="isSuperAdmin" side class="employee-row-actions">
                <q-btn v-if="!assignment.isPrimary" flat round dense :icon="icons.check" aria-label="Hacer principal" :disable="saving" @click="makePrimary(assignment)"><q-tooltip>Hacer principal</q-tooltip></q-btn>
                <q-btn flat round dense color="primary" :icon="icons.tune" aria-label="Cambiar asignación" @click="openTransition(assignment)"><q-tooltip>Cambiar</q-tooltip></q-btn>
                <q-btn flat round dense color="negative" :icon="icons.block" aria-label="Finalizar asignación" @click="requestEndAssignment(assignment)"><q-tooltip>Finalizar</q-tooltip></q-btn>
              </q-item-section>
            </q-item>
            <q-item v-if="activeAssignments.length === 0"><q-item-section><q-item-label caption>Sin asignaciones activas.</q-item-label></q-item-section></q-item>
          </q-list>
        </q-card>

        <q-expansion-item v-if="employee.employmentRelationships.some((item) => item.status === 'ENDED')" dense dense-toggle label="Historial laboral" class="employee-history">
          <q-list separator>
            <template v-for="relationship in employee.employmentRelationships.filter((item) => item.status === 'ENDED')" :key="relationship.id">
              <q-item v-for="assignment in relationship.assignments" :key="assignment.id">
                <q-item-section><q-item-label>{{ assignment.jobPositionName }} · {{ assignment.branchName }}</q-item-label><q-item-label caption>{{ formatDate(assignment.startedOn) }} — {{ formatDate(assignment.endedOn) }}</q-item-label></q-item-section>
              </q-item>
            </template>
          </q-list>
        </q-expansion-item>
      </template>
    </main>
  </TenantLayout>

  <AppDialog v-if="isSuperAdmin" v-model="contactDialog" title="Contacto de notificaciones" description="El consentimiento es obligatorio para enviar WhatsApp." :icon="icons.notifications" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form class="compact-upload-form" @submit.prevent="saveContact">
      <q-input v-model="contactForm.mobilePhone" outlined dense label="Teléfono móvil" hint="Formato +573001234567" />
      <q-input v-model="contactForm.contactEmail" outlined dense type="email" label="Correo de contacto" />
      <q-checkbox v-model="contactForm.whatsAppConsent" dense label="El trabajador autorizó notificaciones por WhatsApp" />
      <q-card-actions align="right" class="q-px-none q-pt-sm"><q-btn flat dense no-caps label="Cancelar" :disable="saving" @click="contactDialog = false" /><q-btn unelevated dense no-caps color="primary" type="submit" label="Guardar" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <AppDialog v-model="documentDialog" :title="`Cargar · ${selectedDocumentCategory?.name ?? ''}`" description="Registra una nueva versión documental." :icon="icons.upload" size="md" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form class="compact-upload-form" @submit.prevent="saveDocument">
      <q-select v-model="documentForm.documentTypeId" outlined dense emit-value map-options :options="selectedDocumentCategory?.documentTypes ?? []" option-label="name" option-value="id" label="Documento" @update:model-value="resetDocumentEvidence" />
      <div class="compact-form-grid">
        <q-input v-if="selectedDocumentType?.issueDateMode !== 'NEVER'" v-model="documentForm.issuedOn" outlined dense type="date" label="Expedición" :max="today" :rules="selectedDocumentType?.issueDateMode === 'REQUIRED' ? [(value: string) => Boolean(value) || 'Obligatoria'] : []" />
        <q-input v-if="selectedDocumentType?.expirationDateMode !== 'NEVER'" v-model="documentForm.expiresOn" outlined dense type="date" label="Vencimiento" :min="today" :rules="selectedDocumentType?.expirationDateMode === 'REQUIRED' ? [(value: string) => Boolean(value) || 'Obligatoria'] : []" />
      </div>
      <q-file
        v-if="documentFileAccept"
        v-model="documentForm.files"
        outlined
        dense
        use-chips
        counter
        multiple
        :max-files="selectedDocumentType?.allowsMultipleEvidenceItems ? undefined : 1"
        :accept="documentFileAccept"
        :label="selectedDocumentType?.allowsMultipleEvidenceItems ? 'Archivos' : 'Archivo'"
        :hint="selectedDocumentType?.allowsMultipleEvidenceItems ? 'Puedes combinar varios PDF, imágenes o videos permitidos.' : 'Este documento admite una sola evidencia.'"
      />
      <q-input v-if="documentAllowsLink" v-model="documentForm.link" outlined dense type="url" label="Enlace HTTPS" />
      <q-card-actions align="right" class="q-px-none q-pt-sm"><q-btn flat dense no-caps label="Cancelar" :disable="saving" @click="documentDialog = false" /><q-btn unelevated dense no-caps color="primary" type="submit" label="Cargar" :disable="!canSaveDocument" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <AppDialog v-if="isSuperAdmin" v-model="assignmentDialog" :title="assignmentMode === 'transition' ? 'Cambiar asignación' : activeRelationship ? 'Nueva asignación' : 'Recontratar trabajador'" :description="assignmentMode === 'transition' ? 'La asignación anterior finalizará el día previo a la fecha efectiva.' : 'Selecciona la sucursal, el cargo y la fecha de inicio.'" :icon="icons.tune" size="lg" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form @submit.prevent="saveAssignment">
      <AssignmentFields v-model:branch-id="form.branchId" v-model:job-position-id="form.jobPositionId" v-model:started-on="form.effectiveOn" v-model:is-primary="form.isPrimary" :positions="positions" :branches="branches" show-branch :show-primary="assignmentMode === 'add'" :date-label="assignmentMode === 'transition' ? 'Fecha efectiva' : 'Fecha de inicio'" :max-date="today" />
      <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="assignmentDialog = false" /><q-btn unelevated no-caps color="primary" type="submit" label="Guardar" :disable="!canSaveAssignment" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <AppDialog v-if="isSuperAdmin" v-model="endDialog" title="Finalizar asignación" description="La asignación quedará disponible en el historial." :icon="icons.block" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-input v-model="endDate" outlined type="date" :min="selectedAssignment?.startedOn" :max="today" label="Fecha de finalización" />
    <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="endDialog = false" /><q-btn unelevated no-caps color="negative" label="Finalizar" :loading="saving" @click="confirmEndAssignment" /></q-card-actions>
  </AppDialog>

  <AppDialog v-if="isSuperAdmin" v-model="endRelationshipDialog" title="Finalizar relación laboral" description="Se cerrarán todas las asignaciones activas y se suspenderá inmediatamente la cuenta administrativa vinculada." :icon="icons.warning" tone="danger" :persistent="saving">
    <q-input v-model="endDate" outlined type="date" :min="activeRelationship?.startedOn" :max="today" label="Fecha de finalización" />
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="endRelationshipDialog = false" /><q-btn unelevated no-caps color="negative" label="Finalizar relación" :loading="saving" @click="confirmEndRelationship" /></q-card-actions>
  </AppDialog>
</template>
