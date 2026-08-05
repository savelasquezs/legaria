export interface EmployeeAssignment {
  id: string
  branchId: string
  branchName: string
  jobPositionId: string
  jobPositionName: string
  isPrimary: boolean
  startedOn: string
}

export interface EmployeeAdministrativeAccess {
  accountId: string
  email: string
  accountStatus: 'ACTIVE' | 'SUSPENDED'
  invitationStatus: 'PENDING_DELIVERY' | 'SENT' | 'DELIVERY_FAILED' | 'EXPIRED' | 'ACCEPTED' | 'REVOKED'
  invitationExpiresAt: string | null
  branchIds: string[]
}

export interface Employee {
  id: string
  documentType: string
  documentNumber: string
  firstName: string
  lastName: string
  assignments: EmployeeAssignment[]
  administrativeAccess: EmployeeAdministrativeAccess | null
  createdAt: string
  updatedAt: string
}

export interface EmployeePage {
  items: Employee[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface AdministrativeAccessData {
  email: string | null
  branchIds: string[]
}

export interface CreateEmployeeData {
  documentType: string
  documentNumber: string
  firstName: string
  lastName: string
  jobPositionId: string
  startedOn: string
  isPrimary: boolean
  administrativeAccess: AdministrativeAccessData | null
}

export interface AssignEmployeeData {
  jobPositionId: string
  startedOn: string
  isPrimary: boolean
  administrativeAccess: AdministrativeAccessData | null
}

export interface JobPosition {
  id: string
  name: string
  status: 'ACTIVE' | 'INACTIVE'
}
