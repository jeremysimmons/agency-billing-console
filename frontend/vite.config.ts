import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import basicSsl from '@vitejs/plugin-basic-ssl'

// Frontend runs on https://localhost:3000 (matches the Google OAuth client origins)
// and proxies /api to the ASP.NET Core API.
export default defineConfig({
  plugins: [vue(), basicSsl()],
  server: {
    port: 3000,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5080',
        changeOrigin: true,
      },
    },
  },
})
