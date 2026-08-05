import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'
import { createBranch, updateBranch } from './branches'
import type { BranchData } from '../types/branches'

vi.mock('./api', () => ({
  api: {
    post: vi.fn(),
    put: vi.fn(),
  },
}))

const branch: BranchData = {
  name: 'Santander',
  contactEmail: '',
  phone: '   ',
  address: 'Calle 108A # 77D-30',
  municipalityCode: '05001',
}

describe('branch service', () => {
  beforeEach(() => {
    vi.mocked(api.post).mockResolvedValue({ data: {} })
    vi.mocked(api.put).mockResolvedValue({ data: {} })
  })

  it('sends empty optional contact fields as null when creating', async () => {
    await createBranch(branch)

    expect(api.post).toHaveBeenCalledWith('/api/tenant/branches', {
      ...branch,
      contactEmail: null,
      phone: null,
    })
  })

  it('sends empty optional contact fields as null when updating', async () => {
    await updateBranch('branch-id', branch)

    expect(api.put).toHaveBeenCalledWith('/api/tenant/branches/branch-id', {
      ...branch,
      contactEmail: null,
      phone: null,
    })
  })
})
