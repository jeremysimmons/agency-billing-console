import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { WorkTask } from '../api/types'

export interface TaskFilterOptions {
  createdMonths: string[]
  doneMonths: string[]
  statuses: string[]
}

export interface TaskListFilters {
  clientId?: string
  missingOnly?: boolean
  includeInvoiced?: boolean
  projectFilter?: string
  createdMonth?: string
  doneMonth?: string
  statuses?: string[]
}

function taskListParams(filters: TaskListFilters): string {
  const params = new URLSearchParams()
  if (filters.clientId) params.set('clientId', filters.clientId)
  if (filters.missingOnly) params.set('missingOnly', 'true')
  if (filters.includeInvoiced) params.set('includeInvoiced', 'true')
  if (filters.projectFilter === '__unassigned__') params.set('unassignedOnly', 'true')
  else if (filters.projectFilter) params.set('projectId', filters.projectFilter)
  if (filters.createdMonth) params.set('createdMonth', filters.createdMonth)
  if (filters.doneMonth) params.set('doneMonth', filters.doneMonth)
  if (filters.statuses?.length) {
    for (const status of filters.statuses) params.append('statuses', status)
  }
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

export function useTasks(filters: MaybeRefOrGetter<TaskListFilters>) {
  const f = computed(() => toValue(filters))
  return useQuery({
    key: () => [
      'tasks',
      f.value.clientId ?? 'all',
      f.value.missingOnly ? 'missing' : 'all',
      f.value.includeInvoiced ? 'invoiced' : 'not-invoiced',
      f.value.projectFilter ?? 'all',
      f.value.createdMonth ?? 'all',
      f.value.doneMonth ?? 'all',
      f.value.statuses?.slice().sort().join('|') ?? 'all',
    ],
    query: async () =>
      (await http.get<WorkTask[]>(`/tasks?${taskListParams(f.value)}`)).data,
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
