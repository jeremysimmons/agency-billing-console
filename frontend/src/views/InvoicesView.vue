<script setup lang="ts">
import { ref } from 'vue'
import { useInvoices, useCreateInvoice, useUpdateInvoice } from '../queries/invoices'
import type { Invoice, InvoiceStatus } from '../api/types'

const STATUS_OPTIONS: { value: InvoiceStatus; label: string }[] = [
  { value: 'preparing', label: 'preparing' },
  { value: 'sent', label: 'sent' },
  { value: 'partially-paid', label: 'partially-paid' },
  { value: 'fully-paid', label: 'fully paid' },
]

const { data: invoices, isLoading, error } = useInvoices()
const createInvoice = useCreateInvoice()
const updateInvoice = useUpdateInvoice()

const name = ref('')
const formError = ref('')
const statusErrors = ref<Record<string, string>>({})
const savingId = ref<string | null>(null)

function statusKey(status: string) {
  const s = status.trim().toLowerCase().replaceAll(' ', '-').replaceAll('_', '-')
  if (s === 'partiallypaid') return 'partially-paid'
  if (s === 'fullypaid') return 'fully-paid'
  return s
}

function toApiStatus(status: string): InvoiceStatus {
  switch (statusKey(status)) {
    case 'sent': return 'sent'
    case 'partially-paid': return 'partially-paid'
    case 'fully-paid': return 'fully-paid'
    default: return 'preparing'
  }
}

function isNoneInvoice(inv: Invoice) {
  return inv.name.trim().toLowerCase() === 'none'
}

async function add() {
  formError.value = ''
  try {
    await createInvoice.mutateAsync({ name: name.value.trim() })
    name.value = ''
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not create invoice.'
  }
}

async function onStatusChange(inv: Invoice, value: string) {
  const status = toApiStatus(value)
  if (statusKey(inv.status) === statusKey(status)) return
  savingId.value = inv.id
  delete statusErrors.value[inv.id]
  try {
    await updateInvoice.mutateAsync({ id: inv.id, name: inv.name, status })
  } catch (e: any) {
    statusErrors.value[inv.id] = e?.response?.data?.error ?? 'Could not update status.'
  } finally {
    savingId.value = null
  }
}
</script>

<template>
  <section data-testid="invoices-view">
    <h1>Invoices</h1>

    <form class="row" data-testid="invoice-create-form" @submit.prevent="add">
      <input v-model="name" placeholder="Invoice name" required data-testid="invoice-create-name" />
      <button :disabled="createInvoice.isLoading.value || !name.trim()" data-testid="invoice-create-submit">
        Add invoice
      </button>
    </form>
    <p v-if="formError" class="error" data-testid="invoice-create-error">{{ formError }}</p>

    <p v-if="isLoading" data-testid="invoices-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="invoices-error">Failed to load invoices.</p>
    <table v-else class="grid" data-testid="invoices-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="inv in invoices" :key="inv.id" :data-testid="`invoice-row-${inv.id}`">
          <td :data-testid="`invoice-name-${inv.id}`">{{ inv.name }}</td>
          <td class="status-cell">
            <span
              v-if="isNoneInvoice(inv)"
              class="muted"
              :data-testid="`invoice-status-${inv.id}`"
            >—</span>
            <template v-else>
              <select
                class="status-select"
                :value="toApiStatus(inv.status)"
                :disabled="savingId === inv.id"
                :data-testid="`invoice-status-${inv.id}`"
                :aria-label="`Status for ${inv.name}`"
                @change="onStatusChange(inv, ($event.target as HTMLSelectElement).value)"
              >
                <option v-for="opt in STATUS_OPTIONS" :key="opt.value" :value="opt.value">
                  {{ opt.label }}
                </option>
              </select>
              <span
                v-if="statusErrors[inv.id]"
                class="error inline"
                :data-testid="`invoice-status-error-${inv.id}`"
              >{{ statusErrors[inv.id] }}</span>
            </template>
          </td>
        </tr>
        <tr v-if="invoices && invoices.length === 0">
          <td colspan="2" data-testid="invoices-empty">No invoices yet.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.row { display: flex; gap: 0.5rem; margin-bottom: 1rem; flex-wrap: wrap; }
input {
  padding: 0.5rem 0.7rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  min-width: 14rem;
}
button {
  padding: 0.5rem 0.9rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
button:disabled { opacity: 0.6; cursor: default; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #eee; }
.status-cell { min-width: 12rem; }
.status-select {
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  background: #fff;
}
.error { color: #dc2626; }
.error.inline { display: block; margin-top: 0.25rem; font-size: 0.85rem; }
.muted { color: #9ca3af; }
</style>
