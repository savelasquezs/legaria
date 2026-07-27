import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from 'axios'
import type { ProblemDetails } from '../types/auth'

interface RetryableRequest extends InternalAxiosRequestConfig {
  _retried?: boolean
}

interface ApiSession {
  getAccessToken: () => string | null
  refresh: () => Promise<boolean>
  clear: () => void
}

export const api: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

let session: ApiSession | null = null
let refreshPromise: Promise<boolean> | null = null

export function configureApiSession(value: ApiSession): void {
  session = value
}

api.interceptors.request.use((config) => {
  const token = session?.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const request = error.config as RetryableRequest | undefined
    const isAuthEndpoint =
      request?.url?.includes('/api/auth/login') ||
      request?.url?.includes('/api/auth/refresh')

    if (
      error.response?.status !== 401 ||
      !request ||
      request._retried ||
      isAuthEndpoint ||
      !session
    ) {
      return Promise.reject(error)
    }

    request._retried = true
    refreshPromise ??= session.refresh().finally(() => {
      refreshPromise = null
    })
    const refreshed = await refreshPromise
    if (!refreshed) {
      session.clear()
      return Promise.reject(error)
    }

    return api(request)
  },
)

export function getProblem(error: unknown): ProblemDetails | null {
  if (!axios.isAxiosError<ProblemDetails>(error)) {
    return null
  }
  return error.response?.data ?? null
}
