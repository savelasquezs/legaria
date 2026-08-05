<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppDialog from '../components/AppDialog.vue'
import AppAlert from '../components/AppAlert.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import FormSection from '../components/FormSection.vue'
import LoadingSkeleton from '../components/LoadingSkeleton.vue'
import PageHeader from '../components/PageHeader.vue'
import PlatformLayout from '../components/PlatformLayout.vue'
import StatusChip from '../components/StatusChip.vue'
import { icons } from '../design-system/icons'
import { optionalEmailRule, optionalPhoneRule, requiredRule } from '../helpers/branchFormRules'
import { getProblem } from '../services/api'
import {
  changeOrganizationStatus,
  createInitialBranch,
  createOrganization,
  getDepartments,
  getMunicipalities,
  getOrganization,
  resendInvitation,
  updateInitialAdmin,
  updateOrganization,
} from '../services/organizations'
import { useAuthStore } from '../stores/auth'
import type {
  Department,
  InitialAdministratorData,
  InvitationStatus,
  Municipality,
  Organization,
  OrganizationData,
} from '../types/organizations'
import type { BranchData } from '../types/branches'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const organizationId = computed(() => route.params.id as string | undefined)
const editing = computed(() => Boolean(organizationId.value))
const isOwner = computed(() => auth.account?.roles.includes('OWNER') === true)
const loading = ref(true)
const saving = ref(false)
const adminSaving = ref(false)
const sending = ref(false)
const changingStatus = ref(false)
const statusDialog = ref(false)
const branchSuggestion = ref(false)
const branchDialog = ref(false)
const branchSaving = ref(false)
const branchLocationLoading = ref(false)
const branchError = ref('')
const errorMessage = ref('')
const organization = ref<Organization | null>(null)
const departments = ref<Department[]>([])
const municipalities = ref<Municipality[]>([])
const departmentCode = ref<string | null>(null)
const branchDepartmentCode = ref<string | null>(null)
const branchMunicipalities = ref<Municipality[]>([])
const branchForm = ref<{ validate: () => Promise<boolean> } | null>(null)

const company = reactive<OrganizationData>({
  tradeName: '',
  legalName: '',
  nit: '',
  verificationDigit: 0,
  contactEmail: '',
  phone: '',
  address: '',
  municipalityCode: '',
})
const admin = reactive<InitialAdministratorData>({
  firstName: '',
  lastName: '',
  email: '',
})
const initialBranch = reactive<BranchData>({
  name: 'Sede principal',
  contactEmail: null,
  phone: null,
  address: '',
  municipalityCode: '',
})

const emailRule = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) || 'Ingresa un correo válido.'
const nitIsValid = computed(() =>
  /^[0-9]{6,14}$/.test(company.nit) &&
  calculateVerificationDigit(company.nit) === Number(company.verificationDigit),
)
const adminIsPending = computed(() => organization.value?.initialAdmin.invitationStatus !== 'ACCEPTED')
const adminIsValid = computed(() =>
  Boolean(admin.firstName.trim()) &&
  Boolean(admin.lastName.trim()) &&
  /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(admin.email),
)

function calculateVerificationDigit(nit: string): number | null {
  if (!/^[0-9]{1,15}$/.test(nit)) return null
  const weights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71]
  let sum = 0
  for (let index = nit.length - 1; index >= 0; index -= 1) {
    sum += Number(nit[index]) * weights[nit.length - 1 - index]!
  }
  const remainder = sum % 11
  return remainder > 1 ? 11 - remainder : remainder
}

function invitationLabel(value: InvitationStatus): string {
  return {
    PENDING_DELIVERY: 'Pendiente de envío',
    SENT: 'Invitación enviada',
    DELIVERY_FAILED: 'Falló el envío',
    EXPIRED: 'Invitación expirada',
    ACCEPTED: 'Cuenta activada',
    REVOKED: 'Invitación revocada',
  }[value]
}

async function loadMunicipalities(clearSelection = true): Promise<void> {
  if (clearSelection) company.municipalityCode = ''
  municipalities.value = departmentCode.value
    ? await getMunicipalities(departmentCode.value)
    : []
}

async function loadBranchMunicipalities(clearSelection = true): Promise<void> {
  if (clearSelection) initialBranch.municipalityCode = ''
  branchLocationLoading.value = true
  try {
    branchMunicipalities.value = branchDepartmentCode.value
      ? await getMunicipalities(branchDepartmentCode.value)
      : []
  } catch (error) {
    branchError.value = getProblem(error)?.detail ?? 'No fue posible cargar los municipios.'
  } finally {
    branchLocationLoading.value = false
  }
}

async function openInitialBranchDialog(): Promise<void> {
  if (!organization.value || organization.value.hasBranches) return
  branchError.value = ''
  Object.assign(initialBranch, {
    name: 'Sede principal',
    contactEmail: organization.value.contactEmail,
    phone: organization.value.phone,
    address: organization.value.address,
    municipalityCode: organization.value.municipalityCode,
  })
  branchDepartmentCode.value = organization.value.departmentCode
  branchDialog.value = true
  await loadBranchMunicipalities(false)
}

async function acceptBranchSuggestion(): Promise<void> {
  branchSuggestion.value = false
  await openInitialBranchDialog()
}

async function saveInitialBranch(): Promise<void> {
  if (!organization.value || branchSaving.value || !(await branchForm.value?.validate())) return
  branchSaving.value = true
  branchError.value = ''
  try {
    await createInitialBranch(organization.value.id, { ...initialBranch })
    organization.value = { ...organization.value, hasBranches: true }
    branchDialog.value = false
    Notify.create({ type: 'positive', message: 'Primera sucursal creada.' })
  } catch (error) {
    branchError.value = getProblem(error)?.detail ?? 'No fue posible crear la sucursal.'
  } finally {
    branchSaving.value = false
  }
}

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    departments.value = await getDepartments()
    if (organizationId.value) {
      const loaded = await getOrganization(organizationId.value)
      organization.value = loaded
      Object.assign(company, {
        tradeName: loaded.tradeName,
        legalName: loaded.legalName,
        nit: loaded.nit,
        verificationDigit: loaded.verificationDigit,
        contactEmail: loaded.contactEmail,
        phone: loaded.phone,
        address: loaded.address,
        municipalityCode: loaded.municipalityCode,
      })
      Object.assign(admin, {
        firstName: loaded.initialAdmin.firstName,
        lastName: loaded.initialAdmin.lastName,
        email: loaded.initialAdmin.email,
      })
      departmentCode.value = loaded.departmentCode
      await loadMunicipalities(false)
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar la información.'
  } finally {
    loading.value = false
  }
}

async function saveCompany(): Promise<void> {
  if (!nitIsValid.value || saving.value) return
  saving.value = true
  errorMessage.value = ''
  try {
    if (organizationId.value) {
      organization.value = await updateOrganization(organizationId.value, { ...company })
      Notify.create({ type: 'positive', message: 'Datos de la organización actualizados.' })
    } else {
      const created = await createOrganization({ ...company, initialAdmin: { ...admin } })
      organization.value = created
      Notify.create({
        type: created.initialAdmin.invitationStatus === 'DELIVERY_FAILED' ? 'warning' : 'positive',
        message:
          created.initialAdmin.invitationStatus === 'DELIVERY_FAILED'
            ? 'La organización se creó, pero el correo no pudo enviarse.'
            : 'Organización creada e invitación procesada.',
      })
      await router.replace(`/platform/organizations/${created.id}`)
      branchSuggestion.value = true
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible guardar la organización.'
  } finally {
    saving.value = false
  }
}

async function saveAdmin(): Promise<void> {
  if (!organizationId.value || !adminIsValid.value || adminSaving.value) return
  adminSaving.value = true
  errorMessage.value = ''
  try {
    organization.value = await updateInitialAdmin(organizationId.value, { ...admin })
    Notify.create({ type: 'positive', message: 'Administrador actualizado y reinvitado.' })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible actualizar el administrador.'
  } finally {
    adminSaving.value = false
  }
}

async function resend(): Promise<void> {
  if (!organizationId.value || sending.value) return
  sending.value = true
  errorMessage.value = ''
  try {
    organization.value = await resendInvitation(organizationId.value)
    Notify.create({
      type: organization.value.initialAdmin.invitationStatus === 'DELIVERY_FAILED' ? 'warning' : 'positive',
      message:
        organization.value.initialAdmin.invitationStatus === 'DELIVERY_FAILED'
          ? 'Se generó una invitación nueva, pero el correo volvió a fallar.'
          : 'La invitación anterior fue revocada y se envió una nueva.',
    })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible reenviar la invitación.'
  } finally {
    sending.value = false
  }
}

async function toggleStatus(): Promise<void> {
  if (!organizationId.value || !organization.value || changingStatus.value) return
  const suspending = organization.value.status === 'ACTIVE'
  statusDialog.value = false
  changingStatus.value = true
  errorMessage.value = ''
  try {
    organization.value = await changeOrganizationStatus(
      organizationId.value,
      suspending ? 'suspend' : 'reactivate',
    )
    Notify.create({
      type: 'positive',
      message: suspending ? 'Organización suspendida.' : 'Organización reactivada.',
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
  <PlatformLayout>
    <main class="platform-content form-content">
      <PageHeader
        :context="editing ? 'Detalle de organización' : 'Aprovisionamiento tenant'"
        :title="editing ? organization?.tradeName ?? 'Organización' : 'Nueva organización'"
        :description="editing && organization ? `NIT ${organization.nit}-${organization.verificationDigit}` : 'Crea la empresa y envía una invitación segura a su primer superadministrador.'"
        back-to="/platform"
      >
        <template v-if="editing && organization" #actions>
          <StatusChip :tone="organization.status === 'ACTIVE' ? 'success' : 'danger'" :label="organization.status === 'ACTIVE' ? 'Activa' : 'Suspendida'" />
          <q-btn
            v-if="!organization.hasBranches"
            outline
            no-caps
            color="primary"
            :icon="icons.addBusiness"
            label="Crear primera sucursal"
            @click="openInitialBranchDialog"
          />
          <q-btn
            v-if="isOwner"
            outline
            no-caps
            :color="organization.status === 'ACTIVE' ? 'negative' : 'positive'"
            :label="organization.status === 'ACTIVE' ? 'Suspender' : 'Reactivar'"
            :loading="changingStatus"
            @click="statusDialog = true"
          />
        </template>
      </PageHeader>

      <AppAlert v-if="errorMessage" tone="danger">
        {{ errorMessage }}
      </AppAlert>
      <LoadingSkeleton v-if="loading" variant="form" :rows="8" />

      <template v-else>
        <q-form class="organization-grid" @submit.prevent="saveCompany">
          <FormSection title="Empresa" description="Información legal y de contacto." :icon="icons.apartment">
            <div class="fields-grid">
              <q-input v-model="company.tradeName" outlined label="Nombre comercial" :rules="[requiredRule]" />
              <q-input v-model="company.legalName" outlined label="Razón social" :rules="[requiredRule]" />
              <q-input
                v-model="company.nit"
                outlined
                label="NIT sin DV"
                maxlength="14"
                :rules="[
                  (value: string) => /^[0-9]{6,14}$/.test(value) || 'Usa entre 6 y 14 dígitos, sin puntos ni guiones.',
                ]"
              />
              <q-input
                v-model.number="company.verificationDigit"
                outlined
                type="number"
                min="0"
                max="9"
                label="Dígito de verificación"
                :rules="[() => nitIsValid || 'El DV no corresponde al NIT.']"
              />
              <q-input v-model="company.contactEmail" outlined type="email" label="Correo de contacto" :rules="[requiredRule, emailRule]" />
              <q-input v-model="company.phone" outlined label="Teléfono" :rules="[requiredRule]" />
              <q-input v-model="company.address" outlined label="Dirección" :rules="[requiredRule]" class="span-two" />
              <q-select
                v-model="departmentCode"
                outlined
                emit-value
                map-options
                option-value="code"
                option-label="name"
                :options="departments"
                label="Departamento"
                :rules="[(value: string) => Boolean(value) || 'Selecciona un departamento.']"
                @update:model-value="() => loadMunicipalities()"
              />
              <q-select
                v-model="company.municipalityCode"
                outlined
                emit-value
                map-options
                option-value="code"
                option-label="name"
                :options="municipalities"
                label="Municipio"
                :disable="!departmentCode"
                :rules="[(value: string) => Boolean(value) || 'Selecciona un municipio.']"
              />
            </div>
            <template v-if="editing" #actions>
              <q-btn
                unelevated
                no-caps
                color="primary"
                label="Guardar datos"
                type="submit"
                :loading="saving"
                :disable="!nitIsValid"
              />
            </template>
          </FormSection>

          <FormSection title="Superadministrador inicial" description="No se comparte ninguna contraseña." :icon="icons.admin">
            <AppAlert
              v-if="editing && organization"
              :tone="organization.initialAdmin.invitationStatus === 'DELIVERY_FAILED' ? 'danger' : 'info'"
            >
              <strong>{{ invitationLabel(organization.initialAdmin.invitationStatus) }}</strong>
              <div v-if="organization.initialAdmin.invitationExpiresAt" class="text-caption">
                Vigente hasta {{ new Date(organization.initialAdmin.invitationExpiresAt).toLocaleString('es-CO') }}
              </div>
              <div v-if="organization.initialAdmin.invitationStatus === 'DELIVERY_FAILED'" class="text-caption">
                La organización quedó creada. Revisa Resend y usa “Reenviar invitación”.
              </div>
            </AppAlert>

            <div class="fields-grid">
              <q-input v-model="admin.firstName" outlined label="Nombres" :disable="editing && !adminIsPending" :rules="[requiredRule]" />
              <q-input v-model="admin.lastName" outlined label="Apellidos" :disable="editing && !adminIsPending" :rules="[requiredRule]" />
              <q-input v-model="admin.email" outlined type="email" label="Correo de acceso" class="span-two" :disable="editing && !adminIsPending" :rules="[requiredRule, emailRule]" />
            </div>
            <template v-if="editing && adminIsPending" #actions>
              <q-btn flat no-caps color="primary" label="Reenviar invitación" :loading="sending" @click="resend" />
              <q-btn unelevated no-caps color="primary" label="Guardar y reinvitar" :loading="adminSaving" :disable="!adminIsValid" @click="saveAdmin" />
            </template>
          </FormSection>

          <div v-if="!editing" class="provisioning-actions">
            <p>La organización y su superadministrador inicial se crearán en una sola operación.</p>
            <q-btn
              unelevated
              no-caps
              color="primary"
              label="Crear organización"
              type="submit"
              :loading="saving"
              :disable="!nitIsValid || !adminIsValid"
            />
          </div>
        </q-form>
      </template>

      <ConfirmDialog
        v-model="branchSuggestion"
        title="Organización creada"
        message="¿Deseas configurar ahora la primera sucursal reutilizando los datos de contacto y ubicación de la organización?"
        tone="acceptance"
        confirm-label="Crear sucursal"
        cancel-label="Ahora no"
        @confirm="acceptBranchSuggestion"
      />

      <ConfirmDialog
        v-if="organization"
        v-model="statusDialog"
        :title="organization.status === 'ACTIVE' ? 'Suspender organización' : 'Reactivar organización'"
        :message="organization.status === 'ACTIVE'
          ? 'Se bloquearán inmediatamente el login, la renovación de sesión y el acceso de todas las cuentas tenant.'
          : 'Las cuentas activas de la organización recuperarán el acceso, sin restaurar sesiones revocadas.'"
        :tone="organization.status === 'ACTIVE' ? 'danger' : 'acceptance'"
        confirm-label="Confirmar"
        :loading="changingStatus"
        @confirm="toggleStatus"
      />

      <AppDialog
        v-model="branchDialog"
        title="Primera sucursal"
        description="Revisa los datos reutilizados de la organización antes de guardar."
        :icon="icons.storefront"
        size="lg"
        :persistent="branchSaving"
      >
        <AppAlert v-if="branchError" tone="danger">
          {{ branchError }}
        </AppAlert>
        <q-form ref="branchForm" @submit.prevent="saveInitialBranch">
          <div class="fields-grid">
            <q-input
              v-model="initialBranch.name"
              outlined
              label="Nombre"
              class="span-two"
              maxlength="150"
              :rules="[requiredRule]"
            />
            <q-input
              v-model="initialBranch.contactEmail"
              outlined
              type="email"
              label="Correo de contacto (opcional)"
              :rules="[optionalEmailRule]"
            />
            <q-input
              v-model="initialBranch.phone"
              outlined
              label="Teléfono (opcional)"
              :rules="[optionalPhoneRule]"
            />
            <q-input
              v-model="initialBranch.address"
              outlined
              label="Dirección"
              class="span-two"
              maxlength="250"
              :rules="[requiredRule]"
            />
            <q-select
              v-model="branchDepartmentCode"
              outlined
              emit-value
              map-options
              option-value="code"
              option-label="name"
              :options="departments"
              label="Departamento"
              :rules="[(value: string) => Boolean(value) || 'Selecciona un departamento.']"
              @update:model-value="() => loadBranchMunicipalities()"
            />
            <q-select
              v-model="initialBranch.municipalityCode"
              outlined
              emit-value
              map-options
              option-value="code"
              option-label="name"
              :options="branchMunicipalities"
              label="Municipio"
              :loading="branchLocationLoading"
              :disable="!branchDepartmentCode || branchLocationLoading"
              :rules="[(value: string) => Boolean(value) || 'Selecciona un municipio.']"
            />
          </div>
        </q-form>
        <template #actions>
          <q-btn flat no-caps label="Cancelar" :disable="branchSaving" @click="branchDialog = false" />
          <q-btn
            unelevated
            no-caps
            color="primary"
            label="Crear sucursal"
            :loading="branchSaving"
            @click="saveInitialBranch"
          />
        </template>
      </AppDialog>
    </main>
  </PlatformLayout>
</template>
