<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ToggleSwitch from 'primevue/toggleswitch'
import { useClients } from '../queries/clients'
import { useProjects } from '../queries/projects'
import { useAgency, useUpdateAgencyUiPreferences } from '../queries/agency'
import { useTasks, useTaskSummary, useTaskFilterOptions, useUpdateTaskBill, useUpdateTaskBillableHours, useUpdateTaskNonBillableHours, useUpdateTaskPrep, useSyncTask } from '../queries/tasks'
import type { WorkTask } from '../api/types'

const FILTERS_STORAGE_KEY = 'aib.tasks.filters'

type GroupOrderMode = 'alphabetical' | 'custom'

type StoredTaskFilters = {
  viewMode?: 'list' | 'clients' | 'months'
  clientFilter?: string
  missingOnly?: boolean
  invoicedFilter?: 'all' | 'yes' | 'no'
  showListColumn?: boolean
  showProjectColumn?: boolean
  showInvoiceColumn?: boolean
  showIdColumn?: boolean
  showClickUpHoursColumn?: boolean
  groupByClient?: boolean
  groupOrderMode?: GroupOrderMode
  collapsedGroups?: Record<string, boolean>
  projectFilter?: string
  createdMonthFilter?: string
  doneMonthFilter?: string
  statusFilters?: string[]
}

function readStoredFilters(): StoredTaskFilters {
  try {
    const raw = localStorage.getItem(FILTERS_STORAGE_KEY)
    if (!raw) return {}
    const parsed = JSON.parse(raw) as unknown
    return parsed && typeof parsed === 'object' ? parsed as StoredTaskFilters : {}
  } catch {
    return {}
  }
}

const storedFilters = readStoredFilters()
const statusFiltersRestored = Array.isArray(storedFilters.statusFilters)

const route = useRoute()
const router = useRouter()
const viewMode = ref<'list' | 'clients' | 'months'>(
  storedFilters.viewMode === 'clients' || storedFilters.viewMode === 'months' || storedFilters.viewMode === 'list'
    ? storedFilters.viewMode
    : 'list',
)
const clientFilter = ref<string>((route.query.clientId as string) || storedFilters.clientFilter || '')
const listIdFilter = computed(() => (typeof route.query.listId === 'string' ? route.query.listId : '') || '')
const folderIdFilter = computed(() => (typeof route.query.folderId === 'string' ? route.query.folderId : '') || '')
const spaceIdFilter = computed(() => (typeof route.query.spaceId === 'string' ? route.query.spaceId : '') || '')
const containerFilter = computed(() => {
  if (listIdFilter.value) return { type: 'list' as const, id: listIdFilter.value, label: 'List' }
  if (folderIdFilter.value) return { type: 'folder' as const, id: folderIdFilter.value, label: 'Folder' }
  if (spaceIdFilter.value) return { type: 'space' as const, id: spaceIdFilter.value, label: 'Space' }
  return null
})
const missingOnly = ref(
  route.query.missingOnly === 'false'
    ? false
    : route.query.missingOnly === 'true'
      ? true
      : typeof storedFilters.missingOnly === 'boolean'
        ? storedFilters.missingOnly
        : true,
)
const invoicedFilter = ref<'all' | 'yes' | 'no'>(
  route.query.invoiced === 'all' || route.query.invoiced === 'yes' || route.query.invoiced === 'no'
    ? route.query.invoiced
    : storedFilters.invoicedFilter === 'all' || storedFilters.invoicedFilter === 'yes' || storedFilters.invoicedFilter === 'no'
      ? storedFilters.invoicedFilter
      : 'no',
)
const showListColumn = ref(typeof storedFilters.showListColumn === 'boolean' ? storedFilters.showListColumn : false)
const showProjectColumn = ref(typeof storedFilters.showProjectColumn === 'boolean' ? storedFilters.showProjectColumn : false)
const showInvoiceColumn = ref(
  invoicedFilter.value === 'yes'
    ? true
    : invoicedFilter.value === 'no'
      ? false
      : typeof storedFilters.showInvoiceColumn === 'boolean'
        ? storedFilters.showInvoiceColumn
        : false,
)
const showIdColumn = ref(typeof storedFilters.showIdColumn === 'boolean' ? storedFilters.showIdColumn : true)
const showClickUpHoursColumn = ref(
  typeof storedFilters.showClickUpHoursColumn === 'boolean' ? storedFilters.showClickUpHoursColumn : false,
)
const groupByClient = ref(typeof storedFilters.groupByClient === 'boolean' ? storedFilters.groupByClient : false)
const groupOrderMode = ref<GroupOrderMode>(
  storedFilters.groupOrderMode === 'custom' ? 'custom' : 'alphabetical',
)
const customGroupOrder = ref<string[]>([])
const draggingGroupId = ref<string | null>(null)
const dragOverGroupId = ref<string | null>(null)
const collapsedGroups = ref<Record<string, boolean>>(
  storedFilters.collapsedGroups && typeof storedFilters.collapsedGroups === 'object'
    ? { ...storedFilters.collapsedGroups }
    : {},
)
const projectFilter = ref(storedFilters.projectFilter || '')
const createdMonthFilter = ref(storedFilters.createdMonthFilter || '')
const doneMonthFilter = ref(storedFilters.doneMonthFilter || '')
const statusFilters = ref<string[]>(statusFiltersRestored ? [...storedFilters.statusFilters!] : [])
const clientId = computed(() => clientFilter.value || undefined)

function clearContainerFilter() {
  const query = { ...route.query }
  delete query.listId
  delete query.folderId
  delete query.spaceId
  delete query.missingOnly
  delete query.invoiced
  router.replace({ path: '/tasks', query })
}

watch(
  () => [route.query.listId, route.query.folderId, route.query.spaceId, route.query.missingOnly, route.query.invoiced] as const,
  () => {
    if (!route.query.listId && !route.query.folderId && !route.query.spaceId) return
    if (route.query.missingOnly === 'false') missingOnly.value = false
    else if (route.query.missingOnly === 'true') missingOnly.value = true
    if (route.query.invoiced === 'all' || route.query.invoiced === 'yes' || route.query.invoiced === 'no') {
      invoicedFilter.value = route.query.invoiced
    }
  },
)

const taskFilters = computed(() => ({
  clientId: clientId.value,
  missingOnly: missingOnly.value,
  invoiced: invoicedFilter.value,
  projectFilter: projectFilter.value || undefined,
  createdMonth: createdMonthFilter.value || undefined,
  doneMonth: doneMonthFilter.value || undefined,
  statuses: statusFilters.value.length ? statusFilters.value : undefined,
  listId: listIdFilter.value || undefined,
  folderId: folderIdFilter.value || undefined,
  spaceId: spaceIdFilter.value || undefined,
}))

const { data: clients } = useClients()
const { data: agency } = useAgency()
const updateUiPreferences = useUpdateAgencyUiPreferences()
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
const updateBill = useUpdateTaskBill(taskFilters)
const updateBillableHours = useUpdateTaskBillableHours(taskFilters)
const updateNonBillableHours = useUpdateTaskNonBillableHours(taskFilters)
const syncTask = useSyncTask(taskFilters)
const savingBillId = ref<string | null>(null)
const savingBillableId = ref<string | null>(null)
const savingNonBillableId = ref<string | null>(null)
const syncingTaskId = ref<string | null>(null)
const syncTaskErrors = ref<Record<string, string>>({})
const billErrors = ref<Record<string, string>>({})
const billableWarnings = ref<Record<string, string>>({})
const nonBillableErrors = ref<Record<string, string>>({})

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
const taskDepthById = computed(() => {
  const list = tasks.value ?? []
  const byClickUpId = new Map<string, WorkTask>()
  for (const t of list) {
    if (t.clickUpTaskId) byClickUpId.set(t.clickUpTaskId, t)
  }
  const depths = new Map<string, number>()
  function depthOf(t: WorkTask, stack: Set<string>): number {
    const cached = depths.get(t.id)
    if (cached != null) return cached
    if (!t.clickUpParentId || !byClickUpId.has(t.clickUpParentId) || stack.has(t.id)) {
      depths.set(t.id, 0)
      return 0
    }
    stack.add(t.id)
    const parent = byClickUpId.get(t.clickUpParentId)!
    const d = depthOf(parent, stack) + 1
    stack.delete(t.id)
    depths.set(t.id, d)
    return d
  }
  for (const t of list) depthOf(t, new Set())
  return depths
})
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
const showClientColumn = computed(() => !groupByClient.value)
const taskGroups = computed(() => {
  const list = tasks.value ?? []
  if (!groupByClient.value) {
    return [{ key: 'all', clientId: '', clientName: '', showHeader: false, tasks: list }]
  }
  const groups: { key: string; clientId: string; clientName: string; showHeader: boolean; tasks: WorkTask[] }[] = []
  const indexById = new Map<string, number>()
  for (const t of list) {
    let i = indexById.get(t.clientId)
    if (i == null) {
      i = groups.length
      indexById.set(t.clientId, i)
      groups.push({
        key: t.clientId,
        clientId: t.clientId,
        clientName: t.clientName,
        showHeader: true,
        tasks: [],
      })
    }
    groups[i].tasks.push(t)
  }
  if (groupOrderMode.value === 'custom') {
    const orderIndex = new Map(customGroupOrder.value.map((id, i) => [id, i]))
    groups.sort((a, b) => {
      const ai = orderIndex.has(a.clientId) ? orderIndex.get(a.clientId)! : Number.MAX_SAFE_INTEGER
      const bi = orderIndex.has(b.clientId) ? orderIndex.get(b.clientId)! : Number.MAX_SAFE_INTEGER
      if (ai !== bi) return ai - bi
      return a.clientName.localeCompare(b.clientName, undefined, { sensitivity: 'base' })
    })
  } else {
    groups.sort((a, b) => a.clientName.localeCompare(b.clientName, undefined, { sensitivity: 'base' }))
  }
  return groups
})
const editColspan = computed(() =>
  9
  + (showIdColumn.value ? 1 : 0)
  + (showClientColumn.value ? 1 : 0)
  + (showListColumn.value ? 1 : 0)
  + (showProjectColumn.value ? 1 : 0)
  + (showClickUpHoursColumn.value ? 1 : 0)
  + (showInvoice.value ? 1 : 0))

function isGroupCollapsed(key: string) {
  return !!collapsedGroups.value[key]
}

function toggleGroup(key: string) {
  collapsedGroups.value = {
    ...collapsedGroups.value,
    [key]: !collapsedGroups.value[key],
  }
}

function collapseAllGroups() {
  const next: Record<string, boolean> = {}
  for (const group of taskGroups.value) {
    if (group.showHeader) next[group.key] = true
  }
  collapsedGroups.value = next
}

function expandAllGroups() {
  collapsedGroups.value = {}
}

function alphabeticalClientIds(groups: { clientId: string; clientName: string }[]) {
  return [...groups]
    .sort((a, b) => a.clientName.localeCompare(b.clientName, undefined, { sensitivity: 'base' }))
    .map((g) => g.clientId)
}

function mergeCustomOrder(existing: string[], groups: { clientId: string; clientName: string }[]) {
  const present = new Set(groups.map((g) => g.clientId))
  const kept = existing.filter((id) => present.has(id))
  const known = new Set(kept)
  const missing = groups
    .filter((g) => !known.has(g.clientId))
    .sort((a, b) => a.clientName.localeCompare(b.clientName, undefined, { sensitivity: 'base' }))
    .map((g) => g.clientId)
  return [...kept, ...missing]
}

async function persistCustomGroupOrder(order: string[]) {
  customGroupOrder.value = order
  try {
    await updateUiPreferences.mutateAsync(order)
  } catch {
    // keep local order; server sync can retry on next drag
  }
}

function onGroupDragStart(clientId: string, event: DragEvent) {
  draggingGroupId.value = clientId
  event.dataTransfer?.setData('text/plain', clientId)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onGroupDragOver(clientId: string, event: DragEvent) {
  if (groupOrderMode.value !== 'custom' || !draggingGroupId.value) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  dragOverGroupId.value = clientId
}

function onGroupDragLeave(clientId: string) {
  if (dragOverGroupId.value === clientId) dragOverGroupId.value = null
}

function onGroupDrop(targetClientId: string, event: DragEvent) {
  event.preventDefault()
  event.stopPropagation()
  const fromId = event.dataTransfer?.getData('text/plain') || draggingGroupId.value
  draggingGroupId.value = null
  dragOverGroupId.value = null
  if (!fromId || fromId === targetClientId || groupOrderMode.value !== 'custom') return

  const groups = taskGroups.value.filter((g) => g.showHeader)
  const order = mergeCustomOrder(customGroupOrder.value, groups)
  const fromIdx = order.indexOf(fromId)
  const toIdx = order.indexOf(targetClientId)
  if (fromIdx < 0 || toIdx < 0) return
  order.splice(fromIdx, 1)
  order.splice(toIdx, 0, fromId)
  void persistCustomGroupOrder(order)
}

function onGroupDragEnd() {
  draggingGroupId.value = null
  dragOverGroupId.value = null
}

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

async function syncOne(t: WorkTask) {
  if (!t.clickUpTaskId || syncingTaskId.value) return
  syncingTaskId.value = t.id
  delete syncTaskErrors.value[t.id]
  try {
    await syncTask.mutateAsync(t.id)
  } catch (e: any) {
    syncTaskErrors.value[t.id] = e?.response?.data?.error ?? e?.message ?? 'Sync failed.'
  } finally {
    syncingTaskId.value = null
  }
}

function parseHours(v: string): number | null {
  const s = v.trim()
  if (!s) return null
  const n = Number(s)
  return Number.isFinite(n) ? n : null
}

async function updateBillInline(t: WorkTask, value: string) {
  const bill = value.trim() || null
  const current = t.bill?.trim() || null
  if (bill === current) return

  savingBillId.value = t.id
  delete billErrors.value[t.id]
  try {
    await updateBill.mutateAsync({ id: t.id, bill })
  } catch (e: any) {
    billErrors.value[t.id] = e?.response?.data?.error ?? 'Could not save bill.'
  } finally {
    savingBillId.value = null
  }
}

async function updateBillableHoursInline(t: WorkTask, raw: string) {
  const hours = parseHours(raw)
  const current = t.billableHours
  if (hours === current || (hours == null && current == null)) return

  savingBillableId.value = t.id
  delete billableWarnings.value[t.id]
  try {
    const result = await updateBillableHours.mutateAsync({ id: t.id, hours })
    if (result.warning) billableWarnings.value[t.id] = result.warning
  } catch (e: any) {
    billableWarnings.value[t.id] = e?.response?.data?.error ?? 'Could not save billable hours.'
  } finally {
    savingBillableId.value = null
  }
}

async function updateNonBillableHoursInline(t: WorkTask, raw: string) {
  const hours = parseHours(raw)
  const current = t.nonBillableHours
  if (hours === current || (hours == null && current == null)) return

  savingNonBillableId.value = t.id
  delete nonBillableErrors.value[t.id]
  try {
    const result = await updateNonBillableHours.mutateAsync({ id: t.id, hours })
    if (result.warning) nonBillableErrors.value[t.id] = result.warning
  } catch (e: any) {
    nonBillableErrors.value[t.id] = e?.response?.data?.error ?? 'Could not save non-billable hours.'
  } finally {
    savingNonBillableId.value = null
  }
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
watch(groupByClient, (on) => {
  if (!on) collapsedGroups.value = {}
})
watch(
  () => agency.value?.uiPreferences?.taskGroupClientOrder,
  (order) => {
    if (Array.isArray(order)) customGroupOrder.value = [...order]
  },
  { immediate: true },
)
watch(
  [groupOrderMode, taskGroups],
  () => {
    if (!groupByClient.value || groupOrderMode.value !== 'custom') return
    const groups = taskGroups.value.filter((g) => g.showHeader)
    if (!groups.length) return
    const merged = mergeCustomOrder(customGroupOrder.value, groups)
    if (merged.length === customGroupOrder.value.length
      && merged.every((id, i) => id === customGroupOrder.value[i])) {
      return
    }
    if (customGroupOrder.value.length === 0) {
      customGroupOrder.value = alphabeticalClientIds(groups)
      return
    }
    customGroupOrder.value = merged
  },
  { deep: true },
)
watch(clientFilter, () => {
  editingId.value = null
  projectFilter.value = ''
  createdMonthFilter.value = ''
  doneMonthFilter.value = ''
})
watch(
  () => filterOptions.value?.statuses,
  (statuses) => {
    if (!statuses?.length) {
      statusFilters.value = []
      return
    }
    const available = new Set(statuses)
    const kept = statusFilters.value.filter((s) => available.has(s))
    if (statusFiltersRestored) {
      statusFilters.value = kept
      return
    }
    statusFilters.value = kept.length ? kept : [...statuses]
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

watch(
  [
    viewMode,
    clientFilter,
    missingOnly,
    invoicedFilter,
    showListColumn,
    showProjectColumn,
    showInvoiceColumn,
    showIdColumn,
    showClickUpHoursColumn,
    groupByClient,
    groupOrderMode,
    collapsedGroups,
    projectFilter,
    createdMonthFilter,
    doneMonthFilter,
    statusFilters,
  ],
  () => {
    const payload: StoredTaskFilters = {
      viewMode: viewMode.value,
      clientFilter: clientFilter.value,
      missingOnly: missingOnly.value,
      invoicedFilter: invoicedFilter.value,
      showListColumn: showListColumn.value,
      showProjectColumn: showProjectColumn.value,
      showInvoiceColumn: showInvoiceColumn.value,
      showIdColumn: showIdColumn.value,
      showClickUpHoursColumn: showClickUpHoursColumn.value,
      groupByClient: groupByClient.value,
      groupOrderMode: groupOrderMode.value,
      collapsedGroups: collapsedGroups.value,
      projectFilter: projectFilter.value,
      createdMonthFilter: createdMonthFilter.value,
      doneMonthFilter: doneMonthFilter.value,
      statusFilters: statusFilters.value,
    }
    localStorage.setItem(FILTERS_STORAGE_KEY, JSON.stringify(payload))
  },
  { deep: true },
)

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
      <div v-if="viewMode === 'list'" class="missing-legend" data-testid="tasks-missing-legend">
        <span class="legend-label">Missing data</span>
        <span v-for="item in missingBadgeLegend" :key="item.key" class="legend-item">
          <span class="missing-badge" :class="`missing-${item.key}`">{{ item.key }}</span>
          {{ item.label }}
        </span>
      </div>
    </div>

    <div class="filters">
      <div v-if="containerFilter" class="container-filter" data-testid="tasks-container-filter">
        <span class="filter-label">{{ containerFilter.label }}</span>
        <code>{{ containerFilter.id }}</code>
        <button type="button" class="linkish" data-testid="tasks-container-filter-clear" @click="clearContainerFilter">Clear</button>
      </div>
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
        <span class="filter-label">Group by</span>
        <div class="column-checks">
          <label class="check">
            <input v-model="groupByClient" type="checkbox" data-testid="tasks-group-by-client" />
            Client
          </label>
        </div>
      </div>
      <div v-if="viewMode === 'list'" class="column-toggles">
        <span class="filter-label">Columns</span>
        <div class="column-checks">
          <label class="check">
            <input v-model="showIdColumn" type="checkbox" data-testid="tasks-show-id-column" />
            Id
          </label>
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
              v-model="showClickUpHoursColumn"
              type="checkbox"
              data-testid="tasks-show-clickup-hours-column"
            />
            ClickUp hours
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
        <button
          type="button"
          class="status-popover-trigger"
          popovertarget="tasks-status-popover"
          data-testid="tasks-status-filter-trigger"
        >({{ statusFilters.length }}) statuses</button>
        <div
          id="tasks-status-popover"
          popover
          class="status-popover"
          data-testid="tasks-status-popover"
        >
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
    </div>

    <div
      v-if="viewMode === 'list' && groupByClient"
      class="group-actions"
      data-testid="tasks-group-actions"
    >
      <button
        type="button"
        class="link"
        data-testid="tasks-expand-all-groups"
        @click="expandAllGroups"
      >Expand all</button>
      <button
        type="button"
        class="link"
        data-testid="tasks-collapse-all-groups"
        @click="collapseAllGroups"
      >Collapse all</button>
      <span class="group-order-label">Group order</span>
      <label class="check">
        <input
          v-model="groupOrderMode"
          type="radio"
          value="alphabetical"
          data-testid="tasks-group-order-alphabetical"
        />
        Alphabetical
      </label>
      <label class="check">
        <input
          v-model="groupOrderMode"
          type="radio"
          value="custom"
          data-testid="tasks-group-order-custom"
        />
        Custom
      </label>
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
          <th v-if="showIdColumn">Id</th>
          <th v-if="showClientColumn">Client</th>
          <th v-if="showListColumn">List</th>
          <th v-if="showProjectColumn">Project</th>
          <th>Task</th>
          <th>Bill</th>
          <th>Billable hours</th>
          <th>Non-billable hours</th>
          <th v-if="showClickUpHoursColumn">ClickUp hours</th>
          <th v-if="showInvoice">Invoice</th>
          <th>Status</th>
          <th>Created</th>
          <th>Done</th>
          <th></th>
        </tr>
      </thead>
      <template v-for="group in taskGroups" :key="group.key">
        <thead v-if="group.showHeader">
          <tr
            class="group-header-row"
            :class="{
              'group-header-row--dragging': draggingGroupId === group.clientId,
              'group-header-row--drag-over': dragOverGroupId === group.clientId && draggingGroupId !== group.clientId,
            }"
            :data-testid="`task-group-${group.clientId}`"
            @click="toggleGroup(group.key)"
            @dragover="onGroupDragOver(group.clientId, $event)"
            @dragleave="onGroupDragLeave(group.clientId)"
            @drop="onGroupDrop(group.clientId, $event)"
          >
            <th
              :colspan="editColspan"
              class="group-header"
              role="button"
              tabindex="0"
              :aria-expanded="!isGroupCollapsed(group.key)"
              :aria-label="isGroupCollapsed(group.key) ? `Expand ${group.clientName}` : `Collapse ${group.clientName}`"
              @keydown.enter.prevent="toggleGroup(group.key)"
              @keydown.space.prevent="toggleGroup(group.key)"
            >
              <span
                v-if="groupOrderMode === 'custom'"
                class="group-drag-handle"
                draggable="true"
                title="Drag to reorder"
                :data-testid="`task-group-drag-${group.clientId}`"
                @click.stop
                @dragstart="onGroupDragStart(group.clientId, $event)"
                @dragend="onGroupDragEnd"
              >⠿</span>
              <span
                class="group-toggle"
                aria-hidden="true"
                :data-testid="`task-group-toggle-${group.clientId}`"
              >{{ isGroupCollapsed(group.key) ? '▶' : '▼' }}</span>
              {{ group.clientName }}
              <span class="group-count">({{ group.tasks.length }})</span>
            </th>
          </tr>
        </thead>
        <tbody v-show="!group.showHeader || !isGroupCollapsed(group.key)">
          <template v-for="t in group.tasks" :key="t.id">
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
              <td v-if="showIdColumn" class="id-col" :title="t.id" :data-testid="`task-id-${t.id}`">{{ t.shortId }}</td>
              <td v-if="showClientColumn" :data-testid="`task-client-${t.id}`">{{ t.clientName }}</td>
              <td v-if="showListColumn" :data-testid="`task-list-${t.id}`">{{ t.clickUpListName ?? '—' }}</td>
              <td v-if="showProjectColumn" :data-testid="`task-project-${t.id}`">{{ t.projectName ?? '—' }}</td>
              <td>
                <span
                  class="task-title-cell"
                  :class="{ 'task-title--child': (taskDepthById.get(t.id) ?? 0) > 0 }"
                  :style="{ '--task-depth': taskDepthById.get(t.id) ?? 0 }"
                >
                  <a
                    v-if="t.clickUpUrl"
                    class="task-title"
                    :href="t.clickUpUrl"
                    target="_blank"
                    rel="noopener"
                    :title="t.title"
                    :data-testid="`task-title-${t.id}`"
                  >{{ displayTitle(t.title) }}</a>
                  <span
                    v-else
                    class="task-title"
                    :title="t.title"
                    :data-testid="`task-title-${t.id}`"
                  >{{ displayTitle(t.title) }}</span>
                </span>
              </td>
              <td class="bill-cell" :data-testid="`task-bill-${t.id}`">
                <select
                  class="inline-select"
                  :value="t.bill ?? ''"
                  :disabled="savingBillId === t.id"
                  :data-testid="`task-bill-select-${t.id}`"
                  @change="updateBillInline(t, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="">—</option>
                  <option value="yes">yes</option>
                  <option value="no">no</option>
                </select>
                <span v-if="billErrors[t.id]" class="inline-error" :data-testid="`task-bill-error-${t.id}`">{{ billErrors[t.id] }}</span>
              </td>
              <td class="hours-cell" :data-testid="`task-billable-hours-${t.id}`">
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  class="inline-input"
                  :value="t.billableHours ?? ''"
                  :disabled="savingBillableId === t.id"
                  :data-testid="`task-billable-hours-input-${t.id}`"
                  @blur="updateBillableHoursInline(t, ($event.target as HTMLInputElement).value)"
                />
                <span v-if="billableWarnings[t.id]" class="inline-warning" :data-testid="`task-billable-hours-warning-${t.id}`">{{ billableWarnings[t.id] }}</span>
              </td>
              <td class="hours-cell" :data-testid="`task-non-billable-hours-${t.id}`">
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  class="inline-input"
                  :value="t.nonBillableHours ?? ''"
                  :disabled="savingNonBillableId === t.id"
                  :data-testid="`task-non-billable-hours-input-${t.id}`"
                  @blur="updateNonBillableHoursInline(t, ($event.target as HTMLInputElement).value)"
                />
                <span v-if="nonBillableErrors[t.id]" class="inline-error" :data-testid="`task-non-billable-hours-error-${t.id}`">{{ nonBillableErrors[t.id] }}</span>
              </td>
              <td
                v-if="showClickUpHoursColumn"
                :data-testid="`task-clickup-hours-${t.id}`"
              >{{ t.actualHours ?? '—' }}</td>
              <td v-if="showInvoice" :data-testid="`task-invoice-${t.id}`">{{ t.invoiceLabel ?? '—' }}</td>
              <td :data-testid="`task-status-${t.id}`">{{ t.clickUpStatus ?? '—' }}</td>
              <td :title="formatDateTime(t.dateCreated)" :data-testid="`task-date-created-${t.id}`">{{ formatDate(t.dateCreated) }}</td>
              <td :title="formatDateTime(t.dateDone)" :data-testid="`task-date-done-${t.id}`">{{ formatDate(t.dateDone) }}</td>
              <td>
                <div v-if="editingId !== t.id" class="row-actions">
                  <button
                    class="link"
                    :data-testid="`task-edit-${t.id}`"
                    @click="startEdit(t)"
                  >Edit</button>
                  <button
                    class="link"
                    :disabled="!t.clickUpTaskId || syncingTaskId === t.id"
                    :data-testid="`task-sync-${t.id}`"
                    @click="syncOne(t)"
                  >{{ syncingTaskId === t.id ? 'Syncing…' : 'Sync' }}</button>
                  <span
                    v-if="syncTaskErrors[t.id]"
                    class="inline-error"
                    :data-testid="`task-sync-error-${t.id}`"
                  >{{ syncTaskErrors[t.id] }}</span>
                </div>
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
      </template>
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
.id-col {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.75rem;
  color: #6b7280;
  white-space: nowrap;
}
.task-title-cell {
  --task-indent: 1.25rem;
  display: inline-block;
  padding-left: calc(var(--task-depth, 0) * var(--task-indent));
}
.task-title--child::before {
  content: '↳';
  display: inline-block;
  width: var(--task-indent);
  margin-left: calc(-1 * var(--task-indent));
  color: #6b7280;
  font-weight: 400;
  text-decoration: none;
  text-align: left;
}
.table-wrap {
  width: 100%;
  overflow-x: auto;
}
.header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}
.missing-legend {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.65rem 1rem;
  margin-left: auto;
  font-size: 0.85rem;
  color: #4b5563;
}
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
.container-filter {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.container-filter code {
  font-size: 0.8rem;
  color: #111827;
}
.container-filter .linkish {
  align-self: start;
  padding: 0;
  border: none;
  background: none;
  color: #059669;
  cursor: pointer;
  font-size: 0.8rem;
}
.group-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1rem;
  margin: -0.35rem 0 0.75rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.group-order-label {
  margin-left: 0.5rem;
  font-weight: 600;
  color: #374151;
}
.group-actions .check {
  display: inline-flex;
  flex-direction: row;
  align-items: center;
  gap: 0.35rem;
}
.column-toggles { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filter-label { line-height: 1.2; }
.column-checks { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; padding-bottom: 0.35rem; }
.column-checks .check { padding-bottom: 0; }
.status-filters {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
  padding-bottom: 0.35rem;
}
.status-popover-trigger {
  anchor-name: --tasks-status-trigger;
  align-self: flex-start;
  padding: 0.35rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: #fff;
  color: #374151;
  font: inherit;
  cursor: pointer;
}
.status-popover-trigger:hover {
  background: #f9fafb;
  border-color: #9ca3af;
}
.status-popover {
  margin: 0;
  inset: unset;
  position-anchor: --tasks-status-trigger;
  top: anchor(bottom);
  left: anchor(left);
  margin-top: 0.35rem;
  padding: 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #fff;
  color: #374151;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.12);
}
.status-checks {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  align-items: flex-start;
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
.group-header-row { cursor: pointer; }
.group-header-row--dragging { opacity: 0.55; }
.group-header-row--drag-over .group-header {
  box-shadow: inset 0 2px 0 #059669;
}
.group-header {
  background: #f3f4f6;
  color: #111827;
  font-weight: 600;
  border-bottom: 1px solid #d1d5db;
  padding-top: 0.65rem;
  padding-bottom: 0.65rem;
  user-select: none;
}
.group-header:hover { background: #e5e7eb; }
.group-drag-handle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.25rem;
  margin-right: 0.2rem;
  color: #9ca3af;
  font-size: 0.85rem;
  line-height: 1;
  cursor: grab;
  vertical-align: middle;
}
.group-drag-handle:active { cursor: grabbing; }
.group-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  margin-right: 0.35rem;
  color: #4b5563;
  font-size: 0.7rem;
  line-height: 1;
}
.group-count {
  margin-left: 0.35rem;
  color: #6b7280;
  font-weight: 500;
}
.bill-cell { min-width: 4.5rem; }
.hours-cell { min-width: 5rem; }
.inline-select {
  padding: 0.25rem 0.35rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  font-size: 0.85rem;
  background: #fff;
}
.inline-input {
  width: 4.5rem;
  padding: 0.25rem 0.35rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  font-size: 0.85rem;
  background: #fff;
}
.inline-warning {
  display: block;
  margin-top: 0.2rem;
  font-size: 0.72rem;
  color: #b45309;
}
.inline-error {
  display: block;
  margin-top: 0.2rem;
  font-size: 0.72rem;
  color: #b91c1c;
}
.link { background: none; border: none; color: #059669; cursor: pointer; padding: 0; font: inherit; }
.link:disabled { opacity: 0.6; cursor: default; }
.row-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.65rem;
}
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
