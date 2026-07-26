import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { TaskSummary, WorkTask } from '../api/types'

export interface TaskFilterOptions {
  createdMonths: string[]
  doneMonths: string[]
  statuses: string[]
}

export type InvoicedFilter = 'paid' | 'pending' | 'none'

export interface TaskListFilters {
  clientId?: string
  missingOnly?: boolean
  invoiced?: InvoicedFilter[]
  projectFilter?: string
  createdMonth?: string
  doneMonth?: string
  statuses?: string[]
  listId?: string
  folderId?: string
  spaceId?: string
  invoiceLabel?: string
}

function taskListParams(filters: TaskListFilters): string {
  const params = new URLSearchParams()
  if (filters.clientId) params.set('clientId', filters.clientId)
  if (filters.missingOnly) params.set('missingOnly', 'true')
  if (filters.invoiced?.length) {
    for (const value of filters.invoiced) params.append('invoiced', value)
  }
  if (filters.projectFilter === '__unassigned__') params.set('unassignedOnly', 'true')
  else if (filters.projectFilter) params.set('projectId', filters.projectFilter)
  if (filters.createdMonth) params.set('createdMonth', filters.createdMonth)
  if (filters.doneMonth) params.set('doneMonth', filters.doneMonth)
  if (filters.statuses?.length) {
    for (const status of filters.statuses) params.append('statuses', status)
  }
  if (filters.listId) params.set('listId', filters.listId)
  if (filters.folderId) params.set('folderId', filters.folderId)
  if (filters.spaceId) params.set('spaceId', filters.spaceId)
  if (filters.invoiceLabel) params.set('invoiceLabel', filters.invoiceLabel)
  return params.toString()
}

export function useTaskFilterOptions(clientId: MaybeRefOrGetter<string | undefined>) {
  const cid = computed(() => toValue(clientId))
  return useQuery({
    key: () => ['tasks', 'filter-options', cid.value ?? 'all'],
    query: async () =>
      (await http.get<TaskFilterOptions>('/tasks/filter-options', {
        params: cid.value ? { clientId: cid.value } : {},
      })).data,
  })
}

export function taskListQueryKey(filters: TaskListFilters) {
  return [
    'tasks',
    filters.clientId ?? 'all',
    filters.missingOnly ? 'missing' : 'all',
    filters.invoiced?.slice().sort().join('|') ?? 'all',
    filters.projectFilter ?? 'all',
    filters.createdMonth ?? 'all',
    filters.doneMonth ?? 'all',
    filters.statuses?.slice().sort().join('|') ?? 'all',
    filters.listId ?? '',
    filters.folderId ?? '',
    filters.spaceId ?? '',
    filters.invoiceLabel ?? '',
  ]
}

export function useTasks(
  filters: MaybeRefOrGetter<TaskListFilters>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const f = computed(() => toValue(filters))
  const on = computed(() => toValue(enabled))
  return useQuery({
    key: () => taskListQueryKey(f.value),
    enabled: () => on.value,
    query: async () =>
      (await http.get<WorkTask[]>(`/tasks?${taskListParams(f.value)}`)).data,
  })
}

function taskSummaryQueryKey(filters: TaskListFilters) {
  return [
    'tasks',
    'summary',
    filters.clientId ?? 'all',
    filters.missingOnly ? 'missing' : 'all',
    filters.invoiced?.slice().sort().join('|') ?? 'all',
    filters.projectFilter ?? 'all',
    filters.createdMonth ?? 'all',
    filters.doneMonth ?? 'all',
    filters.statuses?.slice().sort().join('|') ?? 'all',
    filters.listId ?? '',
    filters.folderId ?? '',
    filters.spaceId ?? '',
    filters.invoiceLabel ?? '',
  ]
}

export function useTaskSummary(
  filters: MaybeRefOrGetter<TaskListFilters>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const f = computed(() => toValue(filters))
  const on = computed(() => toValue(enabled))
  return useQuery({
    key: () => taskSummaryQueryKey(f.value),
    enabled: () => on.value,
    query: async () =>
      (await http.get<TaskSummary>(`/tasks/summary?${taskListParams(f.value)}`)).data,
  })
}

export interface TaskPrepInput {
  projectId?: string | null
  bill?: string | null
  billableHours?: number | null
  nonBillableHours?: number | null
  invoiceLabel?: string | null
  note?: string | null
}

export interface TaskHoursUpdateResult {
  task: WorkTask
  clickUpTrackedHours: number | null
  warning: string | null
}

function patchTaskList(cache: ReturnType<typeof useQueryCache>, filters: TaskListFilters, task: WorkTask) {
  cache.setQueryData(taskListQueryKey(filters), (tasks) =>
    Array.isArray(tasks)
      ? tasks.map((t) => (t.id === task.id ? task : t))
      : tasks,
  )
}

/** Mirror backend cascade: assign project to unassigned ClickUp descendants. */
function patchTaskListAfterProjectAssign(
  cache: ReturnType<typeof useQueryCache>,
  filters: TaskListFilters,
  updated: WorkTask,
) {
  cache.setQueryData(taskListQueryKey(filters), (tasks) => {
    if (!Array.isArray(tasks)) return tasks

    const list = tasks.map((t) => (t.id === updated.id ? updated : t))
    if (!updated.projectId || !updated.clickUpTaskId) return list

    const descendantClickUpIds = new Set<string>()
    let frontier = new Set<string>([updated.clickUpTaskId])
    while (frontier.size > 0) {
      const next = new Set<string>()
      for (const t of list) {
        if (
          t.clickUpParentId
          && frontier.has(t.clickUpParentId)
          && t.clickUpTaskId
          && !descendantClickUpIds.has(t.clickUpTaskId)
        ) {
          descendantClickUpIds.add(t.clickUpTaskId)
          next.add(t.clickUpTaskId)
        }
      }
      frontier = next
    }
    if (descendantClickUpIds.size === 0) return list

    return list.map((t) => {
      if (
        !t.clickUpTaskId
        || !descendantClickUpIds.has(t.clickUpTaskId)
        || t.projectId != null
      ) {
        return t
      }
      return {
        ...t,
        projectId: updated.projectId,
        projectName: updated.projectName,
      }
    })
  })
}

export function useUpdateTaskBill(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, bill }: { id: string; bill: string | null }) =>
      (await http.patch<WorkTask>(`/tasks/${id}/bill`, { bill })).data,
    onSuccess: (updated) => {
      patchTaskList(cache, f.value, updated)
    },
  })
}

export function useUpdateTaskProject(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, projectId }: { id: string; projectId: string | null }) =>
      (await http.patch<WorkTask>(`/tasks/${id}/project`, { projectId })).data,
    onSuccess: (updated) => {
      // Patch in place (incl. cascaded children) — avoid invalidate/refetch scroll jump.
      patchTaskListAfterProjectAssign(cache, f.value, updated)
    },
  })
}

export function useUpdateTaskInvoice(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, invoiceLabel }: { id: string; invoiceLabel: string | null }) =>
      (await http.patch<WorkTask>(`/tasks/${id}/invoice`, { invoiceLabel })).data,
    onSuccess: (updated) => {
      patchTaskList(cache, f.value, updated)
    },
  })
}

export function useUpdateTaskDiscount(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, discountPercent }: { id: string; discountPercent: number }) =>
      (await http.patch<WorkTask>(`/tasks/${id}/discount`, { discountPercent })).data,
    onSuccess: (updated) => {
      patchTaskList(cache, f.value, updated)
    },
  })
}

export function useUpdateTaskBillableHours(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, hours }: { id: string; hours: number | null }) =>
      (await http.patch<TaskHoursUpdateResult>(`/tasks/${id}/billable-hours`, { hours })).data,
    onSuccess: (result) => {
      patchTaskList(cache, f.value, result.task)
    },
  })
}

export function useUpdateTaskNonBillableHours(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async ({ id, hours }: { id: string; hours: number | null }) =>
      (await http.patch<TaskHoursUpdateResult>(`/tasks/${id}/non-billable-hours`, { hours })).data,
    onSuccess: (result) => {
      patchTaskList(cache, f.value, result.task)
    },
  })
}

export function useUpdateTaskPrep() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ id, input }: { id: string; input: TaskPrepInput }) =>
      (await http.patch<WorkTask>(`/tasks/${id}/prep`, input)).data,
    onSettled: () => {
      cache.invalidateQueries({ key: ['tasks'] })
    },
  })
}

export function useSyncTask(filters: MaybeRefOrGetter<TaskListFilters>) {
  const cache = useQueryCache()
  const f = computed(() => toValue(filters))
  return useMutation({
    mutation: async (id: string) =>
      (await http.post<WorkTask>(`/tasks/${id}/sync`)).data,
    onSuccess: (updated) => {
      patchTaskList(cache, f.value, updated)
    },
  })
}
