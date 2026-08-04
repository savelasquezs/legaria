<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PlatformLayout from '../components/PlatformLayout.vue'
import { getProblem } from '../services/api'
import { listOrganizations } from '../services/organizations'
import type {
  InvitationStatus,
  OrganizationListItem,
  OrganizationPage,
  OrganizationStatus,
} from '../types/organizations'

const router = useRouter()
const loading = ref(true)
const errorMessage = ref('')
const search = ref('')
const status = ref<OrganizationStatus | null>(null)
const page = ref(1)
const pageSize = 20
const result = ref<OrganizationPage>({
  items: [],
  page: 1,
  pageSize,
  totalItems: 0,
  totalPages: 0,
})
const statusOptions = [
  { label: 'Todas', value: null },
  { label: 'Activas', value: 'ACTIVE' },
  { label: 'Suspendidas', value: 'SUSPENDED' },
]
const columns: QTableColumn<OrganizationListItem>[] = [
  { name: 'organization', label: 'Organización', field: 'tradeName', align: 'left' },
  { name: 'nit', label: 'NIT', field: 'nit', align: 'left' },
  { name: 'location', label: 'Ubicación', field: 'municipalityName', align: 'left' },
  { name: 'invitation', label: 'Invitación', field: 'invitationStatus', align: 'left' },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

async function load(resetPage = false): Promise<void> {
  if (resetPage) page.value = 1
  loading.value = true
  errorMessage.value = ''
  try {
    result.value = await listOrganizations({
      page: page.value,
      pageSize,
      search: search.value.trim() || undefined,
      status: status.value ?? undefined,
    })
  } catch (error) {
    errorMessage.value =
      getProblem(error)?.detail ?? 'No fue posible cargar las organizaciones.'
  } finally {
    loading.value = false
  }
}

function invitationLabel(value: InvitationStatus): string {
  return {
    PENDING_DELIVERY: 'Pendiente',
    SENT: 'Enviada',
    DELIVERY_FAILED: 'Falló el envío',
    EXPIRED: 'Expirada',
    ACCEPTED: 'Aceptada',
  }[value]
}

function invitationColor(value: InvitationStatus): string {
  return value === 'ACCEPTED' ? 'positive' : value === 'DELIVERY_FAILED' ? 'negative' : 'warning'
}

onMounted(() => load())
</script>

<template>
  <PlatformLayout>
    <main class="platform-content">
      <header class="page-heading">
        <div>
          <p class="eyebrow">Consola de plataforma</p>
          <h1>Organizaciones</h1>
          <p>Administra empresas y aprovisiona su primer superadministrador.</p>
        </div>
        <q-btn
          unelevated
          no-caps
          color="primary"
          icon="add_business"
          label="Nueva organización"
          class="primary-action"
          @click="router.push('/platform/organizations/new')"
        />
      </header>

      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <q-form class="search-form" @submit.prevent="load(true)">
            <q-input
              v-model="search"
              outlined
              dense
              clearable
              debounce="350"
              placeholder="Buscar por nombre o NIT"
              aria-label="Buscar organizaciones"
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
          </q-form>
          <span class="result-count">{{ result.totalItems }} organizaciones</span>
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
          no-data-label="Todavía no hay organizaciones para mostrar."
        >
          <template #body-cell-organization="props">
            <q-td :props="props">
              <strong>{{ props.row.tradeName }}</strong>
              <span class="table-secondary">{{ props.row.legalName }}</span>
            </q-td>
          </template>
          <template #body-cell-nit="props">
            <q-td :props="props">{{ props.row.nit }}-{{ props.row.verificationDigit }}</q-td>
          </template>
          <template #body-cell-location="props">
            <q-td :props="props">
              {{ props.row.municipalityName }}
              <span class="table-secondary">{{ props.row.departmentName }}</span>
            </q-td>
          </template>
          <template #body-cell-invitation="props">
            <q-td :props="props">
              <q-chip
                dense
                :color="invitationColor(props.row.invitationStatus)"
                text-color="white"
              >
                {{ invitationLabel(props.row.invitationStatus) }}
              </q-chip>
            </q-td>
          </template>
          <template #body-cell-status="props">
            <q-td :props="props">
              <q-chip
                dense
                outline
                :color="props.row.status === 'ACTIVE' ? 'positive' : 'negative'"
              >
                {{ props.row.status === 'ACTIVE' ? 'Activa' : 'Suspendida' }}
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
                aria-label="Ver organización"
                @click="router.push(`/platform/organizations/${props.row.id}`)"
              />
            </q-td>
          </template>
        </q-table>

        <q-card-section v-if="result.totalPages > 1" class="row justify-center">
          <q-pagination
            v-model="page"
            :max="result.totalPages"
            direction-links
            @update:model-value="load()"
          />
        </q-card-section>
      </q-card>
    </main>
  </PlatformLayout>
</template>
