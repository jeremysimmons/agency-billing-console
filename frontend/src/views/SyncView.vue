<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useAgency } from '../queries/agency'
import {
  fetchClickUpSyncRun,
  formatClickUpSyncStatus,
  useClickUpSync,
  useClickUpSyncRuns,
  useCsvImport,
} from '../queries/clickup'

const { data: agency } = useAgency()
const sync = useClickUpSync()
const syncRuns = useClickUpSyncRuns()
const csvImport = useCsvImport()

const syncMsg = ref('')
const syncError = ref('')
const syncStatus = ref('')
const syncLog = ref('')
const selectedRunId = ref<string | null>(null)
const logEl = ref<HTMLElement | null>(null)
const csvMsg = ref('')
const csvError = ref('')

async function scrollLogToBottom() {
  await nextTick()
  if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight
}

watch(syncLog, () => {
  void scrollLogToBottom()
})

watch(
  () => syncRuns.data.value,
  (runs) => {
    if (!syncLog.value && !sync.isLoading.value && runs?.length && !selectedRunId.value) {
      void loadRunLog(runs[0].id)
    }
  },
  { immediate: true },
)

async function loadRunLog(id: string) {
  selectedRunId.value = id
  try {
    const run = await fetchClickUpSyncRun(id)
    syncLog.value = run.log
  } catch (e: any) {
    syncError.value = e?.message ?? 'Failed to load sync log.'
  }
}

async function runSync() {
  syncMsg.value = ''
  syncError.value = ''
  syncStatus.value = ''
  syncLog.value = ''
  selectedRunId.value = null
  try {
    const result = await sync.mutateAsync({
      onProgress: (event) => {
        if (event.syncRunId) selectedRunId.value = event.syncRunId
        if (event.phase === 'log' && event.message) {
          syncLog.value += (syncLog.value ? '\n' : '') + event.message
          syncStatus.value = 'Writing verbose log…'
          return
        }
        if (event.phase !== 'log') {
          syncStatus.value = formatClickUpSyncStatus(event)
        }
      },
    })
    syncMsg.value = result.summary
    syncStatus.value = ''
    if (result.syncRunId) {
      selectedRunId.value = result.syncRunId
      await loadRunLog(result.syncRunId)
    }
  } catch (e: any) {
    syncError.value = e?.message ?? e?.response?.data?.error ?? 'Sync failed.'
    syncStatus.value = ''
    if (selectedRunId.value) {
      try {
        await loadRunLog(selectedRunId.value)
      } catch {
        /* keep streamed log */
      }
    }
  }
}

async function onFile(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  csvMsg.value = ''
  csvError.value = ''
  try {
    const result = await csvImport.mutateAsync(file)
    csvMsg.value = result.summary
  } catch (err: any) {
    csvError.value = err?.response?.data?.error ?? 'CSV import failed.'
  } finally {
    input.value = ''
  }
}
</script>

<template>
  <section data-testid="sync-view">
    <h1>Sync</h1>

    <div class="card" data-testid="sync-clickup-card">
      <h2>ClickUp API</h2>
      <p class="muted">
        Pulls hierarchy + your assigned tasks, then fetches any missing parents. Preserves Bill / hours / invoice / note / project assign.
      </p>
      <p v-if="agency?.lastClickUpSyncAt" class="muted" data-testid="sync-last-run">
        Last sync: {{ new Date(agency.lastClickUpSyncAt).toLocaleString() }}
        <template v-if="agency.lastClickUpSyncSummary"> — {{ agency.lastClickUpSyncSummary }}</template>
      </p>
      <p v-else class="muted" data-testid="sync-never-run">Never synced.</p>
      <button :disabled="sync.isLoading.value" data-testid="sync-clickup-button" @click="runSync">
        {{ sync.isLoading.value ? 'Syncing…' : 'Sync now' }}
      </button>
      <p v-if="syncStatus" class="muted" data-testid="sync-clickup-status">{{ syncStatus }}</p>
      <p v-if="syncMsg" class="ok" data-testid="sync-clickup-result">{{ syncMsg }}</p>
      <p v-if="syncError" class="error" data-testid="sync-clickup-error">{{ syncError }}</p>

      <div v-if="syncLog || (syncRuns.data.value?.length ?? 0) > 0" class="log-panel" data-testid="sync-log-panel">
        <div class="log-header">
          <h3>Verbose sync log</h3>
          <select
            v-if="(syncRuns.data.value?.length ?? 0) > 0"
            data-testid="sync-run-select"
            :value="selectedRunId ?? ''"
            @change="loadRunLog(($event.target as HTMLSelectElement).value)"
          >
            <option disabled value="">Previous runs…</option>
            <option v-for="run in syncRuns.data.value" :key="run.id" :value="run.id">
              {{ new Date(run.startedAt).toLocaleString() }} — {{ run.status }}
              <template v-if="run.summary"> ({{ run.tasksCreated }} new / {{ run.tasksUpdated }} upd)</template>
            </option>
          </select>
        </div>
        <pre ref="logEl" class="log" data-testid="sync-log">{{ syncLog || 'No log loaded.' }}</pre>
      </div>
    </div>

  </section>
</template>

<style scoped>
.card {
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 1rem 1.15rem;
  margin-bottom: 1rem;
  max-width: 56rem;
}
h2 { margin: 0 0 0.4rem; }
h3 { margin: 0; font-size: 0.95rem; }
.muted { color: #6b7280; font-size: 0.9rem; }
button {
  margin-top: 0.5rem;
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
button:disabled { opacity: 0.6; cursor: default; }
.ok { color: #047857; }
.error { color: #b91c1c; }
input[type="file"] { margin-top: 0.5rem; }
.log-panel { margin-top: 1rem; }
.log-header {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}
.log-header select {
  max-width: 100%;
  font-size: 0.85rem;
}
.log {
  margin: 0;
  max-height: 28rem;
  overflow: auto;
  padding: 0.75rem;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #0f172a;
  color: #e2e8f0;
  font-size: 0.75rem;
  line-height: 1.45;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
