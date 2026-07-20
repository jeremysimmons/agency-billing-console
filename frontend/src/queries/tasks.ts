import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { WorkTask } from '../api/types'

export function useTasks(
  clientId: MaybeRefOrGetter<string | undefined>,
  missingOnly: MaybeRefOrGetter<boolean> = false,
) {
  const cid = computed(() => toValue(clientId))
  const missing = computed(() => toValue(missingOnly))
  return useQuery({
    key: () => ['tasks', cid.value ?? 'all', missing.value ? 'missing' : 'all'],
    query: async () => {
      const params: Record<string, string | boolean> = {}
      if (cid.value) params.clientId = cid.value
      if (missing.value) params.missingOnly = true
      return (await http.get<WorkTask[]>('/tasks', { params })).data
    },
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
    onSettled: () => cache.invalidateQueries({ key: ['tasks'] }),
  })
}
