<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ToggleSwitch from 'primevue/toggleswitch'
import type { ClickUpHierarchyNode } from '../api/types'
import { useClickUpHierarchy, useClickUpSync, formatClickUpSyncStatus } from '../queries/clickup'
import HierarchyNode from '../components/HierarchyNode.vue'

const HIDE_EMPTY_STORAGE_KEY = 'aib.hierarchy.hideEmpty'

function readHideEmpty(): boolean {
  try {
    const raw = localStorage.getItem(HIDE_EMPTY_STORAGE_KEY)
    if (raw === 'true') return true
    if (raw === 'false') return false
  } catch { /* ignore */ }
  return false
}

const { data: tree, isLoading, error } = useClickUpHierarchy()
const sync = useClickUpSync()
const syncMsg = ref('')
const syncError = ref('')
const syncStatus = ref('')

const expandLevel = ref(1)
const expandedIds = ref<Set<string>>(new Set())
const selected = ref<ClickUpHierarchyNode | null>(null)
const hideEmpty = ref(readHideEmpty())

watch(hideEmpty, (value) => {
  try {
    localStorage.setItem(HIDE_EMPTY_STORAGE_KEY, String(value))
  } catch { /* ignore */ }
})

function filterEmpty(nodes: ClickUpHierarchyNode[]): ClickUpHierarchyNode[] {
  return nodes
    .filter((n) => n.taskCount > 0)
    .map((n) => ({ ...n, children: filterEmpty(n.children) }))
}

const displayTree = computed(() => {
  const nodes = tree.value ?? []
  return hideEmpty.value ? filterEmpty(nodes) : nodes
})

function isWorkspaceRoot(node: ClickUpHierarchyNode) {
  return node.type.toLowerCase() === 'workspace'
}

function workspaceRootIds(nodes: ClickUpHierarchyNode[]): string[] {
  return nodes.filter((n) => isWorkspaceRoot(n) && n.children.length).map((n) => n.id)
}

/** Keep workspace root expanded; level 0 would collapse it. */
const minExpandLevel = computed(() => (workspaceRootIds(displayTree.value).length ? 1 : 0))

function treeMaxDepth(nodes: ClickUpHierarchyNode[] | undefined, depth = 0): number {
  if (!nodes?.length) return depth
  return Math.max(...nodes.map((n) => treeMaxDepth(n.children, depth + 1)))
}

function collectIdsToDepth(nodes: ClickUpHierarchyNode[], maxOpenDepth: number, depth = 0): string[] {
  const ids: string[] = []
  for (const n of nodes) {
    if (n.children.length && depth < maxOpenDepth) {
      ids.push(n.id)
      ids.push(...collectIdsToDepth(n.children, maxOpenDepth, depth + 1))
    }
  }
  return ids
}

function collectAllExpandableIds(nodes: ClickUpHierarchyNode[]): string[] {
  const ids: string[] = []
  for (const n of nodes) {
    if (n.children.length) {
      ids.push(n.id)
      ids.push(...collectAllExpandableIds(n.children))
    }
  }
  return ids
}

const maxDepth = computed(() => Math.max(0, treeMaxDepth(displayTree.value) - 1))

function applyExpandLevel(level: number) {
  const clamped = Math.max(level, minExpandLevel.value)
  expandedIds.value = new Set(collectIdsToDepth(displayTree.value, clamped))
}

watch(
  [() => tree.value, hideEmpty],
  () => {
    const nodes = displayTree.value
    if (expandLevel.value < minExpandLevel.value) expandLevel.value = minExpandLevel.value
    if (expandLevel.value > maxDepth.value) expandLevel.value = Math.max(maxDepth.value, minExpandLevel.value)
    applyExpandLevel(expandLevel.value)
    if (!selected.value) return
    selected.value = findNode(nodes, selected.value.id)
  },
  { immediate: true },
)

function findNode(nodes: ClickUpHierarchyNode[], id: string): ClickUpHierarchyNode | null {
  for (const n of nodes) {
    if (n.id === id) return n
    const child = findNode(n.children, id)
    if (child) return child
  }
  return null
}

function expandLevelPlus() {
  if (expandLevel.value >= maxDepth.value) return
  expandLevel.value += 1
  applyExpandLevel(expandLevel.value)
}

function expandLevelMinus() {
  if (expandLevel.value <= minExpandLevel.value) return
  expandLevel.value -= 1
  applyExpandLevel(expandLevel.value)
}

function expandAll() {
  expandLevel.value = Math.max(maxDepth.value, minExpandLevel.value)
  expandedIds.value = new Set(collectAllExpandableIds(displayTree.value))
}

function collapseAll() {
  expandLevel.value = minExpandLevel.value
  expandedIds.value = new Set(workspaceRootIds(displayTree.value))
}

function onSelect(node: ClickUpHierarchyNode) {
  selected.value = node
}

function onToggle(id: string) {
  if (workspaceRootIds(displayTree.value).includes(id) && expandedIds.value.has(id)) return
  const next = new Set(expandedIds.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expandedIds.value = next
}

function formatUpdatedAt(value: string) {
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString()
}

async function runSync() {
  syncMsg.value = ''
  syncError.value = ''
  syncStatus.value = ''
  try {
    const result = await sync.mutateAsync({
      onProgress: (event) => {
        syncStatus.value = formatClickUpSyncStatus(event)
      },
    })
    syncMsg.value = result.summary
    syncStatus.value = ''
  } catch (e: any) {
    syncError.value = e?.message ?? e?.response?.data?.error ?? 'Sync failed.'
    syncStatus.value = ''
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
    <p v-if="syncStatus" class="hint" data-testid="hierarchy-sync-status">{{ syncStatus }}</p>
    <p v-if="syncMsg" class="ok" data-testid="hierarchy-sync-result">{{ syncMsg }}</p>
    <p v-if="syncError" class="error" data-testid="hierarchy-sync-error">{{ syncError }}</p>
    <p class="hint">Names refresh when you sync. Arrow expands; row selects. Task count links to filtered tasks.</p>
    <ul class="legend" data-testid="hierarchy-legend" aria-label="Node type legend">
      <li><span class="badge badge-workspace">W</span> Workspace</li>
      <li><span class="badge badge-space">S</span> Space</li>
      <li><span class="badge badge-folder">F</span> Folder</li>
      <li><span class="badge badge-list">L</span> List</li>
    </ul>

    <div v-if="tree && tree.length" class="toolbar" data-testid="hierarchy-expand-toolbar">
      <span class="level-hint" data-testid="hierarchy-expand-level">Level {{ expandLevel }} / {{ maxDepth }}</span>
      <button
        type="button"
        class="secondary"
        :disabled="expandLevel <= minExpandLevel"
        data-testid="hierarchy-expand-level-minus"
        @click="expandLevelMinus"
      >Expand level −</button>
      <button
        type="button"
        class="secondary"
        :disabled="expandLevel >= maxDepth"
        data-testid="hierarchy-expand-level-plus"
        @click="expandLevelPlus"
      >Expand level +</button>
      <button type="button" class="secondary" data-testid="hierarchy-expand-all" @click="expandAll">Expand all</button>
      <button type="button" class="secondary" data-testid="hierarchy-collapse-all" @click="collapseAll">Collapse all</button>
      <div class="toggle-field inline-label">
        <span id="hierarchy-hide-empty-label" class="toggle-label" style="margin-right: 0.7em; align-self: center;">Empty nodes</span>
        <div class="toggle-row" style="display: inline-flex; align-items: center;">
          <span class="toggle-side" :class="{ active: !hideEmpty }" data-testid="hierarchy-show-empty-label">Show</span>
          <ToggleSwitch
            v-model="hideEmpty"
            aria-labelledby="hierarchy-hide-empty-label"
            :pt="{ input: { 'data-testid': 'hierarchy-hide-empty-toggle' } }"
          />
          <span class="toggle-side" :class="{ active: hideEmpty }" data-testid="hierarchy-hide-empty-label-active">Hide</span>
        </div>
      </div>
    </div>

    <p v-if="isLoading" data-testid="hierarchy-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="hierarchy-error">Failed to load hierarchy.</p>
    <p v-else-if="tree && tree.length === 0" class="empty" data-testid="hierarchy-empty">No containers yet. Run Sync.</p>
    <p v-else-if="displayTree.length === 0" class="empty" data-testid="hierarchy-empty-filtered">No nodes with tasks. Show empty to see all.</p>

    <div v-else class="layout" data-testid="hierarchy-layout">
      <ul class="tree" data-testid="hierarchy-tree">
        <HierarchyNode
          v-for="node in displayTree"
          :key="node.id"
          :node="node"
          :expanded-ids="expandedIds"
          :selected-id="selected?.id ?? null"
          @select="onSelect"
          @toggle="onToggle"
        />
      </ul>

      <aside class="details" data-testid="hierarchy-details">
        <h2>Details</h2>
        <p v-if="!selected" class="empty" data-testid="hierarchy-details-empty">Select a node to view details.</p>
        <dl v-else data-testid="hierarchy-details-body">
          <div>
            <dt>Name</dt>
            <dd data-testid="hierarchy-details-name">{{ selected.name }}</dd>
          </div>
          <div>
            <dt>Type</dt>
            <dd data-testid="hierarchy-details-type">{{ selected.type }}</dd>
          </div>
          <div>
            <dt>ClickUp ID</dt>
            <dd data-testid="hierarchy-details-id">{{ selected.id }}</dd>
          </div>
          <div>
            <dt>Parent type</dt>
            <dd data-testid="hierarchy-details-parent-type">{{ selected.parentType ?? '—' }}</dd>
          </div>
          <div>
            <dt>Parent ID</dt>
            <dd data-testid="hierarchy-details-parent-id">{{ selected.parentId ?? '—' }}</dd>
          </div>
          <div>
            <dt>Updated</dt>
            <dd data-testid="hierarchy-details-updated">{{ formatUpdatedAt(selected.updatedAt) }}</dd>
          </div>
          <div>
            <dt>Tasks</dt>
            <dd data-testid="hierarchy-details-task-count">{{ selected.taskCount }}</dd>
          </div>
        </dl>
      </aside>
    </div>
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
button.secondary {
  background: #fff;
  color: #374151;
  border: 1px solid #d1d5db;
}
button:disabled { opacity: 0.6; cursor: default; }
.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin: 0.75rem 0 1rem;
}
.toggle-field {
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: #4b5563;
  margin-left: 0.25rem;
}
.toggle-label { line-height: 1.2; white-space: nowrap; }
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
.level-hint { color: #6b7280; font-size: 0.85rem; margin-left: 0.25rem; }
.hint, .empty { color: #6b7280; font-size: 0.9rem; }
.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1.25rem;
  list-style: none;
  margin: 0.5rem 0 0;
  padding: 0;
  color: #4b5563;
  font-size: 0.85rem;
}
.legend li { display: inline-flex; align-items: center; gap: 0.4rem; }
.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 4px;
  font-size: 0.65rem;
  font-weight: 700;
  flex-shrink: 0;
}
.badge-space { background: #dbeafe; color: #1d4ed8; }
.badge-folder { background: #fef3c7; color: #b45309; }
.badge-list { background: #d1fae5; color: #047857; }
.badge-workspace { background: #ede9fe; color: #6d28d9; }
.ok { color: #047857; }
.error { color: #b91c1c; }
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(16rem, 20rem);
  gap: 1.25rem;
  align-items: start;
}
.tree { list-style: none; margin: 0; padding: 0; }
.details {
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 1rem 1.15rem;
  position: sticky;
  top: 1rem;
}
.details h2 { margin: 0 0 0.75rem; font-size: 1rem; }
.details dl { margin: 0; display: grid; gap: 0.65rem; }
.details dl > div { display: grid; gap: 0.15rem; }
.details dt { color: #6b7280; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.03em; }
.details dd { margin: 0; font-size: 0.95rem; word-break: break-word; }
@media (max-width: 800px) {
  .layout { grid-template-columns: 1fr; }
  .details { position: static; }
}
</style>
