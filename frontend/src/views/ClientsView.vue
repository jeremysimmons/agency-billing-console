<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useClients, useCreateClient } from '../queries/clients'

const router = useRouter()
const { data: clients, isLoading, error } = useClients()
const createClient = useCreateClient()

const name = ref('')
const code = ref('')
const formError = ref('')

async function add() {
  formError.value = ''
  try {
    await createClient.mutateAsync({ name: name.value, code: code.value || null })
    name.value = ''; code.value = ''
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not create client.'
  }
}
</script>

<template>
  <section>
    <h1>Clients</h1>

    <form class="row" @submit.prevent="add">
      <input v-model="name" placeholder="Client name" required />
      <input v-model="code" placeholder="Code (optional)" />
      <button :disabled="createClient.isLoading.value">Add client</button>
    </form>
    <p v-if="formError" class="error">{{ formError }}</p>

    <p v-if="isLoading">Loading…</p>
    <p v-else-if="error" class="error">Failed to load clients.</p>
    <table v-else class="grid">
      <thead><tr><th>Name</th><th>Code</th><th>Status</th><th></th></tr></thead>
      <tbody>
        <tr v-for="c in clients" :key="c.id">
          <td>{{ c.name }}</td>
          <td>{{ c.code ?? '—' }}</td>
          <td>{{ c.status }}</td>
          <td><button class="link" @click="router.push(`/clients/${c.id}`)">Open</button></td>
        </tr>
        <tr v-if="clients && clients.length === 0"><td colspan="4">No clients yet.</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.row { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
input { padding: 0.5rem 0.7rem; border: 1px solid #d1d5db; border-radius: 8px; }
button { padding: 0.5rem 0.9rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #eee; }
.link { background: none; color: #10b981; }
.error { color: #dc2626; }
</style>
