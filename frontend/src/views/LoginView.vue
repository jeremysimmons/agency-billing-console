<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { GOOGLE_CLIENT_ID } from '../config'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const usernameOrEmail = ref('')
const password = ref('')
const magicEmail = ref('')
const error = ref('')
const info = ref('')
const busy = ref(false)

function redirect() {
  const to = (route.query.redirect as string) || '/'
  router.push(to)
}

async function submitPassword() {
  error.value = ''; busy.value = true
  try {
    await auth.loginPassword(usernameOrEmail.value, password.value)
    redirect()
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Sign-in failed.'
  } finally {
    busy.value = false
  }
}

async function sendMagicLink() {
  error.value = ''; info.value = ''; busy.value = true
  try {
    await auth.requestMagicLink(magicEmail.value)
    info.value = 'If the email matches an account, a sign-in link has been sent.'
  } catch {
    info.value = 'If the email matches an account, a sign-in link has been sent.'
  } finally {
    busy.value = false
  }
}

// Google Identity Services
function loadGis(): Promise<void> {
  return new Promise((resolve, reject) => {
    if ((window as any).google?.accounts?.id) return resolve()
    const s = document.createElement('script')
    s.src = 'https://accounts.google.com/gsi/client'
    s.async = true
    s.onload = () => resolve()
    s.onerror = () => reject(new Error('Failed to load Google script'))
    document.head.appendChild(s)
  })
}

onMounted(async () => {
  if (!GOOGLE_CLIENT_ID) return
  try {
    await loadGis()
    const google = (window as any).google
    google.accounts.id.initialize({
      client_id: GOOGLE_CLIENT_ID,
      callback: async (resp: any) => {
        error.value = ''; busy.value = true
        try {
          await auth.loginGoogle(resp.credential)
          redirect()
        } catch (e: any) {
          error.value = e?.response?.data?.error ?? 'Google sign-in failed.'
        } finally {
          busy.value = false
        }
      },
    })
    google.accounts.id.renderButton(document.getElementById('google-btn'), {
      theme: 'outline', size: 'large', width: 320,
    })
  } catch { /* google button just won't render */ }
})
</script>

<template>
  <div class="login">
    <h1>Agency Billing Console</h1>
    <p class="sub">Sign in to continue</p>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="info" class="info">{{ info }}</p>

    <form @submit.prevent="submitPassword" class="card">
      <h3>Login</h3>
      <input v-model="usernameOrEmail" placeholder="Username or email" autocomplete="username" />
      <input v-model="password" type="password" placeholder="Password" autocomplete="current-password" />
      <button type="submit" :disabled="busy">Sign in</button>
    </form>

    <form @submit.prevent="sendMagicLink" class="card">
      <h3>Email magic link</h3>
      <input v-model="magicEmail" type="email" placeholder="you@example.com" />
      <button type="submit" :disabled="busy">Send sign-in link</button>
    </form>

    <div class="card">
      <h3>Google Workspace</h3>
      <div id="google-btn"></div>
    </div>
  </div>
</template>

<style scoped>
.login { max-width: 380px; margin: 3rem auto; display: flex; flex-direction: column; gap: 1rem; }
h1 { margin: 0; font-size: 1.4rem; }
.sub { margin: 0 0 0.5rem; opacity: 0.7; }
.card { display: flex; flex-direction: column; gap: 0.5rem; padding: 1rem; border: 1px solid #e5e7eb; border-radius: 10px; }
.card h3 { margin: 0 0 0.25rem; font-size: 0.95rem; }
input { padding: 0.55rem 0.7rem; border: 1px solid #d1d5db; border-radius: 8px; }
button { padding: 0.55rem 0.7rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
button:disabled { opacity: 0.6; cursor: default; }
.error { color: #dc2626; }
.info { color: #2563eb; }
</style>
