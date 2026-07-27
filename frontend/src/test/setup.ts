import { config } from '@vue/test-utils'

const container = { template: '<div><slot /></div>' }

config.global.stubs = {
  QPage: container,
  QCard: container,
  QCardSection: container,
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
  QBtn: {
    props: ['label', 'disable', 'type'],
    emits: ['click'],
    template:
      '<button :type="type || \'button\'" :disabled="disable" @click="$emit(\'click\', $event)">' +
      '{{ label }}<slot /></button>',
  },
  QIcon: true,
  QSpinner: true,
  QSeparator: true,
  QSpace: true,
}

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub
