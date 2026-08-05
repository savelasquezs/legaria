import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'
import {
  changeDocumentCategoryStatus,
  createDocumentType,
  listDocumentCategories,
  listDocumentTypes,
  updateDocumentCategory,
} from './documentCatalog'

vi.mock('./api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }))

describe('document catalog service', () => {
  beforeEach(() => vi.clearAllMocks())

  it('sends catalog filters and category mutations to tenant endpoints', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] })
    vi.mocked(api.put).mockResolvedValue({ data: { id: 'category-1' } })
    vi.mocked(api.post).mockResolvedValue({ data: { id: 'category-1' } })

    await listDocumentCategories({ scope: 'EMPLOYEE', status: 'ACTIVE', search: 'social' })
    await updateDocumentCategory('category-1', { name: 'Seguridad Social', description: null })
    await changeDocumentCategoryStatus('category-1', 'deactivate')

    expect(api.get).toHaveBeenCalledWith('/api/tenant/document-categories', {
      params: { scope: 'EMPLOYEE', status: 'ACTIVE', search: 'social' },
    })
    expect(api.put).toHaveBeenCalledWith('/api/tenant/document-categories/category-1', {
      name: 'Seguridad Social', description: null,
    })
    expect(api.post).toHaveBeenCalledWith('/api/tenant/document-categories/category-1/deactivate')
  })

  it('uses typed document endpoints without changing the payload', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] })
    vi.mocked(api.post).mockResolvedValue({ data: { id: 'type-1' } })
    const input = {
      categoryId: 'category-1',
      name: 'Afiliación EPS',
      description: null,
      isRequiredByDefault: true,
      issueDateMode: 'OPTIONAL' as const,
      expirationDateMode: 'NEVER' as const,
      allowsMultipleActiveVersions: false,
      allowsMultipleEvidenceItems: true,
      allowedEvidenceKinds: ['PDF', 'LINK'] as const,
    }

    await listDocumentTypes({ categoryId: 'category-1', status: 'ALL' })
    await createDocumentType({ ...input, allowedEvidenceKinds: [...input.allowedEvidenceKinds] })

    expect(api.get).toHaveBeenCalledWith('/api/tenant/document-types', {
      params: { categoryId: 'category-1', status: 'ALL' },
    })
    expect(api.post).toHaveBeenCalledWith('/api/tenant/document-types', {
      ...input, allowedEvidenceKinds: ['PDF', 'LINK'],
    })
  })
})
