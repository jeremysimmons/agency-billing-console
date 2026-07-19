import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http, ensureCsrf } from '../api/http'
import type { WorkTask } from '../api/types'

export interface TaskInput {
  clientId: string
  projectId?: string | null
  parentTaskId?: string | null
  title: string
  description?: string | null
  billingType?: string
  billable?: boolean
  hourlyRate?: number | null
  fixedFee?: number | null
  estimatedMinutes?: number | null
  estimateRollupMode?: string
  actualRollupMode?: string
  billingRollupMode?: string
  dueDate?: string | null
}

export function useTasks(clientId: MaybeRefOrGetter<string | undefined>) {
  const id = computed(() => toValue(clientId))
  return useQuery({
    key: () => ['tasks', id.value ?? 'none'],
    query: async () => (await http.get<WorkTask[]>('/tasks', { params: { clientId: id.value } })).data,
    enabled: () => !!id.value,
  })
}

export function useCreateTask() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: TaskInput) => {
      await ensureCsrf()
      return (await http.post<WorkTask>('/tasks', input)).data
    },
    onSettled: (_d, _e, input) => cache.invalidateQueries({ key: ['tasks', input.clientId] }),
  })
}
