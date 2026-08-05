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
import {
  assignEmployee,
  endEmployeeAssignment,
  endEmploymentRelationship,
  getEmployee,
  listJobPositions,
  makePrimaryEmployeeAssignment,
  transitionEmployeeAssignment,
} from '../services/employees'
import type { Branch } from '../types/branches'
import type { EmployeeAssignment, EmployeeDetail, JobPosition } from '../types/employees'
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
const positions = ref<JobPosition[]>([])
const branches = ref<Branch[]>([])
const assignmentDialog = ref(false)
const endDialog = ref(false)
const endRelationshipDialog = ref(false)
const selectedAssignment = ref<EmployeeAssignment | null>(null)
const assignmentMode = ref<'add' | 'transition'>('add')
const endDate = ref(today)
const form = reactive({ branchId: '', jobPositionId: '', effectiveOn: today, isPrimary: false })

const activeRelationship = computed(() => employee.value?.employmentRelationships.find((item) => item.status === 'ACTIVE') ?? null)
const activeAssignments = computed(() => activeRelationship.value?.assignments.filter((item) => item.status === 'ACTIVE') ?? [])
const canSaveAssignment = computed(() => Boolean(form.branchId && form.jobPositionId && form.effectiveOn))

function formatDate(value: string | null): string {
  if (!value) return 'Actualidad'
  return new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`))
}

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    if (isSuperAdmin.value) {
      const [detail, loadedPositions, loadedBranches] = await Promise.all([
        getEmployee(employeeId.value),
        listJobPositions('ACTIVE'),
        listBranches({ page: 1, pageSize: 100, status: 'ACTIVE' }),
      ])
      employee.value = detail
      positions.value = loadedPositions
      branches.value = loadedBranches.items
    } else {
      employee.value = await getEmployee(employeeId.value)
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar el trabajador.'
  } finally {
    loading.value = false
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
    <main class="platform-content">
      <PageHeader context="Trabajadores" :title="employee ? `${employee.firstName} ${employee.lastName}` : 'Detalle del trabajador'" :description="employee ? `${employee.documentType} ${employee.documentNumber}` : 'Relación laboral y asignaciones.'" :back-to="backTo" back-label="Volver">
        <template v-if="employee && isSuperAdmin" #actions><q-btn unelevated no-caps color="primary" :icon="icons.personAdd" :label="activeRelationship ? 'Nueva asignación' : 'Recontratar y asignar'" @click="openAddAssignment" /></template>
      </PageHeader>
      <LoadingSkeleton v-if="loading" variant="form" :rows="6" />
      <AppAlert v-else-if="errorMessage && !employee" tone="danger">{{ errorMessage }}<template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="load" /></template></AppAlert>
      <template v-else-if="employee">
        <AppAlert v-if="errorMessage" tone="danger">{{ errorMessage }}</AppAlert>
        <q-card flat bordered class="platform-card employment-summary">
          <q-card-section class="section-heading">
            <q-icon :name="icons.groups" />
            <div><h2>Relación laboral</h2><p v-if="activeRelationship">Vigente desde {{ formatDate(activeRelationship.startedOn) }}.</p><p v-else>No existe una relación laboral activa.</p></div>
            <q-space />
            <StatusChip :tone="activeRelationship ? 'success' : 'neutral'" :label="activeRelationship ? 'Activa' : 'Finalizada'" />
            <q-btn v-if="activeRelationship && isSuperAdmin" outline no-caps color="negative" :icon="icons.block" label="Finalizar relación" @click="requestEndRelationship" />
          </q-card-section>
        </q-card>

        <section v-for="relationship in employee.employmentRelationships" :key="relationship.id" class="employment-period">
          <div class="employment-period__header">
            <div><h2>{{ relationship.status === 'ACTIVE' ? 'Asignaciones actuales' : 'Relación finalizada' }}</h2><p>{{ formatDate(relationship.startedOn) }} — {{ formatDate(relationship.endedOn) }}</p></div>
            <StatusChip :tone="relationship.status === 'ACTIVE' ? 'success' : 'neutral'" :label="relationship.status === 'ACTIVE' ? 'Activa' : 'Finalizada'" />
          </div>
          <div class="assignment-grid">
            <q-card v-for="assignment in relationship.assignments" :key="assignment.id" flat bordered class="assignment-card">
              <q-card-section>
                <div class="assignment-card__heading"><div><strong>{{ assignment.jobPositionName }}</strong><span>{{ assignment.branchName }}</span></div><StatusChip v-if="assignment.isPrimary" tone="info" label="Principal" /></div>
                <p>{{ formatDate(assignment.startedOn) }} — {{ formatDate(assignment.endedOn) }}</p>
              </q-card-section>
              <q-card-actions v-if="assignment.status === 'ACTIVE' && isSuperAdmin" align="right">
                <q-btn v-if="!assignment.isPrimary" flat no-caps label="Hacer principal" :disable="saving" @click="makePrimary(assignment)" />
                <q-btn flat no-caps color="primary" label="Cambiar" @click="openTransition(assignment)" />
                <q-btn flat no-caps color="negative" label="Finalizar" @click="requestEndAssignment(assignment)" />
              </q-card-actions>
            </q-card>
            <p v-if="relationship.assignments.length === 0" class="empty-copy">Esta relación no tiene asignaciones.</p>
          </div>
        </section>
        <q-card v-if="employee.employmentRelationships.length === 0" flat bordered class="platform-card"><q-card-section><p class="empty-copy">El trabajador aún no tiene historial laboral.</p></q-card-section></q-card>
      </template>
    </main>
  </TenantLayout>

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
