import '@quasar/extras/material-icons/material-icons.css'
import 'quasar/src/css/index.sass'
import './styles/app.css'
import { createApp } from 'vue'
import { Quasar, Notify } from 'quasar'
import App from './App.vue'
import { pinia } from './stores'
import { router } from './router'
import { configureApiSession } from './services/api'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
app.use(Quasar, {
  plugins: { Notify },
  config: {
    brand: {
      primary: '#345ff6',
      secondary: '#13233f',
      positive: '#158466',
      negative: '#c43d4b',
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
