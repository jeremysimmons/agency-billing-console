<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ToggleSwitch from 'primevue/toggleswitch'
import ProgressSpinner from 'primevue/progressspinner'
import { useClients } from '../queries/clients'
import { useProjects, useCreateProject } from '../queries/projects'
import { useInvoices, useCreateInvoice } from '../queries/invoices'
import { useAgency, useUpdateAgencyUiPreferences } from '../queries/agency'
import { useTasks, useTaskSummary, useTaskFilterOptions, useUpdateTaskBill, useUpdateTaskProject, useUpdateTaskInvoice, useUpdateTaskBillableHours, useUpdateTaskNonBillableHours, useUpdateTaskFlatFee, useUpdateTaskPrep, useSyncTask, type InvoicedFilter } from '../queries/tasks'
import { http } from '../api/http'
import type { Invoice, Project, WorkTask } from '../api/types'

const FILTERS_STORAGE_KEY = 'aib.tasks.filters'
const INVOICED_OPTIONS: { value: InvoicedFilter; label: string }[] = [
  { value: 'paid', label: 'Paid' },
  { value: 'pending', label: 'Pending' },
  { value: 'none', label: 'None' },
]

type GroupOrderMode = 'alphabetical' | 'custom'

type StoredTaskFilters = {
  viewMode?: 'list' | 'clients' | 'months'
  clientFilter?: string
  missingOnly?: boolean
  invoicedFilter?: InvoicedFilter[] | 'all' | 'paid' | 'pending' | 'none' | 'yes' | 'no'
  showListColumn?: boolean
  showProjectColumn?: boolean
  showInvoiceColumn?: boolean
  showIdColumn?: boolean
  showClickUpIdColumn?: boolean
  showClickUpHoursColumn?: boolean
  showClickUpEstimateColumn?: boolean
  groupByClient?: boolean
  groupOrderMode?: GroupOrderMode
  collapsedGroups?: Record<string, boolean>
  projectFilter?: string
  clickUpIdFilter?: string
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

function normalizeInvoicedFilters(value: unknown): InvoicedFilter[] {
  const allowed = new Set<InvoicedFilter>(['paid', 'pending', 'none'])
  if (Array.isArray(value)) {
    return value.filter((v): v is InvoicedFilter => typeof v === 'string' && allowed.has(v as InvoicedFilter))
  }
  if (value === 'paid' || value === 'pending' || value === 'none') return [value]
  if (value === 'no') return ['none']
  if (value === 'all' || value === 'yes') return ['paid', 'pending', 'none']
  return ['none']
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
const invoicedFilters = ref<InvoicedFilter[]>(
  normalizeInvoicedFilters(route.query.invoiced ?? storedFilters.invoicedFilter),
)
const showListColumn = ref(typeof storedFilters.showListColumn === 'boolean' ? storedFilters.showListColumn : false)
const showProjectColumn = ref(typeof storedFilters.showProjectColumn === 'boolean' ? storedFilters.showProjectColumn : false)
const showInvoiceColumn = ref(
  typeof storedFilters.showInvoiceColumn === 'boolean' ? storedFilters.showInvoiceColumn : true,
)
const showIdColumn = ref(typeof storedFilters.showIdColumn === 'boolean' ? storedFilters.showIdColumn : true)
const showClickUpIdColumn = ref(
  typeof storedFilters.showClickUpIdColumn === 'boolean' ? storedFilters.showClickUpIdColumn : false,
)
const showClickUpHoursColumn = ref(
  typeof storedFilters.showClickUpHoursColumn === 'boolean' ? storedFilters.showClickUpHoursColumn : false,
)
const showClickUpEstimateColumn = ref(
  typeof storedFilters.showClickUpEstimateColumn === 'boolean' ? storedFilters.showClickUpEstimateColumn : false,
)
const groupByClient = ref(typeof storedFilters.groupByClient === 'boolean' ? storedFilters.groupByClient : false)
const groupOrderMode = ref<GroupOrderMode>(
  storedFilters.groupOrderMode === 'custom' ? 'custom' : 'alphabetical',
)
const groupOrderCustom = computed({
  get: () => groupOrderMode.value === 'custom',
  set: (v: boolean) => {
    groupOrderMode.value = v ? 'custom' : 'alphabetical'
  },
})
const customGroupOrder = ref<string[]>([])
const draggingGroupId = ref<string | null>(null)
const dragOverGroupId = ref<string | null>(null)
const collapsedGroups = ref<Record<string, boolean>>(
  storedFilters.collapsedGroups && typeof storedFilters.collapsedGroups === 'object'
    ? { ...storedFilters.collapsedGroups }
    : {},
)
const projectFilter = ref(storedFilters.projectFilter || '')
const clickUpIdFilter = ref(storedFilters.clickUpIdFilter || '')
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

function clearScopeFilters() {
  createdMonthFilter.value = ''
  doneMonthFilter.value = ''
  projectFilter.value = ''
  clickUpIdFilter.value = ''
  clientFilter.value = ''
  if (route.query.clientId) {
    const query = { ...route.query }
    delete query.clientId
    router.replace({ path: '/tasks', query })
  }
}

function filterTasksByClickUpId(list: WorkTask[], query: string): WorkTask[] {
  const q = query.trim().toLowerCase()
  if (!q) return list

  const childrenByParentClickUpId = new Map<string, WorkTask[]>()
  for (const t of list) {
    if (!t.clickUpParentId) continue
    const kids = childrenByParentClickUpId.get(t.clickUpParentId)
    if (kids) kids.push(t)
    else childrenByParentClickUpId.set(t.clickUpParentId, [t])
  }

  const matched = new Set<string>()
  function includeWithDescendants(t: WorkTask) {
    if (matched.has(t.id)) return
    matched.add(t.id)
    if (!t.clickUpTaskId) return
    for (const child of childrenByParentClickUpId.get(t.clickUpTaskId) ?? []) {
      includeWithDescendants(child)
    }
  }

  for (const t of list) {
    if (t.clickUpTaskId?.toLowerCase().includes(q)) includeWithDescendants(t)
  }
  return list.filter((t) => matched.has(t.id))
}

watch(
  () => [route.query.listId, route.query.folderId, route.query.spaceId, route.query.missingOnly, route.query.invoiced] as const,
  () => {
    if (!route.query.listId && !route.query.folderId && !route.query.spaceId) return
    if (route.query.missingOnly === 'false') missingOnly.value = false
    else if (route.query.missingOnly === 'true') missingOnly.value = true
    if (route.query.invoiced != null) {
      invoicedFilters.value = normalizeInvoicedFilters(route.query.invoiced)
    }
  },
)

const taskFilters = computed(() => ({
  clientId: clientId.value,
  missingOnly: missingOnly.value,
  invoiced: invoicedFilters.value.length ? invoicedFilters.value : undefined,
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
const { data: filterProjects } = useProjects(clientId, { includeShared: true })
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
const updateProject = useUpdateTaskProject(taskFilters)
const updateInvoice = useUpdateTaskInvoice(taskFilters)
const updateBillableHours = useUpdateTaskBillableHours(taskFilters)
const updateNonBillableHours = useUpdateTaskNonBillableHours(taskFilters)
const updateFlatFee = useUpdateTaskFlatFee(taskFilters)
const syncTask = useSyncTask(taskFilters)
const createProject = useCreateProject()
const { data: invoices } = useInvoices()
const createInvoice = useCreateInvoice()
const savingBillId = ref<string | null>(null)
const savingBillableId = ref<string | null>(null)
const savingNonBillableId = ref<string | null>(null)
const savingFlatFeeId = ref<string | null>(null)
const savingProjectId = ref<string | null>(null)
const savingInvoiceId = ref<string | null>(null)
const syncingTaskId = ref<string | null>(null)
const syncTaskErrors = ref<Record<string, string>>({})
const billErrors = ref<Record<string, string>>({})
const billableWarnings = ref<Record<string, string>>({})
const nonBillableErrors = ref<Record<string, string>>({})
const flatFeeErrors = ref<Record<string, string>>({})
const projectErrors = ref<Record<string, string>>({})
const invoiceErrors = ref<Record<string, string>>({})
const projectsByClient = ref<Record<string, Project[]>>({})
const loadingProjectsClientId = ref<string | null>(null)
const addingProjectTaskId = ref<string | null>(null)
const newProjectName = ref('')
const ADD_PROJECT_VALUE = '__add_project__'
const addingInvoiceTaskId = ref<string | null>(null)
const newInvoiceName = ref('')
const ADD_INVOICE_VALUE = '__add_invoice__'

const editingId = ref<string | null>(null)
const draft = ref({
  projectId: '' as string,
  bill: '' as string,
  billableHours: '' as string,
  nonBillableHours: '' as string,
  flatFee: '' as string,
  invoiceLabel: '' as string,
  note: '' as string,
})
const saveError = ref('')

const editClientId = computed(() => {
  const t = tasks.value?.find((x) => x.id === editingId.value)
  return t?.clientId
})
const { data: projects } = useProjects(editClientId, { includeShared: true })

const filteredTasks = computed(() => filterTasksByClickUpId(tasks.value ?? [], clickUpIdFilter.value))
const missingCount = computed(() => filteredTasks.value.filter((t) => t.needsAttention).length)
const childrenByParentClickUpId = computed(() => {
  const map = new Map<string, WorkTask[]>()
  for (const t of tasks.value ?? []) {
    if (!t.clickUpParentId) continue
    const kids = map.get(t.clickUpParentId)
    if (kids) kids.push(t)
    else map.set(t.clickUpParentId, [t])
  }
  return map
})
const parentIdsWithChildren = computed(() => {
  const ids = new Set<string>()
  for (const t of tasks.value ?? []) {
    if (t.clickUpTaskId && childrenByParentClickUpId.value.has(t.clickUpTaskId)) {
      ids.add(t.id)
    }
  }
  return ids
})
function taskHasChildren(t: WorkTask) {
  return parentIdsWithChildren.value.has(t.id)
}
function billIsYesOrNo(t: WorkTask) {
  const bill = t.bill?.trim().toLowerCase()
  return bill === 'yes' || bill === 'no'
}
function descendantsOf(t: WorkTask): WorkTask[] {
  if (!t.clickUpTaskId) return []
  const out: WorkTask[] = []
  const queue = [...(childrenByParentClickUpId.value.get(t.clickUpTaskId) ?? [])]
  while (queue.length > 0) {
    const child = queue.shift()!
    out.push(child)
    if (child.clickUpTaskId) {
      queue.push(...(childrenByParentClickUpId.value.get(child.clickUpTaskId) ?? []))
    }
  }
  return out
}
const taskDepthById = computed(() => {
  const list = filteredTasks.value
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
  if (invoicedFilters.value.includes('none')) return true
  return showInvoiceColumn.value
})
const visibleColumnCount = computed(() =>
  [
    showIdColumn.value,
    showClickUpIdColumn.value,
    showListColumn.value,
    showProjectColumn.value,
    showClickUpEstimateColumn.value,
    showClickUpHoursColumn.value,
    showInvoice.value,
  ].filter(Boolean).length,
)
const showClientColumn = computed(() => !groupByClient.value)
const taskGroups = computed(() => {
  const list = filteredTasks.value
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
  10
  + (showIdColumn.value ? 1 : 0)
  + (showClickUpIdColumn.value ? 1 : 0)
  + (showClientColumn.value ? 1 : 0)
  + (showListColumn.value ? 1 : 0)
  + (showProjectColumn.value ? 1 : 0)
  + (showClickUpEstimateColumn.value ? 1 : 0)
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
  if (isComplete(t)) return false
  if (t.bill?.toLowerCase() !== 'yes') return false
  if (t.flatFee != null) return false
  const eitherPopulated = t.billableHours != null || t.nonBillableHours != null
  const anyPositive = (t.billableHours ?? 0) > 0 || (t.nonBillableHours ?? 0) > 0
  return !(eitherPopulated && anyPositive)
}

function isComplete(t: WorkTask) {
  return t.clickUpStatus?.trim().toLowerCase() === 'cancelled'
    && t.bill?.trim().toLowerCase() === 'no'
}

function isTaskMissing(t: WorkTask, key: string) {
  if (isComplete(t)) return false
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
    flatFee: t.flatFee != null ? String(t.flatFee) : '',
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

function projectsForClient(clientId: string): Project[] {
  return projectsByClient.value[clientId] ?? []
}

function projectOptionLabel(p: Project, taskClientId: string) {
  return p.clientId !== taskClientId ? `${p.name} (Shared)` : p.name
}

async function ensureProjectsLoaded(clientId: string) {
  if (projectsByClient.value[clientId] || loadingProjectsClientId.value === clientId) return
  loadingProjectsClientId.value = clientId
  try {
    const list = (await http.get<Project[]>('/projects', {
      params: { clientId, includeShared: true },
    })).data
    projectsByClient.value = { ...projectsByClient.value, [clientId]: list }
  } catch {
    projectsByClient.value = { ...projectsByClient.value, [clientId]: [] }
  } finally {
    if (loadingProjectsClientId.value === clientId) loadingProjectsClientId.value = null
  }
}

function rememberProject(project: Project, forClientId?: string) {
  const targets = new Set<string>([project.clientId])
  if (forClientId) targets.add(forClientId)
  let next = { ...projectsByClient.value }
  for (const cid of targets) {
    const existing = next[cid] ?? []
    if (existing.some((p) => p.id === project.id)) continue
    next = {
      ...next,
      [cid]: [...existing, project].sort((a, b) => a.name.localeCompare(b.name)),
    }
  }
  projectsByClient.value = next
}

async function updateProjectInline(t: WorkTask, projectId: string | null) {
  const current = t.projectId ?? null
  // Re-calling with the same project still cascades to unassigned descendants.
  if (projectId === current && !(projectId && taskHasChildren(t))) return

  savingProjectId.value = t.id
  delete projectErrors.value[t.id]
  try {
    await updateProject.mutateAsync({ id: t.id, projectId })
  } catch (e: any) {
    projectErrors.value[t.id] = e?.response?.data?.error ?? 'Could not save project.'
  } finally {
    savingProjectId.value = null
  }
}

async function applyProjectToUnassignedChildren(t: WorkTask) {
  if (!t.projectId || !taskHasChildren(t)) return
  await updateProjectInline(t, t.projectId)
}

async function onProjectSelect(t: WorkTask, value: string, selectEl: HTMLSelectElement) {
  if (value === ADD_PROJECT_VALUE) {
    selectEl.value = t.projectId ?? ''
    addingProjectTaskId.value = t.id
    newProjectName.value = ''
    return
  }
  await updateProjectInline(t, value || null)
}

function cancelAddProject() {
  addingProjectTaskId.value = null
  newProjectName.value = ''
}

async function confirmAddProject(t: WorkTask) {
  const name = newProjectName.value.trim()
  if (!name) return
  savingProjectId.value = t.id
  delete projectErrors.value[t.id]
  try {
    const project = await createProject.mutateAsync({ clientId: t.clientId, name })
    rememberProject(project, t.clientId)
    addingProjectTaskId.value = null
    newProjectName.value = ''
    await updateProject.mutateAsync({ id: t.id, projectId: project.id })
  } catch (e: any) {
    projectErrors.value[t.id] = e?.response?.data?.error ?? 'Could not create project.'
  } finally {
    savingProjectId.value = null
  }
}

function invoiceStatusKey(status: string) {
  const s = status.trim().toLowerCase().replaceAll(' ', '-').replaceAll('_', '-')
  if (s === 'partiallypaid') return 'partially-paid'
  if (s === 'fullypaid') return 'fully-paid'
  return s
}

function isInvoiceAssignable(inv: Invoice) {
  return invoiceStatusKey(inv.status) === 'preparing'
}

function formatInvoiceStatus(status: string) {
  switch (invoiceStatusKey(status)) {
    case 'preparing': return 'preparing'
    case 'sent': return 'sent'
    case 'partially-paid': return 'partially-paid'
    case 'fully-paid': return 'fully paid'
    default: return status
  }
}

function invoiceOptionLabel(inv: Invoice) {
  if (inv.name.trim().toLowerCase() === 'none' || isInvoiceAssignable(inv)) return inv.name
  return `${inv.name} (${formatInvoiceStatus(inv.status)})`
}

const assignableInvoices = computed(() =>
  (invoices.value ?? [])
    .filter(isInvoiceAssignable)
    .slice()
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name)),
)

function invoiceOptionsFor(t: WorkTask): Invoice[] {
  const list = assignableInvoices.value
  const current = t.invoiceLabel?.trim()
  if (!current) return list
  if (list.some((i) => i.name === current)) return list
  const existing = (invoices.value ?? []).find((i) => i.name === current)
  return existing
    ? [...list, existing]
    : [...list, { id: current, name: current, status: 'fully-paid', sortOrder: Number.MAX_SAFE_INTEGER }]
}

async function updateInvoiceInline(t: WorkTask, invoiceLabel: string | null) {
  const current = t.invoiceLabel?.trim() || null
  const next = invoiceLabel?.trim() || null
  if (next === current) return

  savingInvoiceId.value = t.id
  delete invoiceErrors.value[t.id]
  try {
    await updateInvoice.mutateAsync({ id: t.id, invoiceLabel: next })
  } catch (e: any) {
    invoiceErrors.value[t.id] = e?.response?.data?.error ?? 'Could not save invoice.'
  } finally {
    savingInvoiceId.value = null
  }
}

async function onInvoiceSelect(t: WorkTask, value: string, selectEl: HTMLSelectElement) {
  if (value === ADD_INVOICE_VALUE) {
    selectEl.value = t.invoiceLabel ?? ''
    addingInvoiceTaskId.value = t.id
    newInvoiceName.value = ''
    return
  }
  await updateInvoiceInline(t, value || null)
}

function cancelAddInvoice() {
  addingInvoiceTaskId.value = null
  newInvoiceName.value = ''
}

async function confirmAddInvoice(t: WorkTask) {
  const name = newInvoiceName.value.trim()
  if (!name) return
  savingInvoiceId.value = t.id
  delete invoiceErrors.value[t.id]
  try {
    const invoice = await createInvoice.mutateAsync({ name })
    addingInvoiceTaskId.value = null
    newInvoiceName.value = ''
    await updateInvoice.mutateAsync({ id: t.id, invoiceLabel: invoice.name })
  } catch (e: any) {
    invoiceErrors.value[t.id] = e?.response?.data?.error ?? 'Could not create invoice.'
  } finally {
    savingInvoiceId.value = null
  }
}

function parseHours(v: string): number | null {
  const s = v.trim()
  if (!s) return null
  const n = Number(s)
  return Number.isFinite(n) ? n : null
}

function applyClickUpHoursForBill(
  bill: string | null | undefined,
  billableHours: string,
  nonBillableHours: string,
  clickUpHours: number | null | undefined,
): { billableHours: string; nonBillableHours: string } {
  const billNorm = bill?.trim().toLowerCase()
  if (billNorm === 'yes' && !billableHours.trim() && clickUpHours != null) {
    return { billableHours: String(clickUpHours), nonBillableHours }
  }
  if (billNorm === 'no' && !nonBillableHours.trim()) {
    return { billableHours, nonBillableHours: String(clickUpHours ?? 0) }
  }
  return { billableHours, nonBillableHours }
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
    return
  } finally {
    savingBillId.value = null
  }

  // Re-read task after mutation so hours checks use updated fields when available.
  const latest = tasks.value?.find((x) => x.id === t.id) ?? t
  const clickUpHours = latest.actualHours
  const billNorm = bill?.toLowerCase()
  if (billNorm === 'yes' && latest.billableHours == null && clickUpHours != null) {
    await updateBillableHoursInline(latest, String(clickUpHours))
  } else if (billNorm === 'no' && latest.nonBillableHours == null) {
    await updateNonBillableHoursInline(latest, String(clickUpHours ?? 0))
  }
}

async function applyBillToAllChildren(t: WorkTask) {
  const bill = t.bill?.trim().toLowerCase()
  if (bill !== 'yes' && bill !== 'no') return
  const kids = descendantsOf(t)
  if (kids.length === 0) return
  for (const child of kids) {
    const latest = tasks.value?.find((x) => x.id === child.id) ?? child
    await updateBillInline(latest, bill)
  }
}

function onEditBillChange(t: WorkTask) {
  const filled = applyClickUpHoursForBill(
    draft.value.bill,
    draft.value.billableHours,
    draft.value.nonBillableHours,
    t.actualHours,
  )
  draft.value.billableHours = filled.billableHours
  draft.value.nonBillableHours = filled.nonBillableHours
  if (draft.value.bill?.trim().toLowerCase() === 'no' && !draft.value.invoiceLabel.trim()) {
    draft.value.invoiceLabel = 'none'
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

async function updateFlatFeeInline(t: WorkTask, raw: string) {
  const flatFee = parseHours(raw)
  const current = t.flatFee
  if (flatFee === current || (flatFee == null && current == null)) return

  savingFlatFeeId.value = t.id
  delete flatFeeErrors.value[t.id]
  try {
    await updateFlatFee.mutateAsync({ id: t.id, flatFee })
  } catch (e: any) {
    flatFeeErrors.value[t.id] = e?.response?.data?.error ?? 'Could not save flat fee.'
  } finally {
    savingFlatFeeId.value = null
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
        flatFee: parseHours(draft.value.flatFee),
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
watch(
  [clientId, filterProjects],
  ([cid, list]) => {
    if (cid && list) {
      projectsByClient.value = { ...projectsByClient.value, [cid]: [...list] }
    }
  },
  { immediate: true },
)
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
    // Options still loading — keep restored selection; do not wipe/persist empty.
    if (!statuses?.length) return
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
watch(invoicedFilters, () => { editingId.value = null }, { deep: true })
watch([createdMonthFilter, doneMonthFilter], () => { editingId.value = null })
watch(statusFilters, () => { editingId.value = null }, { deep: true })

watch(
  [
    viewMode,
    clientFilter,
    missingOnly,
    invoicedFilters,
    showListColumn,
    showProjectColumn,
    showInvoiceColumn,
    showIdColumn,
    showClickUpIdColumn,
    showClickUpEstimateColumn,
    showClickUpHoursColumn,
    groupByClient,
    groupOrderMode,
    collapsedGroups,
    projectFilter,
    clickUpIdFilter,
    createdMonthFilter,
    doneMonthFilter,
    statusFilters,
  ],
  () => {
    const payload: StoredTaskFilters = {
      viewMode: viewMode.value,
      clientFilter: clientFilter.value,
      missingOnly: missingOnly.value,
      invoicedFilter: invoicedFilters.value,
      showListColumn: showListColumn.value,
      showProjectColumn: showProjectColumn.value,
      showInvoiceColumn: showInvoiceColumn.value,
      showIdColumn: showIdColumn.value,
      showClickUpIdColumn: showClickUpIdColumn.value,
      showClickUpEstimateColumn: showClickUpEstimateColumn.value,
      showClickUpHoursColumn: showClickUpHoursColumn.value,
      groupByClient: groupByClient.value,
      groupOrderMode: groupOrderMode.value,
      collapsedGroups: collapsedGroups.value,
      projectFilter: projectFilter.value,
      clickUpIdFilter: clickUpIdFilter.value,
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

function invertStatusFilters() {
  const all = filterOptions.value?.statuses ?? []
  const selected = new Set(statusFilters.value)
  statusFilters.value = all.filter((s) => !selected.has(s))
}

function selectAllStatusFilters() {
  statusFilters.value = [...(filterOptions.value?.statuses ?? [])]
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
      <div class="filter-clear">
        <span class="filter-label" aria-hidden="true">&nbsp;</span>
        <button
          type="button"
          class="filter-clear-btn"
          data-testid="tasks-clear-filters"
          @click="clearScopeFilters"
        >Clear</button>
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
          <option v-for="p in filterProjects" :key="p.id" :value="p.id">
            {{ clientFilter && p.clientId !== clientFilter ? `${p.name} (Shared)` : p.name }}
          </option>
        </select>
      </label>
      <label>
        ClickUp ID
        <input
          v-model="clickUpIdFilter"
          type="search"
          placeholder="Contains…"
          data-testid="tasks-clickup-id-filter"
        />
      </label>
      <div class="invoiced-filters">
        <span class="filter-label">Invoiced</span>
        <button
          type="button"
          class="invoiced-popover-trigger"
          popovertarget="tasks-invoiced-popover"
          data-testid="tasks-invoiced-filter-trigger"
        >({{ invoicedFilters.length }})</button>
        <div
          id="tasks-invoiced-popover"
          popover
          class="invoiced-popover"
          data-testid="tasks-invoiced-popover"
        >
          <div class="invoiced-checks">
            <label v-for="opt in INVOICED_OPTIONS" :key="opt.value" class="check">
              <input
                v-model="invoicedFilters"
                type="checkbox"
                :value="opt.value"
                :data-testid="`tasks-invoiced-filter-${opt.value}`"
              />
              {{ opt.label }}
            </label>
          </div>
        </div>
      </div>
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
      <div v-if="viewMode === 'list' && groupByClient" class="toggle-field">
        <span id="tasks-group-order-label" class="filter-label">Group order</span>
        <div class="toggle-row">
          <span class="toggle-side" :class="{ active: !groupOrderCustom }" data-testid="tasks-group-order-alphabetical">A-Z</span>
          <ToggleSwitch
            v-model="groupOrderCustom"
            aria-labelledby="tasks-group-order-label"
            :pt="{ input: { 'data-testid': 'tasks-group-order-custom' } }"
          />
          <span class="toggle-side" :class="{ active: groupOrderCustom }" data-testid="tasks-group-order-custom-label">Custom</span>
        </div>
      </div>
      <div v-if="viewMode === 'list'" class="column-filters">
        <span class="filter-label">Columns</span>
        <button
          type="button"
          class="column-popover-trigger"
          popovertarget="tasks-columns-popover"
          data-testid="tasks-columns-filter-trigger"
        >({{ visibleColumnCount }})</button>
        <div
          id="tasks-columns-popover"
          popover
          class="column-popover"
          data-testid="tasks-columns-popover"
        >
          <div class="column-popover-checks">
            <label class="check">
              <input v-model="showIdColumn" type="checkbox" data-testid="tasks-show-id-column" />
              Id
            </label>
            <label class="check">
              <input
                v-model="showClickUpIdColumn"
                type="checkbox"
                data-testid="tasks-show-clickup-id-column"
              />
              ClickUp Id
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
                v-model="showClickUpEstimateColumn"
                type="checkbox"
                data-testid="tasks-show-clickup-estimate-column"
              />
              ClickUp estimate
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
      </div>
      <div class="status-filters">
        <span class="filter-label">Statuses</span>
        <button
          type="button"
          class="status-popover-trigger"
          popovertarget="tasks-status-popover"
          data-testid="tasks-status-filter-trigger"
        >({{ statusFilters.length }})</button>
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
          <div class="status-popover-actions">
            <button
              type="button"
              class="status-action"
              data-testid="tasks-status-filter-all"
              @click="selectAllStatusFilters"
            >All</button>
            <button
              type="button"
              class="status-action"
              data-testid="tasks-status-filter-invert"
              @click="invertStatusFilters"
            >Invert</button>
          </div>
        </div>
      </div>
    </div>

    <template v-if="viewMode === 'list'">
    <p v-if="isLoading" data-testid="tasks-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="tasks-error">Failed to load tasks.</p>
    <p v-else-if="tasks && filteredTasks.length === 0" class="empty" data-testid="tasks-empty">
      No tasks match. Sync from ClickUp or clear the missing-data filter.
    </p>

    <div v-else class="table-wrap">
    <table class="grid" data-testid="tasks-table">
      <thead>
        <tr>
          <th class="flags-col" :aria-label="groupByClient ? undefined : 'Missing data'">
            <div
              v-if="groupByClient"
              class="group-expand-collapse"
              data-testid="tasks-group-actions"
            >
              <button
                type="button"
                class="link icon-btn"
                data-testid="tasks-expand-all-groups"
                aria-label="Expand all"
                title="Expand all"
                @click="expandAllGroups"
              ><span class="material-icons" aria-hidden="true">unfold_more</span></button>
              <button
                type="button"
                class="link icon-btn"
                data-testid="tasks-collapse-all-groups"
                aria-label="Collapse all"
                title="Collapse all"
                @click="collapseAllGroups"
              ><span class="material-icons" aria-hidden="true">unfold_less</span></button>
            </div>
          </th>
          <th v-if="showIdColumn">Id</th>
          <th v-if="showClickUpIdColumn">ClickUp Id</th>
          <th v-if="showClientColumn">Client</th>
          <th v-if="showListColumn">List</th>
          <th v-if="showProjectColumn">Project</th>
          <th>Task</th>
          <th>Status</th>
          <th>Bill</th>
          <th>Billable hours</th>
          <th>Flat fee</th>
          <th>Non-billable hours</th>
          <th v-if="showClickUpEstimateColumn">ClickUp estimate</th>
          <th v-if="showClickUpHoursColumn">ClickUp hours</th>
          <th v-if="showInvoice">Invoice</th>
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
              <td v-if="showIdColumn" class="id-col" :title="t.id" :data-testid="`task-id-${t.id}`">
                <span class="cell-text">{{ t.shortId }}</span>
              </td>
              <td
                v-if="showClickUpIdColumn"
                class="id-col"
                :data-testid="`task-clickup-id-${t.id}`"
              >
                <span class="cell-text">
                  <a
                    v-if="t.clickUpTaskId && t.clickUpUrl"
                    :href="t.clickUpUrl"
                    target="_blank"
                    rel="noopener"
                    :data-testid="`task-clickup-id-link-${t.id}`"
                  >{{ t.clickUpTaskId }}</a>
                  <template v-else>{{ t.clickUpTaskId ?? '—' }}</template>
                </span>
              </td>
              <td v-if="showClientColumn" :data-testid="`task-client-${t.id}`">
                <span class="cell-text">{{ t.clientName }}</span>
              </td>
              <td v-if="showListColumn" :data-testid="`task-list-${t.id}`">
                <span class="cell-text">{{ t.clickUpListName ?? '—' }}</span>
              </td>
              <td v-if="showProjectColumn" class="project-cell" :data-testid="`task-project-${t.id}`">
                <div v-if="addingProjectTaskId === t.id" class="add-project">
                  <input
                    v-model="newProjectName"
                    type="text"
                    class="inline-input"
                    placeholder="Project name"
                    :disabled="savingProjectId === t.id"
                    :data-testid="`task-project-new-name-${t.id}`"
                    @keydown.enter.prevent="confirmAddProject(t)"
                    @keydown.escape.prevent="cancelAddProject"
                  />
                  <button
                    type="button"
                    class="link"
                    :disabled="savingProjectId === t.id || !newProjectName.trim()"
                    :data-testid="`task-project-new-save-${t.id}`"
                    @click="confirmAddProject(t)"
                  >Add</button>
                  <button
                    type="button"
                    class="link"
                    :disabled="savingProjectId === t.id"
                    :data-testid="`task-project-new-cancel-${t.id}`"
                    @click="cancelAddProject"
                  >Cancel</button>
                </div>
                <div v-else class="project-control">
                  <select
                    class="inline-select"
                    :value="t.projectId ?? ''"
                    :disabled="savingProjectId === t.id"
                    :data-testid="`task-project-select-${t.id}`"
                    @focus="ensureProjectsLoaded(t.clientId)"
                    @change="onProjectSelect(t, ($event.target as HTMLSelectElement).value, $event.target as HTMLSelectElement)"
                  >
                    <option value="">—</option>
                    <option
                      v-for="p in projectsForClient(t.clientId)"
                      :key="p.id"
                      :value="p.id"
                    >{{ projectOptionLabel(p, t.clientId) }}</option>
                    <option
                      v-if="t.projectId && !projectsForClient(t.clientId).some((p) => p.id === t.projectId)"
                      :value="t.projectId"
                    >{{ t.projectName ?? t.projectId }}</option>
                    <option :value="ADD_PROJECT_VALUE">Add project…</option>
                  </select>
                  <button
                    v-if="taskHasChildren(t) && t.projectId"
                    type="button"
                    class="link bill-all-link"
                    :disabled="savingProjectId != null"
                    :data-testid="`task-project-all-${t.id}`"
                    title="Set project on child tasks that have none"
                    @click="applyProjectToUnassignedChildren(t)"
                  >All</button>
                </div>
                <span v-if="projectErrors[t.id]" class="inline-error" :data-testid="`task-project-error-${t.id}`">{{ projectErrors[t.id] }}</span>
              </td>
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
              <td :data-testid="`task-status-${t.id}`">
                <span class="cell-text">{{ t.clickUpStatus ?? '—' }}</span>
              </td>
              <td class="bill-cell" :data-testid="`task-bill-${t.id}`">
                <div class="bill-control">
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
                  <button
                    v-if="taskHasChildren(t) && billIsYesOrNo(t)"
                    type="button"
                    class="link bill-all-link"
                    :disabled="savingBillId != null"
                    :data-testid="`task-bill-all-${t.id}`"
                    title="Set bill on all child tasks"
                    @click="applyBillToAllChildren(t)"
                  >All</button>
                </div>
                <span v-if="billErrors[t.id]" class="inline-error" :data-testid="`task-bill-error-${t.id}`">{{ billErrors[t.id] }}</span>
              </td>
              <td class="hours-cell" :data-testid="`task-billable-hours-${t.id}`">
                <div class="hours-control">
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
                  <ProgressSpinner
                    v-if="savingBillableId === t.id"
                    class="hours-spinner"
                    strokeWidth="6"
                    aria-label="Saving billable hours"
                    :pt="{ root: { 'data-testid': `task-billable-hours-spinner-${t.id}` } }"
                  />
                </div>
                <span v-if="billableWarnings[t.id]" class="inline-warning" :data-testid="`task-billable-hours-warning-${t.id}`">{{ billableWarnings[t.id] }}</span>
              </td>
              <td class="hours-cell" :data-testid="`task-flat-fee-${t.id}`">
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  class="inline-input"
                  :value="t.flatFee ?? ''"
                  :disabled="savingFlatFeeId === t.id"
                  :data-testid="`task-flat-fee-input-${t.id}`"
                  @blur="updateFlatFeeInline(t, ($event.target as HTMLInputElement).value)"
                />
                <span v-if="flatFeeErrors[t.id]" class="inline-error" :data-testid="`task-flat-fee-error-${t.id}`">{{ flatFeeErrors[t.id] }}</span>
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
                v-if="showClickUpEstimateColumn"
                :data-testid="`task-clickup-estimate-${t.id}`"
              ><span class="cell-text">{{ t.estimatedHours ?? '—' }}</span></td>
              <td
                v-if="showClickUpHoursColumn"
                class="hours-cell"
                :data-testid="`task-clickup-hours-${t.id}`"
              >
                <div class="hours-control">
                  <span class="cell-text">{{ t.actualHours ?? '—' }}</span>
                  <ProgressSpinner
                    v-if="savingBillableId === t.id || syncingTaskId === t.id"
                    class="hours-spinner"
                    strokeWidth="6"
                    aria-label="Updating ClickUp hours"
                    :pt="{ root: { 'data-testid': `task-clickup-hours-spinner-${t.id}` } }"
                  />
                </div>
              </td>
              <td v-if="showInvoice" class="invoice-cell" :data-testid="`task-invoice-${t.id}`">
                <div v-if="addingInvoiceTaskId === t.id" class="add-project">
                  <input
                    v-model="newInvoiceName"
                    type="text"
                    class="inline-input"
                    placeholder="Invoice name"
                    :disabled="savingInvoiceId === t.id"
                    :data-testid="`task-invoice-new-name-${t.id}`"
                    @keydown.enter.prevent="confirmAddInvoice(t)"
                    @keydown.escape.prevent="cancelAddInvoice"
                  />
                  <button
                    type="button"
                    class="link"
                    :disabled="savingInvoiceId === t.id || !newInvoiceName.trim()"
                    :data-testid="`task-invoice-new-save-${t.id}`"
                    @click="confirmAddInvoice(t)"
                  >Add</button>
                  <button
                    type="button"
                    class="link"
                    :disabled="savingInvoiceId === t.id"
                    :data-testid="`task-invoice-new-cancel-${t.id}`"
                    @click="cancelAddInvoice"
                  >Cancel</button>
                </div>
                <select
                  v-else
                  class="inline-select"
                  :value="t.invoiceLabel ?? ''"
                  :disabled="savingInvoiceId === t.id"
                  :data-testid="`task-invoice-select-${t.id}`"
                  @change="onInvoiceSelect(t, ($event.target as HTMLSelectElement).value, $event.target as HTMLSelectElement)"
                >
                  <option value="">—</option>
                  <option
                    v-for="inv in invoiceOptionsFor(t)"
                    :key="inv.id"
                    :value="inv.name"
                  >{{ invoiceOptionLabel(inv) }}</option>
                  <option :value="ADD_INVOICE_VALUE">Add invoice…</option>
                </select>
                <span v-if="invoiceErrors[t.id]" class="inline-error" :data-testid="`task-invoice-error-${t.id}`">{{ invoiceErrors[t.id] }}</span>
              </td>
              <td :title="formatDateTime(t.dateCreated)" :data-testid="`task-date-created-${t.id}`">
                <span class="cell-text">{{ formatDate(t.dateCreated) }}</span>
              </td>
              <td :title="formatDateTime(t.dateDone)" :data-testid="`task-date-done-${t.id}`">
                <span class="cell-text">{{ formatDate(t.dateDone) }}</span>
              </td>
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
                      <option v-for="p in projects" :key="p.id" :value="p.id">
                        {{ t.clientId && p.clientId !== t.clientId ? `${p.name} (Shared)` : p.name }}
                      </option>
                    </select>
                  </label>
                  <label>
                    Bill
                    <select
                      v-model="draft.bill"
                      :data-testid="`task-edit-bill-${t.id}`"
                      @change="onEditBillChange(t)"
                    >
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
                    Flat fee
                    <input v-model="draft.flatFee" type="number" step="0.01" min="0" :data-testid="`task-edit-flat-fee-${t.id}`" />
                  </label>
                  <label>
                    Non-billable hours
                    <input v-model="draft.nonBillableHours" type="number" step="0.01" min="0" :data-testid="`task-edit-non-billable-hours-${t.id}`" />
                  </label>
                  <label>
                    Invoice
                    <select v-model="draft.invoiceLabel" :data-testid="`task-edit-invoice-${t.id}`">
                      <option value="">—</option>
                      <option
                        v-for="inv in invoiceOptionsFor(t)"
                        :key="inv.id"
                        :value="inv.name"
                      >{{ invoiceOptionLabel(inv) }}</option>
                    </select>
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
  display: inline-flex;
  align-items: center;
  min-height: var(--inline-control-height);
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
.filters {
  --filter-control-height: 2.35rem;
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: start;
  margin-bottom: 1rem;
}
.filters label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filters .check { flex-direction: row; align-items: center; gap: 0.4rem; padding-bottom: 0; }
.filter-clear {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
}
.filter-clear-btn {
  box-sizing: border-box;
  min-height: var(--filter-control-height);
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #fff;
  color: #374151;
  font: inherit;
  cursor: pointer;
}
.filter-clear-btn:hover {
  background: #f9fafb;
  border-color: #9ca3af;
}
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
.group-expand-collapse {
  display: inline-flex;
  flex-direction: row;
  align-items: center;
  gap: 0.15rem;
  white-space: nowrap;
  color: #4b5563;
}
.group-expand-collapse .icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
}
.group-expand-collapse .material-icons {
  font-size: 1.25rem;
}
.column-toggles { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #4b5563; }
.filter-label { line-height: 1.2; }
.column-checks {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  min-height: var(--filter-control-height);
  box-sizing: border-box;
}
.column-checks .check { padding-bottom: 0; }
.column-filters {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.invoiced-filters {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.invoiced-popover-trigger {
  anchor-name: --tasks-invoiced-trigger;
  align-self: flex-start;
  box-sizing: border-box;
  min-height: var(--filter-control-height);
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #fff;
  color: #374151;
  font: inherit;
  cursor: pointer;
}
.invoiced-popover-trigger:hover {
  background: #f9fafb;
  border-color: #9ca3af;
}
.invoiced-popover {
  margin: 0;
  inset: unset;
  position-anchor: --tasks-invoiced-trigger;
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
.invoiced-checks {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  align-items: flex-start;
}
.invoiced-checks .check { padding-bottom: 0; }
.column-popover-trigger {
  anchor-name: --tasks-columns-trigger;
  align-self: flex-start;
  box-sizing: border-box;
  min-height: var(--filter-control-height);
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #fff;
  color: #374151;
  font: inherit;
  cursor: pointer;
}
.column-popover-trigger:hover {
  background: #f9fafb;
  border-color: #9ca3af;
}
.column-popover {
  margin: 0;
  inset: unset;
  position-anchor: --tasks-columns-trigger;
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
.column-popover-checks {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  align-items: flex-start;
}
.column-popover-checks .check { padding-bottom: 0; }
.status-filters {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.status-popover-trigger {
  anchor-name: --tasks-status-trigger;
  align-self: flex-start;
  box-sizing: border-box;
  min-height: var(--filter-control-height);
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
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
.status-popover-actions {
  display: flex;
  gap: 0.4rem;
  margin-top: 0.65rem;
}
.status-action {
  padding: 0.25rem 0.55rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: #fff;
  color: #374151;
  font: inherit;
  font-size: 0.85rem;
  cursor: pointer;
}
.status-action:hover {
  background: #f9fafb;
  border-color: #9ca3af;
}
.toggle-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #4b5563;
}
.toggle-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-height: var(--filter-control-height);
  box-sizing: border-box;
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
.filters select,
.filters input[type='search'] {
  box-sizing: border-box;
  min-height: var(--filter-control-height);
}
select, input:not([role='switch']) {
  padding: 0.45rem 0.65rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font: inherit;
}
.grid { width: 100%; border-collapse: collapse; font-size: 0.9rem; --inline-control-height: 2rem; }
.grid th, .grid td { text-align: left; padding: 0.45rem 0.4rem; border-bottom: 1px solid #eee; vertical-align: top; }
.grid tbody td { vertical-align: middle; }
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
.bill-control {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  min-height: var(--inline-control-height);
}
.bill-all-link {
  opacity: 0;
  pointer-events: none;
  font-size: 0.8rem;
}
tr:hover .bill-all-link {
  opacity: 1;
  pointer-events: auto;
}
.hours-cell { min-width: 5rem; }
.hours-control {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  min-height: var(--inline-control-height);
}
.hours-spinner {
  width: 1rem !important;
  height: 1rem !important;
}
.cell-text {
  display: inline-flex;
  align-items: center;
  min-height: var(--inline-control-height);
  line-height: 1.2;
}
.project-cell { min-width: 9rem; }
.project-cell .inline-select { max-width: 12rem; }
.project-control {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}
.add-project {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.add-project .inline-input {
  width: 8rem;
  min-width: 6rem;
}
.inline-select,
.inline-input {
  box-sizing: border-box;
  height: var(--inline-control-height);
  min-height: var(--inline-control-height);
  padding: 0 0.45rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  font-size: 0.85rem;
  line-height: normal;
  background: #fff;
  vertical-align: middle;
}
.inline-input {
  width: 4.5rem;
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
