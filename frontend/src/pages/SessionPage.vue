<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const props = defineProps<{
  platform: boolean
}>()

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const title = computed(() =>
  props.platform ? 'Sesión iniciada como propietario' : 'Sesión tenant iniciada',
)

async function closeAllSessions(): Promise<void> {
  loading.value = true
  await auth.logoutAll()
  await router.replace('/')
}
</script>

<template>
  <q-layout view="hHh lpR fFf" class="session-page">
    <q-header class="session-toolbar">
      <q-toolbar class="q-px-lg">
        <div class="brand-mark text-white">
          <span class="brand-dot" aria-hidden="true" />
          LEGARIA
        </div>
        <q-space />
        <q-btn
          flat
          no-caps
          icon="logout"
          label="Cerrar todas las sesiones"
          :loading="loading"
          @click="closeAllSessions"
        />
      </q-toolbar>
    </q-header>
    <q-page-container>
      <q-page class="flex flex-center q-pa-md">
        <q-card class="session-card">
          <q-card-section class="q-pa-xl">
            <q-icon name="verified_user" color="positive" size="48px" />
            <h1 class="auth-title">{{ title }}</h1>
            <p class="auth-copy">
              Hola, {{ auth.account?.firstName }}. La autenticación segura de Legaria está activa.
            </p>
            <q-separator class="q-my-lg" />
            <dl class="row q-col-gutter-md q-mb-none">
              <div class="col-12 col-sm-6">
                <dt class="text-caption text-grey-7">Correo</dt>
                <dd class="q-ml-none text-weight-medium">{{ auth.account?.email }}</dd>
              </div>
              <div class="col-12 col-sm-6">
                <dt class="text-caption text-grey-7">Tipo de cuenta</dt>
                <dd class="q-ml-none text-weight-medium">{{ auth.account?.accountType }}</dd>
              </div>
            </dl>
            <q-banner class="bg-blue-1 text-primary rounded-borders q-mt-lg">
              Los módulos administrativos se incorporarán en incrementos posteriores.
            </q-banner>
          </q-card-section>
        </q-card>
      </q-page>
    </q-page-container>
  </q-layout>
</template>
