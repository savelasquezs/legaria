<script setup lang="ts">
import { computed } from 'vue'
import { icons } from '../design-system/icons'

const props = withDefaults(defineProps<{
  modelValue: string
  label?: string
  placeholder?: string
  debounce?: number
  loading?: boolean
}>(), {
  label: 'Buscar',
  placeholder: '',
  debounce: 350,
  loading: false,
})

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const value = computed({ get: () => props.modelValue, set: (next: string | null) => emit('update:modelValue', next ?? '') })
</script>

<template>
  <q-input v-model="value" outlined dense clearable :debounce="debounce" :label="label" :placeholder="placeholder" :loading="loading" class="search-field">
    <template #prepend><q-icon :name="icons.search" /></template>
  </q-input>
</template>
