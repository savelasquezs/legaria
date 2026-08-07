<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Notify } from 'quasar'
import AppAlert from './AppAlert.vue'
import AssignmentFields from './AssignmentFields.vue'
import AppDataTable from './AppDataTable.vue'
import AppDialog from './AppDialog.vue'
import ConfirmDialog from './ConfirmDialog.vue'
import SearchField from './SearchField.vue'
import StatusChip from './StatusChip.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import {
  assignEmployee,
  createEmployee,
  createJobPosition,
  grantEmployeeAdministrativeAccess,
  listEmployees,
  listJobPositions,
} from '../services/employees'
import {
  changeBranchAdministratorStatus,
  listBranches,
  resendBranchAdministratorInvitation,
  updateBranchAdministratorAssignments,
} from '../services/branches'
import type { Employee, EmployeePage, JobPosition } from '../types/employees'
import type { Branch } from '../types/branches'

const props = defineProps<{ branchId: string; branchActive: boolean; readOnly?: boolean }>()
const router = useRouter()

const emptyPage = (): EmployeePage => ({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 })
const result = ref<EmployeePage>(emptyPage())
const candidates = ref<EmployeePage>(emptyPage())
const positions = ref<JobPosition[]>([])
const availableBranches = ref<Branch[]>([])
const loading = ref(true)
const saving = ref(false)
const searching = ref(false)
const creatingPosition = ref(false)
const dialog = ref(false)
const statusDialog = ref(false)
const changingAccountStatus = ref(false)
const mode = ref<'create' | 'existing' | 'access'>('create')
const selectedEmployee = ref<Employee | null>(null)
const statusEmployee = ref<Employee | null>(null)
const search = ref('')
const candidateSearch = ref('')
const errorMessage = ref('')
const dialogError = ref('')
const newPositionName = ref('')
const today = new Date().toISOString().slice(0, 10)

const form = reactive({
  documentType: '',
  documentNumber: '',
  firstName: '',
  lastName: '',
  mobilePhone: '',
  contactEmail: '',
  whatsAppConsent: false,
  jobPositionId: '',
  startedOn: today,
  isPrimary: true,
  grantAccess: false,
  email: '',
  accessBranchIds: [] as string[],
})

const columns = computed(() => [
  { name: 'employee', label: 'Trabajador', field: 'firstName', align: 'left' as const },
  { name: 'document', label: 'Documento', field: 'documentNumber', align: 'left' as const },
  { name: 'position', label: 'Cargo', field: 'position', align: 'left' as const },
  ...(!props.readOnly ? [{ name: 'access', label: 'Acceso', field: 'access', align: 'left' as const }] : []),
])

const requiredRule = (value: string) => Boolean(value?.trim()) || 'Este campo es obligatorio.'
const emailRule = (value: string) =>
  !form.grantAccess || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) || 'Ingresa un correo válido.'
const canSave = computed(() => {
  if (mode.value === 'access') {
    return form.accessBranchIds.length > 0 && Boolean(selectedEmployee.value?.administrativeAccess || form.email.trim())
  }
  return Boolean(
    form.jobPositionId &&
    form.startedOn &&
    (mode.value === 'existing'
      ? selectedEmployee.value
      : form.documentType.trim() && form.documentNumber.trim() && form.firstName.trim() && form.lastName.trim()) &&
    (!form.grantAccess || (form.email.trim() && form.accessBranchIds.length > 0)),
  )
})

function assignmentForBranch(employee: Employee) {
  return employee.assignments.find((item) => item.branchId === props.branchId)
}

function invitationLabel(employee: Employee): string {
  const access = employee.administrativeAccess
  if (!access) return 'Sin acceso'
  if (access.accountStatus === 'SUSPENDED') return 'Suspendido'
  const labels: Record<string, string> = {
    ACCEPTED: 'Administrador activo',
    SENT: 'Invitación enviada',
    DELIVERY_FAILED: 'Error de envío',
    EXPIRED: 'Invitación vencida',
    REVOKED: 'Invitación revocada',
    PENDING_DELIVERY: 'Pendiente de envío',
  }
  return labels[access.invitationStatus] ?? access.invitationStatus
}

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    if (props.readOnly) {
      result.value = await listEmployees({
        page: result.value.page,
        pageSize: 10,
        search: search.value || undefined,
        branchId: props.branchId,
      })
    } else {
      const [employees, loadedPositions, branches] = await Promise.all([
        listEmployees({ page: result.value.page, pageSize: 10, search: search.value || undefined, branchId: props.branchId }),
        listJobPositions(),
        listBranches({ page: 1, pageSize: 100, status: 'ACTIVE' }),
      ])
      result.value = employees
      positions.value = loadedPositions
      availableBranches.value = branches.items
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar los trabajadores.'
  } finally {
    loading.value = false
  }
}

async function loadCandidates(): Promise<void> {
  searching.value = true
  dialogError.value = ''
  try {
    candidates.value = await listEmployees({
      page: 1,
      pageSize: 20,
      search: candidateSearch.value || undefined,
      excludeBranchId: props.branchId,
    })
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible buscar trabajadores.'
  } finally {
    searching.value = false
  }
}

function resetForm(): void {
  Object.assign(form, {
    documentType: '',
    documentNumber: '',
    firstName: '',
    lastName: '',
    mobilePhone: '',
    contactEmail: '',
    whatsAppConsent: false,
    jobPositionId: positions.value[0]?.id ?? '',
    startedOn: today,
    isPrimary: true,
    grantAccess: false,
    email: '',
    accessBranchIds: [props.branchId],
  })
  selectedEmployee.value = null
  candidateSearch.value = ''
  dialogError.value = ''
}

async function openDialog(selectedMode: 'create' | 'existing'): Promise<void> {
  mode.value = selectedMode
  resetForm()
  dialog.value = true
  if (selectedMode === 'existing') await loadCandidates()
}

function selectEmployee(employee: Employee): void {
  selectedEmployee.value = employee
  form.grantAccess = Boolean(employee.administrativeAccess)
  form.email = employee.administrativeAccess?.email ?? ''
  form.accessBranchIds = employee.administrativeAccess
    ? [...new Set([...employee.administrativeAccess.branchIds, props.branchId])]
    : [props.branchId]
  form.isPrimary = employee.assignments.every((item) => !item.isPrimary)
}

function openAccess(employee: Employee): void {
  mode.value = 'access'
  resetForm()
  selectedEmployee.value = employee
  form.grantAccess = true
  form.email = employee.administrativeAccess?.email ?? ''
  form.accessBranchIds = employee.administrativeAccess?.branchIds.slice() ?? [props.branchId]
  dialog.value = true
}

async function resend(employee: Employee): Promise<void> {
  if (!employee.administrativeAccess) return
  try {
    await resendBranchAdministratorInvitation(employee.administrativeAccess.accountId)
    Notify.create({ type: 'positive', message: 'Invitación reenviada.' })
    await load()
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible reenviar la invitación.'
  }
}

function requestAccountStatusChange(employee: Employee): void {
  statusEmployee.value = employee
  statusDialog.value = true
}

async function toggleAccountStatus(): Promise<void> {
  const employee = statusEmployee.value
  if (!employee || changingAccountStatus.value) return
  const access = employee.administrativeAccess
  if (!access) return
  const suspending = access.accountStatus === 'ACTIVE'
  changingAccountStatus.value = true
  try {
    await changeBranchAdministratorStatus(access.accountId, suspending ? 'suspend' : 'reactivate')
    Notify.create({ type: 'positive', message: suspending ? 'Cuenta suspendida.' : 'Cuenta reactivada.' })
    statusDialog.value = false
    await load()
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cambiar el estado de la cuenta.'
  } finally {
    changingAccountStatus.value = false
  }
}

async function addPosition(): Promise<void> {
  if (!newPositionName.value.trim() || creatingPosition.value) return
  creatingPosition.value = true
  dialogError.value = ''
  try {
    const created = await createJobPosition(newPositionName.value.trim())
    positions.value = [...positions.value, created].sort((a, b) => a.name.localeCompare(b.name, 'es'))
    form.jobPositionId = created.id
    newPositionName.value = ''
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible crear el cargo.'
  } finally {
    creatingPosition.value = false
  }
}

async function save(): Promise<void> {
  if (!canSave.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  const administrativeAccess = form.grantAccess
    ? { email: form.email.trim() || null, branchIds: form.accessBranchIds }
    : null
  try {
    if (mode.value === 'access' && selectedEmployee.value) {
      if (selectedEmployee.value.administrativeAccess) {
        await updateBranchAdministratorAssignments(
          selectedEmployee.value.administrativeAccess.accountId,
          form.accessBranchIds,
        )
        Notify.create({ type: 'positive', message: 'Sucursales autorizadas actualizadas.' })
      } else {
        await grantEmployeeAdministrativeAccess(selectedEmployee.value.id, {
          email: form.email.trim(),
          branchIds: form.accessBranchIds,
        })
        Notify.create({ type: 'positive', message: 'Cuenta creada e invitación enviada.' })
      }
    } else if (mode.value === 'create') {
      await createEmployee(props.branchId, {
        documentType: form.documentType,
        documentNumber: form.documentNumber,
        firstName: form.firstName,
        lastName: form.lastName,
        jobPositionId: form.jobPositionId,
        startedOn: form.startedOn,
        isPrimary: form.isPrimary,
        administrativeAccess,
        mobilePhone: form.mobilePhone.trim() || null,
        contactEmail: form.contactEmail.trim() || null,
        whatsAppConsent: form.whatsAppConsent,
      })
      Notify.create({ type: 'positive', message: 'Trabajador creado y asignado.' })
    } else if (selectedEmployee.value) {
      await assignEmployee(props.branchId, selectedEmployee.value.id, {
        jobPositionId: form.jobPositionId,
        startedOn: form.startedOn,
        isPrimary: form.isPrimary,
        administrativeAccess,
      })
      Notify.create({ type: 'positive', message: 'Trabajador asignado a la sucursal.' })
    }
    dialog.value = false
    await load()
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar el trabajador.'
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <q-card flat bordered class="platform-card workers-card">
    <q-card-section>
      <div class="section-heading workers-heading">
        <q-icon :name="icons.groups" />
        <div><h2>Trabajadores</h2><p>{{ readOnly ? 'Personas asignadas a esta sucursal.' : 'Personas asignadas a esta sucursal y su acceso administrativo.' }}</p></div>
        <q-space />
        <q-btn v-if="branchActive && !readOnly" outline no-caps color="primary" :icon="icons.personSearch" label="Asignar existente" @click="openDialog('existing')" />
        <q-btn v-if="branchActive && !readOnly" unelevated no-caps color="primary" :icon="icons.personAdd" label="Nuevo trabajador" @click="openDialog('create')" />
      </div>
      <AppAlert v-if="errorMessage" tone="danger">{{ errorMessage }}</AppAlert>
      <SearchField v-model="search" label="Buscar trabajadores" placeholder="Nombre o documento" :loading="loading" class="workers-search" @update:model-value="load" />
      <AppDataTable :rows="result.items" :columns="columns" :loading="loading" :page="result.page" :total-pages="result.totalPages" empty-title="No hay trabajadores asignados" empty-description="Asigna un trabajador existente o crea uno nuevo." @update:page="result.page = $event; load()">
        <template #body-cell-employee="rowProps"><q-td :props="rowProps"><q-btn flat no-caps color="primary" class="employee-link" :label="`${rowProps.row.firstName} ${rowProps.row.lastName}`" @click="router.push({ name: 'tenant-employee-detail', params: { id: rowProps.row.id }, query: { branchId: props.branchId } })" /></q-td></template>
        <template #body-cell-document="rowProps"><q-td :props="rowProps">{{ rowProps.row.documentType }} {{ rowProps.row.documentNumber }}</q-td></template>
        <template #body-cell-position="rowProps"><q-td :props="rowProps">{{ assignmentForBranch(rowProps.row)?.jobPositionName }}</q-td></template>
        <template #body-cell-access="rowProps"><q-td :props="rowProps"><div class="access-actions"><StatusChip :tone="rowProps.row.administrativeAccess ? 'info' : 'neutral'" :label="invitationLabel(rowProps.row)" /><q-btn flat round dense :icon="rowProps.row.administrativeAccess ? icons.tune : icons.personAdd" :aria-label="rowProps.row.administrativeAccess ? 'Configurar sucursales' : 'Crear acceso administrativo'" @click="openAccess(rowProps.row)"><q-tooltip>{{ rowProps.row.administrativeAccess ? 'Configurar sucursales' : 'Crear acceso administrativo' }}</q-tooltip></q-btn><q-btn v-if="rowProps.row.administrativeAccess && rowProps.row.administrativeAccess.invitationStatus !== 'ACCEPTED' && rowProps.row.administrativeAccess.accountStatus === 'ACTIVE'" flat round dense :icon="icons.forwardToInbox" aria-label="Reenviar invitación" @click="resend(rowProps.row)"><q-tooltip>Reenviar invitación</q-tooltip></q-btn><q-btn v-if="rowProps.row.administrativeAccess" flat round dense :icon="rowProps.row.administrativeAccess.accountStatus === 'ACTIVE' ? icons.block : icons.check" :aria-label="rowProps.row.administrativeAccess.accountStatus === 'ACTIVE' ? 'Suspender cuenta' : 'Reactivar cuenta'" @click="requestAccountStatusChange(rowProps.row)"><q-tooltip>{{ rowProps.row.administrativeAccess.accountStatus === 'ACTIVE' ? 'Suspender cuenta' : 'Reactivar cuenta' }}</q-tooltip></q-btn></div></q-td></template>
      </AppDataTable>
    </q-card-section>
  </q-card>

  <AppDialog v-if="!readOnly" v-model="dialog" :title="mode === 'create' ? 'Nuevo trabajador' : mode === 'existing' ? 'Asignar trabajador existente' : 'Acceso administrativo'" :description="mode === 'access' ? 'Define las sucursales que puede administrar.' : 'La asignación quedará vinculada a esta sucursal.'" :icon="icons.personAdd" size="lg" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <template v-if="mode === 'existing' && !selectedEmployee">
      <q-input v-model="candidateSearch" outlined debounce="350" label="Buscar por nombre o documento" @update:model-value="loadCandidates" />
      <div class="candidate-list">
        <button v-for="employee in candidates.items" :key="employee.id" type="button" class="candidate-item" @click="selectEmployee(employee)">
          <strong>{{ employee.firstName }} {{ employee.lastName }}</strong><span>{{ employee.documentType }} {{ employee.documentNumber }}</span>
        </button>
        <p v-if="!searching && candidates.items.length === 0" class="empty-copy">No hay trabajadores disponibles para asignar.</p>
      </div>
    </template>
    <q-form v-else class="employee-form" @submit.prevent="save">
      <div v-if="selectedEmployee" class="selected-employee"><strong>{{ selectedEmployee.firstName }} {{ selectedEmployee.lastName }}</strong><span>{{ selectedEmployee.documentType }} {{ selectedEmployee.documentNumber }}</span></div>
      <div v-if="mode === 'create'" class="fields-grid">
        <q-input v-model="form.documentType" outlined label="Tipo de documento" :rules="[requiredRule]" />
        <q-input v-model="form.documentNumber" outlined label="Número de documento" :rules="[requiredRule]" />
        <q-input v-model="form.firstName" outlined label="Nombres" :rules="[requiredRule]" />
        <q-input v-model="form.lastName" outlined label="Apellidos" :rules="[requiredRule]" />
        <q-input v-model="form.mobilePhone" outlined label="WhatsApp" hint="Formato +573001234567" />
        <q-input v-model="form.contactEmail" outlined type="email" label="Correo de contacto" />
      </div>
      <q-checkbox v-if="mode === 'create'" v-model="form.whatsAppConsent" label="Autoriza notificaciones por WhatsApp" />
      <AssignmentFields v-if="mode !== 'access'" v-model:job-position-id="form.jobPositionId" v-model:started-on="form.startedOn" :positions="positions" :max-date="today" :show-primary="false" class="q-mt-md" />
      <div v-if="mode !== 'access'" class="inline-position"><q-input v-model="newPositionName" outlined dense label="Crear otro cargo" /><q-btn outline no-caps color="primary" label="Agregar cargo" :loading="creatingPosition" @click="addPosition" /></div>
      <q-checkbox v-if="mode !== 'access'" v-model="form.isPrimary" label="Asignación principal" />
      <q-separator v-if="mode !== 'access'" class="q-my-md" />
      <q-checkbox v-if="mode !== 'access'" v-model="form.grantAccess" label="Dar acceso como administrador de sucursal" />
      <div v-if="form.grantAccess" class="access-fields">
        <q-input v-model="form.email" outlined type="email" label="Correo de acceso" :disable="Boolean(selectedEmployee?.administrativeAccess)" :rules="[emailRule]" />
        <q-select v-model="form.accessBranchIds" outlined multiple use-chips emit-value map-options option-value="id" option-label="name" :options="availableBranches" label="Sucursales administrables" />
        <p v-if="mode !== 'access'">La invitación vence en 24 horas.</p>
      </div>
      <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="dialog = false" /><q-btn unelevated no-caps color="primary" type="submit" label="Guardar" :disable="!canSave" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <ConfirmDialog
    v-if="!readOnly"
    v-model="statusDialog"
    :title="statusEmployee?.administrativeAccess?.accountStatus === 'ACTIVE' ? 'Suspender cuenta' : 'Reactivar cuenta'"
    :message="statusEmployee?.administrativeAccess?.accountStatus === 'ACTIVE'
      ? 'Sus sesiones quedarán invalidadas inmediatamente y no podrá iniciar sesión.'
      : 'La cuenta recuperará el acceso, pero sus sesiones anteriores no se restaurarán.'"
    :tone="statusEmployee?.administrativeAccess?.accountStatus === 'ACTIVE' ? 'danger' : 'acceptance'"
    confirm-label="Confirmar"
    :loading="changingAccountStatus"
    @confirm="toggleAccountStatus"
  />
</template>
