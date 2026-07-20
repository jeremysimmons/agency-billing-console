<script setup lang="ts">
import { useRouter, RouterView, RouterLink } from 'vue-router'
import { useAuthStore } from './stores/auth'

const auth = useAuthStore()
const router = useRouter()

async function logout() {
  await auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <div class="app">
    <header v-if="auth.user" class="topbar">
      <div class="brand">Agency Billing Console</div>
      <nav>
        <RouterLink to="/">Dashboard</RouterLink>
        <RouterLink to="/clients">Clients</RouterLink>
        <RouterLink to="/mappings">Mappings</RouterLink>
        <RouterLink to="/work">Work</RouterLink>
      </nav>
      <div class="spacer" />
      <span class="user">{{ auth.user.displayName }}</span>
      <button class="link" @click="logout">Sign out</button>
    </header>
    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.topbar {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  padding: 0.75rem 1.25rem;
  border-bottom: 1px solid #e5e7eb;
  background: #fff;
  color: #1f2937;
}
.brand { font-weight: 700; }
nav { display: flex; gap: 1rem; }
nav a { text-decoration: none; color: #374151; }
nav a.router-link-active { color: #059669; font-weight: 600; }
.spacer { flex: 1; }
.user { color: #6b7280; }
.link { background: none; border: none; cursor: pointer; color: #059669; }
.content { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
</style>
