import { createRouter, createWebHistory } from 'vue-router'
import type { AccountType } from '../types/auth'
import { pinia } from '../stores'
import { useAuthStore } from '../stores/auth'
import LoginPage from '../pages/LoginPage.vue'
import ForgotPasswordPage from '../pages/ForgotPasswordPage.vue'
import ResetPasswordPage from '../pages/ResetPasswordPage.vue'
import VerifyEmailPage from '../pages/VerifyEmailPage.vue'
import SessionPage from '../pages/SessionPage.vue'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    guestOnly?: boolean
    accountType?: AccountType
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
      path: '/platform',
      name: 'platform',
      component: SessionPage,
      props: { platform: true },
      meta: { accountType: 'PLATFORM' },
    },
    {
      path: '/app',
      name: 'tenant-app',
      component: SessionPage,
      props: { platform: false },
      meta: { accountType: 'TENANT' },
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
  }

  if (to.meta.guestOnly && auth.account) {
    return auth.account.accountType === 'PLATFORM' ? '/platform' : '/app'
  }

  return true
})
