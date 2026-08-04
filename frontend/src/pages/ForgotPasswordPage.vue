<script setup lang="ts">
import { computed, ref } from 'vue'
import AuthShell from '../components/AuthShell.vue'
import { api } from '../services/api'

const email = ref('')
const loading = ref(false)
const sent = ref(false)
const errorMessage = ref('')
const emailIsValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value))

async function submit(): Promise<void> {
  if (!emailIsValid.value || loading.value) return
  loading.value = true
  errorMessage.value = ''
  try {
    await api.post('/api/auth/forgot-password', { email: email.value.trim() })
    sent.value = true
  } catch {
    errorMessage.value = 'No fue posible enviar la solicitud. Revisa tu conexión e inténtalo de nuevo.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AuthShell
    title="Recupera tu acceso"
    description="Te enviaremos un enlace seguro para crear una nueva contraseña."
  >
    <div v-if="sent">
      <q-banner class="bg-green-1 text-positive rounded-borders" role="status">
        Si existe una cuenta asociada al correo, recibirás las instrucciones.
      </q-banner>
      <router-link class="auth-link block text-center q-mt-lg" to="/">
        Volver al inicio de sesión
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
      <q-btn
        unelevated
        no-caps
        size="lg"
        color="primary"
        class="auth-submit full-width"
        label="Enviar instrucciones"
        type="submit"
        :loading="loading"
        :disable="!emailIsValid || loading"
      />
      <router-link class="auth-link block text-center" to="/">
        Volver al inicio de sesión
      </router-link>
    </q-form>
  </AuthShell>
</template>
