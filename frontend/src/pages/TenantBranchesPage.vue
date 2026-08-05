<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppAlert from '../components/AppAlert.vue'
import AppDataTable from '../components/AppDataTable.vue'
import PageHeader from '../components/PageHeader.vue'
import SearchField from '../components/SearchField.vue'
import StatusChip from '../components/StatusChip.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import { listBranches } from '../services/branches'
import { useAuthStore } from '../stores/auth'
import type { Branch, BranchPage, BranchStatus } from '../types/branches'

const router = useRouter()
const auth = useAuthStore()
const isSuperAdmin = computed(() => auth.account?.roles.includes('SUPER_ADMIN') === true)
const loading = ref(true)
const errorMessage = ref('')
const search = ref('')
const status = ref<BranchStatus | null>(null)
const page = ref(1)
const pageSize = 20
const result = ref<BranchPage>({ items: [], page: 1, pageSize, totalItems: 0, totalPages: 0 })
const statusOptions = [{ label: 'Todas', value: null }, { label: 'Activas', value: 'ACTIVE' }, { label: 'Inactivas', value: 'INACTIVE' }]
const columns: QTableColumn<Branch>[] = [
  { name: 'name', label: 'Sucursal', field: 'name', align: 'left' },
  { name: 'location', label: 'Ubicación', field: 'municipalityName', align: 'left' },
  { name: 'contact', label: 'Contacto', field: 'phone', align: 'left' },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

async function load(resetPage = false): Promise<void> {
  if (resetPage) page.value = 1
  loading.value = true
  errorMessage.value = ''
  try {
    result.value = await listBranches({ page: page.value, pageSize, search: search.value.trim() || undefined, status: status.value ?? undefined })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar las sucursales.'
  } finally {
    loading.value = false
  }
}

function openBranch(id: string): void { void router.push(`/app/branches/${id}`) }
onMounted(() => load())
</script>

<template>
  <TenantLayout>
    <main class="platform-content">
      <PageHeader context="Consola de organización" title="Sucursales" :description="isSuperAdmin ? 'Administra las sedes de tu organización y su disponibilidad.' : 'Consulta las sucursales que tienes asignadas.'">
        <template v-if="isSuperAdmin" #actions><q-btn unelevated no-caps color="primary" :icon="icons.addBusiness" label="Nueva sucursal" @click="router.push('/app/branches/new')" /></template>
      </PageHeader>
      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <div class="search-form">
            <SearchField v-model="search" label="Buscar sucursales" placeholder="Nombre o dirección" :loading="loading" @update:model-value="load(true)" />
            <q-select v-model="status" outlined dense emit-value map-options :options="statusOptions" label="Estado" @update:model-value="load(true)" />
          </div>
          <span class="result-count">{{ result.totalItems }} sucursales</span>
        </q-card-section>
        <AppAlert v-if="errorMessage" tone="danger" class="table-alert">{{ errorMessage }}<template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="load()" /></template></AppAlert>
        <AppDataTable class="desktop-records" :rows="result.items" :columns="columns" :loading="loading" :page="page" :total-pages="result.totalPages" empty-title="No hay sucursales" empty-description="Crea la primera sucursal o ajusta los filtros." @update:page="page = $event; load()">
          <template #body-cell-name="props"><q-td :props="props"><strong>{{ props.row.name }}</strong><span class="table-secondary">{{ props.row.address }}</span></q-td></template>
          <template #body-cell-location="props"><q-td :props="props">{{ props.row.municipalityName }}<span class="table-secondary">{{ props.row.departmentName }}</span></q-td></template>
          <template #body-cell-contact="props"><q-td :props="props">{{ props.row.phone || 'Sin teléfono' }}<span class="table-secondary">{{ props.row.contactEmail || 'Sin correo' }}</span></q-td></template>
          <template #body-cell-status="props"><q-td :props="props"><StatusChip :tone="props.row.status === 'ACTIVE' ? 'success' : 'neutral'" :label="props.row.status === 'ACTIVE' ? 'Activa' : 'Inactiva'" /></q-td></template>
          <template #body-cell-actions="props"><q-td :props="props"><q-btn flat round dense color="primary" :icon="icons.arrowForward" :aria-label="`Ver ${props.row.name}`" @click="openBranch(props.row.id)"><q-tooltip>Ver sucursal</q-tooltip></q-btn></q-td></template>
        </AppDataTable>
        <div class="mobile-records">
          <button v-for="item in result.items" :key="item.id" type="button" class="mobile-record-card" @click="openBranch(item.id)">
            <span class="mobile-record-card__heading"><strong>{{ item.name }}</strong><StatusChip :tone="item.status === 'ACTIVE' ? 'success' : 'neutral'" :label="item.status === 'ACTIVE' ? 'Activa' : 'Inactiva'" /></span>
            <span>{{ item.address }}</span><span>{{ item.municipalityName }}, {{ item.departmentName }}</span>
          </button>
          <div v-if="result.totalPages > 1" class="pagination-row"><q-pagination v-model="page" :max="result.totalPages" boundary-numbers @update:model-value="load()" /></div>
        </div>
      </q-card>
    </main>
  </TenantLayout>
</template>
