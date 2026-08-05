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
import { listDocumentCategories, listDocumentTypes } from '../services/documentCatalog'
import {
  changeJobPositionStatus,
  createJobPosition,
  getJobPositionDocumentRequirements,
  listJobPositions,
  updateJobPosition,
  updateJobPositionDocumentRequirements,
} from '../services/employees'
import type { DocumentCategory, DocumentType } from '../types/documentCatalog'
import type { JobPosition } from '../types/employees'

const loading = ref(true)
const saving = ref(false)
const changingStatus = ref(false)
const requirementsLoading = ref(false)
const requirementsSaving = ref(false)
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
const requirementsDialog = ref(false)
const requirementsError = ref('')
const requirementCategories = ref<DocumentCategory[]>([])
const requirementTypes = ref<DocumentType[]>([])
const selectedDocumentTypeIds = ref<string[]>([])

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
  { name: 'documents', label: 'Documentos requeridos', field: 'requiredDocumentCount', align: 'left' },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

function documentTypesFor(categoryId: string): DocumentType[] {
  return requirementTypes.value.filter((item) => item.categoryId === categoryId && item.isAvailable)
}

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

async function openRequirements(position: JobPosition): Promise<void> {
  selected.value = position
  requirementsDialog.value = true
  requirementsLoading.value = true
  requirementsError.value = ''
  requirementCategories.value = []
  requirementTypes.value = []
  selectedDocumentTypeIds.value = []
  try {
    const [categories, documentTypes, requirements] = await Promise.all([
      listDocumentCategories({ scope: 'EMPLOYEE', status: 'ACTIVE' }),
      listDocumentTypes({ scope: 'EMPLOYEE', status: 'ACTIVE' }),
      getJobPositionDocumentRequirements(position.id),
    ])
    requirementCategories.value = categories
    requirementTypes.value = documentTypes.filter((item) => item.isAvailable)
    selectedDocumentTypeIds.value = [...requirements.documentTypeIds]
  } catch (error) {
    requirementsError.value = getProblem(error)?.detail ?? 'No fue posible cargar los documentos del cargo.'
  } finally {
    requirementsLoading.value = false
  }
}

async function saveRequirements(): Promise<void> {
  if (!selected.value || requirementsSaving.value) return
  requirementsSaving.value = true
  requirementsError.value = ''
  try {
    const result = await updateJobPositionDocumentRequirements(
      selected.value.id,
      selectedDocumentTypeIds.value,
    )
    const position = positions.value.find((item) => item.id === selected.value?.id)
    if (position) position.requiredDocumentCount = result.documentTypeIds.length
    requirementsDialog.value = false
    Notify.create({ type: 'positive', message: 'Documentos requeridos actualizados.' })
  } catch (error) {
    requirementsError.value = getProblem(error)?.detail ?? 'No fue posible guardar los documentos requeridos.'
  } finally {
    requirementsSaving.value = false
  }
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
          <template #body-cell-documents="props"><q-td :props="props">{{ props.row.requiredDocumentCount }} {{ props.row.requiredDocumentCount === 1 ? 'documento' : 'documentos' }}</q-td></template>
          <template #body-cell-status="props"><q-td :props="props"><StatusChip :tone="props.row.status === 'ACTIVE' ? 'success' : 'neutral'" :label="props.row.status === 'ACTIVE' ? 'Activo' : 'Inactivo'" /></q-td></template>
          <template #body-cell-actions="props">
            <q-td :props="props">
              <q-btn flat round dense :icon="icons.description" :aria-label="`Configurar documentos de ${props.row.name}`" @click="openRequirements(props.row)"><q-tooltip>Documentos requeridos</q-tooltip></q-btn>
              <q-btn flat round dense :icon="icons.tune" :aria-label="`Editar ${props.row.name}`" @click="openForm(props.row)"><q-tooltip>Editar cargo</q-tooltip></q-btn>
              <q-btn flat round dense :color="props.row.status === 'ACTIVE' ? 'negative' : 'positive'" :icon="props.row.status === 'ACTIVE' ? icons.block : icons.check" :aria-label="props.row.status === 'ACTIVE' ? `Desactivar ${props.row.name}` : `Reactivar ${props.row.name}`" @click="requestStatusChange(props.row)"><q-tooltip>{{ props.row.status === 'ACTIVE' ? 'Desactivar cargo' : 'Reactivar cargo' }}</q-tooltip></q-btn>
            </q-td>
          </template>
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

  <AppDialog
    v-model="requirementsDialog"
    size="lg"
    :title="`Documentos requeridos · ${selected?.name ?? ''}`"
    description="Selecciona los documentos que debe presentar una persona que ocupe este cargo."
    :icon="icons.description"
    :persistent="requirementsSaving"
  >
    <AppAlert v-if="requirementsError" tone="danger">
      {{ requirementsError }}
      <template #action>
        <q-btn v-if="selected && !requirementsLoading" flat no-caps label="Reintentar" :icon="icons.refresh" @click="openRequirements(selected)" />
      </template>
    </AppAlert>
    <div v-if="requirementsLoading" class="q-gutter-sm" role="status" aria-label="Cargando documentos">
      <q-skeleton v-for="index in 3" :key="index" type="rect" height="56px" />
    </div>
    <q-list v-else-if="requirementCategories.length" bordered separator class="rounded-borders">
      <q-expansion-item
        v-for="category in requirementCategories"
        :key="category.id"
        :label="category.name"
        :caption="`${documentTypesFor(category.id).length} tipos de documento`"
        :default-opened="requirementCategories.length === 1"
        expand-separator
      >
        <q-item v-for="documentType in documentTypesFor(category.id)" :key="documentType.id" tag="label">
          <q-item-section avatar>
            <q-checkbox v-model="selectedDocumentTypeIds" :val="documentType.id" />
          </q-item-section>
          <q-item-section>
            <q-item-label>{{ documentType.name }}</q-item-label>
            <q-item-label v-if="documentType.isRequiredByDefault" caption>Obligatorio por defecto para todos los trabajadores</q-item-label>
          </q-item-section>
        </q-item>
        <q-item v-if="documentTypesFor(category.id).length === 0">
          <q-item-section><q-item-label caption>No hay tipos activos en esta categoría.</q-item-label></q-item-section>
        </q-item>
      </q-expansion-item>
    </q-list>
    <p v-else-if="!requirementsError" class="empty-copy">No hay categorías activas con alcance de trabajador.</p>
    <q-card-actions align="right" class="q-px-none q-pt-lg">
      <q-btn flat no-caps label="Cancelar" :disable="requirementsSaving" @click="requirementsDialog = false" />
      <q-btn unelevated no-caps color="primary" label="Guardar" :loading="requirementsSaving" :disable="requirementsLoading || Boolean(requirementsError)" @click="saveRequirements" />
    </q-card-actions>
  </AppDialog>

  <ConfirmDialog v-model="statusDialog" :title="selected?.status === 'ACTIVE' ? 'Desactivar cargo' : 'Reactivar cargo'" :message="selected?.status === 'ACTIVE' ? 'El cargo dejará de estar disponible para nuevas asignaciones. Su historial se conservará.' : 'El cargo volverá a estar disponible para nuevas asignaciones.'" :tone="selected?.status === 'ACTIVE' ? 'danger' : 'acceptance'" confirm-label="Confirmar" :loading="changingStatus" @confirm="changeStatus" />
</template>
