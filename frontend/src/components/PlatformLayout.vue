<script setup lang="ts">
import AppShell, { type NavigationItem } from './AppShell.vue'
import { icons } from '../design-system/icons'

const navigation: NavigationItem[] = [
  { label: 'Organizaciones', icon: icons.apartment, to: '/platform', activeMatch: '/platform' },
]
</script>

<template>
  <AppShell home-to="/platform" context-label="Plataforma" role-label="Propietario de plataforma" :navigation="navigation">
    <slot />
  </AppShell>
</template>
