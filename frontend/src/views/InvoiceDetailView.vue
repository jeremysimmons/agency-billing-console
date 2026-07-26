<script setup lang="ts">
import { computed, ref } from 'vue'
import { useInvoices, useUpdateInvoice } from '../queries/invoices'
import { useTasks, useUpdateTaskDiscount } from '../queries/tasks'
import type { IncludeNonBillableTasks, InvoiceStatus, WorkTask } from '../api/types'

const props = defineProps<{ id: string }>()

const INCLUDE_NON_BILLABLE_OPTIONS: { value: IncludeNonBillableTasks; label: string }[] = [
  { value: 'none', label: 'None' },
  { value: 'detail', label: 'Detail' },
  { value: 'summary', label: 'Summary' },
]

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
const savingIncludeNonBillable = ref(false)
const includeNonBillableError = ref('')

function toApiStatus(status: string): InvoiceStatus {
  const s = status.trim().toLowerCase().replaceAll(' ', '-').replaceAll('_', '-')
  if (s === 'sent') return 'sent'
  if (s === 'partially-paid' || s === 'partiallypaid') return 'partially-paid'
  if (s === 'fully-paid' || s === 'fullypaid') return 'fully-paid'
  return 'preparing'
}

function toIncludeNonBillable(value: string | null | undefined): IncludeNonBillableTasks {
  const v = (value ?? '').trim().toLowerCase()
  if (v === 'detail' || v === 'summary') return v
  return 'none'
}

function isNonBillableTask(task: WorkTask) {
  return (task.bill ?? '').trim().toLowerCase() === 'no'
}

interface LineRow {
  key: string
  task: WorkTask | null
  projectName: string | null
  title: string
  hours: number
  rate: number
  discountPercent: number
  subtotal: number
  isFlatFee: boolean
  isNonBillable: boolean
  allowDiscount: boolean
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
const includeMode = computed(() => toIncludeNonBillable(invoice.value?.includeNonBillableTasks))

const clientGroups = computed((): ClientGroup[] => {
  const list = tasks.value ?? []
  const hourlyRate = invoiceRate.value
  const mode = includeMode.value

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

    const billable = sorted.filter((t) => !isNonBillableTask(t))
    const nonBillable = sorted.filter((t) => isNonBillableTask(t))

    const rows: LineRow[] = []
    for (const task of billable) {
      const discountPercent = task.discountPercent ?? 0
      if (task.flatFee != null) {
        rows.push({
          key: task.id,
          task,
          projectName: task.projectName,
          title: task.title,
          hours: 1,
          rate: task.flatFee,
          discountPercent,
          subtotal: lineSubtotal(1, task.flatFee, discountPercent),
          isFlatFee: true,
          isNonBillable: false,
          allowDiscount: true,
        })
        continue
      }
      if (hourlyRate == null) continue
      const hours = task.billableHours ?? 0
      rows.push({
        key: task.id,
        task,
        projectName: task.projectName,
        title: task.title,
        hours,
        rate: hourlyRate,
        discountPercent,
        subtotal: lineSubtotal(hours, hourlyRate, discountPercent),
        isFlatFee: false,
        isNonBillable: false,
        allowDiscount: true,
      })
    }

    if (mode === 'detail') {
      for (const task of nonBillable) {
        const hours = task.nonBillableHours ?? 0
        rows.push({
          key: task.id,
          task,
          projectName: task.projectName,
          title: task.title,
          hours,
          rate: 0,
          discountPercent: 0,
          subtotal: 0,
          isFlatFee: false,
          isNonBillable: true,
          allowDiscount: false,
        })
      }
    } else if (mode === 'summary' && nonBillable.length > 0) {
      const hours = nonBillable.reduce((sum, t) => sum + (t.nonBillableHours ?? 0), 0)
      const count = nonBillable.length
      rows.push({
        key: `non-billable-summary-${clientId}`,
        task: null,
        projectName: null,
        title: `${count} non-billable task${count === 1 ? '' : 's'}`,
        hours,
        rate: 0,
        discountPercent: 0,
        subtotal: 0,
        isFlatFee: false,
        isNonBillable: true,
        allowDiscount: false,
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

async function persistInvoice(patch: {
  rate?: number | null
  includeNonBillableTasks?: IncludeNonBillableTasks
}) {
  const inv = invoice.value
  if (!inv) return
  await updateInvoice.mutateAsync({
    id: inv.id,
    name: inv.name,
    status: toApiStatus(inv.status),
    isDefault: !!inv.isDefault,
    rate: patch.rate !== undefined ? patch.rate : (inv.rate ?? null),
    includeNonBillableTasks: patch.includeNonBillableTasks
      ?? toIncludeNonBillable(inv.includeNonBillableTasks),
  })
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
    await persistInvoice({ rate })
  } catch (e: any) {
    rateError.value = e?.response?.data?.error ?? 'Could not update rate.'
  } finally {
    savingRate.value = false
  }
}

async function onIncludeNonBillableChange(value: string) {
  const inv = invoice.value
  if (!inv) return
  const mode = toIncludeNonBillable(value)
  if (toIncludeNonBillable(inv.includeNonBillableTasks) === mode) return
  savingIncludeNonBillable.value = true
  includeNonBillableError.value = ''
  try {
    await persistInvoice({ includeNonBillableTasks: mode })
  } catch (e: any) {
    includeNonBillableError.value = e?.response?.data?.error ?? 'Could not update setting.'
  } finally {
    savingIncludeNonBillable.value = false
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
      <div class="settings-row" data-testid="invoice-detail-settings">
        <div class="setting" data-testid="invoice-detail-rate-row">
          <label class="setting-label" for="invoice-detail-rate">Hourly rate</label>
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
            class="muted setting-hint"
            data-testid="invoice-detail-rate-default-hint"
          >Using default ({{ formatRate(invoice.effectiveRate) }})</span>
          <span
            v-if="rateError"
            class="error inline"
            data-testid="invoice-detail-rate-error"
          >{{ rateError }}</span>
        </div>
        <div class="setting" data-testid="invoice-detail-include-non-billable-row">
          <label class="setting-label" for="invoice-detail-include-non-billable">Include Non-Billable Tasks</label>
          <select
            id="invoice-detail-include-non-billable"
            class="setting-select"
            :value="includeMode"
            :disabled="savingIncludeNonBillable"
            data-testid="invoice-detail-include-non-billable"
            aria-label="Include non-billable tasks"
            @change="onIncludeNonBillableChange(($event.target as HTMLSelectElement).value)"
          >
            <option
              v-for="opt in INCLUDE_NON_BILLABLE_OPTIONS"
              :key="opt.value"
              :value="opt.value"
            >{{ opt.label }}</option>
          </select>
          <span
            v-if="includeNonBillableError"
            class="error inline"
            data-testid="invoice-detail-include-non-billable-error"
          >{{ includeNonBillableError }}</span>
        </div>
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
              :key="row.key"
              :class="{ 'non-billable-row': row.isNonBillable }"
              :data-testid="row.task ? `invoice-task-row-${row.task.id}` : `invoice-non-billable-summary-${group.clientId}`"
            >
              <td :data-testid="row.task ? `invoice-task-project-${row.task.id}` : undefined">
                {{ row.projectName ?? '—' }}
              </td>
              <td :data-testid="row.task ? `invoice-task-title-${row.task.id}` : undefined">
                <template v-if="row.task">
                  <RouterLink
                    v-if="row.task.clickUpTaskId"
                    :to="{
                      path: '/tasks',
                      query: {
                        clickUpId: row.task.clickUpTaskId,
                        missingOnly: 'false',
                        invoiced: ['paid', 'pending', 'none'],
                      },
                    }"
                    :data-testid="`invoice-task-title-link-${row.task.id}`"
                  >{{ row.title }}</RouterLink>
                  <template v-else>{{ row.title }}</template>
                </template>
                <template v-else>{{ row.title }}</template>
              </td>
              <td class="num" :data-testid="row.task ? `invoice-task-hours-${row.task.id}` : undefined">
                {{ formatHours(row.hours) }}
              </td>
              <td class="num" :data-testid="row.task ? `invoice-task-rate-${row.task.id}` : undefined">
                {{ formatRate(row.rate) }}<span v-if="row.isFlatFee" class="muted flat-fee-tag"> flat</span>
              </td>
              <td class="num discount-cell">
                <template v-if="row.allowDiscount && row.task">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="100"
                    class="discount-input"
                    :value="row.discountPercent"
                    :disabled="savingDiscountId === row.task.id"
                    :data-testid="`invoice-task-discount-${row.task.id}`"
                    :aria-label="`Discount for ${row.title}`"
                    @blur="onDiscountChange(row.task, ($event.target as HTMLInputElement).value)"
                  />
                  <span
                    v-if="discountErrors[row.task.id]"
                    class="error inline"
                    :data-testid="`invoice-task-discount-error-${row.task.id}`"
                  >{{ discountErrors[row.task.id] }}</span>
                </template>
                <span v-else class="muted">—</span>
              </td>
              <td class="num" :data-testid="row.task ? `invoice-task-subtotal-${row.task.id}` : undefined">
                {{ formatMoney(row.subtotal) }}
              </td>
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
.settings-row {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem 2rem;
  margin: 0.75rem 0 1.25rem;
}
.setting {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
}
.setting-label { font-weight: 600; }
.rate-input {
  width: 6rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  text-align: right;
}
.setting-select {
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  background: #fff;
}
.setting-hint { font-size: 0.85rem; }
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
