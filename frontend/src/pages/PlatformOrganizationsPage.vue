<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppAlert from '../components/AppAlert.vue'
import AppDataTable from '../components/AppDataTable.vue'
import PageHeader from '../components/PageHeader.vue'
import PlatformLayout from '../components/PlatformLayout.vue'
import SearchField from '../components/SearchField.vue'
import StatusChip, { type StatusTone } from '../components/StatusChip.vue'
import { icons } from '../design-system/icons'
import { getProblem } from '../services/api'
import { listOrganizations } from '../services/organizations'
import type { InvitationStatus, OrganizationListItem, OrganizationPage, OrganizationStatus } from '../types/organizations'

const router = useRouter()
const loading = ref(true)
const errorMessage = ref('')
const search = ref('')
const status = ref<OrganizationStatus | null>(null)
const page = ref(1)
const pageSize = 20
const result = ref<OrganizationPage>({ items: [], page: 1, pageSize, totalItems: 0, totalPages: 0 })
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
    result.value = await listOrganizations({ page: page.value, pageSize, search: search.value.trim() || undefined, status: status.value ?? undefined })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar las organizaciones.'
  } finally {
    loading.value = false
  }
}

function invitationLabel(value: InvitationStatus): string {
  return { PENDING_DELIVERY: 'Pendiente', SENT: 'Enviada', DELIVERY_FAILED: 'Falló el envío', EXPIRED: 'Expirada', ACCEPTED: 'Aceptada', REVOKED: 'Revocada' }[value]
}

function invitationTone(value: InvitationStatus): StatusTone {
  return value === 'ACCEPTED' ? 'success' : value === 'DELIVERY_FAILED' ? 'danger' : 'warning'
}

function openOrganization(id: string): void {
  void router.push(`/platform/organizations/${id}`)
}

onMounted(() => load())
</script>

<template>
  <PlatformLayout>
    <main class="platform-content">
      <PageHeader context="Consola de plataforma" title="Organizaciones" description="Administra empresas y aprovisiona su primer superadministrador.">
        <template #actions>
          <q-btn unelevated no-caps color="primary" :icon="icons.addBusiness" label="Nueva organización" @click="router.push('/platform/organizations/new')" />
        </template>
      </PageHeader>

      <q-card flat bordered class="platform-card">
        <q-card-section class="organization-filters">
          <div class="search-form">
            <SearchField v-model="search" label="Buscar organizaciones" placeholder="Nombre o NIT" :loading="loading" @update:model-value="load(true)" />
            <q-select v-model="status" outlined dense emit-value map-options :options="statusOptions" label="Estado" @update:model-value="load(true)" />
          </div>
          <span class="result-count">{{ result.totalItems }} organizaciones</span>
        </q-card-section>

        <AppAlert v-if="errorMessage" tone="danger" class="table-alert">
          {{ errorMessage }}
          <template #action><q-btn flat no-caps label="Reintentar" :icon="icons.refresh" @click="load()" /></template>
        </AppAlert>

        <AppDataTable
          class="desktop-records"
          :rows="result.items"
          :columns="columns"
          :loading="loading"
          :page="page"
          :total-pages="result.totalPages"
          empty-title="No hay organizaciones"
          empty-description="Crea la primera organización o ajusta los filtros."
          @update:page="page = $event; load()"
        >
          <template #body-cell-organization="props"><q-td :props="props"><strong>{{ props.row.tradeName }}</strong><span class="table-secondary">{{ props.row.legalName }}</span></q-td></template>
          <template #body-cell-nit="props"><q-td :props="props">{{ props.row.nit }}-{{ props.row.verificationDigit }}</q-td></template>
          <template #body-cell-location="props"><q-td :props="props">{{ props.row.municipalityName }}<span class="table-secondary">{{ props.row.departmentName }}</span></q-td></template>
          <template #body-cell-invitation="props"><q-td :props="props"><StatusChip :tone="invitationTone(props.row.invitationStatus)" :label="invitationLabel(props.row.invitationStatus)" /></q-td></template>
          <template #body-cell-status="props"><q-td :props="props"><StatusChip :tone="props.row.status === 'ACTIVE' ? 'success' : 'danger'" :label="props.row.status === 'ACTIVE' ? 'Activa' : 'Suspendida'" /></q-td></template>
          <template #body-cell-actions="props"><q-td :props="props"><q-btn flat round dense color="primary" :icon="icons.arrowForward" aria-label="Ver organización" @click="openOrganization(props.row.id)"><q-tooltip>Ver organización</q-tooltip></q-btn></q-td></template>
        </AppDataTable>

        <div class="mobile-records">
          <button v-for="item in result.items" :key="item.id" type="button" class="mobile-record-card" @click="openOrganization(item.id)">
            <span class="mobile-record-card__heading"><strong>{{ item.tradeName }}</strong><StatusChip :tone="item.status === 'ACTIVE' ? 'success' : 'danger'" :label="item.status === 'ACTIVE' ? 'Activa' : 'Suspendida'" /></span>
            <span>{{ item.nit }}-{{ item.verificationDigit }}</span>
            <span>{{ item.municipalityName }}, {{ item.departmentName }}</span>
          </button>
          <div v-if="result.totalPages > 1" class="pagination-row"><q-pagination v-model="page" :max="result.totalPages" boundary-numbers @update:model-value="load()" /></div>
        </div>
      </q-card>
    </main>
  </PlatformLayout>
</template>
