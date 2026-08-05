<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const loggingOut = ref(false)
const isSuperAdmin = computed(() => auth.account?.roles.includes('SUPER_ADMIN') === true)

async function logout(): Promise<void> {
  loggingOut.value = true
  await auth.logout()
  await router.replace('/')
}
</script>

<template>
  <q-layout view="hHh lpR fFf" class="tenant-layout">
    <q-header class="tenant-header">
      <q-toolbar class="tenant-toolbar">
        <router-link class="platform-brand" to="/app/branches">
          <span class="brand-dot" aria-hidden="true" />
          LEGARIA
        </router-link>
        <nav class="tenant-nav" aria-label="Navegación tenant">
          <q-btn
            flat
            no-caps
            icon="storefront"
            label="Sucursales"
            :class="{ active: route.path.startsWith('/app/branches') }"
            to="/app/branches"
          />
          <q-btn
            v-if="isSuperAdmin"
            flat
            no-caps
            icon="manage_accounts"
            label="Administradores"
            :class="{ active: route.path.startsWith('/app/administrators') }"
            to="/app/administrators"
          />
        </nav>
        <q-space />
        <div class="platform-user gt-sm">
          <span>{{ auth.account?.firstName }} {{ auth.account?.lastName }}</span>
          <small>{{ isSuperAdmin ? 'Superadministrador' : 'Administrador de sucursal' }}</small>
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
