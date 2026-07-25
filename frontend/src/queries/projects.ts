import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { Project } from '../api/types'

export interface ProjectInput {
  clientId: string
  name: string
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
    mutation: async (input: ProjectInput) => (await http.post<Project>('/projects', input)).data,
    onSettled: (_d, _e, input) => cache.invalidateQueries({ key: ['projects', input.clientId] }),
  })
}

export function useUpdateProject() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ id, name }: { id: string; name: string; clientId: string }) =>
      (await http.put<Project>(`/projects/${id}`, { name })).data,
    onSettled: (_d, _e, vars) => cache.invalidateQueries({ key: ['projects', vars.clientId] }),
  })
}
