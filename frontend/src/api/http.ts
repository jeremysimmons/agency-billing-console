import axios from 'axios'

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'))
  return match ? decodeURIComponent(match[1]) : null
}

export const http = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

// Attach the double-submit CSRF token on every state-changing request.
http.interceptors.request.use((config) => {
  const method = (config.method ?? 'get').toUpperCase()
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    const token = readCookie('aib_csrf')
    if (token) config.headers['X-CSRF-Token'] = token
  }
  return config
})

/** Ensures the CSRF cookie is issued before the first mutation. */
export async function ensureCsrf(): Promise<void> {
  if (!readCookie('aib_csrf')) await http.get('/auth/csrf')
}
