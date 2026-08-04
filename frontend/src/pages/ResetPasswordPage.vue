<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import AuthShell from '../components/AuthShell.vue'
import { api, getProblem } from '../services/api'

const route = useRoute()
const token = typeof route.query.token === 'string' ? route.query.token : ''
const password = ref('')
const confirmation = ref('')
const showPassword = ref(false)
const loading = ref(false)
const updated = ref(false)
const errorMessage = ref(token ? '' : 'El enlace no contiene un token válido.')
const formIsValid = computed(
  () =>
    Boolean(token) &&
    password.value.length >= 8 &&
    password.value.length <= 128 &&
    password.value === confirmation.value,
)

async function submit(): Promise<void> {
  if (!formIsValid.value || loading.value) return
  loading.value = true
  errorMessage.value = ''
  try {
    await api.post('/api/auth/reset-password', {
      token,
      newPassword: password.value,
    })
    updated.value = true
    password.value = ''
    confirmation.value = ''
  } catch (error) {
    const problem = getProblem(error)
    errorMessage.value =
      problem?.code === 'auth.token_expired'
        ? 'El enlace venció. Solicita uno nuevo.'
        : problem?.code === 'auth.token_used'
          ? 'El enlace ya fue utilizado o reemplazado.'
          : problem?.detail ?? 'No fue posible actualizar la contraseña.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AuthShell
    title="Crea una nueva contraseña"
    description="Usa entre 8 y 128 caracteres y evita reutilizar una contraseña anterior."
  >
    <div v-if="updated">
      <q-banner class="bg-green-1 text-positive rounded-borders" role="status">
        Tu contraseña fue actualizada. Todas las sesiones anteriores quedaron cerradas.
      </q-banner>
      <router-link class="auth-link block text-center q-mt-lg" to="/">
        Iniciar sesión
      </router-link>
    </div>
    <q-form v-else class="auth-form" @submit.prevent="submit">
      <q-banner
        v-if="errorMessage"
        class="bg-red-1 text-negative rounded-borders"
        role="alert"
      >
        {{ errorMessage }}
      </q-banner>
      <q-input
        v-model="password"
        outlined
        :type="showPassword ? 'text' : 'password'"
        label="Nueva contraseña"
        autocomplete="new-password"
        :rules="[
          (value: string) => value.length >= 8 || 'Usa al menos 8 caracteres.',
          (value: string) => value.length <= 128 || 'Usa máximo 128 caracteres.',
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
      <q-input
        v-model="confirmation"
        outlined
        :type="showPassword ? 'text' : 'password'"
        label="Confirma la contraseña"
        autocomplete="new-password"
        :rules="[
          (value: string) => value === password || 'Las contraseñas no coinciden.',
        ]"
        lazy-rules
      />
      <q-btn
        unelevated
        no-caps
        size="lg"
        color="primary"
        class="auth-submit full-width"
        label="Actualizar contraseña"
        type="submit"
        :loading="loading"
        :disable="!formIsValid || loading"
      />
      <router-link class="auth-link block text-center" to="/">
        Volver al inicio de sesión
      </router-link>
    </q-form>
  </AuthShell>
</template>
