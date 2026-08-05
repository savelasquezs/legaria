<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import TenantLayout from '../components/TenantLayout.vue'
import { getProblem } from '../services/api'
import { listBranchAdministrators } from '../services/branches'
import type {
  BranchAdministrator,
  BranchAdministratorPage,
  TenantAccountStatus,
} from '../types/branches'
import type { InvitationStatus } from '../types/organizations'

const router = useRouter()
const loading = ref(true)
const errorMessage = ref('')
const search = ref('')
const status = ref<TenantAccountStatus | null>(null)
const page = ref(1)
const pageSize = 20
const result = ref<BranchAdministratorPage>({ items: [], page: 1, pageSize, totalItems: 0, totalPages: 0 })
const statusOptions = [
  { label: 'Todos', value: null },
  { label: 'Activos', value: 'ACTIVE' },
  { label: 'Suspendidos', value: 'SUSPENDED' },
]
const columns: QTableColumn<BranchAdministrator>[] = [
  { name: 'administrator', label: 'Administrador', field: 'firstName', align: 'left' },
  { name: 'branches', label: 'Sucursales', field: 'branches', align: 'left' },
  { name: 'invitation', label: 'Invitación', field: 'invitationStatus', align: 'left' },
  { name: 'status', label: 'Cuenta', field: 'accountStatus', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

function invitationLabel(value: InvitationStatus): string {
  return {
    PENDING_DELIVERY: 'Pendiente',
    SENT: 'Enviada',
    DELIVERY_FAILED: 'Falló el envío',
    EXPIRED: 'Expirada',
    ACCEPTED: 'Aceptada',
    REVOKED: 'Revocada',
  }[value]
}

async function load(resetPage = false): Promise<void> {
  if (resetPage) page.value = 1
  loading.value = true
  errorMessage.value = ''
  try {
    result.value = await listBranchAdministrators({
      page: page.value,
      pageSize,
      search: search.value.trim() || undefined,
      status: status.value ?? undefined,
    })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar los administradores.'
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
          <h1>Administradores</h1>
          <p>Invita administradores y controla exactamente qué sucursales pueden consultar.</p>
        </div>
        <q-btn unelevated no-caps color="primary" icon="person_add" label="Invitar administrador" class="primary-action" @click="router.push('/app/administrators/new')" />
      </header>

      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <div class="search-form">
            <q-input v-model="search" outlined dense clearable debounce="350" placeholder="Buscar por nombre o correo" aria-label="Buscar administradores" @update:model-value="load(true)">
              <template #prepend><q-icon name="search" /></template>
            </q-input>
            <q-select v-model="status" outlined dense emit-value map-options :options="statusOptions" label="Estado" @update:model-value="load(true)" />
          </div>
          <span class="result-count">{{ result.totalItems }} administradores</span>
        </q-card-section>

        <q-banner v-if="errorMessage" class="bg-red-1 text-negative q-ma-md rounded-borders">
          {{ errorMessage }}
          <template #action><q-btn flat label="Reintentar" @click="load()" /></template>
        </q-banner>

        <q-table flat :rows="result.items" :columns="columns" row-key="id" :loading="loading" hide-pagination :rows-per-page-options="[0]" class="organizations-table" no-data-label="Todavía no hay administradores de sucursal.">
          <template #body-cell-administrator="props">
            <q-td :props="props">
              <strong>{{ props.row.firstName }} {{ props.row.lastName }}</strong>
              <span class="table-secondary">{{ props.row.email }}</span>
            </q-td>
          </template>
          <template #body-cell-branches="props">
            <q-td :props="props">
              <span>{{ props.row.branches.length }} asignada{{ props.row.branches.length === 1 ? '' : 's' }}</span>
              <span class="table-secondary branch-summary">{{ props.row.branches.map((branch: { name: string }) => branch.name).join(', ') }}</span>
            </q-td>
          </template>
          <template #body-cell-invitation="props">
            <q-td :props="props">
              <q-chip dense :color="props.row.invitationStatus === 'ACCEPTED' ? 'positive' : props.row.invitationStatus === 'DELIVERY_FAILED' ? 'negative' : 'warning'" text-color="white">
                {{ invitationLabel(props.row.invitationStatus) }}
              </q-chip>
            </q-td>
          </template>
          <template #body-cell-status="props">
            <q-td :props="props">
              <q-chip dense outline :color="props.row.accountStatus === 'ACTIVE' ? 'positive' : 'negative'">
                {{ props.row.accountStatus === 'ACTIVE' ? 'Activa' : 'Suspendida' }}
              </q-chip>
            </q-td>
          </template>
          <template #body-cell-actions="props">
            <q-td :props="props"><q-btn flat round color="primary" icon="arrow_forward" :aria-label="`Ver ${props.row.firstName}`" @click="router.push(`/app/administrators/${props.row.id}`)" /></q-td>
          </template>
        </q-table>

        <q-card-section v-if="result.totalPages > 1" class="row justify-center">
          <q-pagination v-model="page" :max="result.totalPages" direction-links @update:model-value="load()" />
        </q-card-section>
      </q-card>
    </main>
  </TenantLayout>
</template>
