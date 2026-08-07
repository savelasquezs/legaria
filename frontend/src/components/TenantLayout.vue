<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Notify } from 'quasar'
import AppShell, { type NavigationItem } from './AppShell.vue'
import AppDialog from './AppDialog.vue'
import { icons } from '../design-system/icons'
import { useAuthStore } from '../stores/auth'
import { getMyNotificationContact, updateMyNotificationContact } from '../services/notifications'

const auth = useAuthStore()
const contactDialog = ref(false)
const contactLoading = ref(false)
const mobilePhone = ref('')
const whatsAppConsent = ref(false)
const roleLabel = computed(() => auth.account?.roles.includes('SUPER_ADMIN') ? 'Superadministrador' : 'Administrador de sucursal')
const navigation = computed<NavigationItem[]>(() => [
  { label: 'Sucursales', icon: icons.storefront, to: '/app/branches' },
  { label: 'Documentos', icon: icons.description, to: '/app/document-catalog' },
  ...(auth.account?.roles.includes('SUPER_ADMIN')
    ? [
        { label: 'Cargos', icon: icons.groups, to: '/app/job-positions' },
        { label: 'Notificaciones', icon: icons.notifications, to: '/app/notifications' },
      ]
    : []),
])

async function loadContact(): Promise<void> {
  try {
    const contact = await getMyNotificationContact()
    mobilePhone.value = contact.mobilePhone ?? ''
    whatsAppConsent.value = Boolean(contact.whatsAppConsentAt)
  } catch {
    mobilePhone.value = ''
    whatsAppConsent.value = false
  }
}

async function saveContact(): Promise<void> {
  contactLoading.value = true
  try {
    await updateMyNotificationContact({ mobilePhone: mobilePhone.value.trim() || null, whatsAppConsent: whatsAppConsent.value })
    contactDialog.value = false
    Notify.create({ type: 'positive', message: 'Contacto de notificaciones actualizado.' })
  } catch {
    Notify.create({ type: 'negative', message: 'No fue posible actualizar el contacto.' })
  } finally { contactLoading.value = false }
}

onMounted(loadContact)
</script>

<template>
  <AppShell home-to="/app/branches" context-label="Organización" :role-label="roleLabel" :navigation="navigation" notification-contact @notification-contact="contactDialog = true">
    <slot />
  </AppShell>
  <AppDialog v-model="contactDialog" title="Mi WhatsApp" description="Este contacto se usa para notificaciones de la organización." :loading="contactLoading">
    <q-input v-model="mobilePhone" dense outlined label="Teléfono móvil" hint="Formato +573001234567" />
    <q-checkbox v-model="whatsAppConsent" label="Autorizo notificaciones por WhatsApp" />
    <template #actions><q-btn flat no-caps label="Cancelar" @click="contactDialog = false" /><q-btn unelevated no-caps color="primary" label="Guardar" :loading="contactLoading" @click="saveContact" /></template>
  </AppDialog>
</template>
