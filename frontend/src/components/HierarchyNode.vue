<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { ClickUpHierarchyNode } from '../api/types'

const props = defineProps<{
  node: ClickUpHierarchyNode
  expandedIds: Set<string>
  selectedId: string | null
}>()

const emit = defineEmits<{
  select: [node: ClickUpHierarchyNode]
  toggle: [id: string]
}>()

const open = computed(() => props.expandedIds.has(props.node.id))

const tasksLink = computed(() => {
  const t = props.node.type.toLowerCase()
  const id = props.node.id
  const query: Record<string, string> = { missingOnly: 'false' }
  if (t === 'list') query.listId = id
  else if (t === 'folder') query.folderId = id
  else if (t === 'space') query.spaceId = id
  else return null
  return { path: '/tasks', query }
})

function typeBadge(type: string) {
  switch (type.toLowerCase()) {
    case 'space': return 'S'
    case 'folder': return 'F'
    case 'list': return 'L'
    case 'workspace': return 'W'
    default: return type.charAt(0).toUpperCase()
  }
}

function badgeClass(type: string) {
  const t = type.toLowerCase()
  if (t === 'space' || t === 'folder' || t === 'list' || t === 'workspace')
    return `badge badge-${t}`
  return 'badge badge-other'
}

function onToggle(e: MouseEvent) {
  e.stopPropagation()
  if (!props.node.children.length) return
  emit('toggle', props.node.id)
}

function onRowClick() {
  emit('select', props.node)
}
</script>

<template>
  <li :data-testid="`hierarchy-node-${node.id}`">
    <div
      class="row"
      :class="{ selected: selectedId === node.id }"
      :title="node.id"
      :data-testid="`hierarchy-node-row-${node.id}`"
      @click="onRowClick"
    >
      <button
        type="button"
        class="toggle"
        :disabled="!node.children.length"
        :aria-label="open ? `Collapse ${node.name}` : `Expand ${node.name}`"
        :data-testid="`hierarchy-node-toggle-${node.id}`"
        @click="onToggle"
      >{{ node.children.length ? (open ? '▾' : '▸') : '·' }}</button>
      <span :class="badgeClass(node.type)" :data-testid="`hierarchy-node-type-${node.id}`">{{ typeBadge(node.type) }}</span>
      <span class="name" :data-testid="`hierarchy-node-name-${node.id}`">{{ node.name }}</span>
      <RouterLink
        v-if="tasksLink"
        class="count count-link"
        :to="tasksLink"
        :data-testid="`hierarchy-node-count-${node.id}`"
        :title="`View tasks in this ${node.type.toLowerCase()}`"
        @click.stop
      >{{ node.taskCount }}</RouterLink>
      <span v-else class="count" :data-testid="`hierarchy-node-count-${node.id}`">{{ node.taskCount }}</span>
    </div>
    <ul v-if="open && node.children.length" :data-testid="`hierarchy-node-children-${node.id}`">
      <HierarchyNode
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :expanded-ids="expandedIds"
        :selected-id="selectedId"
        @select="emit('select', $event)"
        @toggle="emit('toggle', $event)"
      />
    </ul>
  </li>
</template>

<style scoped>
ul { list-style: none; margin: 0; padding-left: 1.25rem; }
.row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0.35rem;
  border-radius: 6px;
  cursor: pointer;
}
.row:hover { background: #f3f4f6; }
.row.selected { background: #ecfdf5; outline: 1px solid #a7f3d0; }
.toggle {
  width: 1.25rem;
  height: 1.25rem;
  padding: 0;
  border: none;
  border-radius: 4px;
  background: transparent;
  color: #9ca3af;
  font-size: 0.85rem;
  line-height: 1;
  flex-shrink: 0;
  cursor: pointer;
}
.toggle:hover:not(:disabled) { background: #e5e7eb; color: #4b5563; }
.toggle:disabled { cursor: default; opacity: 0.7; }
.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 4px;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0;
  flex-shrink: 0;
}
.badge-space { background: #dbeafe; color: #1d4ed8; }
.badge-folder { background: #fef3c7; color: #b45309; }
.badge-list { background: #d1fae5; color: #047857; }
.badge-workspace { background: #ede9fe; color: #6d28d9; }
.badge-other { background: #f3f4f6; color: #4b5563; }
.name { font-weight: 500; min-width: 0; flex: 1; }
.count { color: #9ca3af; font-size: 0.8rem; font-variant-numeric: tabular-nums; flex-shrink: 0; }
.count-link {
  color: #059669;
  text-decoration: none;
  border-radius: 4px;
  padding: 0 0.15rem;
}
.count-link:hover { text-decoration: underline; background: #ecfdf5; }
</style>
