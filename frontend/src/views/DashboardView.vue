<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { useAgency } from '../queries/agency'
import { useClients } from '../queries/clients'

const auth = useAuthStore()
const { data: agency, isLoading: agencyLoading } = useAgency()
const { data: clients, isLoading } = useClients()
</script>

<template>
  <section>
    <p class="crumb">Top-level agency</p>
    <h1>{{ agencyLoading ? '…' : (agency?.name ?? 'Agency') }}</h1>
    <p class="lede">
      You are viewing the default agency workspace.
      Signed in as <strong>{{ auth.user?.displayName }}</strong>
      <span v-if="auth.user?.roles.length"> ({{ auth.user.roles.join(', ') }})</span>.
    </p>
    <div class="cards">
      <div class="stat">
        <div class="n">{{ isLoading ? '…' : clients?.length ?? 0 }}</div>
        <div class="l">Clients under this agency</div>
      </div>
    </div>
    <p class="links">
      <RouterLink to="/agency">Edit agency →</RouterLink>
      <RouterLink to="/clients">Manage clients →</RouterLink>
    </p>
  </section>
</template>

<style scoped>
.crumb {
  margin: 0 0 0.25rem;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #6b7280;
}
.lede { margin: 0.35rem 0 0; color: #4b5563; max-width: 40rem; }
.cards { display: flex; gap: 1rem; margin: 1rem 0; }
.stat { border: 1px solid #e5e7eb; border-radius: 10px; padding: 1rem 1.5rem; text-align: center; }
.n { font-size: 1.8rem; font-weight: 700; }
.l { opacity: 0.7; font-size: 0.85rem; }
.links { display: flex; gap: 1.25rem; }
</style>
