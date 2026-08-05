import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { extname, join, resolve } from 'node:path'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import AppAlert from '../components/AppAlert.vue'
import SearchField from '../components/SearchField.vue'
import StatusChip from '../components/StatusChip.vue'

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    return entry.isDirectory() ? sourceFiles(path) : [path]
  })
}

describe('design system contract', () => {
  it('keeps visual colors centralized in the token source', () => {
    const sourceRoot = resolve(__dirname, '..')
    const violations = sourceFiles(sourceRoot)
      .filter((file) => ['.vue', '.scss', '.css'].includes(extname(file)))
      .filter((file) => !file.endsWith('_tokens.scss'))
      .filter((file) => /#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(/i.test(readFileSync(file, 'utf8')))

    expect(violations).toEqual([])
  })

  it('uses only the SVG Material Symbols integration', () => {
    const main = readFileSync(resolve(__dirname, '../main.ts'), 'utf8')
    expect(main).toContain('svg-material-symbols-rounded')
    expect(main).not.toContain('material-symbols-rounded.css')
    expect(existsSync(resolve(__dirname, 'icons.ts'))).toBe(true)
  })

  it('rejects isolated palette utilities and inline component styles', () => {
    const sourceRoot = resolve(__dirname, '..')
    const violations = sourceFiles(sourceRoot)
      .filter((file) => extname(file) === '.vue')
      .filter((file) => /\b(?:bg|text)-(?:red|green|blue|orange|grey)-\d+\b|\sstyle\s*=/i.test(readFileSync(file, 'utf8')))

    expect(violations).toEqual([])
  })
})

describe('semantic components', () => {
  it('exposes alerts and statuses without relying only on color', () => {
    const alert = mount(AppAlert, { props: { tone: 'danger', title: 'Error' }, slots: { default: 'No fue posible guardar.' } })
    const chip = mount(StatusChip, { props: { tone: 'success', label: 'Activa' } })

    expect(alert.attributes('role')).toBe('alert')
    expect(alert.text()).toContain('No fue posible guardar.')
    expect(chip.text()).toContain('Activa')
    expect(chip.classes()).toContain('status-chip--success')
  })

  it('emits debounced search model changes through the shared field', async () => {
    const wrapper = mount(SearchField, { props: { modelValue: '' } })
    await wrapper.find('input').setValue('empresa')
    expect(wrapper.emitted('update:modelValue')).toContainEqual(['empresa'])
  })
})
