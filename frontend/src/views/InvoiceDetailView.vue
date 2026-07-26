<script setup lang="ts">
import { computed, ref } from 'vue'
import { useInvoices, useUpdateInvoice } from '../queries/invoices'
import { useTasks, useUpdateTaskDiscount } from '../queries/tasks'
import type { InvoiceStatus, WorkTask } from '../api/types'

const props = defineProps<{ id: string }>()

const { data: invoices, isLoading: invoicesLoading, error: invoicesError } = useInvoices()
const invoice = computed(() => invoices.value?.find((i) => i.id === props.id))
const updateInvoice = useUpdateInvoice()

const taskFilters = computed(() => ({
  invoiceLabel: invoice.value?.name,
}))
const tasksEnabled = computed(() => !!invoice.value?.name)
const { data: tasks, isLoading: tasksLoading, error: tasksError } = useTasks(taskFilters, tasksEnabled)
const updateDiscount = useUpdateTaskDiscount(taskFilters)

const savingDiscountId = ref<string | null>(null)
const discountErrors = ref<Record<string, string>>({})
const savingRate = ref(false)
const rateError = ref('')

function toApiStatus(status: string): InvoiceStatus {
  const s = status.trim().toLowerCase().replaceAll(' ', '-').replaceAll('_', '-')
  if (s === 'sent') return 'sent'
  if (s === 'partially-paid' || s === 'partiallypaid') return 'partially-paid'
  if (s === 'fully-paid' || s === 'fullypaid') return 'fully-paid'
  return 'preparing'
}

interface LineRow {
  task: WorkTask
  hours: number
  rate: number
  discountPercent: number
  subtotal: number
  isFlatFee: boolean
}

interface ClientGroup {
  clientId: string
  clientName: string
  rows: LineRow[]
  hours: number
  subtotal: number
}

function compareDate(a: string | null, b: string | null) {
  if (!a && !b) return 0
  if (!a) return 1
  if (!b) return -1
  return a.localeCompare(b)
}

function lineSubtotal(units: number, rate: number, discountPercent: number) {
  return units * rate * (1 - discountPercent / 100)
}

const invoiceRate = computed(() => invoice.value?.effectiveRate ?? null)

const clientGroups = computed((): ClientGroup[] => {
  const list = tasks.value ?? []
  const hourlyRate = invoiceRate.value

  const byClient = new Map<string, WorkTask[]>()
  for (const t of list) {
    const key = t.clientId
    const bucket = byClient.get(key)
    if (bucket) bucket.push(t)
    else byClient.set(key, [t])
  }

  const groups: ClientGroup[] = []
  for (const [clientId, clientTasks] of byClient) {
    const sorted = clientTasks.slice().sort((a, b) => {
      const projectCmp = (a.projectName ?? '\uffff').localeCompare(b.projectName ?? '\uffff')
      if (projectCmp !== 0) return projectCmp
      return compareDate(a.dateDone, b.dateDone)
    })
    const rows: LineRow[] = []
    for (const task of sorted) {
      const discountPercent = task.discountPercent ?? 0
      if (task.flatFee != null) {
        rows.push({
          task,
          hours: 1,
          rate: task.flatFee,
          discountPercent,
          subtotal: lineSubtotal(1, task.flatFee, discountPercent),
          isFlatFee: true,
        })
        continue
      }
      if (hourlyRate == null) continue
      const hours = task.billableHours ?? 0
      rows.push({
        task,
        hours,
        rate: hourlyRate,
        discountPercent,
        subtotal: lineSubtotal(hours, hourlyRate, discountPercent),
        isFlatFee: false,
      })
    }
    if (rows.length === 0) continue
    const hours = rows.reduce((sum, r) => sum + r.hours, 0)
    const subtotal = rows.reduce((sum, r) => sum + r.subtotal, 0)
    groups.push({
      clientId,
      clientName: clientTasks[0]?.clientName ?? 'Unknown',
      rows,
      hours,
      subtotal,
    })
  }

  return groups.sort((a, b) => a.clientName.localeCompare(b.clientName))
})

const grandHours = computed(() => clientGroups.value.reduce((sum, g) => sum + g.hours, 0))
const grandTotal = computed(() => {
  if (clientGroups.value.length === 0) return null
  return clientGroups.value.reduce((sum, g) => sum + g.subtotal, 0)
})

const money = new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' })
const hoursFmt = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 })

function formatMoney(n: number | null) {
  if (n == null) return '—'
  return money.format(n)
}

function formatHours(n: number) {
  return hoursFmt.format(n)
}

function formatRate(n: number | null) {
  if (n == null) return '—'
  return money.format(n)
}

function parseDiscount(raw: string): number | undefined {
  const trimmed = raw.trim()
  if (!trimmed) return 0
  const n = Number(trimmed)
  if (!Number.isFinite(n) || n < 0 || n > 100) return undefined
  return n
}

function parseRate(raw: string): number | null | undefined {
  const trimmed = raw.trim()
  if (!trimmed) return null
  const n = Number(trimmed)
  if (!Number.isFinite(n) || n < 0) return undefined
  return n
}

async function onRateChange(raw: string) {
  const inv = invoice.value
  if (!inv) return
  const rate = parseRate(raw)
  if (rate === undefined) {
    rateError.value = 'Rate must be a non-negative number.'
    return
  }
  if ((inv.rate ?? null) === rate) {
    rateError.value = ''
    return
  }
  savingRate.value = true
  rateError.value = ''
  try {
    await updateInvoice.mutateAsync({
      id: inv.id,
      name: inv.name,
      status: toApiStatus(inv.status),
      isDefault: !!inv.isDefault,
      rate,
    })
  } catch (e: any) {
    rateError.value = e?.response?.data?.error ?? 'Could not update rate.'
  } finally {
    savingRate.value = false
  }
}

async function onDiscountChange(task: WorkTask, raw: string) {
  const discountPercent = parseDiscount(raw)
  if (discountPercent === undefined) {
    discountErrors.value[task.id] = 'Discount must be 0–100.'
    return
  }
  if ((task.discountPercent ?? 0) === discountPercent) {
    delete discountErrors.value[task.id]
    return
  }
  savingDiscountId.value = task.id
  delete discountErrors.value[task.id]
  try {
    await updateDiscount.mutateAsync({ id: task.id, discountPercent })
  } catch (e: any) {
    discountErrors.value[task.id] = e?.response?.data?.error ?? 'Could not update discount.'
  } finally {
    savingDiscountId.value = null
  }
}
</script>

<template>
  <section data-testid="invoice-detail-view">
    <p><RouterLink to="/invoices" data-testid="invoice-detail-back">← Invoices</RouterLink></p>

    <p v-if="invoicesLoading" data-testid="invoice-detail-loading">Loading…</p>
    <p v-else-if="invoicesError" class="error" data-testid="invoice-detail-error">Failed to load invoice.</p>
    <p v-else-if="!invoice" data-testid="invoice-not-found">Invoice not found.</p>

    <template v-else>
      <h1 data-testid="invoice-detail-name">{{ invoice.name }}</h1>
      <p class="meta" data-testid="invoice-detail-meta">
        Status: {{ invoice.status }}
      </p>
      <div class="rate-row" data-testid="invoice-detail-rate-row">
        <label class="rate-label" for="invoice-detail-rate">Hourly rate</label>
        <input
          id="invoice-detail-rate"
          type="number"
          step="0.01"
          min="0"
          class="rate-input"
          :value="invoice.rate ?? ''"
          :placeholder="String(invoice.effectiveRate)"
          :disabled="savingRate"
          data-testid="invoice-detail-rate"
          aria-label="Invoice hourly rate"
          @blur="onRateChange(($event.target as HTMLInputElement).value)"
        />
        <span
          v-if="invoice.rate == null"
          class="muted rate-hint"
          data-testid="invoice-detail-rate-default-hint"
        >Using default ({{ formatRate(invoice.effectiveRate) }})</span>
        <span
          v-if="rateError"
          class="error inline"
          data-testid="invoice-detail-rate-error"
        >{{ rateError }}</span>
      </div>

      <p v-if="tasksLoading" data-testid="invoice-detail-tasks-loading">Loading tasks…</p>
      <p v-else-if="tasksError" class="error" data-testid="invoice-detail-tasks-error">Failed to load tasks.</p>
      <template v-else-if="tasksEnabled">
        <p v-if="clientGroups.length === 0" class="muted" data-testid="invoice-detail-empty">
          No tasks assigned to this invoice.
        </p>

        <table
          v-else
          class="grid"
          data-testid="invoice-detail-table"
        >
          <thead>
            <tr>
              <th>Project</th>
              <th>Task</th>
              <th class="num">Hours</th>
              <th class="num">Rate</th>
              <th class="num">Discount %</th>
              <th class="num">Subtotal</th>
            </tr>
          </thead>
          <tbody
            v-for="group in clientGroups"
            :key="group.clientId"
            :data-testid="`invoice-client-group-${group.clientId}`"
          >
            <tr class="client-header">
              <th
                colspan="6"
                :data-testid="`invoice-client-name-${group.clientId}`"
              >{{ group.clientName }}</th>
            </tr>
            <tr
              v-for="row in group.rows"
              :key="row.task.id"
              :data-testid="`invoice-task-row-${row.task.id}`"
            >
              <td :data-testid="`invoice-task-project-${row.task.id}`">{{ row.task.projectName ?? '—' }}</td>
              <td :data-testid="`invoice-task-title-${row.task.id}`">{{ row.task.title }}</td>
              <td class="num" :data-testid="`invoice-task-hours-${row.task.id}`">{{ formatHours(row.hours) }}</td>
              <td class="num" :data-testid="`invoice-task-rate-${row.task.id}`">
                {{ formatRate(row.rate) }}<span v-if="row.isFlatFee" class="muted flat-fee-tag"> flat</span>
              </td>
              <td class="num discount-cell">
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  class="discount-input"
                  :value="row.discountPercent"
                  :disabled="savingDiscountId === row.task.id"
                  :data-testid="`invoice-task-discount-${row.task.id}`"
                  :aria-label="`Discount for ${row.task.title}`"
                  @blur="onDiscountChange(row.task, ($event.target as HTMLInputElement).value)"
                />
                <span
                  v-if="discountErrors[row.task.id]"
                  class="error inline"
                  :data-testid="`invoice-task-discount-error-${row.task.id}`"
                >{{ discountErrors[row.task.id] }}</span>
              </td>
              <td class="num" :data-testid="`invoice-task-subtotal-${row.task.id}`">{{ formatMoney(row.subtotal) }}</td>
            </tr>
            <tr class="group-subtotal" :data-testid="`invoice-client-subtotal-${group.clientId}`">
              <td colspan="2">Client subtotal</td>
              <td class="num">{{ formatHours(group.hours) }}</td>
              <td class="num"></td>
              <td class="num"></td>
              <td class="num">{{ formatMoney(group.subtotal) }}</td>
            </tr>
          </tbody>
          <tfoot data-testid="invoice-grand-total">
            <tr class="grand">
              <td colspan="2">Grand total</td>
              <td class="num">{{ formatHours(grandHours) }}</td>
              <td class="num"></td>
              <td class="num"></td>
              <td class="num">{{ formatMoney(grandTotal) }}</td>
            </tr>
          </tfoot>
        </table>
      </template>
    </template>
  </section>
</template>

<style scoped>
.meta, .muted { color: #6b7280; font-size: 0.9rem; }
.error { color: #dc2626; }
.error.inline { display: block; margin-top: 0.25rem; font-size: 0.85rem; }
.rate-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  margin: 0.75rem 0 1.25rem;
}
.rate-label { font-weight: 600; }
.rate-input {
  width: 6rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  text-align: right;
}
.rate-hint { font-size: 0.85rem; }
.flat-fee-tag { font-size: 0.8rem; margin-left: 0.25rem; }
.grid { width: 100%; border-collapse: collapse; table-layout: fixed; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #eee; }
.grid th.num, .grid td.num { text-align: right; font-variant-numeric: tabular-nums; }
.grid thead th { font-weight: 600; border-bottom: 2px solid #e5e7eb; }
.client-header th {
  padding-top: 1.25rem;
  font-size: 1.1rem;
  font-weight: 600;
  border-bottom: 1px solid #e5e7eb;
  background: transparent;
}
.discount-cell { width: 6.5rem; }
.discount-input {
  width: 4.5rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  text-align: right;
}
.group-subtotal td { font-weight: 600; border-bottom: none; padding-top: 0.75rem; }
.grand td { font-weight: 700; border-bottom: none; border-top: 2px solid #e5e7eb; font-size: 1.05rem; padding-top: 1rem; }
a { color: #10b981; }
</style>
