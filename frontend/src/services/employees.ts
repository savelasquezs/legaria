import { api } from './api'
import type {
  AssignEmployeeData,
  CreateEmployeeData,
  EmployeeDetail,
  EmployeePage,
  JobPosition,
  TransitionAssignmentData,
} from '../types/employees'

export async function listEmployees(params: {
  page: number
  pageSize: number
  search?: string
  branchId?: string
  excludeBranchId?: string
}): Promise<EmployeePage> {
  const { data } = await api.get<EmployeePage>('/api/tenant/employees', { params })
  return data
}

export async function createEmployee(
  branchId: string,
  input: CreateEmployeeData,
): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(`/api/tenant/branches/${branchId}/employees`, input)
  return data
}

export async function assignEmployee(
  branchId: string,
  employeeId: string,
  input: AssignEmployeeData,
): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/branches/${branchId}/employees/${employeeId}/assignments`,
    input,
  )
  return data
}

export async function grantEmployeeAdministrativeAccess(
  employeeId: string,
  input: { email: string; branchIds: string[] },
): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/employees/${employeeId}/administrative-access`,
    input,
  )
  return data
}

export async function getEmployee(employeeId: string): Promise<EmployeeDetail> {
  const { data } = await api.get<EmployeeDetail>(`/api/tenant/employees/${employeeId}`)
  return data
}

export async function listJobPositions(status: 'ACTIVE' | 'INACTIVE' | 'ALL' = 'ACTIVE'): Promise<JobPosition[]> {
  const { data } = await api.get<JobPosition[]>('/api/tenant/job-positions', { params: { status } })
  return data
}

export async function createJobPosition(name: string): Promise<JobPosition> {
  const { data } = await api.post<JobPosition>('/api/tenant/job-positions', { name })
  return data
}

export async function updateJobPosition(id: string, name: string): Promise<JobPosition> {
  const { data } = await api.put<JobPosition>(`/api/tenant/job-positions/${id}`, { name })
  return data
}

export async function changeJobPositionStatus(id: string, action: 'deactivate' | 'reactivate'): Promise<JobPosition> {
  const { data } = await api.post<JobPosition>(`/api/tenant/job-positions/${id}/${action}`)
  return data
}

export async function endEmploymentRelationship(employeeId: string, relationshipId: string, endedOn: string): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/employees/${employeeId}/employment-relationships/${relationshipId}/end`,
    { endedOn },
  )
  return data
}

export async function endEmployeeAssignment(employeeId: string, assignmentId: string, endedOn: string): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/employees/${employeeId}/assignments/${assignmentId}/end`,
    { endedOn },
  )
  return data
}

export async function transitionEmployeeAssignment(employeeId: string, assignmentId: string, input: TransitionAssignmentData): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/employees/${employeeId}/assignments/${assignmentId}/transition`,
    input,
  )
  return data
}

export async function makePrimaryEmployeeAssignment(employeeId: string, assignmentId: string): Promise<EmployeeDetail> {
  const { data } = await api.post<EmployeeDetail>(
    `/api/tenant/employees/${employeeId}/assignments/${assignmentId}/make-primary`,
  )
  return data
}
