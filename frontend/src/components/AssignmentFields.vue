<script setup lang="ts">
import type { Branch } from '../types/branches'
import type { JobPosition } from '../types/employees'

withDefaults(defineProps<{
  positions: JobPosition[]
  branches?: Branch[]
  showBranch?: boolean
  showPrimary?: boolean
  dateLabel?: string
  maxDate?: string
}>(), {
  branches: () => [],
  showBranch: false,
  showPrimary: true,
  dateLabel: 'Fecha de inicio',
  maxDate: undefined,
})

const branchId = defineModel<string>('branchId', { default: '' })
const jobPositionId = defineModel<string>('jobPositionId', { required: true })
const startedOn = defineModel<string>('startedOn', { required: true })
const isPrimary = defineModel<boolean>('isPrimary', { default: false })
const requiredRule = (value: string) => Boolean(value?.trim()) || 'Este campo es obligatorio.'
</script>

<template>
  <div class="fields-grid">
    <q-select v-if="showBranch" v-model="branchId" outlined emit-value map-options option-value="id" option-label="name" :options="branches" label="Sucursal" :rules="[requiredRule]" />
    <q-select v-model="jobPositionId" outlined emit-value map-options option-value="id" option-label="name" :options="positions" label="Cargo" :rules="[requiredRule]" />
    <q-input v-model="startedOn" outlined type="date" :max="maxDate" :label="dateLabel" :rules="[requiredRule]" />
    <q-checkbox v-if="showPrimary" v-model="isPrimary" label="Asignación principal" />
  </div>
</template>
