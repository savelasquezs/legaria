<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import AuthShell from '../components/AuthShell.vue'
import AppAlert from '../components/AppAlert.vue'
import { api, getProblem } from '../services/api'

type VerificationState = 'loading' | 'verified' | 'invalid' | 'expired' | 'error'

const route = useRoute()
const state = ref<VerificationState>('loading')
const message = ref('')

onMounted(async () => {
  const token = typeof route.query.token === 'string' ? route.query.token : ''
  if (!token) {
    state.value = 'invalid'
    message.value = 'El enlace no contiene un token válido.'
    return
  }

  try {
    await api.post('/api/auth/verify-email', { token })
    state.value = 'verified'
  } catch (error) {
    const problem = getProblem(error)
    if (problem?.code === 'auth.token_expired') {
      state.value = 'expired'
      message.value = 'El enlace de verificación venció.'
    } else if (problem?.code === 'auth.token_invalid' || problem?.code === 'auth.token_used') {
      state.value = 'invalid'
      message.value = 'El enlace es inválido o ya fue utilizado.'
    } else {
      state.value = 'error'
      message.value = 'No pudimos verificar el correo. Revisa tu conexión e inténtalo de nuevo.'
    }
  }
})
</script>

<template>
  <AuthShell
    title="Verificación de correo"
    description="Estamos validando el enlace seguro de tu cuenta."
  >
    <div v-if="state === 'loading'" class="text-center q-py-lg" role="status">
      <q-spinner color="primary" size="44px" />
      <p class="loading-copy">Verificando correo…</p>
    </div>
    <div v-else-if="state === 'verified'">
      <AppAlert tone="success">
        Tu correo quedó verificado. Ya puedes iniciar sesión.
      </AppAlert>
      <router-link class="auth-link block text-center q-mt-lg" to="/">
        Iniciar sesión
      </router-link>
    </div>
    <div v-else>
      <AppAlert tone="danger">
        {{ message }}
      </AppAlert>
      <router-link class="auth-link block text-center q-mt-lg" to="/">
        Volver al inicio de sesión
      </router-link>
    </div>
  </AuthShell>
</template>
