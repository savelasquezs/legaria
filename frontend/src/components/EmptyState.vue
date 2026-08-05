<script setup lang="ts">
import { icons } from '../design-system/icons'

withDefaults(defineProps<{ title: string; description?: string; icon?: string }>(), {
  description: '', icon: icons.search,
})
</script>

<template>
  <div class="empty-state" role="status">
    <q-icon :name="icon" class="empty-state__icon" />
    <h3>{{ title }}</h3>
    <p v-if="description">{{ description }}</p>
    <div v-if="$slots.action" class="empty-state__action"><slot name="action" /></div>
  </div>
</template>
