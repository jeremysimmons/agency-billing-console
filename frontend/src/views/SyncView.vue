<script setup lang="ts">
import { ref } from 'vue'
import { useAgency } from '../queries/agency'
import { useClickUpSync, useCsvImport } from '../queries/clickup'

const { data: agency } = useAgency()
const sync = useClickUpSync()
const csvImport = useCsvImport()

const syncMsg = ref('')
const syncError = ref('')
const csvMsg = ref('')
const csvError = ref('')

async function runSync() {
  syncMsg.value = ''
  syncError.value = ''
  try {
    const result = await sync.mutateAsync()
    syncMsg.value = result.summary
  } catch (e: any) {
    syncError.value = e?.response?.data?.error ?? 'Sync failed.'
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
        Pulls hierarchy + your assigned tasks. Preserves Bill / hours / invoice / note / project assign.
      </p>
      <p v-if="agency?.lastClickUpSyncAt" class="muted" data-testid="sync-last-run">
        Last sync: {{ new Date(agency.lastClickUpSyncAt).toLocaleString() }}
        <template v-if="agency.lastClickUpSyncSummary"> — {{ agency.lastClickUpSyncSummary }}</template>
      </p>
      <p v-else class="muted" data-testid="sync-never-run">Never synced.</p>
      <button :disabled="sync.isLoading.value" data-testid="sync-clickup-button" @click="runSync">
        {{ sync.isLoading.value ? 'Syncing…' : 'Sync now' }}
      </button>
      <p v-if="syncMsg" class="ok" data-testid="sync-clickup-result">{{ syncMsg }}</p>
      <p v-if="syncError" class="error" data-testid="sync-clickup-error">{{ syncError }}</p>
    </div>

    <div class="card" data-testid="sync-csv-card">
      <h2>CSV bootstrap</h2>
      <p class="muted">
        One-shot import of the sheet export (manual cols + ClickUp cols), keyed by task URL.
      </p>
      <input type="file" accept=".csv,text/csv" data-testid="sync-csv-input" @change="onFile" />
      <p v-if="csvImport.isLoading.value" class="muted" data-testid="sync-csv-loading">Importing…</p>
      <p v-if="csvMsg" class="ok" data-testid="sync-csv-result">{{ csvMsg }}</p>
      <p v-if="csvError" class="error" data-testid="sync-csv-error">{{ csvError }}</p>
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
  max-width: 40rem;
}
h2 { margin: 0 0 0.4rem; }
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
</style>
