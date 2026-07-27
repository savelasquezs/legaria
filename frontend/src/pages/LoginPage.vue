<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import AuthShell from '../components/AuthShell.vue'
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
    <q-banner
      v-if="errorMessage"
      class="bg-red-1 text-negative q-mb-md rounded-borders"
      role="alert"
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
    </q-banner>
    <q-banner
      v-if="verificationSent"
      class="bg-green-1 text-positive q-mb-md rounded-borders"
      role="status"
    >
      Si la cuenta requiere verificación, recibirás un nuevo enlace.
    </q-banner>

    <q-form class="q-gutter-md" @submit.prevent="submit">
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
        <template #prepend><q-icon name="mail_outline" /></template>
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
        <template #prepend><q-icon name="lock_outline" /></template>
        <template #append>
          <q-btn
            flat
            round
            dense
            :icon="showPassword ? 'visibility_off' : 'visibility'"
            :aria-label="showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'"
            @click="showPassword = !showPassword"
          />
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
        class="full-width"
        label="Ingresar"
        type="submit"
        :loading="loading"
        :disable="!formIsValid || loading"
      />
    </q-form>
  </AuthShell>
</template>
