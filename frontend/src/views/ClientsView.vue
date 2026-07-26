<script setup lang="ts">
import { ref } from 'vue'
import { useClients, useCreateClient, useDeleteClient } from '../queries/clients'

const { data: clients, isLoading, error } = useClients()
const createClient = useCreateClient()
const deleteClient = useDeleteClient()

const name = ref('')
const formError = ref('')
const confirmId = ref<string | null>(null)
const deleteError = ref('')

async function add() {
  formError.value = ''
  try {
    await createClient.mutateAsync({ name: name.value })
    name.value = ''
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not create client.'
  }
}

async function remove(id: string) {
  deleteError.value = ''
  try {
    await deleteClient.mutateAsync(id)
    confirmId.value = null
  } catch (e: any) {
    deleteError.value = e?.response?.data?.error ?? 'Could not delete client.'
    confirmId.value = null
  }
}
</script>

<template>
  <section data-testid="clients-view">
    <h1>Clients</h1>

    <form class="row create-form" data-testid="client-create-form" @submit.prevent="add">
      <label>
        Client
        <input v-model="name" placeholder="Client name" required data-testid="client-create-name" />
      </label>
      <button :disabled="createClient.isLoading.value" data-testid="client-create-submit">Add client</button>
    </form>
    <p v-if="formError" class="error" data-testid="client-create-error">{{ formError }}</p>
    <p v-if="deleteError" class="error" data-testid="client-delete-error">{{ deleteError }}</p>

    <p v-if="isLoading" data-testid="clients-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="clients-error">Failed to load clients.</p>
    <table v-else class="grid" data-testid="clients-table">
      <thead><tr><th>Name</th><th>Original name</th><th>Status</th><th></th></tr></thead>
      <tbody>
        <tr v-for="c in clients" :key="c.id" :data-testid="`client-row-${c.id}`">
          <td>
            <RouterLink :to="`/clients/${c.id}`" :data-testid="`client-name-${c.id}`">{{ c.name }}</RouterLink>
          </td>
          <td :data-testid="`client-original-name-${c.id}`">{{ c.originalName ?? '—' }}</td>
          <td :data-testid="`client-status-${c.id}`">{{ c.status }}</td>
          <td class="actions">
            <template v-if="confirmId === c.id">
              <button class="link danger" :disabled="deleteClient.isLoading.value" :data-testid="`client-delete-confirm-${c.id}`" @click="remove(c.id)">Confirm</button>
              <button class="link" :disabled="deleteClient.isLoading.value" :data-testid="`client-delete-cancel-${c.id}`" @click="confirmId = null">Cancel</button>
            </template>
            <button v-else class="link danger" :data-testid="`client-delete-${c.id}`" @click="confirmId = c.id">Delete</button>
          </td>
        </tr>
        <tr v-if="clients && clients.length === 0"><td colspan="4" data-testid="clients-empty">No clients yet.</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.row { display: flex; gap: 0.5rem; margin-bottom: 1rem; align-items: flex-end; flex-wrap: wrap; }
.create-form label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
input { padding: 0.5rem 0.7rem; border: 1px solid #d1d5db; border-radius: 8px; }
button { padding: 0.5rem 0.9rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #eee; }
.actions { display: flex; flex-wrap: wrap; gap: 0.6rem; }
.link { background: none; color: #10b981; padding: 0; }
.link.danger { color: #dc2626; }
a { color: #059669; text-decoration: underline; text-underline-offset: 2px; }
.error { color: #dc2626; }
</style>
