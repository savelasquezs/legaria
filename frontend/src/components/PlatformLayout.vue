<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const loggingOut = ref(false)

async function logout(): Promise<void> {
  loggingOut.value = true
  await auth.logout()
  await router.replace('/')
}
</script>

<template>
  <q-layout view="hHh lpR fFf" class="platform-layout">
    <q-header class="platform-header">
      <q-toolbar class="platform-toolbar">
        <router-link class="platform-brand" to="/platform">
          <span class="brand-dot" aria-hidden="true" />
          LEGARIA
        </router-link>
        <q-space />
        <div class="platform-user gt-xs">
          <span>{{ auth.account?.firstName }} {{ auth.account?.lastName }}</span>
          <small>{{ auth.account?.roles.join(' · ') }}</small>
        </div>
        <q-btn
          flat
          round
          icon="logout"
          aria-label="Cerrar sesión"
          :loading="loggingOut"
          @click="logout"
        />
      </q-toolbar>
    </q-header>
    <q-page-container>
      <q-page class="platform-page">
        <slot />
      </q-page>
    </q-page-container>
  </q-layout>
</template>
