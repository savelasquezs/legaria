<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import AppAlert from '../components/AppAlert.vue'
import AppDataTable from '../components/AppDataTable.vue'
import AppDialog from '../components/AppDialog.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import EmptyState from '../components/EmptyState.vue'
import LoadingSkeleton from '../components/LoadingSkeleton.vue'
import PageHeader from '../components/PageHeader.vue'
import SearchField from '../components/SearchField.vue'
import StatusChip from '../components/StatusChip.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import {
  changeDocumentCategoryStatus,
  changeDocumentTypeStatus,
  createDocumentCategory,
  createDocumentType,
  listDocumentCategories,
  listDocumentTypes,
  updateDocumentCategory,
  updateDocumentType,
} from '../services/documentCatalog'
import { useAuthStore } from '../stores/auth'
import type {
  CatalogStatusFilter,
  DocumentCategory,
  DocumentDateMode,
  DocumentEvidenceKind,
  DocumentScope,
  DocumentType,
} from '../types/documentCatalog'

const auth = useAuthStore()
const isSuperAdmin = computed(() => auth.account?.roles.includes('SUPER_ADMIN') === true)
const selectedScope = ref<DocumentScope>('EMPLOYEE')
const categoryStatus = ref<CatalogStatusFilter>('ALL')
const typeStatus = ref<CatalogStatusFilter>('ALL')
const categorySearch = ref('')
const typeSearch = ref('')
const categories = ref<DocumentCategory[]>([])
const categoryOptions = ref<DocumentCategory[]>([])
const documentTypes = ref<DocumentType[]>([])
const selectedCategory = ref<DocumentCategory | null>(null)
const editingCategory = ref<DocumentCategory | null>(null)
const editingType = ref<DocumentType | null>(null)
const statusTarget = ref<{ kind: 'category' | 'type'; item: DocumentCategory | DocumentType } | null>(null)
const loadingCategories = ref(true)
const loadingTypes = ref(false)
const saving = ref(false)
const changingStatus = ref(false)
const categoryDialog = ref(false)
const typeDialog = ref(false)
const statusDialog = ref(false)
const errorMessage = ref('')
const typeError = ref('')
const dialogError = ref('')

const categoryForm = reactive({ name: '', description: '', scope: 'EMPLOYEE' as DocumentScope })
const typeForm = reactive({
  categoryId: '',
  name: '',
  description: '',
  isRequiredByDefault: false,
  issueDateMode: 'NEVER' as DocumentDateMode,
  expirationDateMode: 'NEVER' as DocumentDateMode,
  allowsMultipleActiveVersions: false,
  allowsMultipleEvidenceItems: false,
  allowedEvidenceKinds: ['PDF'] as DocumentEvidenceKind[],
})

const scopeOptions = [
  { label: 'Trabajadores', value: 'EMPLOYEE' },
  { label: 'Sucursales', value: 'BRANCH' },
]
const statusOptions = [
  { label: 'Todos', value: 'ALL' },
  { label: 'Activos', value: 'ACTIVE' },
  { label: 'Inactivos', value: 'INACTIVE' },
]
const dateModeOptions = [
  { label: 'No aplica', value: 'NEVER' },
  { label: 'Opcional', value: 'OPTIONAL' },
  { label: 'Obligatoria', value: 'REQUIRED' },
]
const evidenceOptions: Array<{ label: string; value: DocumentEvidenceKind }> = [
  { label: 'PDF', value: 'PDF' },
  { label: 'Imágenes', value: 'IMAGE' },
  { label: 'Videos', value: 'VIDEO' },
  { label: 'Enlaces', value: 'LINK' },
]
const canManageSelectedScope = computed(() => isSuperAdmin.value || selectedScope.value === 'BRANCH')
const canSaveCategory = computed(() => Boolean(categoryForm.name.trim()))
const canSaveType = computed(() => Boolean(
  typeForm.categoryId &&
  typeForm.name.trim() &&
  typeForm.allowedEvidenceKinds.length > 0,
))
const typeColumns: QTableColumn<DocumentType>[] = [
  { name: 'name', label: 'Tipo de documento', field: 'name', align: 'left' },
  { name: 'dates', label: 'Fechas', field: 'issueDateMode', align: 'left' },
  { name: 'evidence', label: 'Evidencias', field: 'allowedEvidenceKinds', align: 'left' },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

function dateModeLabel(value: DocumentDateMode): string {
  return dateModeOptions.find((item) => item.value === value)?.label ?? value
}

function evidenceLabel(value: DocumentEvidenceKind): string {
  return evidenceOptions.find((item) => item.value === value)?.label ?? value
}

async function loadCategories(): Promise<void> {
  loadingCategories.value = true
  errorMessage.value = ''
  try {
    categories.value = await listDocumentCategories({
      scope: selectedScope.value,
      status: categoryStatus.value,
      search: categorySearch.value || undefined,
    })
    const retained = categories.value.find((item) => item.id === selectedCategory.value?.id)
    selectedCategory.value = retained ?? categories.value[0] ?? null
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar las categorías.'
    categories.value = []
    selectedCategory.value = null
  } finally {
    loadingCategories.value = false
  }
}

async function loadTypes(): Promise<void> {
  if (!selectedCategory.value) {
    documentTypes.value = []
    return
  }
  loadingTypes.value = true
  typeError.value = ''
  try {
    documentTypes.value = await listDocumentTypes({
      categoryId: selectedCategory.value.id,
      status: typeStatus.value,
      search: typeSearch.value || undefined,
    })
  } catch (error) {
    typeError.value = getProblem(error)?.detail ?? 'No fue posible cargar los tipos de documento.'
    documentTypes.value = []
  } finally {
    loadingTypes.value = false
  }
}

function selectCategory(category: DocumentCategory): void {
  selectedCategory.value = category
}

function openCategoryForm(category: DocumentCategory | null = null): void {
  editingCategory.value = category
  Object.assign(categoryForm, {
    name: category?.name ?? '',
    description: category?.description ?? '',
    scope: category?.scope ?? selectedScope.value,
  })
  dialogError.value = ''
  categoryDialog.value = true
}

async function saveCategory(): Promise<void> {
  if (!canSaveCategory.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  try {
    const saved = editingCategory.value
      ? await updateDocumentCategory(editingCategory.value.id, {
          name: categoryForm.name.trim(),
          description: categoryForm.description.trim() || null,
        })
      : await createDocumentCategory({
          name: categoryForm.name.trim(),
          description: categoryForm.description.trim() || null,
          scope: categoryForm.scope,
        })
    Notify.create({ type: 'positive', message: editingCategory.value ? 'Categoría actualizada.' : 'Categoría creada.' })
    categoryDialog.value = false
    await loadCategories()
    selectedCategory.value = categories.value.find((item) => item.id === saved.id) ?? selectedCategory.value
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar la categoría.'
  } finally {
    saving.value = false
  }
}

async function openTypeForm(documentType: DocumentType | null = null): Promise<void> {
  if (!selectedCategory.value) return
  editingType.value = documentType
  let active: DocumentCategory[]
  try {
    active = await listDocumentCategories({ scope: selectedScope.value, status: 'ACTIVE' })
  } catch (error) {
    typeError.value = getProblem(error)?.detail ?? 'No fue posible preparar el formulario.'
    return
  }
  categoryOptions.value = selectedCategory.value.status === 'INACTIVE' && !active.some((item) => item.id === selectedCategory.value?.id)
    ? [...active, selectedCategory.value].sort((a, b) => a.name.localeCompare(b.name, 'es'))
    : active
  Object.assign(typeForm, {
    categoryId: documentType?.categoryId ?? selectedCategory.value.id,
    name: documentType?.name ?? '',
    description: documentType?.description ?? '',
    isRequiredByDefault: documentType?.isRequiredByDefault ?? false,
    issueDateMode: documentType?.issueDateMode ?? 'NEVER',
    expirationDateMode: documentType?.expirationDateMode ?? 'NEVER',
    allowsMultipleActiveVersions: documentType?.allowsMultipleActiveVersions ?? false,
    allowsMultipleEvidenceItems: documentType?.allowsMultipleEvidenceItems ?? false,
    allowedEvidenceKinds: documentType?.allowedEvidenceKinds.slice() ?? ['PDF'],
  })
  dialogError.value = ''
  typeDialog.value = true
}

async function saveType(): Promise<void> {
  if (!canSaveType.value || saving.value) return
  saving.value = true
  dialogError.value = ''
  const input = {
    categoryId: typeForm.categoryId,
    name: typeForm.name.trim(),
    description: typeForm.description.trim() || null,
    isRequiredByDefault: typeForm.isRequiredByDefault,
    issueDateMode: typeForm.issueDateMode,
    expirationDateMode: typeForm.expirationDateMode,
    allowsMultipleActiveVersions: typeForm.allowsMultipleActiveVersions,
    allowsMultipleEvidenceItems: typeForm.allowsMultipleEvidenceItems,
    allowedEvidenceKinds: typeForm.allowedEvidenceKinds,
  }
  try {
    if (editingType.value) await updateDocumentType(editingType.value.id, input)
    else await createDocumentType(input)
    Notify.create({ type: 'positive', message: editingType.value ? 'Tipo de documento actualizado.' : 'Tipo de documento creado.' })
    typeDialog.value = false
    await loadCategories()
    if (input.categoryId !== selectedCategory.value?.id) {
      selectedCategory.value = categories.value.find((item) => item.id === input.categoryId) ?? selectedCategory.value
    }
  } catch (error) {
    dialogError.value = getProblem(error)?.detail ?? 'No fue posible guardar el tipo de documento.'
  } finally {
    saving.value = false
  }
}

function requestStatusChange(kind: 'category' | 'type', item: DocumentCategory | DocumentType): void {
  statusTarget.value = { kind, item }
  statusDialog.value = true
}

async function changeStatus(): Promise<void> {
  if (!statusTarget.value || changingStatus.value) return
  changingStatus.value = true
  const { kind, item } = statusTarget.value
  const action = item.status === 'ACTIVE' ? 'deactivate' : 'reactivate'
  try {
    if (kind === 'category') await changeDocumentCategoryStatus(item.id, action)
    else await changeDocumentTypeStatus(item.id, action)
    Notify.create({ type: 'positive', message: action === 'deactivate' ? 'Elemento desactivado.' : 'Elemento reactivado.' })
    statusDialog.value = false
    await loadCategories()
  } catch (error) {
    const message = getProblem(error)?.detail ?? 'No fue posible cambiar el estado.'
    if (kind === 'category') errorMessage.value = message
    else typeError.value = message
  } finally {
    changingStatus.value = false
  }
}

watch(selectedCategory, loadTypes)
watch(selectedScope, async () => {
  selectedCategory.value = null
  await loadCategories()
})

onMounted(loadCategories)
</script>

<template>
  <TenantLayout>
    <main class="platform-content">
      <PageHeader context="Consola de organización" title="Catálogo de documentos" description="Define categorías y requisitos reutilizables para trabajadores y sucursales." />

      <q-card flat bordered class="platform-card document-catalog-filters">
        <q-card-section class="search-form">
          <q-select v-model="selectedScope" outlined dense emit-value map-options :options="scopeOptions" label="Alcance" />
          <q-select v-model="categoryStatus" outlined dense emit-value map-options :options="statusOptions" label="Estado de categorías" @update:model-value="loadCategories" />
        </q-card-section>
      </q-card>

      <AppAlert v-if="errorMessage" tone="danger" class="q-mb-md">
        {{ errorMessage }}
        <template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="loadCategories" /></template>
      </AppAlert>

      <div class="document-catalog-layout">
        <q-card flat bordered class="platform-card document-category-pane">
          <q-card-section class="document-pane-heading">
            <div><h2>Categorías</h2><p>{{ selectedScope === 'EMPLOYEE' ? 'Documentos de trabajadores' : 'Documentos de sucursales' }}</p></div>
            <q-btn v-if="canManageSelectedScope" flat round dense :icon="icons.folder" aria-label="Nueva categoría" @click="openCategoryForm()"><q-tooltip>Nueva categoría</q-tooltip></q-btn>
          </q-card-section>
          <q-card-section class="document-pane-search"><SearchField v-model="categorySearch" label="Buscar categorías" :loading="loadingCategories" @update:model-value="loadCategories" /></q-card-section>
          <LoadingSkeleton v-if="loadingCategories" variant="card" :rows="5" />
          <div v-else-if="categories.length" class="document-category-list">
            <button
              v-for="category in categories"
              :key="category.id"
              type="button"
              class="document-category-item"
              :class="{ 'document-category-item--active': selectedCategory?.id === category.id }"
              @click="selectCategory(category)"
            >
              <span><strong>{{ category.name }}</strong><small>{{ category.documentTypeCount }} tipos</small></span>
              <StatusChip :tone="category.status === 'ACTIVE' ? 'success' : 'neutral'" :label="category.status === 'ACTIVE' ? 'Activa' : 'Inactiva'" />
            </button>
          </div>
          <EmptyState v-else title="No hay categorías" description="Crea la primera categoría o ajusta los filtros.">
            <template v-if="canManageSelectedScope" #action><q-btn outline no-caps color="primary" label="Crear categoría" @click="openCategoryForm()" /></template>
          </EmptyState>
        </q-card>

        <q-card flat bordered class="platform-card document-type-pane">
          <template v-if="selectedCategory">
            <q-card-section class="document-pane-heading">
              <div><h2>{{ selectedCategory.name }}</h2><p>{{ selectedCategory.description || 'Tipos de documento de esta categoría.' }}</p></div>
              <div v-if="canManageSelectedScope" class="document-heading-actions">
                <q-btn flat round dense :icon="icons.tune" :aria-label="`Editar ${selectedCategory.name}`" @click="openCategoryForm(selectedCategory)"><q-tooltip>Editar categoría</q-tooltip></q-btn>
                <q-btn flat round dense :color="selectedCategory.status === 'ACTIVE' ? 'negative' : 'positive'" :icon="selectedCategory.status === 'ACTIVE' ? icons.block : icons.check" :aria-label="selectedCategory.status === 'ACTIVE' ? 'Desactivar categoría' : 'Reactivar categoría'" @click="requestStatusChange('category', selectedCategory)"><q-tooltip>{{ selectedCategory.status === 'ACTIVE' ? 'Desactivar categoría' : 'Reactivar categoría' }}</q-tooltip></q-btn>
                <q-btn v-if="selectedCategory.status === 'ACTIVE'" unelevated no-caps color="primary" :icon="icons.description" label="Nuevo tipo" @click="openTypeForm()" />
              </div>
            </q-card-section>
            <q-card-section class="document-type-filters">
              <SearchField v-model="typeSearch" label="Buscar tipos" :loading="loadingTypes" @update:model-value="loadTypes" />
              <q-select v-model="typeStatus" outlined dense emit-value map-options :options="statusOptions" label="Estado" @update:model-value="loadTypes" />
            </q-card-section>
            <AppAlert v-if="typeError" tone="danger" class="table-alert">
              {{ typeError }}
              <template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="loadTypes" /></template>
            </AppAlert>
            <AppDataTable :rows="documentTypes" :columns="typeColumns" :loading="loadingTypes" empty-title="No hay tipos de documento" empty-description="Crea el primer tipo o ajusta los filtros.">
              <template #body-cell-name="props"><q-td :props="props"><div class="document-type-name"><strong>{{ props.row.name }}</strong><span v-if="props.row.isRequiredByDefault">Obligatorio por defecto</span></div></q-td></template>
              <template #body-cell-dates="props"><q-td :props="props"><div class="document-date-summary"><span>Expedición: {{ dateModeLabel(props.row.issueDateMode) }}</span><span>Vencimiento: {{ dateModeLabel(props.row.expirationDateMode) }}</span></div></q-td></template>
              <template #body-cell-evidence="props"><q-td :props="props"><div class="document-evidence-list"><StatusChip v-for="kind in props.row.allowedEvidenceKinds" :key="kind" tone="info" :label="evidenceLabel(kind)" /></div></q-td></template>
              <template #body-cell-status="props"><q-td :props="props"><StatusChip :tone="props.row.isAvailable ? 'success' : 'neutral'" :label="props.row.isAvailable ? 'Disponible' : props.row.status === 'INACTIVE' ? 'Inactivo' : 'Categoría inactiva'" /></q-td></template>
              <template #body-cell-actions="props"><q-td :props="props"><div v-if="canManageSelectedScope" class="table-actions"><q-btn flat round dense :icon="icons.tune" :aria-label="`Editar ${props.row.name}`" @click="openTypeForm(props.row)"><q-tooltip>Editar tipo</q-tooltip></q-btn><q-btn flat round dense :color="props.row.status === 'ACTIVE' ? 'negative' : 'positive'" :icon="props.row.status === 'ACTIVE' ? icons.block : icons.check" :aria-label="props.row.status === 'ACTIVE' ? `Desactivar ${props.row.name}` : `Reactivar ${props.row.name}`" @click="requestStatusChange('type', props.row)"><q-tooltip>{{ props.row.status === 'ACTIVE' ? 'Desactivar tipo' : 'Reactivar tipo' }}</q-tooltip></q-btn></div></q-td></template>
            </AppDataTable>
          </template>
          <EmptyState v-else title="Selecciona una categoría" description="Elige una categoría para consultar sus tipos de documento." :icon="icons.folder" />
        </q-card>
      </div>
    </main>
  </TenantLayout>

  <AppDialog v-model="categoryDialog" :title="editingCategory ? 'Editar categoría' : 'Nueva categoría'" description="Las categorías agrupan tipos con el mismo alcance." :icon="icons.folder" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form @submit.prevent="saveCategory">
      <q-input v-model="categoryForm.name" outlined autofocus label="Nombre" maxlength="150" :rules="[(value: string) => Boolean(value?.trim()) || 'El nombre es obligatorio.']" />
      <q-select v-model="categoryForm.scope" outlined emit-value map-options :options="scopeOptions" label="Alcance" :disable="Boolean(editingCategory) || !isSuperAdmin" />
      <q-input v-model="categoryForm.description" outlined type="textarea" autogrow label="Descripción (opcional)" maxlength="1000" counter />
      <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="categoryDialog = false" /><q-btn unelevated no-caps color="primary" type="submit" label="Guardar" :disable="!canSaveCategory" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <AppDialog v-model="typeDialog" :title="editingType ? 'Editar tipo de documento' : 'Nuevo tipo de documento'" description="La configuración se aplicará cuando se carguen documentos." :icon="icons.description" size="lg" :persistent="saving">
    <AppAlert v-if="dialogError" tone="danger">{{ dialogError }}</AppAlert>
    <q-form @submit.prevent="saveType">
      <div class="fields-grid">
        <q-input v-model="typeForm.name" outlined autofocus label="Nombre" maxlength="150" :rules="[(value: string) => Boolean(value?.trim()) || 'El nombre es obligatorio.']" />
        <q-select v-model="typeForm.categoryId" outlined emit-value map-options option-value="id" option-label="name" :options="categoryOptions" label="Categoría" />
        <q-select v-model="typeForm.issueDateMode" outlined emit-value map-options :options="dateModeOptions" label="Fecha de expedición" />
        <q-select v-model="typeForm.expirationDateMode" outlined emit-value map-options :options="dateModeOptions" label="Fecha de vencimiento" />
        <q-input v-model="typeForm.description" outlined type="textarea" autogrow label="Descripción (opcional)" maxlength="1000" counter class="span-two" />
      </div>
      <div class="document-options-section">
        <h3>Reglas del documento</h3>
        <q-checkbox v-model="typeForm.isRequiredByDefault" label="Obligatorio por defecto" />
        <q-checkbox v-model="typeForm.allowsMultipleActiveVersions" label="Permite varias versiones vigentes" />
        <q-checkbox v-model="typeForm.allowsMultipleEvidenceItems" label="Permite varias evidencias por versión" />
      </div>
      <div class="document-options-section">
        <h3>Evidencias permitidas</h3>
        <div class="document-evidence-options"><q-checkbox v-for="option in evidenceOptions" :key="option.value" v-model="typeForm.allowedEvidenceKinds" :val="option.value" :label="option.label" /></div>
        <p v-if="typeForm.allowedEvidenceKinds.length === 0" class="field-error">Selecciona al menos una evidencia.</p>
      </div>
      <q-card-actions align="right" class="q-px-none q-pt-lg"><q-btn flat no-caps label="Cancelar" :disable="saving" @click="typeDialog = false" /><q-btn unelevated no-caps color="primary" type="submit" label="Guardar" :disable="!canSaveType" :loading="saving" /></q-card-actions>
    </q-form>
  </AppDialog>

  <ConfirmDialog
    v-model="statusDialog"
    :title="statusTarget?.item.status === 'ACTIVE' ? 'Desactivar elemento' : 'Reactivar elemento'"
    :message="statusTarget?.kind === 'category' && statusTarget.item.status === 'ACTIVE' ? 'La categoría y sus tipos dejarán de estar disponibles para usos nuevos, sin perder su configuración.' : statusTarget?.item.status === 'ACTIVE' ? 'El elemento dejará de estar disponible para usos nuevos.' : 'El elemento volverá a estar disponible cuando su categoría también esté activa.'"
    :tone="statusTarget?.item.status === 'ACTIVE' ? 'danger' : 'acceptance'"
    confirm-label="Confirmar"
    :loading="changingStatus"
    @confirm="changeStatus"
  />
</template>
