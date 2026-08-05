<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppAlert from '../components/AppAlert.vue'
import PageHeader from '../components/PageHeader.vue'
import PlatformLayout from '../components/PlatformLayout.vue'
import TenantLayout from '../components/TenantLayout.vue'
import { icons } from '../design-system/icons'
import { useAuthStore } from '../stores/auth'

const props = defineProps<{ platform: boolean }>()
const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const title = computed(() => props.platform ? 'Sesión iniciada como propietario' : 'Sesión tenant iniciada')
const layout = computed(() => props.platform ? PlatformLayout : TenantLayout)

async function closeAllSessions(): Promise<void> {
  loading.value = true
  await auth.logoutAll()
  await router.replace('/')
}
</script>

<template>
  <component :is="layout">
    <div class="platform-content form-content">
      <PageHeader :title="title" description="La autenticación segura de Legaria está activa.">
        <template #actions>
          <q-btn outline no-caps color="negative" :icon="icons.logout" label="Cerrar todas las sesiones" :loading="loading" @click="closeAllSessions" />
        </template>
      </PageHeader>
      <q-card class="session-card">
        <q-card-section>
          <q-icon :name="icons.verified" color="positive" size="48px" />
          <p class="auth-copy">Hola, {{ auth.account?.firstName }}.</p>
          <q-separator class="q-my-lg" />
          <dl class="session-details">
            <div><dt>Correo</dt><dd>{{ auth.account?.email }}</dd></div>
            <div><dt>Tipo de cuenta</dt><dd>{{ auth.account?.accountType }}</dd></div>
          </dl>
          <AppAlert tone="info">Los módulos administrativos se incorporarán en incrementos posteriores.</AppAlert>
        </q-card-section>
      </q-card>
    </div>
  </component>
</template>
