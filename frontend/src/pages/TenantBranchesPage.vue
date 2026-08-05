<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import TenantLayout from '../components/TenantLayout.vue'
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
const statusOptions = [
  { label: 'Todas', value: null },
  { label: 'Activas', value: 'ACTIVE' },
  { label: 'Inactivas', value: 'INACTIVE' },
]
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
    result.value = await listBranches({
      page: page.value,
      pageSize,
      search: search.value.trim() || undefined,
      status: status.value ?? undefined,
    })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar las sucursales.'
  } finally {
    loading.value = false
  }
}

onMounted(() => load())
</script>

<template>
  <TenantLayout>
    <main class="platform-content">
      <header class="page-heading">
        <div>
          <p class="eyebrow">Consola de organización</p>
          <h1>Sucursales</h1>
          <p v-if="isSuperAdmin">Administra las sedes de tu organización y su disponibilidad.</p>
          <p v-else>Consulta las sucursales que tienes asignadas.</p>
        </div>
        <q-btn
          v-if="isSuperAdmin"
          unelevated
          no-caps
          color="primary"
          icon="add_business"
          label="Nueva sucursal"
          class="primary-action"
          @click="router.push('/app/branches/new')"
        />
      </header>

      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <div class="search-form">
            <q-input
              v-model="search"
              outlined
              dense
              clearable
              debounce="350"
              placeholder="Buscar por nombre o dirección"
              aria-label="Buscar sucursales"
              @update:model-value="load(true)"
            >
              <template #prepend><q-icon name="search" /></template>
            </q-input>
            <q-select
              v-model="status"
              outlined
              dense
              emit-value
              map-options
              :options="statusOptions"
              label="Estado"
              @update:model-value="load(true)"
            />
          </div>
          <span class="result-count">{{ result.totalItems }} sucursales</span>
        </q-card-section>

        <q-banner v-if="errorMessage" class="bg-red-1 text-negative q-ma-md rounded-borders">
          {{ errorMessage }}
          <template #action><q-btn flat label="Reintentar" @click="load()" /></template>
        </q-banner>

        <q-table
          flat
          :rows="result.items"
          :columns="columns"
          row-key="id"
          :loading="loading"
          hide-pagination
          :rows-per-page-options="[0]"
          class="organizations-table"
          no-data-label="Todavía no hay sucursales para mostrar."
        >
          <template #body-cell-name="props">
            <q-td :props="props">
              <strong>{{ props.row.name }}</strong>
              <span class="table-secondary">{{ props.row.address }}</span>
            </q-td>
          </template>
          <template #body-cell-location="props">
            <q-td :props="props">
              {{ props.row.municipalityName }}
              <span class="table-secondary">{{ props.row.departmentName }}</span>
            </q-td>
          </template>
          <template #body-cell-contact="props">
            <q-td :props="props">
              {{ props.row.phone || 'Sin teléfono' }}
              <span class="table-secondary">{{ props.row.contactEmail || 'Sin correo' }}</span>
            </q-td>
          </template>
          <template #body-cell-status="props">
            <q-td :props="props">
              <q-chip dense outline :color="props.row.status === 'ACTIVE' ? 'positive' : 'grey-7'">
                {{ props.row.status === 'ACTIVE' ? 'Activa' : 'Inactiva' }}
              </q-chip>
            </q-td>
          </template>
          <template #body-cell-actions="props">
            <q-td :props="props">
              <q-btn
                flat
                round
                color="primary"
                icon="arrow_forward"
                :aria-label="`Ver ${props.row.name}`"
                @click="router.push(`/app/branches/${props.row.id}`)"
              />
            </q-td>
          </template>
        </q-table>

        <q-card-section v-if="result.totalPages > 1" class="row justify-center">
          <q-pagination v-model="page" :max="result.totalPages" direction-links @update:model-value="load()" />
        </q-card-section>
      </q-card>
    </main>
  </TenantLayout>
</template>
