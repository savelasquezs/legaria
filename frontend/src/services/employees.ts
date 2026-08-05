import { api } from './api'
import type {
  AssignEmployeeData,
  CreateEmployeeData,
  Employee,
  EmployeePage,
  JobPosition,
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
): Promise<Employee> {
  const { data } = await api.post<Employee>(`/api/tenant/branches/${branchId}/employees`, input)
  return data
}

export async function assignEmployee(
  branchId: string,
  employeeId: string,
  input: AssignEmployeeData,
): Promise<Employee> {
  const { data } = await api.post<Employee>(
    `/api/tenant/branches/${branchId}/employees/${employeeId}/assignments`,
    input,
  )
  return data
}

export async function grantEmployeeAdministrativeAccess(
  employeeId: string,
  input: { email: string; branchIds: string[] },
): Promise<Employee> {
  const { data } = await api.post<Employee>(
    `/api/tenant/employees/${employeeId}/administrative-access`,
    input,
  )
  return data
}

export async function listJobPositions(): Promise<JobPosition[]> {
  const { data } = await api.get<JobPosition[]>('/api/tenant/job-positions')
  return data
}

export async function createJobPosition(name: string): Promise<JobPosition> {
  const { data } = await api.post<JobPosition>('/api/tenant/job-positions', { name })
  return data
}
