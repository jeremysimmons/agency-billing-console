<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useClients } from '../queries/clients'
import { useProjects } from '../queries/projects'
import { useTasks, useTaskFilterOptions, useUpdateTaskPrep } from '../queries/tasks'
import type { WorkTask } from '../api/types'

const route = useRoute()
const clientFilter = ref<string>((route.query.clientId as string) || '')
const missingOnly = ref(true)
const showListColumn = ref(false)
const showProjectColumn = ref(false)
const projectFilter = ref('')
const createdMonthFilter = ref('')
const doneMonthFilter = ref('')
const clientId = computed(() => clientFilter.value || undefined)

const taskFilters = computed(() => ({
  clientId: clientId.value,
  missingOnly: missingOnly.value,
  projectFilter: projectFilter.value || undefined,
  createdMonth: createdMonthFilter.value || undefined,
  doneMonth: doneMonthFilter.value || undefined,
}))

const { data: clients } = useClients()
const { data: filterProjects } = useProjects(clientId)
const { data: filterOptions } = useTaskFilterOptions(clientId)
const { data: tasks, isLoading, error } = useTasks(taskFilters)
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
const editColspan = computed(() =>
  10 + (showListColumn.value ? 1 : 0) + (showProjectColumn.value ? 1 : 0))

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

watch(clientFilter, () => {
  editingId.value = null
  projectFilter.value = ''
  createdMonthFilter.value = ''
  doneMonthFilter.value = ''
})
watch(missingOnly, () => { editingId.value = null })
watch([createdMonthFilter, doneMonthFilter], () => { editingId.value = null })

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
</script>

<template>
  <section data-testid="tasks-view">
    <div class="header">
      <h1>Tasks</h1>
      <span v-if="missingOnly && tasks" class="badge" data-testid="tasks-missing-count">{{ missingCount }} need attention</span>
    </div>

    <div class="filters">
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
      <label class="check">
        <input v-model="missingOnly" type="checkbox" data-testid="tasks-missing-only" />
        Missing data only
      </label>
      <label class="check">
        <input v-model="showListColumn" type="checkbox" data-testid="tasks-show-list-column" />
        Show List column
      </label>
      <label class="check">
        <input v-model="showProjectColumn" type="checkbox" data-testid="tasks-show-project-column" />
        Show Project column
      </label>
    </div>

    <p v-if="isLoading" data-testid="tasks-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="tasks-error">Failed to load tasks.</p>
    <p v-else-if="tasks && tasks.length === 0" class="empty" data-testid="tasks-empty">
      No tasks match. Sync from ClickUp or clear the missing-data filter.
    </p>

    <table v-else class="grid" data-testid="tasks-table">
      <thead>
        <tr>
          <th>Task</th>
          <th v-if="showListColumn">List</th>
          <th>Client</th>
          <th v-if="showProjectColumn">Project</th>
          <th>Bill</th>
          <th>Billable hours</th>
          <th>Non-billable hours</th>
          <th>Invoice</th>
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
            <td :data-testid="`task-client-${t.id}`">{{ t.clientName }}</td>
            <td v-if="showProjectColumn" :data-testid="`task-project-${t.id}`">{{ t.projectName ?? '—' }}</td>
            <td :data-testid="`task-bill-${t.id}`">{{ t.bill ?? '—' }}</td>
            <td :data-testid="`task-billable-hours-${t.id}`">{{ t.billableHours ?? '—' }}</td>
            <td :data-testid="`task-non-billable-hours-${t.id}`">{{ t.nonBillableHours ?? '—' }}</td>
            <td :data-testid="`task-invoice-${t.id}`">{{ t.invoiceLabel ?? '—' }}</td>
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
  </section>
</template>

<style scoped>
.header { display: flex; align-items: baseline; gap: 0.75rem; margin-bottom: 0.75rem; }
.badge {
  font-size: 0.8rem;
  font-weight: 600;
  color: #b45309;
  background: #fef3c7;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
}
.filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; margin-bottom: 1rem; }
.filters label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filters .check { flex-direction: row; align-items: center; gap: 0.4rem; padding-bottom: 0.35rem; }
select, input {
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
