<script setup lang="ts">
import { ref } from 'vue'
import {
  usePendingWork,
  useCompletedWork,
  useFinalizedWork,
  useFinalizeWork,
  useExcludeWork,
} from '../queries/work'
import type { WorkItemReview } from '../api/types'

const tab = ref<'pending' | 'completed' | 'finalized'>('completed')
const { data: pending, isLoading: loadingPending } = usePendingWork()
const { data: completed, isLoading: loadingCompleted } = useCompletedWork()
const { data: finalized, isLoading: loadingFinalized } = useFinalizedWork()

const finalize = useFinalizeWork()
const exclude = useExcludeWork()

const error = ref('')

const rows = () => {
  if (tab.value === 'pending') return pending.value ?? []
  if (tab.value === 'finalized') return finalized.value ?? []
  return completed.value ?? []
}

const loading = () =>
  tab.value === 'pending' ? loadingPending.value
    : tab.value === 'finalized' ? loadingFinalized.value
      : loadingCompleted.value

function hours(mins: number | null | undefined) {
  if (mins == null) return '—'
  return (mins / 60).toFixed(2)
}

async function doFinalize(row: WorkItemReview) {
  error.value = ''
  try { await finalize.mutateAsync(row.taskId) }
  catch (e: any) { error.value = e?.response?.data?.error ?? 'Finalize failed.' }
}

async function doExclude(row: WorkItemReview) {
  error.value = ''
  try { await exclude.mutateAsync({ taskId: row.taskId }) }
  catch (e: any) { error.value = e?.response?.data?.error ?? 'Exclude failed.' }
}
</script>

<template>
  <section>
    <h1>Work review</h1>
    <p class="lead">Pending / completed work awaiting billing decisions. Finalize freezes the item for invoicing.</p>

    <p v-if="error" class="error">{{ error }}</p>

    <div class="tabs">
      <button :class="{ active: tab === 'pending' }" @click="tab = 'pending'">Pending</button>
      <button :class="{ active: tab === 'completed' }" @click="tab = 'completed'">Completed</button>
      <button :class="{ active: tab === 'finalized' }" @click="tab = 'finalized'">Finalized</button>
    </div>

    <p v-if="loading()">Loading…</p>
    <table v-else class="grid">
      <thead>
        <tr>
          <th>Client</th><th>Project</th><th>Task</th><th>Status</th>
          <th>Est h</th><th>Actual h</th><th>Billable h</th><th>Amount</th><th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="r in rows()" :key="r.taskId">
          <td>{{ r.clientName }}</td>
          <td>{{ r.projectName ?? '—' }}</td>
          <td>{{ r.title }}</td>
          <td>{{ r.workStatus }} / {{ r.billingStatus }}</td>
          <td>{{ hours(r.estimatedMinutes) }}</td>
          <td>{{ hours(r.actualMinutes) }}</td>
          <td>{{ hours(r.billableMinutes) }}</td>
          <td>{{ r.billingAmountEstimate != null ? r.billingAmountEstimate.toFixed(2) : '—' }}</td>
          <td class="btns" v-if="tab === 'completed'">
            <button class="link" @click="doFinalize(r)">Finalize</button>
            <button class="link danger" @click="doExclude(r)">Exclude</button>
          </td>
          <td v-else>—</td>
        </tr>
        <tr v-if="rows().length === 0"><td colspan="9">No items.</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.lead { opacity: 0.8; }
.tabs { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
.tabs button { background: #e5e7eb; color: #111; }
.tabs button.active { background: #10b981; color: #fff; }
button { padding: 0.45rem 0.85rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
.grid { width: 100%; border-collapse: collapse; font-size: 0.92rem; }
.grid th, .grid td { text-align: left; padding: 0.4rem 0.45rem; border-bottom: 1px solid #eee; vertical-align: top; }
.btns { display: flex; gap: 0.4rem; }
.link { background: none; color: #10b981; padding: 0; }
.link.danger { color: #dc2626; }
.error { color: #dc2626; }
</style>
