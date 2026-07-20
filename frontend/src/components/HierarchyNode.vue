<script setup lang="ts">
import { ref } from 'vue'
import type { ClickUpHierarchyNode } from '../api/types'

defineProps<{ node: ClickUpHierarchyNode }>()
const open = ref(true)

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
</script>

<template>
  <li>
    <div class="row" :title="node.id" @click="open = !open">
      <span class="toggle">{{ node.children.length ? (open ? '▾' : '▸') : '·' }}</span>
      <span :class="badgeClass(node.type)">{{ typeBadge(node.type) }}</span>
      <span class="name">{{ node.name }}</span>
    </div>
    <ul v-if="open && node.children.length">
      <HierarchyNode
        v-for="child in node.children"
        :key="child.id"
        :node="child"
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
.toggle { width: 1rem; color: #9ca3af; font-size: 0.85rem; flex-shrink: 0; }
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
.name { font-weight: 500; min-width: 0; }
</style>
