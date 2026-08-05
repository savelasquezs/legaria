import { api } from './api'
import type {
  CatalogStatusFilter,
  DocumentCategory,
  DocumentCategoryData,
  DocumentScope,
  DocumentType,
  DocumentTypeData,
} from '../types/documentCatalog'

export async function listDocumentCategories(params: {
  scope?: DocumentScope | 'ALL'
  status?: CatalogStatusFilter
  search?: string
} = {}): Promise<DocumentCategory[]> {
  const { data } = await api.get<DocumentCategory[]>('/api/tenant/document-categories', { params })
  return data
}

export async function createDocumentCategory(input: DocumentCategoryData): Promise<DocumentCategory> {
  const { data } = await api.post<DocumentCategory>('/api/tenant/document-categories', input)
  return data
}

export async function updateDocumentCategory(id: string, input: Omit<DocumentCategoryData, 'scope'>): Promise<DocumentCategory> {
  const { data } = await api.put<DocumentCategory>(`/api/tenant/document-categories/${id}`, input)
  return data
}

export async function changeDocumentCategoryStatus(id: string, action: 'deactivate' | 'reactivate'): Promise<DocumentCategory> {
  const { data } = await api.post<DocumentCategory>(`/api/tenant/document-categories/${id}/${action}`)
  return data
}

export async function listDocumentTypes(params: {
  categoryId?: string
  scope?: DocumentScope | 'ALL'
  status?: CatalogStatusFilter
  search?: string
} = {}): Promise<DocumentType[]> {
  const { data } = await api.get<DocumentType[]>('/api/tenant/document-types', { params })
  return data
}

export async function createDocumentType(input: DocumentTypeData): Promise<DocumentType> {
  const { data } = await api.post<DocumentType>('/api/tenant/document-types', input)
  return data
}

export async function updateDocumentType(id: string, input: DocumentTypeData): Promise<DocumentType> {
  const { data } = await api.put<DocumentType>(`/api/tenant/document-types/${id}`, input)
  return data
}

export async function changeDocumentTypeStatus(id: string, action: 'deactivate' | 'reactivate'): Promise<DocumentType> {
  const { data } = await api.post<DocumentType>(`/api/tenant/document-types/${id}/${action}`)
  return data
}
