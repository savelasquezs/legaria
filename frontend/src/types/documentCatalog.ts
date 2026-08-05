export type DocumentScope = 'EMPLOYEE' | 'BRANCH'
export type CatalogStatus = 'ACTIVE' | 'INACTIVE'
export type CatalogStatusFilter = CatalogStatus | 'ALL'
export type DocumentDateMode = 'NEVER' | 'OPTIONAL' | 'REQUIRED'
export type DocumentEvidenceKind = 'PDF' | 'IMAGE' | 'VIDEO' | 'LINK'

export interface DocumentCategory {
  id: string
  name: string
  description: string | null
  scope: DocumentScope
  status: CatalogStatus
  documentTypeCount: number
  createdAt: string
  updatedAt: string
}

export interface DocumentType {
  id: string
  categoryId: string
  categoryName: string
  scope: DocumentScope
  name: string
  description: string | null
  status: CatalogStatus
  isAvailable: boolean
  isRequiredByDefault: boolean
  issueDateMode: DocumentDateMode
  expirationDateMode: DocumentDateMode
  allowsMultipleActiveVersions: boolean
  allowsMultipleEvidenceItems: boolean
  allowedEvidenceKinds: DocumentEvidenceKind[]
  createdAt: string
  updatedAt: string
}

export interface DocumentCategoryData {
  name: string
  description: string | null
  scope: DocumentScope
}

export interface DocumentTypeData {
  categoryId: string
  name: string
  description: string | null
  isRequiredByDefault: boolean
  issueDateMode: DocumentDateMode
  expirationDateMode: DocumentDateMode
  allowsMultipleActiveVersions: boolean
  allowsMultipleEvidenceItems: boolean
  allowedEvidenceKinds: DocumentEvidenceKind[]
}
