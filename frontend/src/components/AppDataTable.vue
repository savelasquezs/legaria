<script setup lang="ts">
import type { QTableColumn } from 'quasar'
import EmptyState from './EmptyState.vue'

withDefaults(defineProps<{
  rows: readonly Record<string, unknown>[]
  columns: QTableColumn[]
  rowKey?: string
  loading?: boolean
  page?: number
  totalPages?: number
  emptyTitle?: string
  emptyDescription?: string
}>(), {
  rowKey: 'id', loading: false, page: 1, totalPages: 1,
  emptyTitle: 'No hay resultados', emptyDescription: 'Ajusta la búsqueda o crea el primer registro.',
})

const emit = defineEmits<{ 'update:page': [page: number] }>()
</script>

<template>
  <div class="app-data-table">
    <q-table flat :rows="rows" :columns="columns" :row-key="rowKey" :loading="loading" hide-pagination :rows-per-page-options="[0]">
      <template v-for="(_, slotName) in $slots" #[slotName]="slotProps">
        <slot :name="slotName" v-bind="slotProps || {}" />
      </template>
      <template #no-data>
        <EmptyState :title="emptyTitle" :description="emptyDescription" />
      </template>
    </q-table>
    <div v-if="totalPages > 1" class="pagination-row">
      <q-pagination :model-value="page" :max="totalPages" boundary-numbers @update:model-value="emit('update:page', $event)" />
    </div>
  </div>
</template>
