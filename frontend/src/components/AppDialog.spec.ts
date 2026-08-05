import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import AppDialog from './AppDialog.vue'
import ConfirmDialog from './ConfirmDialog.vue'

describe('reusable dialogs', () => {
  it('renders shared content and emits close from the accessible action', async () => {
    const wrapper = mount(AppDialog, {
      props: {
        modelValue: true,
        title: 'Información',
        description: 'Detalle importante',
      },
      slots: { default: '<p>Contenido</p>' },
    })

    expect(wrapper.text()).toContain('Información')
    expect(wrapper.text()).toContain('Detalle importante')
    expect(wrapper.text()).toContain('Contenido')
    await wrapper.get('[aria-label="Cerrar"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')).toEqual([[false]])
  })

  it('supports cancellation and confirmation with semantic labels', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        modelValue: true,
        title: 'Suspender cuenta',
        message: 'Las sesiones serán revocadas.',
        tone: 'danger',
        confirmLabel: 'Suspender',
        cancelLabel: 'Volver',
      },
    })
    const buttons = wrapper.findAll('button')

    await buttons.find((button) => button.text() === 'Volver')!.trigger('click')
    expect(wrapper.emitted('cancel')).toHaveLength(1)
    expect(wrapper.emitted('update:modelValue')).toContainEqual([false])

    await buttons.find((button) => button.text() === 'Suspender')!.trigger('click')
    expect(wrapper.emitted('confirm')).toHaveLength(1)
  })

  it('locks safe closing and exposes progress while loading', () => {
    const wrapper = mount(AppDialog, {
      props: { modelValue: true, title: 'Guardando', size: 'lg', tone: 'danger', loading: true },
    })

    expect(wrapper.find('.app-dialog--lg').exists()).toBe(true)
    expect(wrapper.find('.app-dialog--danger').exists()).toBe(true)
    expect(wrapper.text()).toContain('Procesando…')
    expect(wrapper.get('[aria-label="Cerrar"]').attributes('disabled')).toBeDefined()
  })
})
