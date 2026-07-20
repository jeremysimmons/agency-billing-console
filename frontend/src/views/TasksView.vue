<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import ToggleSwitch from 'primevue/toggleswitch'
import { useClients } from '../queries/clients'
import { useProjects } from '../queries/projects'
import { useTasks, useTaskSummary, useTaskFilterOptions, useUpdateTaskPrep } from '../queries/tasks'
import type { WorkTask } from '../api/types'

const route = useRoute()
const viewMode = ref<'list' | 'clients' | 'months'>('list')
const clientFilter = ref<string>((route.query.clientId as string) || '')
const missingOnly = ref(true)
const invoicedFilter = ref<'all' | 'yes' | 'no'>('no')
const showListColumn = ref(false)
const showProjectColumn = ref(false)
const showInvoiceColumn = ref(false)
const projectFilter = ref('')
const createdMonthFilter = ref('')
const doneMonthFilter = ref('')
const statusFilters = ref<string[]>([])
const clientId = computed(() => clientFilter.value || undefined)

const taskFilters = computed(() => ({
  clientId: clientId.value,
  missingOnly: missingOnly.value,
  invoiced: invoicedFilter.value,
  projectFilter: projectFilter.value || undefined,
  createdMonth: createdMonthFilter.value || undefined,
  doneMonth: doneMonthFilter.value || undefined,
  statuses: statusFilters.value.length ? statusFilters.value : undefined,
}))

const { data: clients } = useClients()
const { data: filterProjects } = useProjects(clientId)
const { data: filterOptions } = useTaskFilterOptions(clientId)
const { data: tasks, isLoading, error } = useTasks(taskFilters)
const { data: taskSummary, isLoading: summaryLoading, error: summaryError } = useTaskSummary(
  taskFilters,
  () => viewMode.value !== 'list',
)
const clientCounts = computed(() => taskSummary.value?.byClient ?? [])
const monthCounts = computed(() => taskSummary.value?.byDoneMonth ?? [])
const updatePrep = useUpdateTaskPrep()

const editingId = ref<string | null>(null)
const draft = ref({
  projectId: '' as string,
  bill: '' as string,
  billableHours: '' as string,
  nonBillableHours: '' as string,
  invoiceLabel: '' as string,
  note: '' as string,
})
const saveError = ref('')

const editClientId = computed(() => {
  const t = tasks.value?.find((x) => x.id === editingId.value)
  return t?.clientId
})
const { data: projects } = useProjects(editClientId)

const missingCount = computed(() => tasks.value?.filter((t) => t.needsAttention).length ?? 0)
const summaryTotals = computed(() => ({
  tasks: clientCounts.value.reduce((sum, row) => sum + row.taskCount, 0),
  missing: clientCounts.value.reduce((sum, row) => sum + row.missingCount, 0),
  uninvoiced: clientCounts.value.reduce((sum, row) => sum + row.uninvoicedCount, 0),
}))
const monthTotals = computed(() => ({
  tasks: monthCounts.value.reduce((sum, row) => sum + row.taskCount, 0),
  missing: monthCounts.value.reduce((sum, row) => sum + row.missingCount, 0),
  uninvoiced: monthCounts.value.reduce((sum, row) => sum + row.uninvoicedCount, 0),
}))
const hasClientSummary = computed(() => clientCounts.value.length > 0)
const hasMonthSummary = computed(() => monthCounts.value.length > 0)
const showInvoice = computed(() => {
  if (invoicedFilter.value === 'yes') return true
  if (invoicedFilter.value === 'no') return false
  return showInvoiceColumn.value
})
const editColspan = computed(() =>
  10
  + (showListColumn.value ? 1 : 0)
  + (showProjectColumn.value ? 1 : 0)
  + (showInvoice.value ? 1 : 0))

const missingBadgeLegend = [
  { key: 'B', label: 'Bill' },
  { key: 'H', label: 'Hours' },
] as const

function hasMissingHours(t: WorkTask) {
  if (t.bill?.toLowerCase() !== 'yes') return false
  const eitherPopulated = t.billableHours != null || t.nonBillableHours != null
  const anyPositive = (t.billableHours ?? 0) > 0 || (t.nonBillableHours ?? 0) > 0
  return !(eitherPopulated && anyPositive)
}

function isTaskMissing(t: WorkTask, key: string) {
  switch (key) {
    case 'B':
      return !t.bill?.trim()
    case 'H':
      return hasMissingHours(t)
    default:
      return false
  }
}

function startEdit(t: WorkTask) {
  editingId.value = t.id
  draft.value = {
    projectId: t.projectId ?? '',
    bill: t.bill ?? '',
    billableHours: t.billableHours != null ? String(t.billableHours) : '',
    nonBillableHours: t.nonBillableHours != null ? String(t.nonBillableHours) : '',
    invoiceLabel: t.invoiceLabel ?? '',
    note: t.note ?? '',
  }
  saveError.value = ''
}

function cancelEdit() {
  editingId.value = null
  saveError.value = ''
}

function parseHours(v: string): number | null {
  const s = v.trim()
  if (!s) return null
  const n = Number(s)
  return Number.isFinite(n) ? n : null
}

async function saveEdit() {
  if (!editingId.value) return
  saveError.value = ''
  try {
    await updatePrep.mutateAsync({
      id: editingId.value,
      input: {
        projectId: draft.value.projectId || null,
        bill: draft.value.bill.trim() || null,
        billableHours: parseHours(draft.value.billableHours),
        nonBillableHours: parseHours(draft.value.nonBillableHours),
        invoiceLabel: draft.value.invoiceLabel.trim() || null,
        note: draft.value.note.trim() || null,
      },
    })
    editingId.value = null
  } catch (e: any) {
    saveError.value = e?.response?.data?.error ?? 'Could not save.'
  }
}

watch(viewMode, () => { editingId.value = null })
watch(clientFilter, () => {
  editingId.value = null
  projectFilter.value = ''
  createdMonthFilter.value = ''
  doneMonthFilter.value = ''
})
watch(
  () => filterOptions.value?.statuses,
  (statuses) => {
    statusFilters.value = statuses?.length ? [...statuses] : []
  },
  { immediate: true },
)
watch(missingOnly, () => { editingId.value = null })
watch(invoicedFilter, (value) => {
  editingId.value = null
  showInvoiceColumn.value = value === 'yes'
})
watch([createdMonthFilter, doneMonthFilter], () => { editingId.value = null })
watch(statusFilters, () => { editingId.value = null }, { deep: true })

function stripTrailingUrl(title: string) {
  const match = title.match(/^(.*)(?:\s*[-–—:]\s*|\s+)(https?:\/\/\S+)\/?\s*$/i)
  if (!match) return title
  const cleaned = match[1].trimEnd()
  return cleaned || title
}

function trimTitleSuffix(title: string) {
  return title.replace(/[\s\-–—]+$/, '')
}

function displayTitle(title: string) {
  const cleaned = trimTitleSuffix(stripTrailingUrl(title))
  return cleaned.length > 100 ? `${trimTitleSuffix(cleaned.slice(0, 100))}…` : cleaned
}

function formatDate(value: string | null) {
  if (!value) return '—'
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString()
}

function formatDateTime(value: string | null) {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toLocaleString()
}

function formatMonthYear(value: string) {
  const [year, month] = value.split('-').map(Number)
  if (!year || !month) return value
  return new Date(year, month - 1, 1).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })
}

function statusSlug(status: string) {
  return status.toLowerCase().replace(/\s+/g, '-')
}

function openClient(clientId: string) {
  clientFilter.value = clientId
  viewMode.value = 'list'
}

function openDoneMonth(month: string) {
  doneMonthFilter.value = month
  viewMode.value = 'list'
}
</script>

<template>
  <section class="tasks-view" data-testid="tasks-view">
    <div class="header">
      <h1>Tasks</h1>
      <div class="view-toggle" data-testid="tasks-view-toggle">
        <button
          type="button"
          :class="{ active: viewMode === 'list' }"
          data-testid="tasks-view-list"
          @click="viewMode = 'list'"
        >List</button>
        <button
          type="button"
          :class="{ active: viewMode === 'clients' }"
          data-testid="tasks-view-clients"
          @click="viewMode = 'clients'"
        >By client</button>
        <button
          type="button"
          :class="{ active: viewMode === 'months' }"
          data-testid="tasks-view-months"
          @click="viewMode = 'months'"
        >By month</button>
      </div>
      <span v-if="viewMode === 'list' && missingOnly && tasks" class="badge" data-testid="tasks-missing-count">{{ missingCount }} need attention</span>
      <span v-else-if="viewMode === 'clients' && hasClientSummary" class="badge" data-testid="tasks-summary-count">{{ summaryTotals.tasks }} tasks · {{ summaryTotals.missing }} missing · {{ summaryTotals.uninvoiced }} uninvoiced</span>
      <span v-else-if="viewMode === 'months' && hasMonthSummary" class="badge" data-testid="tasks-month-summary-count">{{ monthTotals.tasks }} tasks · {{ monthTotals.missing }} missing · {{ monthTotals.uninvoiced }} uninvoiced</span>
    </div>

    <div v-if="viewMode === 'list'" class="missing-legend" data-testid="tasks-missing-legend">
      <span class="legend-label">Missing data</span>
      <span v-for="item in missingBadgeLegend" :key="item.key" class="legend-item">
        <span class="missing-badge" :class="`missing-${item.key}`">{{ item.key }}</span>
        {{ item.label }}
      </span>
    </div>

    <div class="filters">
      <label>
        Created
        <select v-model="createdMonthFilter" data-testid="tasks-created-month-filter">
          <option value="">All months</option>
          <option v-for="m in filterOptions?.createdMonths ?? []" :key="m" :value="m">{{ formatMonthYear(m) }}</option>
        </select>
      </label>
      <label>
        Done
        <select v-model="doneMonthFilter" data-testid="tasks-done-month-filter">
          <option value="">All months</option>
          <option v-for="m in filterOptions?.doneMonths ?? []" :key="m" :value="m">{{ formatMonthYear(m) }}</option>
        </select>
      </label>
      <label>
        Client
        <select v-model="clientFilter" data-testid="tasks-client-filter">
          <option value="">All clients</option>
          <option v-for="c in clients" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </label>
      <label>
        Project
        <select v-model="projectFilter" data-testid="tasks-project-filter" :disabled="!clientFilter">
          <option value="">All projects</option>
          <option value="__unassigned__">Unassigned</option>
          <option v-for="p in filterProjects" :key="p.id" :value="p.id">{{ p.name }}</option>
        </select>
      </label>
      <label>
        Invoiced
        <select v-model="invoicedFilter" data-testid="tasks-invoiced-filter">
          <option value="all">All</option>
          <option value="yes">Yes</option>
          <option value="no">No</option>
        </select>
      </label>
      <div class="toggle-field">
        <span id="tasks-missing-only-label" class="filter-label">Missing data only</span>
        <div class="toggle-row">
          <span class="toggle-side" :class="{ active: !missingOnly }" data-testid="tasks-missing-all-label">All</span>
          <ToggleSwitch
            v-model="missingOnly"
            aria-labelledby="tasks-missing-only-label"
            :pt="{ input: { 'data-testid': 'tasks-missing-only' } }"
          />
          <span class="toggle-side" :class="{ active: missingOnly }" data-testid="tasks-missing-missing-label">Missing</span>
        </div>
      </div>
      <div v-if="viewMode === 'list'" class="column-toggles">
        <span class="filter-label">Columns</span>
        <div class="column-checks">
          <label class="check">
            <input v-model="showListColumn" type="checkbox" data-testid="tasks-show-list-column" />
            List
          </label>
          <label class="check">
            <input v-model="showProjectColumn" type="checkbox" data-testid="tasks-show-project-column" />
            Project
          </label>
          <label class="check">
            <input
              v-model="showInvoiceColumn"
              type="checkbox"
              data-testid="tasks-show-invoice-column"
            />
            Invoice
          </label>
        </div>
      </div>
      <div class="status-filters">
        <span class="filter-label">Status</span>
        <div class="status-checks">
          <label v-for="status in filterOptions?.statuses ?? []" :key="status" class="check">
            <input
              v-model="statusFilters"
              type="checkbox"
              :value="status"
              :data-testid="`tasks-status-filter-${statusSlug(status)}`"
            />
            {{ status }}
          </label>
        </div>
      </div>
    </div>

    <template v-if="viewMode === 'list'">
    <p v-if="isLoading" data-testid="tasks-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="tasks-error">Failed to load tasks.</p>
    <p v-else-if="tasks && tasks.length === 0" class="empty" data-testid="tasks-empty">
      No tasks match. Sync from ClickUp or clear the missing-data filter.
    </p>

    <div v-else class="table-wrap">
    <table class="grid" data-testid="tasks-table">
      <thead>
        <tr>
          <th class="flags-col" aria-label="Missing data"></th>
          <th>Client</th>
          <th>Task</th>
          <th v-if="showListColumn">List</th>
          <th v-if="showProjectColumn">Project</th>
          <th>Bill</th>
          <th>Billable hours</th>
          <th>Non-billable hours</th>
          <th v-if="showInvoice">Invoice</th>
          <th>Status</th>
          <th>Created</th>
          <th>Done</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="t in tasks" :key="t.id">
          <tr
            :class="{ editing: editingId === t.id }"
            :data-testid="`task-row-${t.id}`"
          >
            <td class="flags-col" :data-testid="`task-flags-${t.id}`">
              <span v-for="item in missingBadgeLegend" :key="item.key" class="flag-slot">
                <span
                  v-if="isTaskMissing(t, item.key)"
                  class="missing-badge"
                  :class="`missing-${item.key}`"
                  :title="item.label"
                  :data-testid="`task-flag-${item.key.toLowerCase()}-${t.id}`"
                >{{ item.key }}</span>
              </span>
            </td>
            <td :data-testid="`task-client-${t.id}`">{{ t.clientName }}</td>
            <td>
              <a
                v-if="t.clickUpUrl"
                :href="t.clickUpUrl"
                target="_blank"
                rel="noopener"
                :title="t.title"
                :data-testid="`task-title-${t.id}`"
              >{{ displayTitle(t.title) }}</a>
              <span v-else :title="t.title" :data-testid="`task-title-${t.id}`">{{ displayTitle(t.title) }}</span>
            </td>
            <td v-if="showListColumn" :data-testid="`task-list-${t.id}`">{{ t.clickUpListName ?? '—' }}</td>
            <td v-if="showProjectColumn" :data-testid="`task-project-${t.id}`">{{ t.projectName ?? '—' }}</td>
            <td :data-testid="`task-bill-${t.id}`">{{ t.bill ?? '—' }}</td>
            <td :data-testid="`task-billable-hours-${t.id}`">{{ t.billableHours ?? '—' }}</td>
            <td :data-testid="`task-non-billable-hours-${t.id}`">{{ t.nonBillableHours ?? '—' }}</td>
            <td v-if="showInvoice" :data-testid="`task-invoice-${t.id}`">{{ t.invoiceLabel ?? '—' }}</td>
            <td :data-testid="`task-status-${t.id}`">{{ t.clickUpStatus ?? '—' }}</td>
            <td :title="formatDateTime(t.dateCreated)" :data-testid="`task-date-created-${t.id}`">{{ formatDate(t.dateCreated) }}</td>
            <td :title="formatDateTime(t.dateDone)" :data-testid="`task-date-done-${t.id}`">{{ formatDate(t.dateDone) }}</td>
            <td>
              <button
                v-if="editingId !== t.id"
                class="link"
                :data-testid="`task-edit-${t.id}`"
                @click="startEdit(t)"
              >Edit</button>
            </td>
          </tr>
          <tr v-if="editingId === t.id" class="edit-row" :data-testid="`task-edit-row-${t.id}`">
            <td :colspan="editColspan">
              <form class="edit-form" :data-testid="`task-edit-form-${t.id}`" @submit.prevent="saveEdit">
                <label>
                  Project
                  <select v-model="draft.projectId" :data-testid="`task-edit-project-${t.id}`">
                    <option value="">— unassigned —</option>
                    <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
                  </select>
                </label>
                <label>
                  Bill
                  <select v-model="draft.bill" :data-testid="`task-edit-bill-${t.id}`">
                    <option value="">—</option>
                    <option value="yes">yes</option>
                    <option value="no">no</option>
                  </select>
                </label>
                <label>
                  Billable hours
                  <input v-model="draft.billableHours" type="number" step="0.01" min="0" :data-testid="`task-edit-billable-hours-${t.id}`" />
                </label>
                <label>
                  Non-billable hours
                  <input v-model="draft.nonBillableHours" type="number" step="0.01" min="0" :data-testid="`task-edit-non-billable-hours-${t.id}`" />
                </label>
                <label>
                  Invoice
                  <input v-model="draft.invoiceLabel" placeholder="e.g. Aug 2025" :data-testid="`task-edit-invoice-${t.id}`" />
                </label>
                <label class="grow">
                  Note
                  <input v-model="draft.note" :data-testid="`task-edit-note-${t.id}`" />
                </label>
                <div class="edit-actions">
                  <button type="submit" :disabled="updatePrep.isLoading.value" :data-testid="`task-save-${t.id}`">Save</button>
                  <button type="button" class="link" :data-testid="`task-cancel-${t.id}`" @click="cancelEdit">Cancel</button>
                </div>
              </form>
              <p v-if="saveError" class="error" :data-testid="`task-save-error-${t.id}`">{{ saveError }}</p>
            </td>
          </tr>
        </template>
      </tbody>
    </table>
    </div>
    </template>

    <template v-else-if="viewMode === 'clients'">
    <p v-if="summaryLoading" data-testid="tasks-summary-loading">Loading…</p>
    <p v-else-if="summaryError" class="error" data-testid="tasks-summary-error">Failed to load task summary.</p>
    <p v-else-if="!hasClientSummary" class="empty" data-testid="tasks-summary-empty">
      No tasks match. Sync from ClickUp or adjust filters.
    </p>

    <div v-else class="table-wrap">
    <table class="grid summary-grid" data-testid="tasks-by-client-table">
      <thead>
        <tr>
          <th>Client</th>
          <th>Tasks</th>
          <th>Missing</th>
          <th>Uninvoiced</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in clientCounts" :key="row.clientId" :data-testid="`tasks-client-count-${row.clientId}`">
          <td>
            <button type="button" class="link" :data-testid="`tasks-open-client-${row.clientId}`" @click="openClient(row.clientId)">
              {{ row.clientName }}
            </button>
          </td>
          <td :data-testid="`tasks-client-task-count-${row.clientId}`">{{ row.taskCount }}</td>
          <td :data-testid="`tasks-client-missing-count-${row.clientId}`">{{ row.missingCount }}</td>
          <td :data-testid="`tasks-client-uninvoiced-count-${row.clientId}`">{{ row.uninvoicedCount }}</td>
        </tr>
      </tbody>
      <tfoot v-if="clientCounts.length">
        <tr class="totals-row">
          <th>Total</th>
          <th data-testid="tasks-client-total-tasks">{{ summaryTotals.tasks }}</th>
          <th data-testid="tasks-client-total-missing">{{ summaryTotals.missing }}</th>
          <th data-testid="tasks-client-total-uninvoiced">{{ summaryTotals.uninvoiced }}</th>
        </tr>
      </tfoot>
    </table>
    </div>
    </template>

    <template v-else-if="viewMode === 'months'">
    <p v-if="summaryLoading" data-testid="tasks-month-summary-loading">Loading…</p>
    <p v-else-if="summaryError" class="error" data-testid="tasks-month-summary-error">Failed to load task summary.</p>
    <p v-else-if="!hasMonthSummary" class="empty" data-testid="tasks-month-summary-empty">
      No tasks match. Sync from ClickUp or adjust filters.
    </p>

    <div v-else class="table-wrap">
    <table class="grid summary-grid" data-testid="tasks-by-month-table">
      <thead>
        <tr>
          <th>Month</th>
          <th>Tasks</th>
          <th>Missing</th>
          <th>Uninvoiced</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in monthCounts" :key="row.month" :data-testid="`tasks-month-count-${row.month}`">
          <td>
            <button type="button" class="link" :data-testid="`tasks-open-done-month-${row.month}`" @click="openDoneMonth(row.month)">
              {{ formatMonthYear(row.month) }}
            </button>
          </td>
          <td :data-testid="`tasks-month-task-count-${row.month}`">{{ row.taskCount }}</td>
          <td :data-testid="`tasks-month-missing-count-${row.month}`">{{ row.missingCount }}</td>
          <td :data-testid="`tasks-month-uninvoiced-count-${row.month}`">{{ row.uninvoicedCount }}</td>
        </tr>
      </tbody>
      <tfoot v-if="monthCounts.length">
        <tr class="totals-row">
          <th>Total</th>
          <th data-testid="tasks-month-total-tasks">{{ monthTotals.tasks }}</th>
          <th data-testid="tasks-month-total-missing">{{ monthTotals.missing }}</th>
          <th data-testid="tasks-month-total-uninvoiced">{{ monthTotals.uninvoiced }}</th>
        </tr>
      </tfoot>
    </table>
    </div>
    </template>
  </section>
</template>

<style scoped>
.tasks-view {
  width: calc(100vw - 2rem);
  max-width: none;
  margin-left: calc(50% - 50vw + 1rem);
  box-sizing: border-box;
}
.table-wrap {
  width: 100%;
  overflow-x: auto;
}
.header { display: flex; align-items: baseline; gap: 0.75rem; margin-bottom: 0.75rem; flex-wrap: wrap; }
.view-toggle {
  display: inline-flex;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  overflow: hidden;
}
.view-toggle button {
  padding: 0.35rem 0.75rem;
  border: none;
  background: #fff;
  color: #4b5563;
  font: inherit;
  cursor: pointer;
}
.view-toggle button.active {
  background: #ecfdf5;
  color: #059669;
  font-weight: 600;
}
.summary-grid tfoot th {
  border-top: 2px solid #e5e7eb;
  padding-top: 0.55rem;
}
.totals-row th { color: #1f2937; }
.badge {
  font-size: 0.8rem;
  font-weight: 600;
  color: #b45309;
  background: #fef3c7;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
}
.missing-legend {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.65rem 1rem;
  margin-bottom: 0.75rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.legend-label {
  font-weight: 600;
  color: #374151;
}
.legend-item {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}
.flags-col {
  width: 1%;
  white-space: nowrap;
  vertical-align: top;
}
.flag-slot {
  display: inline-flex;
  width: 1.35rem;
  justify-content: center;
}
.missing-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.15rem;
  height: 1.15rem;
  padding: 0 0.15rem;
  border-radius: 4px;
  font-size: 0.68rem;
  font-weight: 700;
  line-height: 1;
}
.missing-B { background: #fef3c7; color: #b45309; }
.missing-H { background: #ede9fe; color: #6d28d9; }
.filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; margin-bottom: 1rem; }
.filters label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filters .check { flex-direction: row; align-items: center; gap: 0.4rem; padding-bottom: 0.35rem; }
.column-toggles { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filter-label { line-height: 1.2; }
.column-checks { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; padding-bottom: 0.35rem; }
.column-checks .check { padding-bottom: 0; }
.status-filters {
  flex-basis: 100%;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.status-checks {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
  align-items: center;
}
.status-checks .check { padding-bottom: 0; }
.toggle-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
  padding-bottom: 0.35rem;
}
.toggle-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.toggle-side {
  font-size: 0.8rem;
  color: #9ca3af;
  font-weight: 500;
}
.toggle-side.active {
  color: #059669;
  font-weight: 600;
}
select, input:not([role='switch']) {
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font: inherit;
}
.grid { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
.grid th, .grid td { text-align: left; padding: 0.45rem 0.4rem; border-bottom: 1px solid #eee; vertical-align: top; }
.link { background: none; border: none; color: #059669; cursor: pointer; padding: 0; font: inherit; }
.edit-row td { background: #f0fdf4; }
.edit-form {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  align-items: end;
  padding: 0.5rem 0;
}
.edit-form label { display: flex; flex-direction: column; gap: 0.2rem; font-size: 0.8rem; color: #4b5563; }
.edit-form .grow { flex: 1; min-width: 10rem; }
.edit-actions { display: flex; gap: 0.75rem; align-items: center; padding-bottom: 0.15rem; }
.edit-actions button[type="submit"] {
  padding: 0.45rem 0.85rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
.error { color: #b91c1c; }
.empty { color: #6b7280; }
</style>
