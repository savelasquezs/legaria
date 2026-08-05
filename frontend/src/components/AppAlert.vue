<script setup lang="ts">
import { computed } from 'vue'
import { icons } from '../design-system/icons'
import type { StatusTone } from './StatusChip.vue'

const props = withDefaults(defineProps<{ tone?: Exclude<StatusTone, 'neutral'>; title?: string; icon?: string }>(), {
  tone: 'info', title: '', icon: '',
})
const defaultIcons = { success: icons.check, warning: icons.warning, info: icons.info, danger: icons.error }
const alertIcon = computed(() => props.icon || defaultIcons[props.tone])
const role = computed(() => props.tone === 'danger' ? 'alert' : 'status')
</script>

<template>
  <div class="app-alert" :class="`app-alert--${tone}`" :role="role">
    <q-icon :name="alertIcon" class="app-alert__icon" />
    <div class="app-alert__content"><strong v-if="title">{{ title }}</strong><div><slot /></div></div>
    <div v-if="$slots.action" class="app-alert__action"><slot name="action" /></div>
  </div>
</template>
