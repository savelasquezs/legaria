import { api } from './api'
import type {
  CreateOrganizationData,
  Department,
  InitialAdministratorData,
  Municipality,
  Organization,
  OrganizationData,
  OrganizationPage,
  OrganizationStatus,
} from '../types/organizations'
import type { Branch, BranchData } from '../types/branches'

export async function listOrganizations(params: {
  page: number
  pageSize: number
  search?: string
  status?: OrganizationStatus
}): Promise<OrganizationPage> {
  const { data } = await api.get<OrganizationPage>('/api/platform/organizations', { params })
  return data
}

export async function getOrganization(id: string): Promise<Organization> {
  const { data } = await api.get<Organization>(`/api/platform/organizations/${id}`)
  return data
}

export async function createOrganization(input: CreateOrganizationData): Promise<Organization> {
  const { data } = await api.post<Organization>('/api/platform/organizations', input)
  return data
}

export async function createInitialBranch(
  organizationId: string,
  input: BranchData,
): Promise<Branch> {
  const { data } = await api.post<Branch>(
    `/api/platform/organizations/${organizationId}/initial-branch`,
    input,
  )
  return data
}

export async function updateOrganization(id: string, input: OrganizationData): Promise<Organization> {
  const { data } = await api.put<Organization>(`/api/platform/organizations/${id}`, input)
  return data
}

export async function updateInitialAdmin(
  id: string,
  input: InitialAdministratorData,
): Promise<Organization> {
  const { data } = await api.put<Organization>(
    `/api/platform/organizations/${id}/initial-admin`,
    input,
  )
  return data
}

export async function resendInvitation(id: string): Promise<Organization> {
  const { data } = await api.post<Organization>(
    `/api/platform/organizations/${id}/initial-admin/invitations`,
  )
  return data
}

export async function changeOrganizationStatus(
  id: string,
  action: 'suspend' | 'reactivate',
): Promise<Organization> {
  const { data } = await api.post<Organization>(`/api/platform/organizations/${id}/${action}`)
  return data
}

export async function getDepartments(): Promise<Department[]> {
  const { data } = await api.get<Department[]>('/api/catalogs/departments')
  return data
}

export async function getMunicipalities(departmentCode: string): Promise<Municipality[]> {
  const { data } = await api.get<Municipality[]>(
    `/api/catalogs/departments/${departmentCode}/municipalities`,
  )
  return data
}
