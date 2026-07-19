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
  border-bottom: 1px solid var(--p-content-border-color, #e5e7eb);
  background: var(--p-content-background, #fff);
}
.brand { font-weight: 700; }
nav { display: flex; gap: 1rem; }
nav a { text-decoration: none; color: inherit; }
nav a.router-link-active { color: var(--p-primary-color, #10b981); font-weight: 600; }
.spacer { flex: 1; }
.user { opacity: 0.8; }
.link { background: none; border: none; cursor: pointer; color: var(--p-primary-color, #10b981); }
.content { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
</style>
