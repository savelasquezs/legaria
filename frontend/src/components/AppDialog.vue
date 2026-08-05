<script setup lang="ts">
import { computed } from 'vue'
import { icons } from '../design-system/icons'

type DialogTone = 'neutral' | 'success' | 'warning' | 'info' | 'danger'

const props = withDefaults(defineProps<{
  modelValue: boolean
  title: string
  description?: string
  icon?: string
  size?: 'sm' | 'md' | 'lg'
  tone?: DialogTone
  loading?: boolean
  persistent?: boolean
  showClose?: boolean
}>(), {
  description: '',
  icon: '',
  size: 'md',
  tone: 'neutral',
  loading: false,
  persistent: false,
  showClose: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const locked = computed(() => props.persistent || props.loading)
</script>

<template>
  <q-dialog
    :model-value="modelValue"
    :persistent="locked"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <q-card class="app-dialog" :class="[`app-dialog--${size}`, `app-dialog--${tone}`]" :aria-busy="loading || undefined">
      <q-card-section class="app-dialog__heading">
        <q-icon v-if="icon" :name="icon" class="app-dialog__icon" />
        <div class="app-dialog__title">
          <h2>{{ title }}</h2>
          <p v-if="description">{{ description }}</p>
        </div>
        <q-btn
          v-if="showClose"
          flat
          round
          dense
          :icon="icons.close"
          aria-label="Cerrar"
          :disable="locked"
          @click="emit('update:modelValue', false)"
        >
          <q-tooltip>Cerrar</q-tooltip>
        </q-btn>
      </q-card-section>
      <q-card-section class="app-dialog__content">
        <div v-if="loading" class="app-dialog__loading" role="status">
          <q-spinner color="primary" size="24px" />
          <span>Procesando…</span>
        </div>
        <slot />
      </q-card-section>
      <q-card-actions class="app-dialog__actions" align="right">
        <slot name="actions" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>
