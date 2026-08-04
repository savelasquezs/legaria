<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PlatformLayout from '../components/PlatformLayout.vue'
import { getProblem } from '../services/api'
import {
  changeOrganizationStatus,
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
const errorMessage = ref('')
const organization = ref<Organization | null>(null)
const departments = ref<Department[]>([])
const municipalities = ref<Municipality[]>([])
const departmentCode = ref<string | null>(null)

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

const emailRule = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) || 'Ingresa un correo válido.'
const requiredRule = (value: string) => Boolean(value?.trim()) || 'Este campo es obligatorio.'
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
  }[value]
}

async function loadMunicipalities(clearSelection = true): Promise<void> {
  if (clearSelection) company.municipalityCode = ''
  municipalities.value = departmentCode.value
    ? await getMunicipalities(departmentCode.value)
    : []
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
  const accepted = globalThis.confirm(
    suspending
      ? 'Suspender esta organización bloqueará inmediatamente el login, refresh y acceso de todas sus cuentas tenant. ¿Deseas continuar?'
      : '¿Deseas reactivar la organización y recuperar el acceso de sus cuentas?',
  )
  if (!accepted) return

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
      <q-btn flat no-caps icon="arrow_back" label="Volver" class="back-action" @click="router.push('/platform')" />

      <header class="page-heading compact-heading">
        <div>
          <p class="eyebrow">{{ editing ? 'Detalle de organización' : 'Aprovisionamiento tenant' }}</p>
          <h1>{{ editing ? organization?.tradeName ?? 'Organización' : 'Nueva organización' }}</h1>
          <p v-if="editing && organization">NIT {{ organization.nit }}-{{ organization.verificationDigit }}</p>
          <p v-else>Crea la empresa y envía una invitación segura a su primer superadministrador.</p>
        </div>
        <div v-if="editing && organization" class="heading-actions">
          <q-chip
            outline
            :color="organization.status === 'ACTIVE' ? 'positive' : 'negative'"
          >
            {{ organization.status === 'ACTIVE' ? 'Activa' : 'Suspendida' }}
          </q-chip>
          <q-btn
            v-if="isOwner"
            outline
            no-caps
            :color="organization.status === 'ACTIVE' ? 'negative' : 'positive'"
            :label="organization.status === 'ACTIVE' ? 'Suspender' : 'Reactivar'"
            :loading="changingStatus"
            @click="toggleStatus"
          />
        </div>
      </header>

      <q-banner v-if="errorMessage" class="bg-red-1 text-negative q-mb-lg rounded-borders" role="alert">
        {{ errorMessage }}
      </q-banner>
      <div v-if="loading" class="loading-state"><q-spinner color="primary" size="42px" /></div>

      <template v-else>
        <q-form class="organization-grid" @submit.prevent="saveCompany">
          <q-card flat bordered class="platform-card form-card">
            <q-card-section>
              <div class="section-heading">
                <q-icon name="business" />
                <div><h2>Empresa</h2><p>Información legal y de contacto.</p></div>
              </div>
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
            </q-card-section>
            <q-card-actions align="right" class="q-pa-md q-pt-none">
              <q-btn
                unelevated
                no-caps
                color="primary"
                :label="editing ? 'Guardar datos' : 'Crear organización'"
                type="submit"
                :loading="saving"
                :disable="!nitIsValid"
              />
            </q-card-actions>
          </q-card>

          <q-card flat bordered class="platform-card form-card">
            <q-card-section>
              <div class="section-heading">
                <q-icon name="admin_panel_settings" />
                <div><h2>Superadministrador inicial</h2><p>No se comparte ninguna contraseña.</p></div>
              </div>

              <q-banner
                v-if="editing && organization"
                :class="organization.initialAdmin.invitationStatus === 'DELIVERY_FAILED' ? 'bg-orange-1 text-warning' : 'bg-blue-1 text-primary'"
                class="q-mb-lg rounded-borders"
              >
                <strong>{{ invitationLabel(organization.initialAdmin.invitationStatus) }}</strong>
                <div v-if="organization.initialAdmin.invitationExpiresAt" class="text-caption">
                  Vigente hasta {{ new Date(organization.initialAdmin.invitationExpiresAt).toLocaleString('es-CO') }}
                </div>
                <div v-if="organization.initialAdmin.invitationStatus === 'DELIVERY_FAILED'" class="text-caption">
                  La organización quedó creada. Revisa Resend y usa “Reenviar invitación”.
                </div>
              </q-banner>

              <div class="fields-grid">
                <q-input v-model="admin.firstName" outlined label="Nombres" :disable="editing && !adminIsPending" :rules="[requiredRule]" />
                <q-input v-model="admin.lastName" outlined label="Apellidos" :disable="editing && !adminIsPending" :rules="[requiredRule]" />
                <q-input v-model="admin.email" outlined type="email" label="Correo de acceso" class="span-two" :disable="editing && !adminIsPending" :rules="[requiredRule, emailRule]" />
              </div>
            </q-card-section>
            <q-card-actions v-if="editing && adminIsPending" align="right" class="q-pa-md q-pt-none admin-actions">
              <q-btn flat no-caps color="primary" label="Reenviar invitación" :loading="sending" @click="resend" />
              <q-btn unelevated no-caps color="primary" label="Guardar y reinvitar" :loading="adminSaving" :disable="!adminIsValid" @click="saveAdmin" />
            </q-card-actions>
          </q-card>
        </q-form>
      </template>
    </main>
  </PlatformLayout>
</template>
