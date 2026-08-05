<script setup lang="ts">
import { icons } from '../design-system/icons'

withDefaults(defineProps<{
  title: string
  description?: string
  context?: string
  backTo?: string
  backLabel?: string
}>(), {
  description: '',
  context: '',
  backTo: '',
  backLabel: 'Volver',
})
</script>

<template>
  <header class="page-header">
    <q-btn v-if="backTo" flat no-caps :icon="icons.arrowBack" :label="backLabel" :to="backTo" class="page-header__back" />
    <div class="page-header__row">
      <div class="page-header__copy">
        <p v-if="context" class="eyebrow">{{ context }}</p>
        <h1>{{ title }}</h1>
        <p v-if="description" class="page-header__description">{{ description }}</p>
      </div>
      <div v-if="$slots.actions" class="page-header__actions"><slot name="actions" /></div>
    </div>
  </header>
</template>
