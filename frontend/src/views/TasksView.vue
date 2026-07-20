<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useClients } from '../queries/clients'
import { useProjects } from '../queries/projects'
import { useTasks, useUpdateTaskPrep } from '../queries/tasks'
import type { WorkTask } from '../api/types'

const route = useRoute()
const clientFilter = ref<string>((route.query.clientId as string) || '')
const missingOnly = ref(true)
const clientId = computed(() => clientFilter.value || undefined)

const { data: clients } = useClients()
const { data: tasks, isLoading, error } = useTasks(clientId, missingOnly)
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

watch(clientFilter, () => { editingId.value = null })
watch(missingOnly, () => { editingId.value = null })
</script>

<template>
  <section>
    <div class="header">
      <h1>Tasks</h1>
      <span v-if="missingOnly && tasks" class="badge">{{ missingCount }} need attention</span>
    </div>

    <div class="filters">
      <label>
        Client
        <select v-model="clientFilter">
          <option value="">All clients</option>
          <option v-for="c in clients" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </label>
      <label class="check">
        <input v-model="missingOnly" type="checkbox" />
        Missing data only
      </label>
    </div>

    <p v-if="isLoading">Loading…</p>
    <p v-else-if="error" class="error">Failed to load tasks.</p>
    <p v-else-if="tasks && tasks.length === 0" class="empty">
      No tasks match. Sync from ClickUp or clear the missing-data filter.
    </p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th></th>
          <th>Task</th>
          <th>Client</th>
          <th>Project</th>
          <th>Bill</th>
          <th>Billable h</th>
          <th>Invoice</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="t in tasks" :key="t.id">
          <tr :class="{ missing: t.needsAttention, editing: editingId === t.id }">
            <td>
              <span v-if="t.needsAttention" class="dot" title="Needs attention" />
            </td>
            <td>
              <a v-if="t.clickUpUrl" :href="t.clickUpUrl" target="_blank" rel="noopener">{{ t.title }}</a>
              <span v-else>{{ t.title }}</span>
              <div class="muted">{{ t.clickUpListName }}</div>
            </td>
            <td>{{ t.clientName }}</td>
            <td>{{ t.projectName ?? '—' }}</td>
            <td>{{ t.bill ?? '—' }}</td>
            <td>{{ t.billableHours ?? '—' }}</td>
            <td>{{ t.invoiceLabel ?? '—' }}</td>
            <td>{{ t.clickUpStatus ?? '—' }}</td>
            <td>
              <button v-if="editingId !== t.id" class="link" @click="startEdit(t)">Edit</button>
            </td>
          </tr>
          <tr v-if="editingId === t.id" class="edit-row">
            <td colspan="9">
              <form class="edit-form" @submit.prevent="saveEdit">
                <label>
                  Project
                  <select v-model="draft.projectId">
                    <option value="">— unassigned —</option>
                    <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
                  </select>
                </label>
                <label>
                  Bill
                  <select v-model="draft.bill">
                    <option value="">—</option>
                    <option value="yes">yes</option>
                    <option value="no">no</option>
                  </select>
                </label>
                <label>
                  Billable hours
                  <input v-model="draft.billableHours" type="number" step="0.01" min="0" />
                </label>
                <label>
                  Non-billable hours
                  <input v-model="draft.nonBillableHours" type="number" step="0.01" min="0" />
                </label>
                <label>
                  Invoice
                  <input v-model="draft.invoiceLabel" placeholder="e.g. Aug 2025" />
                </label>
                <label class="grow">
                  Note
                  <input v-model="draft.note" />
                </label>
                <div class="edit-actions">
                  <button type="submit" :disabled="updatePrep.isLoading.value">Save</button>
                  <button type="button" class="link" @click="cancelEdit">Cancel</button>
                </div>
              </form>
              <p v-if="saveError" class="error">{{ saveError }}</p>
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
.grid tr.missing { background: #fffbeb; }
.dot {
  display: inline-block;
  width: 0.55rem;
  height: 0.55rem;
  border-radius: 50%;
  background: #f59e0b;
  margin-top: 0.35rem;
}
.muted { font-size: 0.75rem; color: #9ca3af; }
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
