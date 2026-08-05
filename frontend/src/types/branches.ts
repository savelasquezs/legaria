import type { InvitationStatus } from './organizations'

export type BranchStatus = 'ACTIVE' | 'INACTIVE'
export type TenantAccountStatus = 'ACTIVE' | 'SUSPENDED'

export interface Branch {
  id: string
  name: string
  contactEmail: string | null
  phone: string | null
  address: string
  municipalityCode: string
  municipalityName: string
  departmentCode: string
  departmentName: string
  status: BranchStatus
  createdAt: string
  updatedAt: string
}

export interface BranchPage {
  items: Branch[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface BranchData {
  name: string
  contactEmail: string | null
  phone: string | null
  address: string
  municipalityCode: string
}

export interface BranchAssignment {
  id: string
  name: string
  status: BranchStatus
}

export interface BranchAdministrator {
  id: string
  firstName: string
  lastName: string
  email: string
  accountStatus: TenantAccountStatus
  invitationStatus: InvitationStatus
  invitationExpiresAt: string | null
  branches: BranchAssignment[]
  createdAt: string
  updatedAt: string
}

export interface BranchAdministratorPage {
  items: BranchAdministrator[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface BranchAdministratorData {
  firstName: string
  lastName: string
  email: string
  branchIds: string[]
}
