<script setup lang="ts">
import { computed } from 'vue'
import { icons } from '../design-system/icons'
import AppDialog from './AppDialog.vue'

type DialogTone = 'information' | 'acceptance' | 'confirmation' | 'danger'

const props = withDefaults(defineProps<{
  modelValue: boolean
  title: string
  message: string
  tone?: DialogTone
  confirmLabel?: string
  cancelLabel?: string
  showCancel?: boolean
  loading?: boolean
}>(), {
  tone: 'confirmation',
  confirmLabel: 'Aceptar',
  cancelLabel: 'Cancelar',
  showCancel: true,
  loading: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  cancel: []
}>()

const appearance = computed(() => ({
  information: { icon: icons.info, color: 'primary', tone: 'info' as const },
  acceptance: { icon: icons.taskAlt, color: 'positive', tone: 'success' as const },
  confirmation: { icon: icons.help, color: 'primary', tone: 'info' as const },
  danger: { icon: icons.warning, color: 'negative', tone: 'danger' as const },
})[props.tone])

function cancel(): void {
  emit('update:modelValue', false)
  emit('cancel')
}
</script>

<template>
  <AppDialog
    :model-value="modelValue"
    :title="title"
    :icon="appearance.icon"
    :tone="appearance.tone"
    :loading="loading"
    :show-close="false"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <p class="confirm-dialog__message">{{ message }}</p>
    <template #actions>
      <q-btn
        v-if="showCancel"
        flat
        no-caps
        :label="cancelLabel"
        :disable="loading"
        @click="cancel"
      />
      <q-btn
        unelevated
        no-caps
        :color="appearance.color"
        :label="confirmLabel"
        :loading="loading"
        @click="emit('confirm')"
      />
    </template>
  </AppDialog>
</template>
