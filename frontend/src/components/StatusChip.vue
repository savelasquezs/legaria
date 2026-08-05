<script setup lang="ts">
export type StatusTone = 'neutral' | 'success' | 'warning' | 'info' | 'danger'

withDefaults(defineProps<{ tone?: StatusTone; label?: string; icon?: string }>(), {
  tone: 'neutral', label: '', icon: '',
})
</script>

<template>
  <q-chip dense :icon="icon || undefined" class="status-chip" :class="`status-chip--${tone}`">
    {{ label }}<slot />
  </q-chip>
</template>
