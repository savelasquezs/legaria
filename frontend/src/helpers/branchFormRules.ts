export const requiredRule = (value: string | null | undefined) =>
  Boolean(value?.trim()) || 'Este campo es obligatorio.'

export const optionalEmailRule = (value: string | null) =>
  !value || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) || 'Ingresa un correo válido.'

export const optionalPhoneRule = (value: string | null) => {
  if (!value?.trim()) return true
  const normalized = value.replace(/[\s\-()]/g, '')
  const digits = normalized.startsWith('+') ? normalized.slice(1) : normalized
  return /^\d{7,15}$/.test(digits) || 'Usa entre 7 y 15 dígitos; puedes comenzar con +.'
}
