import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { http } from '../api/http'
import type { Project } from '../api/types'

export interface ProjectInput {
  clientId: string
  name: string
}

export function useAllProjects() {
  return useQuery({
    key: ['projects', 'all'],
    query: async () => (await http.get<Project[]>('/projects')).data,
  })
}

export function useProjects(
  clientId: MaybeRefOrGetter<string | undefined>,
  options?: MaybeRefOrGetter<{ includeShared?: boolean } | undefined>,
) {
  const id = computed(() => toValue(clientId))
  const includeShared = computed(() => toValue(options)?.includeShared ?? false)
  return useQuery({
    key: () => ['projects', id.value ?? 'none', includeShared.value ? 'shared' : 'own'],
    query: async () =>
      (await http.get<Project[]>('/projects', {
        params: {
          clientId: id.value,
          ...(includeShared.value ? { includeShared: true } : {}),
        },
      })).data,
    enabled: () => !!id.value,
  })
}

function invalidateProjectQueries(cache: ReturnType<typeof useQueryCache>) {
  cache.invalidateQueries({ key: ['projects'] })
}

export function useCreateProject() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: ProjectInput) => (await http.post<Project>('/projects', input)).data,
    onSettled: () => invalidateProjectQueries(cache),
  })
}

export function useUpdateProject() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ id, name, clientId }: { id: string; name: string; clientId: string }) =>
      (await http.put<Project>(`/projects/${id}`, { name, clientId })).data,
    onSettled: () => invalidateProjectQueries(cache),
  })
}
