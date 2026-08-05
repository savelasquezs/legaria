<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { icons } from '../design-system/icons'
import { useAuthStore } from '../stores/auth'

export interface NavigationItem {
  label: string
  icon: string
  to: string
  activeMatch?: string
}

defineProps<{
  homeTo: string
  contextLabel: string
  roleLabel: string
  navigation: NavigationItem[]
}>()

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const drawerOpen = ref(false)
const loggingOut = ref(false)
const userName = computed(() => `${auth.account?.firstName ?? ''} ${auth.account?.lastName ?? ''}`.trim())
const initials = computed(() => `${auth.account?.firstName?.[0] ?? ''}${auth.account?.lastName?.[0] ?? ''}`.toUpperCase())

function isActive(item: NavigationItem): boolean {
  return route.path.startsWith(item.activeMatch ?? item.to)
}

async function logout(): Promise<void> {
  loggingOut.value = true
  await auth.logout()
  await router.replace('/')
}
</script>

<template>
  <q-layout view="hHh Lpr lFf" class="app-shell">
    <q-header class="app-shell__header">
      <q-toolbar class="app-shell__toolbar">
        <q-btn flat round dense :icon="icons.menu" aria-label="Abrir navegación" class="app-shell__menu" @click="drawerOpen = !drawerOpen"><q-tooltip>Abrir navegación</q-tooltip></q-btn>
        <router-link class="app-brand app-shell__mobile-brand" :to="homeTo" aria-label="Legaria, inicio">
          <span class="app-brand__mark" aria-hidden="true">L</span>
          <span>LEGARIA</span>
        </router-link>
        <q-space />
        <div class="app-shell__header-context">{{ contextLabel }}</div>
      </q-toolbar>
    </q-header>

    <q-drawer v-model="drawerOpen" show-if-above :width="224" bordered class="app-shell__drawer">
      <div class="app-shell__sidebar">
        <router-link class="app-brand" :to="homeTo" aria-label="Legaria, inicio">
          <span class="app-brand__mark" aria-hidden="true">L</span>
          <span>LEGARIA</span>
        </router-link>
        <div class="app-shell__context">{{ contextLabel }}</div>
        <nav class="app-shell__nav" aria-label="Navegación principal">
          <q-list>
            <q-item
              v-for="item in navigation"
              :key="item.to"
              clickable
              :to="item.to"
              :active="isActive(item)"
              active-class="app-shell__nav-item--active"
              @click="drawerOpen = false"
            >
              <q-item-section avatar><q-icon :name="item.icon" /></q-item-section>
              <q-item-section>{{ item.label }}</q-item-section>
            </q-item>
          </q-list>
        </nav>
        <div class="app-shell__account">
          <div class="app-shell__avatar" aria-hidden="true">{{ initials }}</div>
          <div class="app-shell__identity">
            <strong>{{ userName }}</strong>
            <span>{{ roleLabel }}</span>
          </div>
          <q-btn flat round dense :icon="icons.logout" aria-label="Cerrar sesión" :loading="loggingOut" @click="logout">
            <q-tooltip>Cerrar sesión</q-tooltip>
          </q-btn>
        </div>
      </div>
    </q-drawer>

    <q-page-container>
      <q-page class="app-shell__page">
        <slot />
      </q-page>
    </q-page-container>
  </q-layout>
</template>
