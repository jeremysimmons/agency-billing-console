import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { WorkTask } from '../api/types'

export interface TaskFilterOptions {
  createdMonths: string[]
  doneMonths: string[]
}

export interface TaskListFilters {
  clientId?: string
  missingOnly?: boolean
  projectFilter?: string
  createdMonth?: string
  doneMonth?: string
}

function taskListParams(filters: TaskListFilters): Record<string, string | boolean> {
  const params: Record<string, string | boolean> = {}
  if (filters.clientId) params.clientId = filters.clientId
  if (filters.missingOnly) params.missingOnly = true
  if (filters.projectFilter === '__unassigned__') params.unassignedOnly = true
  else if (filters.projectFilter) params.projectId = filters.projectFilter
  if (filters.createdMonth) params.createdMonth = filters.createdMonth
  if (filters.doneMonth) params.doneMonth = filters.doneMonth
  return params
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
      f.value.projectFilter ?? 'all',
      f.value.createdMonth ?? 'all',
      f.value.doneMonth ?? 'all',
    ],
    query: async () =>
      (await http.get<WorkTask[]>('/tasks', { params: taskListParams(f.value) })).data,
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
