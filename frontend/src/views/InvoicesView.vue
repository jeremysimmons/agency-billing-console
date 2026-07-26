<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useInvoices, useCreateInvoice, useUpdateInvoice, useReorderInvoices } from '../queries/invoices'
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
const reorderInvoices = useReorderInvoices()

const name = ref('')
const formError = ref('')
const statusErrors = ref<Record<string, string>>({})
const defaultErrors = ref<Record<string, string>>({})
const savingId = ref<string | null>(null)
const savingDefaultId = ref<string | null>(null)
const reorderError = ref('')
const localOrder = ref<Invoice[]>([])
const draggingId = ref<string | null>(null)
const dragOverId = ref<string | null>(null)

watch(
  invoices,
  (list) => {
    if (!list) {
      localOrder.value = []
      return
    }
    localOrder.value = list
      .slice()
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name))
  },
  { immediate: true },
)

const rows = computed(() => localOrder.value)

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

function canBeDefault(inv: Invoice) {
  return !isNoneInvoice(inv) && statusKey(inv.status) === 'preparing'
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
    await updateInvoice.mutateAsync({
      id: inv.id,
      name: inv.name,
      status,
      isDefault: status === 'preparing' ? !!inv.isDefault : false,
    })
  } catch (e: any) {
    statusErrors.value[inv.id] = e?.response?.data?.error ?? 'Could not update status.'
  } finally {
    savingId.value = null
  }
}

async function onDefaultChange(inv: Invoice, checked: boolean) {
  if (!canBeDefault(inv) && checked) return
  if (!!inv.isDefault === checked) return
  savingDefaultId.value = inv.id
  delete defaultErrors.value[inv.id]
  try {
    await updateInvoice.mutateAsync({
      id: inv.id,
      name: inv.name,
      status: toApiStatus(inv.status),
      isDefault: checked,
    })
  } catch (e: any) {
    defaultErrors.value[inv.id] = e?.response?.data?.error ?? 'Could not update default.'
  } finally {
    savingDefaultId.value = null
  }
}

function onDragStart(id: string, event: DragEvent) {
  draggingId.value = id
  event.dataTransfer?.setData('text/plain', id)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onDragOver(id: string, event: DragEvent) {
  if (!draggingId.value) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  dragOverId.value = id
}

function onDragLeave(id: string) {
  if (dragOverId.value === id) dragOverId.value = null
}

async function onDrop(targetId: string, event: DragEvent) {
  event.preventDefault()
  const fromId = event.dataTransfer?.getData('text/plain') || draggingId.value
  draggingId.value = null
  dragOverId.value = null
  if (!fromId || fromId === targetId) return

  const order = localOrder.value.map((i) => i.id)
  const fromIdx = order.indexOf(fromId)
  const toIdx = order.indexOf(targetId)
  if (fromIdx < 0 || toIdx < 0) return
  order.splice(fromIdx, 1)
  order.splice(toIdx, 0, fromId)

  const byId = new Map(localOrder.value.map((i) => [i.id, i]))
  localOrder.value = order.map((id, i) => {
    const inv = byId.get(id)!
    return { ...inv, sortOrder: i }
  })

  reorderError.value = ''
  try {
    await reorderInvoices.mutateAsync(order)
  } catch (e: any) {
    reorderError.value = e?.response?.data?.error ?? 'Could not save invoice order.'
  }
}

function onDragEnd() {
  draggingId.value = null
  dragOverId.value = null
}
</script>

<template>
  <section data-testid="invoices-view">
    <h1>Invoices</h1>
    <p class="hint">
      Drag rows to set the order used on the Tasks invoice dropdown.
      Only one preparing invoice can be the default; it is assigned when a project is set on a billable task.
    </p>

    <form class="row" data-testid="invoice-create-form" @submit.prevent="add">
      <input v-model="name" placeholder="Invoice name" required data-testid="invoice-create-name" />
      <button :disabled="createInvoice.isLoading.value || !name.trim()" data-testid="invoice-create-submit">
        Add invoice
      </button>
    </form>
    <p v-if="formError" class="error" data-testid="invoice-create-error">{{ formError }}</p>
    <p v-if="reorderError" class="error" data-testid="invoice-reorder-error">{{ reorderError }}</p>

    <p v-if="isLoading" data-testid="invoices-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="invoices-error">Failed to load invoices.</p>
    <table v-else class="grid" data-testid="invoices-table">
      <thead>
        <tr>
          <th class="drag-col" aria-label="Reorder"></th>
          <th>Name</th>
          <th>Status</th>
          <th>Default</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="inv in rows"
          :key="inv.id"
          class="invoice-row"
          :class="{
            'invoice-row--dragging': draggingId === inv.id,
            'invoice-row--drag-over': dragOverId === inv.id && draggingId !== inv.id,
          }"
          :data-testid="`invoice-row-${inv.id}`"
          @dragover="onDragOver(inv.id, $event)"
          @dragleave="onDragLeave(inv.id)"
          @drop="onDrop(inv.id, $event)"
        >
          <td class="drag-col">
            <span
              class="drag-handle"
              draggable="true"
              title="Drag to reorder"
              :data-testid="`invoice-drag-${inv.id}`"
              @dragstart="onDragStart(inv.id, $event)"
              @dragend="onDragEnd"
            >⠿</span>
          </td>
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
          <td class="default-cell">
            <span
              v-if="isNoneInvoice(inv)"
              class="muted"
              :data-testid="`invoice-default-${inv.id}`"
            >—</span>
            <template v-else>
              <input
                type="checkbox"
                :checked="!!inv.isDefault"
                :disabled="savingDefaultId === inv.id || (!canBeDefault(inv) && !inv.isDefault)"
                :data-testid="`invoice-default-${inv.id}`"
                :aria-label="`Default invoice ${inv.name}`"
                @change="onDefaultChange(inv, ($event.target as HTMLInputElement).checked)"
              />
              <span
                v-if="defaultErrors[inv.id]"
                class="error inline"
                :data-testid="`invoice-default-error-${inv.id}`"
              >{{ defaultErrors[inv.id] }}</span>
            </template>
          </td>
        </tr>
        <tr v-if="rows.length === 0">
          <td colspan="4" data-testid="invoices-empty">No invoices yet.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.hint { color: #6b7280; font-size: 0.9rem; margin: -0.35rem 0 1rem; }
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
.drag-col { width: 1%; white-space: nowrap; }
.drag-handle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  color: #9ca3af;
  cursor: grab;
  user-select: none;
}
.drag-handle:active { cursor: grabbing; }
.invoice-row--dragging { opacity: 0.55; }
.invoice-row--drag-over td {
  box-shadow: inset 0 2px 0 #059669;
}
.status-cell { min-width: 12rem; }
.default-cell { width: 1%; white-space: nowrap; vertical-align: middle; }
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
