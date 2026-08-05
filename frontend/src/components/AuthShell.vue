<script setup lang="ts">
defineProps<{
  title: string
  description: string
}>()
</script>

<template>
  <div class="auth-page flex flex-center">
    <main class="auth-wrap">
      <q-card class="auth-card">
        <q-card-section class="auth-card__content">
          <div class="app-brand auth-brand" aria-label="Legaria">
            <span class="app-brand__mark" aria-hidden="true">L</span>
            <span>LEGARIA</span>
          </div>
          <h1 class="auth-title">{{ title }}</h1>
          <p class="auth-copy">{{ description }}</p>
          <div class="q-mt-lg">
            <slot />
          </div>
        </q-card-section>
      </q-card>
      <p class="auth-security-copy">
        Acceso protegido para la gestión segura de tu organización.
      </p>
    </main>
  </div>
</template>
