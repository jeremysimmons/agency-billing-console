import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http, ensureCsrf } from '../api/http'
import type { Project } from '../api/types'

export interface ProjectInput {
  clientId: string
  name: string
  code?: string | null
  description?: string | null
  status?: string
  billingType?: string
  hourlyRate?: number | null
  fixedFee?: number | null
  budgetMinutes?: number | null
  budgetAmount?: number | null
  startDate?: string | null
  endDate?: string | null
}

export function useProjects(clientId: MaybeRefOrGetter<string | undefined>) {
  const id = computed(() => toValue(clientId))
  return useQuery({
    key: () => ['projects', id.value ?? 'none'],
    query: async () => (await http.get<Project[]>('/projects', { params: { clientId: id.value } })).data,
    enabled: () => !!id.value,
  })
}

export function useCreateProject() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: ProjectInput) => {
      await ensureCsrf()
      return (await http.post<Project>('/projects', input)).data
    },
    onSettled: (_d, _e, input) => cache.invalidateQueries({ key: ['projects', input.clientId] }),
  })
}
