<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { useClients } from '../queries/clients'

const auth = useAuthStore()
const { data: clients, isLoading } = useClients()
</script>

<template>
  <section>
    <h1>Dashboard</h1>
    <p>Signed in as <strong>{{ auth.user?.displayName }}</strong> ({{ auth.user?.roles.join(', ') }})</p>
    <div class="cards">
      <div class="stat">
        <div class="n">{{ isLoading ? '…' : clients?.length ?? 0 }}</div>
        <div class="l">Clients</div>
      </div>
    </div>
    <RouterLink to="/clients">Manage clients →</RouterLink>
  </section>
</template>

<style scoped>
.cards { display: flex; gap: 1rem; margin: 1rem 0; }
.stat { border: 1px solid #e5e7eb; border-radius: 10px; padding: 1rem 1.5rem; text-align: center; }
.n { font-size: 1.8rem; font-weight: 700; }
.l { opacity: 0.7; }
</style>
