import 'quasar/src/css/index.sass'
import './styles/app.scss'
import { createApp } from 'vue'
import {
  Dark,
  Notify,
  QBtn,
  QCard,
  QCardActions,
  QCardSection,
  QCheckbox,
  QDialog,
  QDrawer,
  QChip,
  QForm,
  QHeader,
  QIcon,
  QInput,
  QLayout,
  QList,
  QItem,
  QItemSection,
  QPage,
  QPageContainer,
  QPagination,
  QSelect,
  QSeparator,
  QSkeleton,
  QSpace,
  QSpinner,
  QToolbar,
  QTable,
  QTd,
  QTooltip,
  Quasar,
} from 'quasar'
import materialSymbolsRounded from 'quasar/icon-set/svg-material-symbols-rounded'
import App from './App.vue'
import { pinia } from './stores'
import { router } from './router'
import { configureApiSession } from './services/api'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
app.use(Quasar, {
  components: {
    QBtn,
    QCard,
    QCardActions,
    QCardSection,
    QCheckbox,
    QDialog,
    QDrawer,
    QChip,
    QForm,
    QHeader,
    QIcon,
    QInput,
    QLayout,
    QList,
    QItem,
    QItemSection,
    QPage,
    QPageContainer,
    QPagination,
    QSelect,
    QSeparator,
    QSkeleton,
    QSpace,
    QSpinner,
    QToolbar,
    QTable,
    QTd,
    QTooltip,
  },
  plugins: { Dark, Notify },
  iconSet: materialSymbolsRounded,
  config: {
    dark: true,
    notify: {
      position: 'top-right',
      timeout: 3200,
      progress: true,
    },
  },
})
app.use(pinia)

const authStore = useAuthStore(pinia)
configureApiSession({
  getAccessToken: () => authStore.accessToken,
  refresh: () => authStore.refresh(),
  clear: () => authStore.clearSession(),
})

app.use(router)
app.mount('#app')
