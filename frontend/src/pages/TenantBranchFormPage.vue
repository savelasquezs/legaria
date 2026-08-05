<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import TenantLayout from '../components/TenantLayout.vue'
import BranchEmployeesSection from '../components/BranchEmployeesSection.vue'
import AppAlert from '../components/AppAlert.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import FormSection from '../components/FormSection.vue'
import LoadingSkeleton from '../components/LoadingSkeleton.vue'
import PageHeader from '../components/PageHeader.vue'
import { icons } from '../design-system/icons'
import { optionalEmailRule, optionalPhoneRule, requiredRule } from '../helpers/branchFormRules'
import { getProblem } from '../services/api'
import { changeBranchStatus, createBranch, getBranch, updateBranch } from '../services/branches'
import { getDepartments, getMunicipalities } from '../services/organizations'
import { useAuthStore } from '../stores/auth'
import type { Branch, BranchData } from '../types/branches'
import type { Department, Municipality } from '../types/organizations'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const branchId = computed(() => route.params.id as string | undefined)
const editing = computed(() => Boolean(branchId.value))
const isSuperAdmin = computed(() => auth.account?.roles.includes('SUPER_ADMIN') === true)
const loading = ref(true)
const saving = ref(false)
const changingStatus = ref(false)
const confirmStatus = ref(false)
const errorMessage = ref('')
const branch = ref<Branch | null>(null)
const departments = ref<Department[]>([])
const municipalities = ref<Municipality[]>([])
const departmentCode = ref<string | null>(null)
const form = reactive<BranchData>({
  name: '',
  contactEmail: null,
  phone: null,
  address: '',
  municipalityCode: '',
})

async function loadMunicipalities(clear = true): Promise<void> {
  if (clear) form.municipalityCode = ''
  municipalities.value = departmentCode.value
    ? await getMunicipalities(departmentCode.value)
    : []
}

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    departments.value = await getDepartments()
    if (branchId.value) {
      const loaded = await getBranch(branchId.value)
      branch.value = loaded
      Object.assign(form, {
        name: loaded.name,
        contactEmail: loaded.contactEmail,
        phone: loaded.phone,
        address: loaded.address,
        municipalityCode: loaded.municipalityCode,
      })
      departmentCode.value = loaded.departmentCode
      await loadMunicipalities(false)
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar la sucursal.'
  } finally {
    loading.value = false
  }
}

async function save(): Promise<void> {
  if (!isSuperAdmin.value || saving.value) return
  saving.value = true
  errorMessage.value = ''
  try {
    if (branchId.value) {
      branch.value = await updateBranch(branchId.value, { ...form })
      Notify.create({ type: 'positive', message: 'Sucursal actualizada.' })
    } else {
      const created = await createBranch({ ...form })
      Notify.create({ type: 'positive', message: 'Sucursal creada.' })
      await router.replace(`/app/branches/${created.id}`)
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible guardar la sucursal.'
  } finally {
    saving.value = false
  }
}

async function toggleStatus(): Promise<void> {
  if (!branchId.value || !branch.value || changingStatus.value) return
  confirmStatus.value = false
  changingStatus.value = true
  errorMessage.value = ''
  try {
    const action = branch.value.status === 'ACTIVE' ? 'deactivate' : 'reactivate'
    branch.value = await changeBranchStatus(branchId.value, action)
    Notify.create({
      type: 'positive',
      message: action === 'deactivate' ? 'Sucursal desactivada.' : 'Sucursal reactivada.',
    })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cambiar el estado.'
  } finally {
    changingStatus.value = false
  }
}

onMounted(load)
</script>

<template>
  <TenantLayout>
    <main class="platform-content form-content">
      <PageHeader
        context="Sucursales"
        :title="editing ? branch?.name || 'Detalle de sucursal' : 'Nueva sucursal'"
        :description="isSuperAdmin ? 'Mantén actualizados los datos operativos de la sede.' : 'Consulta los datos de tu sucursal asignada.'"
        back-to="/app/branches"
        back-label="Volver a sucursales"
      >
        <template v-if="editing && isSuperAdmin && branch" #actions>
          <q-btn outline no-caps :color="branch.status === 'ACTIVE' ? 'negative' : 'positive'" :icon="branch.status === 'ACTIVE' ? icons.block : icons.check" :label="branch.status === 'ACTIVE' ? 'Desactivar' : 'Reactivar'" :loading="changingStatus" @click="confirmStatus = true" />
        </template>
      </PageHeader>

      <LoadingSkeleton v-if="loading" variant="form" :rows="6" />
      <AppAlert v-else-if="errorMessage && !branch && editing" tone="danger">
        {{ errorMessage }}
        <template #action><q-btn flat label="Reintentar" @click="load" /></template>
      </AppAlert>
      <q-form v-else @submit.prevent="save">
        <AppAlert v-if="errorMessage" tone="danger">{{ errorMessage }}</AppAlert>
        <FormSection title="Datos de la sucursal" description="Nombre, ubicación y canales de contacto." :icon="icons.storefront">
          <div class="fields-grid">
            <q-input v-model="form.name" outlined label="Nombre" :disable="!isSuperAdmin" :rules="[requiredRule]" class="span-two" />
            <q-input v-model="form.contactEmail" outlined type="email" label="Correo de contacto (opcional)" :disable="!isSuperAdmin" :rules="[optionalEmailRule]" />
            <q-input v-model="form.phone" outlined label="Teléfono (opcional)" :disable="!isSuperAdmin" :rules="[optionalPhoneRule]" />
            <q-input v-model="form.address" outlined label="Dirección" :disable="!isSuperAdmin" :rules="[requiredRule]" class="span-two" />
            <q-select
              v-model="departmentCode"
              outlined
              emit-value
              map-options
              option-value="code"
              option-label="name"
              :options="departments"
              label="Departamento"
              :disable="!isSuperAdmin"
              :rules="[(value: string) => Boolean(value) || 'Selecciona un departamento.']"
              @update:model-value="() => loadMunicipalities()"
            />
            <q-select
              v-model="form.municipalityCode"
              outlined
              emit-value
              map-options
              option-value="code"
              option-label="name"
              :options="municipalities"
              label="Municipio"
              :disable="!isSuperAdmin || !departmentCode"
              :rules="[(value: string) => Boolean(value) || 'Selecciona un municipio.']"
            />
          </div>
          <template v-if="isSuperAdmin" #actions><q-btn unelevated no-caps color="primary" type="submit" :label="editing ? 'Guardar cambios' : 'Crear sucursal'" :loading="saving" /></template>
        </FormSection>
      </q-form>

      <BranchEmployeesSection
        v-if="editing && branch"
        :branch-id="branch.id"
        :branch-active="branch.status === 'ACTIVE'"
        :read-only="!isSuperAdmin"
      />

      <ConfirmDialog
        v-model="confirmStatus"
        :title="branch?.status === 'ACTIVE' ? 'Desactivar sucursal' : 'Reactivar sucursal'"
        :message="branch?.status === 'ACTIVE'
          ? 'La sucursal dejará de estar disponible para nuevas operaciones. Sus datos y accesos se conservarán.'
          : 'Los accesos conservados volverán a ser efectivos para esta sucursal.'"
        :tone="branch?.status === 'ACTIVE' ? 'danger' : 'acceptance'"
        confirm-label="Confirmar"
        :loading="changingStatus"
        @confirm="toggleStatus"
      />
    </main>
  </TenantLayout>
</template>
