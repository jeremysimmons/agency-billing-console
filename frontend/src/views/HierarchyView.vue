<script setup lang="ts">
import { ref } from 'vue'
import { useClickUpHierarchy, useClickUpSync } from '../queries/clickup'
import HierarchyNode from '../components/HierarchyNode.vue'

const { data: tree, isLoading, error } = useClickUpHierarchy()
const sync = useClickUpSync()
const syncMsg = ref('')
const syncError = ref('')

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
</script>

<template>
  <section data-testid="hierarchy-view">
    <div class="header">
      <h1>ClickUp hierarchy</h1>
      <button :disabled="sync.isLoading.value" data-testid="hierarchy-sync-button" @click="runSync">
        {{ sync.isLoading.value ? 'Syncing…' : 'Sync from ClickUp' }}
      </button>
    </div>
    <p v-if="syncMsg" class="ok" data-testid="hierarchy-sync-result">{{ syncMsg }}</p>
    <p v-if="syncError" class="error" data-testid="hierarchy-sync-error">{{ syncError }}</p>
    <p class="hint">Space → folder → list. Names refresh when you sync.</p>

    <p v-if="isLoading" data-testid="hierarchy-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="hierarchy-error">Failed to load hierarchy.</p>
    <p v-else-if="tree && tree.length === 0" class="empty" data-testid="hierarchy-empty">No containers yet. Run Sync.</p>

    <ul v-else class="tree" data-testid="hierarchy-tree">
      <HierarchyNode v-for="node in tree" :key="node.id" :node="node" />
    </ul>
  </section>
</template>

<style scoped>
.header { display: flex; align-items: center; gap: 1rem; margin-bottom: 0.5rem; }
button {
  padding: 0.45rem 0.9rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
button:disabled { opacity: 0.6; cursor: default; }
.hint, .empty { color: #6b7280; font-size: 0.9rem; }
.ok { color: #047857; }
.error { color: #b91c1c; }
.tree { list-style: none; margin: 0; padding: 0; }
</style>
