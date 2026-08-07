export interface EmployeeAssignment {
  id: string
  employmentRelationshipId: string
  branchId: string
  branchName: string
  jobPositionId: string
  jobPositionName: string
  isPrimary: boolean
  startedOn: string
  endedOn: string | null
  status: 'ACTIVE' | 'ENDED'
}

export interface EmploymentRelationship {
  id: string
  startedOn: string
  endedOn: string | null
  status: 'ACTIVE' | 'ENDED'
  assignments: EmployeeAssignment[]
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
  mobilePhone?: string | null
  contactEmail?: string | null
  whatsAppConsentAt?: string | null
  assignments: EmployeeAssignment[]
  administrativeAccess: EmployeeAdministrativeAccess | null
  createdAt: string
  updatedAt: string
}

export interface EmployeeDetail extends Omit<Employee, 'assignments'> {
  employmentRelationships: EmploymentRelationship[]
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
  mobilePhone?: string | null
  contactEmail?: string | null
  whatsAppConsent?: boolean
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
  requiredDocumentCount: number
}

export interface JobPositionDocumentRequirements {
  jobPositionId: string
  documentTypeIds: string[]
}

export interface TransitionAssignmentData {
  branchId: string
  jobPositionId: string
  effectiveOn: string
}
