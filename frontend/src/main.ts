import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { PiniaColada } from '@pinia/colada'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import 'primeicons/primeicons.css'
import './style.css'
import App from './App.vue'
import { router } from './router'
import { PRIMEUI_LICENSE } from './config'

createApp(App)
  .use(createPinia())
  .use(PiniaColada)
  .use(router)
  .use(PrimeVue, {
    theme: {
      preset: Aura,
      options: { darkModeSelector: false },
    },
    license: PRIMEUI_LICENSE,
  })
  .mount('#app')
