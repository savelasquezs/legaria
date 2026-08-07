import { createRouter, createWebHistory } from 'vue-router'
import type { AccountType } from '../types/auth'
import { pinia } from '../stores'
import { useAuthStore } from '../stores/auth'
import LoginPage from '../pages/LoginPage.vue'
import ForgotPasswordPage from '../pages/ForgotPasswordPage.vue'
import ResetPasswordPage from '../pages/ResetPasswordPage.vue'
import VerifyEmailPage from '../pages/VerifyEmailPage.vue'
import AcceptInvitationPage from '../pages/AcceptInvitationPage.vue'
import PlatformOrganizationsPage from '../pages/PlatformOrganizationsPage.vue'
import OrganizationFormPage from '../pages/OrganizationFormPage.vue'
import TenantBranchesPage from '../pages/TenantBranchesPage.vue'
import TenantBranchFormPage from '../pages/TenantBranchFormPage.vue'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    guestOnly?: boolean
    accountType?: AccountType
    roles?: string[]
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'login',
      component: LoginPage,
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: ForgotPasswordPage,
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: ResetPasswordPage,
      meta: { public: true },
    },
    {
      path: '/verify-email',
      name: 'verify-email',
      component: VerifyEmailPage,
      meta: { public: true },
    },
    {
      path: '/accept-invitation',
      name: 'accept-invitation',
      component: AcceptInvitationPage,
      meta: { public: true },
    },
    {
      path: '/platform',
      name: 'platform',
      component: PlatformOrganizationsPage,
      meta: { accountType: 'PLATFORM' },
    },
    {
      path: '/platform/organizations/new',
      name: 'organization-create',
      component: OrganizationFormPage,
      meta: { accountType: 'PLATFORM' },
    },
    {
      path: '/platform/organizations/:id',
      name: 'organization-detail',
      component: OrganizationFormPage,
      meta: { accountType: 'PLATFORM' },
    },
    {
      path: '/app',
      redirect: '/app/branches',
      meta: { accountType: 'TENANT' },
    },
    {
      path: '/app/branches',
      name: 'tenant-branches',
      component: TenantBranchesPage,
      meta: { accountType: 'TENANT' },
    },
    {
      path: '/app/branches/new',
      name: 'tenant-branch-create',
      component: TenantBranchFormPage,
      meta: { accountType: 'TENANT', roles: ['SUPER_ADMIN'] },
    },
    {
      path: '/app/branches/:id',
      name: 'tenant-branch-detail',
      component: TenantBranchFormPage,
      meta: { accountType: 'TENANT' },
    },
    {
      path: '/app/job-positions',
      name: 'tenant-job-positions',
      component: () => import('../pages/JobPositionsPage.vue'),
      meta: { accountType: 'TENANT', roles: ['SUPER_ADMIN'] },
    },
    {
      path: '/app/document-catalog',
      name: 'tenant-document-catalog',
      component: () => import('../pages/DocumentCatalogPage.vue'),
      meta: { accountType: 'TENANT', roles: ['SUPER_ADMIN', 'BRANCH_ADMIN'] },
    },
    {
      path: '/app/notifications',
      name: 'tenant-notifications',
      component: () => import('../pages/NotificationsPage.vue'),
      meta: { accountType: 'TENANT', roles: ['SUPER_ADMIN'] },
    },
    {
      path: '/app/employees/:id',
      name: 'tenant-employee-detail',
      component: () => import('../pages/EmployeeDetailPage.vue'),
      meta: { accountType: 'TENANT', roles: ['SUPER_ADMIN', 'BRANCH_ADMIN'] },
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore(pinia)
  await auth.restore()

  if (to.meta.accountType) {
    if (!auth.account) {
      return { name: 'login', query: { redirect: to.fullPath } }
    }
    if (auth.account.accountType !== to.meta.accountType) {
      return auth.account.accountType === 'PLATFORM' ? '/platform' : '/app'
    }
    if (to.meta.roles?.length && !to.meta.roles.some((role) => auth.account?.roles.includes(role))) {
      return auth.account.accountType === 'TENANT' ? '/app/branches' : '/platform'
    }
  }

  if (to.meta.guestOnly && auth.account) {
    return auth.account.accountType === 'PLATFORM' ? '/platform' : '/app'
  }

  return true
})
