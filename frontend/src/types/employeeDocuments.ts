import type { DocumentDateMode, DocumentEvidenceKind } from './documentCatalog'

export interface EmployeeRequiredDocument { documentTypeId: string; name: string; categoryId: string; categoryName: string }
export interface EmployeeUpcomingDocument { employeeDocumentId: string; documentTypeId: string; name: string; categoryName: string; expiresOn: string }
export interface EmployeeDocumentTypeOption { id: string; name: string; isMissing: boolean; issueDateMode: DocumentDateMode; expirationDateMode: DocumentDateMode; allowsMultipleEvidenceItems: boolean; allowedEvidenceKinds: DocumentEvidenceKind[] }
export interface EmployeeDocumentCategory { id: string; name: string; missingCount: number; documentTypes: EmployeeDocumentTypeOption[] }
export interface EmployeeDocumentSummary { requiredCount: number; missingCount: number; missingDocuments: EmployeeRequiredDocument[]; upcomingExpirations: EmployeeUpcomingDocument[]; categories: EmployeeDocumentCategory[] }
