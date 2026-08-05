<script setup lang="ts">
import { computed } from 'vue'
import AppShell, { type NavigationItem } from './AppShell.vue'
import { icons } from '../design-system/icons'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const roleLabel = computed(() => auth.account?.roles.includes('SUPER_ADMIN') ? 'Superadministrador' : 'Administrador de sucursal')
const navigation: NavigationItem[] = [
  { label: 'Sucursales', icon: icons.storefront, to: '/app/branches' },
]
</script>

<template>
  <AppShell home-to="/app/branches" context-label="Organización" :role-label="roleLabel" :navigation="navigation">
    <slot />
  </AppShell>
</template>
