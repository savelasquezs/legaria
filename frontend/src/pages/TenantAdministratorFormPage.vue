<script setup lang="ts">
import { Notify } from 'quasar'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import TenantLayout from '../components/TenantLayout.vue'
import { getProblem } from '../services/api'
import {
  changeBranchAdministratorStatus,
  createBranchAdministrator,
  getBranchAdministrator,
  listBranches,
  resendBranchAdministratorInvitation,
  updateBranchAdministratorAssignments,
  updatePendingBranchAdministrator,
} from '../services/branches'
import type { Branch, BranchAdministrator, BranchAdministratorData } from '../types/branches'
import type { InvitationStatus } from '../types/organizations'

const route = useRoute()
const router = useRouter()
const administratorId = computed(() => route.params.id as string | undefined)
const editing = computed(() => Boolean(administratorId.value))
const loading = ref(true)
const saving = ref(false)
const sending = ref(false)
const changingStatus = ref(false)
const confirmStatus = ref(false)
const errorMessage = ref('')
const administrator = ref<BranchAdministrator | null>(null)
const branches = ref<Branch[]>([])
const form = reactive<BranchAdministratorData>({ firstName: '', lastName: '', email: '', branchIds: [] })
const pending = computed(() => administrator.value?.invitationStatus !== 'ACCEPTED')
const active = computed(() => administrator.value?.accountStatus !== 'SUSPENDED')
const inactiveAssignments = computed(() => administrator.value?.branches.filter((branch) => branch.status === 'INACTIVE') ?? [])

const requiredRule = (value: string) => Boolean(value?.trim()) || 'Este campo es obligatorio.'
const emailRule = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) || 'Ingresa un correo válido.'
const branchRule = (value: string[]) => value.length > 0 || 'Selecciona al menos una sucursal activa.'

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

async function load(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    const branchPage = await listBranches({ page: 1, pageSize: 100, status: 'ACTIVE' })
    branches.value = branchPage.items
    if (administratorId.value) {
      const loaded = await getBranchAdministrator(administratorId.value)
      administrator.value = loaded
      Object.assign(form, {
        firstName: loaded.firstName,
        lastName: loaded.lastName,
        email: loaded.email,
        branchIds: loaded.branches.filter((branch) => branch.status === 'ACTIVE').map((branch) => branch.id),
      })
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible cargar el administrador.'
  } finally {
    loading.value = false
  }
}

async function save(): Promise<void> {
  if (saving.value || form.branchIds.length === 0) return
  saving.value = true
  errorMessage.value = ''
  try {
    if (!administratorId.value) {
      const created = await createBranchAdministrator({ ...form })
      Notify.create({
        type: created.invitationStatus === 'DELIVERY_FAILED' ? 'warning' : 'positive',
        message: created.invitationStatus === 'DELIVERY_FAILED'
          ? 'El administrador se creó, pero el correo no pudo enviarse.'
          : 'Administrador creado e invitación enviada.',
      })
      await router.replace(`/app/administrators/${created.id}`)
    } else if (pending.value) {
      administrator.value = await updatePendingBranchAdministrator(administratorId.value, { ...form })
      Notify.create({ type: 'positive', message: 'Datos actualizados y nueva invitación generada.' })
    } else {
      administrator.value = await updateBranchAdministratorAssignments(administratorId.value, [...form.branchIds])
      Notify.create({ type: 'positive', message: 'Sucursales permitidas actualizadas.' })
    }
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible guardar el administrador.'
  } finally {
    saving.value = false
  }
}

async function resend(): Promise<void> {
  if (!administratorId.value || sending.value) return
  sending.value = true
  errorMessage.value = ''
  try {
    administrator.value = await resendBranchAdministratorInvitation(administratorId.value)
    Notify.create({
      type: administrator.value.invitationStatus === 'DELIVERY_FAILED' ? 'warning' : 'positive',
      message: administrator.value.invitationStatus === 'DELIVERY_FAILED'
        ? 'Se generó una invitación nueva, pero el correo falló.'
        : 'La invitación anterior fue revocada y se envió una nueva.',
    })
  } catch (error) {
    errorMessage.value = getProblem(error)?.detail ?? 'No fue posible reenviar la invitación.'
  } finally {
    sending.value = false
  }
}

async function toggleStatus(): Promise<void> {
  if (!administratorId.value || !administrator.value || changingStatus.value) return
  confirmStatus.value = false
  changingStatus.value = true
  errorMessage.value = ''
  try {
    const action = administrator.value.accountStatus === 'ACTIVE' ? 'suspend' : 'reactivate'
    administrator.value = await changeBranchAdministratorStatus(administratorId.value, action)
    Notify.create({
      type: 'positive',
      message: action === 'suspend'
        ? 'Cuenta suspendida y sesiones revocadas.'
        : 'Cuenta reactivada. Debe iniciar sesión nuevamente.',
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
      <q-btn flat no-caps icon="arrow_back" label="Volver a administradores" class="back-action" to="/app/administrators" />
      <header class="page-heading compact-heading">
        <div>
          <p class="eyebrow">Administradores</p>
          <h1>{{ editing ? `${administrator?.firstName || ''} ${administrator?.lastName || ''}`.trim() || 'Detalle' : 'Invitar administrador' }}</h1>
          <p>La cuenta solo podrá consultar las sucursales seleccionadas.</p>
        </div>
        <q-btn
          v-if="editing && administrator"
          outline
          no-caps
          :color="administrator.accountStatus === 'ACTIVE' ? 'negative' : 'positive'"
          :icon="administrator.accountStatus === 'ACTIVE' ? 'person_off' : 'person_add_alt'"
          :label="administrator.accountStatus === 'ACTIVE' ? 'Suspender cuenta' : 'Reactivar cuenta'"
          :loading="changingStatus"
          @click="confirmStatus = true"
        />
      </header>

      <div v-if="loading" class="loading-state"><q-spinner color="primary" size="42px" /></div>
      <q-banner v-else-if="errorMessage && !administrator && editing" class="bg-red-1 text-negative rounded-borders">{{ errorMessage }}</q-banner>
      <q-form v-else @submit.prevent="save">
        <q-banner v-if="errorMessage" class="bg-red-1 text-negative q-mb-md rounded-borders">{{ errorMessage }}</q-banner>
        <q-banner v-if="branches.length === 0" class="bg-orange-1 text-warning q-mb-md rounded-borders">
          Debes crear al menos una sucursal activa antes de invitar un administrador.
          <template #action><q-btn flat no-caps label="Crear sucursal" to="/app/branches/new" /></template>
        </q-banner>
        <q-banner v-if="administrator" class="invitation-banner q-mb-md rounded-borders" :class="administrator.invitationStatus === 'DELIVERY_FAILED' ? 'bg-orange-1 text-warning' : administrator.accountStatus === 'SUSPENDED' ? 'bg-red-1 text-negative' : 'bg-blue-1 text-primary'">
          <strong>{{ invitationLabel(administrator.invitationStatus) }}</strong>
          <div v-if="administrator.invitationExpiresAt" class="text-caption">Vigente hasta {{ new Date(administrator.invitationExpiresAt).toLocaleString('es-CO') }}</div>
          <div v-if="administrator.invitationStatus === 'REVOKED'" class="text-caption">Reactiva la cuenta y envía una invitación nueva para permitir la activación.</div>
          <template v-if="pending && active" #action><q-btn flat no-caps label="Reenviar invitación" :loading="sending" @click="resend" /></template>
        </q-banner>

        <q-card flat bordered class="platform-card form-card">
          <q-card-section>
            <div class="section-heading">
              <q-icon name="manage_accounts" />
              <div><h2>Cuenta y alcance</h2><p>La invitación vence en 24 horas y no comparte contraseñas.</p></div>
            </div>
            <div class="fields-grid">
              <q-input v-model="form.firstName" outlined label="Nombres" :disable="editing && !pending" :rules="[requiredRule]" />
              <q-input v-model="form.lastName" outlined label="Apellidos" :disable="editing && !pending" :rules="[requiredRule]" />
              <q-input v-model="form.email" outlined type="email" label="Correo de acceso" :disable="editing && !pending" :rules="[requiredRule, emailRule]" class="span-two" />
              <q-select
                v-model="form.branchIds"
                outlined
                multiple
                use-chips
                emit-value
                map-options
                option-value="id"
                option-label="name"
                :options="branches"
                label="Sucursales permitidas"
                :rules="[branchRule]"
                class="span-two"
              />
            </div>
            <q-banner v-if="inactiveAssignments.length" class="bg-grey-2 text-grey-8 rounded-borders q-mt-md">
              Acceso conservado en sucursales inactivas: {{ inactiveAssignments.map((branch) => branch.name).join(', ') }}. Volverá a ser efectivo si se reactivan.
            </q-banner>
          </q-card-section>
          <q-card-actions align="right" class="q-pa-md q-pt-none">
            <q-btn unelevated no-caps color="primary" type="submit" :label="editing ? pending ? 'Guardar y reinvitar' : 'Guardar sucursales' : 'Crear y enviar invitación'" :loading="saving" :disable="branches.length === 0 || (pending && !active)" />
          </q-card-actions>
        </q-card>
      </q-form>

      <q-dialog v-model="confirmStatus">
        <q-card class="confirm-card">
          <q-card-section>
            <h2>{{ administrator?.accountStatus === 'ACTIVE' ? 'Suspender administrador' : 'Reactivar administrador' }}</h2>
            <p v-if="administrator?.accountStatus === 'ACTIVE'">Se bloquearán inmediatamente el login, el refresh, los JWT vigentes y las invitaciones pendientes. Los accesos por sucursal se conservarán.</p>
            <p v-else>La cuenta podrá volver a iniciar sesión, pero las sesiones anteriores no se recuperarán.</p>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat no-caps label="Cancelar" v-close-popup />
            <q-btn unelevated no-caps :color="administrator?.accountStatus === 'ACTIVE' ? 'negative' : 'positive'" label="Confirmar" @click="toggleStatus" />
          </q-card-actions>
        </q-card>
      </q-dialog>
    </main>
  </TenantLayout>
</template>
