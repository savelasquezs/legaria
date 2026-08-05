import { config } from '@vue/test-utils'

const container = { template: '<div><slot /></div>' }

config.global.stubs = {
  QPage: container,
  QCard: container,
  QCardSection: container,
  QCardActions: container,
  QLayout: container,
  QHeader: container,
  QToolbar: container,
  QPageContainer: container,
  QBanner: {
    template: '<div><slot /><slot name="action" /></div>',
  },
  QForm: {
    emits: ['submit'],
    template: '<form @submit="$emit(\'submit\', $event)"><slot /></form>',
  },
  QInput: {
    props: ['modelValue', 'type', 'label'],
    emits: ['update:modelValue'],
    template:
      '<label>{{ label }}<input :type="type" :value="modelValue" ' +
      '@input="$emit(\'update:modelValue\', $event.target.value)" />' +
      '<slot name="prepend" /><slot name="append" /></label>',
  },
  QSelect: {
    props: ['modelValue', 'label', 'options'],
    emits: ['update:modelValue'],
    template: '<label>{{ label }}<select><option v-for="option in options" :key="option.value || option.id || option.code">{{ option.label || option.name }}</option></select></label>',
  },
  QBtn: {
    props: ['label', 'disable', 'type'],
    emits: ['click'],
    template:
      '<button :type="type || \'button\'" :disabled="disable" @click="$emit(\'click\', $event)">' +
      '{{ label }}<slot /></button>',
  },
  QIcon: true,
  QTooltip: container,
  QSpinner: true,
  QSkeleton: container,
  QSeparator: true,
  QSpace: true,
  QChip: container,
  QCheckbox: {
    props: ['modelValue', 'label'],
    emits: ['update:modelValue'],
    template: '<label><input type="checkbox" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" />{{ label }}</label>',
  },
  QTd: container,
  QPagination: true,
  QDialog: container,
  QDrawer: container,
  QList: container,
  QItem: container,
  QItemSection: container,
  QTable: {
    props: ['rows', 'noDataLabel'],
    template: '<div>{{ rows.length === 0 ? noDataLabel : "" }}</div>',
  },
}

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub
