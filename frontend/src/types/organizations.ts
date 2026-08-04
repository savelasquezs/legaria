export type OrganizationStatus = 'ACTIVE' | 'SUSPENDED'
export type InvitationStatus =
  | 'PENDING_DELIVERY'
  | 'SENT'
  | 'DELIVERY_FAILED'
  | 'EXPIRED'
  | 'ACCEPTED'

export interface Department {
  code: string
  name: string
}

export interface Municipality {
  code: string
  name: string
  type: string
}

export interface InitialAdministrator {
  id: string
  firstName: string
  lastName: string
  email: string
  invitationStatus: InvitationStatus
  invitationExpiresAt: string | null
}

export interface Organization {
  id: string
  tradeName: string
  legalName: string
  nit: string
  verificationDigit: number
  contactEmail: string
  phone: string
  address: string
  municipalityCode: string
  municipalityName: string
  departmentCode: string
  departmentName: string
  status: OrganizationStatus
  createdAt: string
  updatedAt: string
  initialAdmin: InitialAdministrator
}

export interface OrganizationListItem {
  id: string
  tradeName: string
  legalName: string
  nit: string
  verificationDigit: number
  municipalityName: string
  departmentName: string
  status: OrganizationStatus
  invitationStatus: InvitationStatus
  createdAt: string
}

export interface OrganizationPage {
  items: OrganizationListItem[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface OrganizationData {
  tradeName: string
  legalName: string
  nit: string
  verificationDigit: number
  contactEmail: string
  phone: string
  address: string
  municipalityCode: string
}

export interface InitialAdministratorData {
  firstName: string
  lastName: string
  email: string
}

export interface CreateOrganizationData extends OrganizationData {
  initialAdmin: InitialAdministratorData
}
