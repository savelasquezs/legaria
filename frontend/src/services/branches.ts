import { api } from './api'
import type {
  Branch,
  BranchAdministrator,
  BranchAdministratorData,
  BranchData,
  BranchPage,
  BranchStatus,
  BranchAdministratorPage,
  TenantAccountStatus,
} from '../types/branches'

export async function listBranches(params: {
  page: number
  pageSize: number
  search?: string
  status?: BranchStatus
}): Promise<BranchPage> {
  const { data } = await api.get<BranchPage>('/api/tenant/branches', { params })
  return data
}

export async function getBranch(id: string): Promise<Branch> {
  const { data } = await api.get<Branch>(`/api/tenant/branches/${id}`)
  return data
}

export async function createBranch(input: BranchData): Promise<Branch> {
  const { data } = await api.post<Branch>('/api/tenant/branches', input)
  return data
}

export async function updateBranch(id: string, input: BranchData): Promise<Branch> {
  const { data } = await api.put<Branch>(`/api/tenant/branches/${id}`, input)
  return data
}

export async function changeBranchStatus(
  id: string,
  action: 'deactivate' | 'reactivate',
): Promise<Branch> {
  const { data } = await api.post<Branch>(`/api/tenant/branches/${id}/${action}`)
  return data
}

export async function listBranchAdministrators(params: {
  page: number
  pageSize: number
  search?: string
  status?: TenantAccountStatus
}): Promise<BranchAdministratorPage> {
  const { data } = await api.get<BranchAdministratorPage>(
    '/api/tenant/branch-administrators',
    { params },
  )
  return data
}

export async function getBranchAdministrator(id: string): Promise<BranchAdministrator> {
  const { data } = await api.get<BranchAdministrator>(
    `/api/tenant/branch-administrators/${id}`,
  )
  return data
}

export async function createBranchAdministrator(
  input: BranchAdministratorData,
): Promise<BranchAdministrator> {
  const { data } = await api.post<BranchAdministrator>(
    '/api/tenant/branch-administrators',
    input,
  )
  return data
}

export async function updatePendingBranchAdministrator(
  id: string,
  input: BranchAdministratorData,
): Promise<BranchAdministrator> {
  const { data } = await api.put<BranchAdministrator>(
    `/api/tenant/branch-administrators/${id}/pending-profile`,
    input,
  )
  return data
}

export async function updateBranchAdministratorAssignments(
  id: string,
  branchIds: string[],
): Promise<BranchAdministrator> {
  const { data } = await api.put<BranchAdministrator>(
    `/api/tenant/branch-administrators/${id}/branches`,
    { branchIds },
  )
  return data
}

export async function resendBranchAdministratorInvitation(
  id: string,
): Promise<BranchAdministrator> {
  const { data } = await api.post<BranchAdministrator>(
    `/api/tenant/branch-administrators/${id}/invitations`,
  )
  return data
}

export async function changeBranchAdministratorStatus(
  id: string,
  action: 'suspend' | 'reactivate',
): Promise<BranchAdministrator> {
  const { data } = await api.post<BranchAdministrator>(
    `/api/tenant/branch-administrators/${id}/${action}`,
  )
  return data
}
