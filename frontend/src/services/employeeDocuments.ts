import { api } from './api'
import type { EmployeeDocumentSummary } from '../types/employeeDocuments'

export async function getEmployeeDocumentSummary(employeeId: string): Promise<EmployeeDocumentSummary> {
  const { data } = await api.get<EmployeeDocumentSummary>(`/api/tenant/employees/${employeeId}/documents/summary`)
  return data
}

export async function uploadEmployeeDocument(employeeId: string, input: {
  documentTypeId: string
  issuedOn: string
  expiresOn: string
  files: File[]
  links: string[]
}): Promise<EmployeeDocumentSummary> {
  const form = new FormData()
  form.append('documentTypeId', input.documentTypeId)
  if (input.issuedOn) form.append('issuedOn', input.issuedOn)
  if (input.expiresOn) form.append('expiresOn', input.expiresOn)
  input.files.forEach((file) => form.append('files', file))
  input.links.filter(Boolean).forEach((link) => form.append('links', link))
  const { data } = await api.post<EmployeeDocumentSummary>(`/api/tenant/employees/${employeeId}/documents`, form)
  return data
}
