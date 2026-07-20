import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const certFile = path.join(repoRoot, 'certs', 'localhost.pem')
const keyFile = path.join(repoRoot, 'certs', 'localhost-key.pem')

if (!fs.existsSync(certFile) || !fs.existsSync(keyFile)) {
  throw new Error(
    `Missing mkcert TLS files:\n  ${certFile}\n  ${keyFile}\n\nRun: ./scripts/dev-bootstrap.sh`,
  )
}

// Frontend runs on https://localhost:3000 (matches the Google OAuth client origins)
// and proxies /api to the ASP.NET Core API.
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 3000,
    strictPort: true,
    https: {
      cert: fs.readFileSync(certFile),
      key: fs.readFileSync(keyFile),
    },
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5080',
        changeOrigin: true,
      },
    },
  },
})
