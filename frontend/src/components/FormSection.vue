<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  icon?: string
}>()
</script>

<template>
  <q-card flat bordered class="form-section">
    <q-card-section class="form-section__header">
      <q-icon v-if="icon" :name="icon" class="form-section__icon" />
      <div>
        <h2>{{ title }}</h2>
        <p v-if="description">{{ description }}</p>
      </div>
    </q-card-section>
    <q-card-section class="form-section__content"><slot /></q-card-section>
    <q-card-actions v-if="$slots.actions" class="form-section__actions"><slot name="actions" /></q-card-actions>
  </q-card>
</template>
