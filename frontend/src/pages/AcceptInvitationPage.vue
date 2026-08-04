<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AuthShell from '../components/AuthShell.vue'
import { api, getProblem } from '../services/api'

const route = useRoute()
const router = useRouter()
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
const password = ref('')
const confirmation = ref('')
const showPassword = ref(false)
const loading = ref(false)
const accepted = ref(false)
const errorMessage = ref('')
const valid = computed(() =>
  Boolean(token.value) &&
  password.value.length >= 8 &&
  password.value.length <= 128 &&
  password.value === confirmation.value,
)

async function submit(): Promise<void> {
  if (!valid.value || loading.value) return
  loading.value = true
  errorMessage.value = ''
  try {
    await api.post('/api/auth/accept-invitation', {
      token: token.value,
      newPassword: password.value,
    })
    accepted.value = true
  } catch (error) {
    const problem = getProblem(error)
    errorMessage.value =
      {
        'invitation.expired': 'La invitación expiró. Solicita a Legaria que envíe una nueva.',
        'invitation.used': 'La invitación ya fue utilizada o reemplazada.',
        'invitation.organization_suspended': 'La organización está suspendida. Contacta a Legaria.',
        'invitation.invalid': 'El enlace de invitación no es válido.',
      }[problem?.code ?? ''] ?? problem?.detail ?? 'No fue posible activar la cuenta.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AuthShell title="Activa tu cuenta" description="Define la contraseña con la que ingresarás a Legaria.">
    <div v-if="accepted" class="text-center">
      <q-icon name="check_circle" color="positive" size="58px" />
      <h2>Cuenta activada</h2>
      <p class="auth-copy q-mb-lg">Tu correo quedó verificado. Ya puedes iniciar sesión.</p>
      <q-btn unelevated no-caps color="primary" label="Ir al inicio de sesión" class="auth-submit full-width" @click="router.replace('/')" />
    </div>
    <template v-else>
      <q-banner v-if="!token" class="bg-red-1 text-negative q-mb-md rounded-borders" role="alert">
        El enlace no contiene una invitación válida.
      </q-banner>
      <q-banner v-if="errorMessage" class="bg-red-1 text-negative q-mb-md rounded-borders" role="alert">
        {{ errorMessage }}
      </q-banner>
      <q-form class="auth-form" @submit.prevent="submit">
        <q-input
          v-model="password"
          outlined
          :type="showPassword ? 'text' : 'password'"
          label="Nueva contraseña"
          autocomplete="new-password"
          :rules="[(value: string) => value.length >= 8 || 'Usa al menos 8 caracteres.', (value: string) => value.length <= 128 || 'Usa máximo 128 caracteres.']"
        >
          <template #prepend><q-icon name="lock_outline" /></template>
          <template #append><q-btn flat round dense :icon="showPassword ? 'visibility_off' : 'visibility'" @click="showPassword = !showPassword" /></template>
        </q-input>
        <q-input
          v-model="confirmation"
          outlined
          :type="showPassword ? 'text' : 'password'"
          label="Confirma la contraseña"
          autocomplete="new-password"
          :rules="[(value: string) => value === password || 'Las contraseñas no coinciden.']"
        >
          <template #prepend><q-icon name="verified_user" /></template>
        </q-input>
        <q-btn unelevated no-caps size="lg" color="primary" class="auth-submit full-width" label="Activar cuenta" type="submit" :loading="loading" :disable="!valid || loading" />
      </q-form>
    </template>
  </AuthShell>
</template>
