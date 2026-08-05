<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import AuthShell from '../components/AuthShell.vue'
import AppAlert from '../components/AppAlert.vue'
import { icons } from '../design-system/icons'
import { api, getProblem } from '../services/api'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const email = ref('')
const password = ref('')
const showPassword = ref(false)
const loading = ref(false)
const errorMessage = ref('')
const canResendVerification = ref(false)
const verificationSent = ref(false)
const emailIsValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value))
const formIsValid = computed(
  () => emailIsValid.value && password.value.length >= 8 && password.value.length <= 128,
)

async function submit(): Promise<void> {
  if (!formIsValid.value || loading.value) return
  loading.value = true
  errorMessage.value = ''
  canResendVerification.value = false
  try {
    await auth.login(email.value.trim(), password.value)
    await router.replace(auth.account?.accountType === 'PLATFORM' ? '/platform' : '/app')
  } catch (error) {
    const problem = getProblem(error)
    canResendVerification.value = problem?.code === 'auth.email_not_verified'
    errorMessage.value =
      problem?.detail ?? 'No fue posible conectar con Legaria. Revisa tu conexión e inténtalo de nuevo.'
  } finally {
    loading.value = false
  }
}

async function resendVerification(): Promise<void> {
  verificationSent.value = false
  try {
    await api.post('/api/auth/resend-verification', { email: email.value.trim() })
    verificationSent.value = true
    canResendVerification.value = false
  } catch {
    errorMessage.value = 'No fue posible solicitar un nuevo correo. Inténtalo más tarde.'
  }
}
</script>

<template>
  <AuthShell
    title="Bienvenido de nuevo"
    description="Ingresa con tu correo y contraseña para continuar."
  >
    <AppAlert
      v-if="errorMessage"
      tone="danger"
    >
      {{ errorMessage }}
      <template v-if="canResendVerification" #action>
        <q-btn
          flat
          color="negative"
          label="Reenviar correo"
          @click="resendVerification"
        />
      </template>
    </AppAlert>
    <AppAlert
      v-if="verificationSent"
      tone="success"
    >
      Si la cuenta requiere verificación, recibirás un nuevo enlace.
    </AppAlert>

    <q-form class="auth-form" @submit.prevent="submit">
      <q-input
        v-model.trim="email"
        outlined
        type="email"
        label="Correo electrónico"
        autocomplete="email"
        :rules="[
          (value: string) => Boolean(value) || 'Ingresa tu correo.',
          () => emailIsValid || 'Ingresa un correo válido.',
        ]"
        lazy-rules
      >
        <template #prepend><q-icon :name="icons.mail" /></template>
      </q-input>
      <q-input
        v-model="password"
        outlined
        :type="showPassword ? 'text' : 'password'"
        label="Contraseña"
        autocomplete="current-password"
        :rules="[
          (value: string) => value.length >= 8 || 'La contraseña debe tener al menos 8 caracteres.',
          (value: string) => value.length <= 128 || 'La contraseña no puede superar 128 caracteres.',
        ]"
        lazy-rules
      >
        <template #prepend><q-icon :name="icons.lock" /></template>
        <template #append>
          <q-btn
            flat
            round
            dense
            :icon="showPassword ? icons.visibilityOff : icons.visibility"
            :aria-label="showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'"
            @click="showPassword = !showPassword"
          >
            <q-tooltip>{{ showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña' }}</q-tooltip>
          </q-btn>
        </template>
      </q-input>

      <div class="text-right">
        <router-link class="auth-link" to="/forgot-password">
          ¿Olvidaste tu contraseña?
        </router-link>
      </div>
      <q-btn
        unelevated
        no-caps
        size="lg"
        color="primary"
        class="auth-submit full-width"
        label="Ingresar"
        type="submit"
        :loading="loading"
        :disable="!formIsValid || loading"
      />
    </q-form>
  </AuthShell>
</template>
