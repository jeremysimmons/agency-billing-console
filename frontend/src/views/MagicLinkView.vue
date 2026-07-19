<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const state = ref<'working' | 'error'>('working')
const message = ref('Signing you in...')

onMounted(async () => {
  const token = route.query.token as string | undefined
  if (!token) { state.value = 'error'; message.value = 'Missing token.'; return }
  try {
    await auth.consumeMagicLink(token)
    router.push('/')
  } catch (e: any) {
    state.value = 'error'
    message.value = e?.response?.data?.error ?? 'This link is invalid or has expired.'
  }
})
</script>

<template>
  <div class="wrap">
    <p :class="state">{{ message }}</p>
    <RouterLink v-if="state === 'error'" to="/login">Back to sign in</RouterLink>
  </div>
</template>

<style scoped>
.wrap { max-width: 380px; margin: 4rem auto; text-align: center; }
.error { color: #dc2626; }
</style>
