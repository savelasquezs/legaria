<script setup lang="ts">
defineProps<{
  title: string
  description: string
}>()
</script>

<template>
  <div class="auth-page flex flex-center">
    <main class="auth-wrap q-py-xl">
      <q-card class="auth-card">
        <q-card-section class="q-pa-xl">
          <div class="brand-mark" aria-label="Legaria">
            <span class="brand-dot" aria-hidden="true" />
            LEGARIA
          </div>
          <h1 class="auth-title">{{ title }}</h1>
          <p class="auth-copy">{{ description }}</p>
          <div class="q-mt-lg">
            <slot />
          </div>
        </q-card-section>
      </q-card>
      <p class="text-center text-grey-7 text-caption q-mt-md">
        Acceso protegido para la gestión segura de tu organización.
      </p>
    </main>
  </div>
</template>
