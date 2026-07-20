<script setup lang="ts">
import { RouterView, RouterLink } from 'vue-router'
</script>

<template>
  <div class="app">
    <header class="topbar">
      <div class="brand">ClickUp Billing Prep</div>
      <nav>
        <RouterLink to="/tasks">Tasks</RouterLink>
        <RouterLink to="/clients">Clients</RouterLink>
        <RouterLink to="/hierarchy">Hierarchy</RouterLink>
        <RouterLink to="/sync">Sync</RouterLink>
      </nav>
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
.content { padding: 1.5rem; max-width: 1400px; margin: 0 auto; }
</style>
