export type AccountType = 'PLATFORM' | 'TENANT'

export interface AuthenticatedAccount {
  id: string
  accountType: AccountType
  email: string
  firstName: string
  lastName: string
  roles: string[]
  organizationId: string | null
  employeeId: string | null
}

export interface AuthenticationResponse {
  accessToken: string
  expiresAt: string
  account: AuthenticatedAccount
}

export interface ProblemDetails {
  status?: number
  detail?: string
  code?: string
}
