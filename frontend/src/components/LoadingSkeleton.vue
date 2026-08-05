<script setup lang="ts">
withDefaults(defineProps<{ rows?: number; variant?: 'table' | 'form' | 'card' }>(), { rows: 5, variant: 'table' })
</script>

<template>
  <div class="loading-skeleton" :class="`loading-skeleton--${variant}`" role="status" aria-label="Cargando">
    <q-skeleton v-for="row in rows" :key="row" :type="variant === 'form' ? 'QInput' : 'rect'" />
  </div>
</template>
