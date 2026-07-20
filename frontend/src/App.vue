<script setup lang="ts">
import { RouterView, RouterLink } from 'vue-router'
import { useAgency } from './queries/agency'

const { data: agency } = useAgency()
</script>

<template>
  <div class="app">
    <header class="topbar">
      <div class="brand-block">
        <div class="brand">ClickUp Billing Prep</div>
        <div v-if="agency" class="agency-context">
          <span class="agency-label">Agency</span>
          <span class="agency-name">{{ agency.name }}</span>
        </div>
      </div>
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
.brand-block { display: flex; flex-direction: column; gap: 0.15rem; min-width: 0; }
.brand { font-weight: 700; line-height: 1.2; }
.agency-context {
  display: inline-flex;
  align-items: baseline;
  gap: 0.4rem;
  max-width: 18rem;
}
.agency-label {
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #6b7280;
}
.agency-name {
  font-size: 0.85rem;
  font-weight: 600;
  color: #059669;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
nav { display: flex; gap: 1rem; }
nav a { text-decoration: none; color: #374151; }
nav a.router-link-active { color: #059669; font-weight: 600; }
.content { padding: 1.5rem; max-width: 1400px; margin: 0 auto; }
</style>
