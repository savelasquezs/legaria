<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { Notify } from 'quasar'
import { computed, onMounted, ref } from 'vue'
import AppAlert from '../components/AppAlert.vue'
import AppDataTable from '../components/AppDataTable.vue'
import AppDialog from '../components/AppDialog.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import PageHeader from '../components/PageHeader.vue'
import SearchField from '../components/SearchField.vue'
import StatusChip from '../components/StatusChip.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import { changeJobPositionStatus, createJobPosition, listJobPositions, updateJobPosition } from '../services/employees'
import type { JobPosition } from '../types/employees'

const loading = ref(true)
const saving = ref(false)
const changingStatus = ref(false)
const errorMessage = ref('')
const dialogError = ref('')
const search = ref('')
const status = ref<'ACTIVE' | 'INACTIVE' | 'ALL'>('ALL')
const positions = ref<JobPosition[]>([])
const selected = ref<JobPosition | null>(null)
const editing = ref<JobPosition | null>(null)
const name = ref('')
const formDialog = ref(false)
const statusDialog = ref(false)

const statusOptions = [
  { label: 'Todos', value: 'ALL' },
  { label: 'Activos', value: 'ACTIVE' },
  { label: 'Inactivos', value: 'INACTIVE' },
]
const filteredPositions = computed(() => {
  const term = search.value.trim().toLocaleLowerCase('es')
  return term ? positions.value.filter((item) => item.name.toLocaleLowerCase('es').includes(term)) : positions.value
})
const columns: QTableColumn<JobPosition>[] = [
  { name: 'name', label: 'Cargo', field: 'name', align: 'left' },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    positions.value = await listJobPositions(status.value)
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar los cargos.'
  } finally {
    loading.value = false
  }
}

function openForm(position: JobPosition | null = null): void {
  editing.value = position
  name.value = position?.name ?? ''
  dialogError.value = ''
  formDialog.value = true
}

async function save(): Promise<void> {
  if (!name.value.trim() || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    if (editing.value) {
      await updateJobPosition(editing.value.id, name.value.trim())
      Notify.create({ type: 'positive', message: 'Cargo actualizado.' })
    } else {
      await createJobPosition(name.value.trim())
      Notify.create({ type: 'positive', message: 'Cargo creado.' })
    }
    formDialog.value = false
    await load()
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar el cargo.'
  } finally {
    saving.value = false
  }
}

function requestStatusChange(position: JobPosition): void {
  selected.value = position
  statusDialog.value = true
}

async function changeStatus(): Promise<void> {
  if (!selected.value || changingStatus.value) return
  changingStatus.value = true
  const action = selected.value.status === 'ACTIVE' ? 'deactivate' : 'reactivate'
  try {
    await changeJobPositionStatus(selected.value.id, action)
    Notify.create({ type: 'positive', message: action === 'deactivate' ? 'Cargo desactivado.' : 'Cargo reactivado.' })
    statusDialog.value = false
    await load()
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cambiar el estado del cargo.'
  } finally {
    changingStatus.value = false
  }
}

onMounted(load)
</script>

<template>
  <TenantLayout>
    <main class="platform-content">
      <PageHeader context="Consola de organización" title="Cargos" description="Administra el catálogo laboral reutilizable en todas las sucursales.">
        <template #actions><q-btn unelevated no-caps color="primary" :icon="icons.personAdd" label="Nuevo cargo" @click="openForm()" /></template>
      </PageHeader>
      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <div class="search-form">
            <SearchField v-model="search" label="Buscar cargos" placeholder="Nombre del cargo" :loading="loading" />
            <q-select v-model="status" outlined dense emit-value map-options :options="statusOptions" label="Estado" @update:model-value="load" />
          </div>
          <span class="result-count">{{ filteredPositions.length }} cargos</span>
        </q-card-section>
        <AppAlert v-if="errorMessage" tone="danger" class="table-alert">{{ errorMessage }}<template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="load" /></template></AppAlert>
        <AppDataTable :rows="filteredPositions" :columns="columns" :loading="loading" empty-title="No hay cargos" empty-description="Crea el primer cargo o ajusta los filtros.">
          <template #body-cell-name="props"><q-td :props="props"><strong>{{ props.row.name }}</strong></q-td></template>
          <template #body-cell-status="props"><q-td :props="props"><StatusChip :tone="props.row.status === 'ACTIVE' ? 'success' : 'neutral'" :label="props.row.status === 'ACTIVE' ? 'Activo' : 'Inactivo'" /></q-td></template>
          <template #body-cell-actions="props"><q-td :props="props"><q-btn flat round dense :icon="icons.tune" :aria-label="`Editar ${props.row.name}`" @click="openForm(props.row)"><q-tooltip>Editar cargo</q-tooltip></q-btn><q-btn flat round dense :color="props.row.status === 'ACTIVE' ? 'negative' : 'positive'" :icon="props.row.status === 'ACTIVE' ? icons.block : icons.check" :aria-label="props.row.status === 'ACTIVE' ? `Desactivar ${props.row.name}` : `Reactivar ${props.row.name}`" @click="requestStatusChange(props.row)"><q-tooltip>{{ props.row.status === 'ACTIVE' ? 'Desactivar cargo' : 'Reactivar cargo' }}</q-tooltip></q-btn></q-td></template>
        </AppDataTable>
      </q-card>
    </main>
  </TenantLayout>

  <AppDialog v-model="formDialog" :title="editing ? 'Editar cargo' : 'Nuevo cargo'" description="El nombre debe ser único dentro de la organización." :icon="icons.groups" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form @submit.prevent="save">
      <q-input v-model="name" outlined autofocus label="Nombre" maxlength="150" :rules="[(value: string) => Boolean(value?.trim()) || 'El nombre es obligatorio.']" />
      <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="formDialog = false" /><q-btn unelevated no-caps color="primary" type="submit" label="Guardar" :disable="!name.trim()" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <ConfirmDialog v-model="statusDialog" :title="selected?.status === 'ACTIVE' ? 'Desactivar cargo' : 'Reactivar cargo'" :message="selected?.status === 'ACTIVE' ? 'El cargo dejará de estar disponible para nuevas asignaciones. Su historial se conservará.' : 'El cargo volverá a estar disponible para nuevas asignaciones.'" :tone="selected?.status === 'ACTIVE' ? 'danger' : 'acceptance'" confirm-label="Confirmar" :loading="changingStatus" @confirm="changeStatus" />
</template>
