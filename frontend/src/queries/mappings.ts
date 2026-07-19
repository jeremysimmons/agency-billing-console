import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http, ensureCsrf } from '../api/http'
import type { SuggestMappingsResult, StatusMapping, UnmappedContainer, UnmappedWorkItem } from '../api/types'

const keys = {
  containers: ['mappings', 'unmapped-containers'] as const,
  tasks: ['mappings', 'unmapped-tasks'] as const,
  statuses: ['mappings', 'statuses'] as const,
}

export function useUnmappedContainers() {
  return useQuery({
    key: keys.containers,
    query: async () => (await http.get<UnmappedContainer[]>('/integrations/clickup/unmapped/containers')).data,
  })
}

export function useUnmappedWorkItems() {
  return useQuery({
    key: keys.tasks,
    query: async () => (await http.get<UnmappedWorkItem[]>('/integrations/clickup/unmapped/tasks')).data,
  })
}

export function useStatusMappings() {
  return useQuery({
    key: keys.statuses,
    query: async () => (await http.get<StatusMapping[]>('/integrations/clickup/mappings/statuses')).data,
  })
}

function invalidateAll(cache: ReturnType<typeof useQueryCache>) {
  cache.invalidateQueries({ key: ['mappings'] })
  cache.invalidateQueries({ key: ['clients'] })
  cache.invalidateQueries({ key: ['projects'] })
  cache.invalidateQueries({ key: ['tasks'] })
}

export function useSuggestMappings() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async () => {
      await ensureCsrf()
      return (await http.post<SuggestMappingsResult>('/integrations/clickup/mappings/suggest')).data
    },
    onSettled: () => invalidateAll(cache),
  })
}

export function useConfirmContainer() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: { containerId: string; createClient?: boolean; createProject?: boolean; clientId?: string | null; projectId?: string | null }) => {
      await ensureCsrf()
      return (await http.post(`/integrations/clickup/mappings/containers/${input.containerId}/confirm`, {
        clientId: input.clientId ?? null,
        projectId: input.projectId ?? null,
        createClient: input.createClient ?? false,
        createProject: input.createProject ?? false,
      })).data
    },
    onSettled: () => invalidateAll(cache),
  })
}

export function useIgnoreContainer() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (containerId: string) => {
      await ensureCsrf()
      await http.post(`/integrations/clickup/mappings/containers/${containerId}/ignore`, {})
    },
    onSettled: () => invalidateAll(cache),
  })
}

export function useConfirmTask() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: { workItemId: string; createTask?: boolean; taskId?: string | null }) => {
      await ensureCsrf()
      return (await http.post(`/integrations/clickup/mappings/tasks/${input.workItemId}/confirm`, {
        taskId: input.taskId ?? null,
        createTask: input.createTask ?? false,
      })).data
    },
    onSettled: () => invalidateAll(cache),
  })
}

export function useIgnoreTask() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (workItemId: string) => {
      await ensureCsrf()
      await http.post(`/integrations/clickup/mappings/tasks/${workItemId}/ignore`, {})
    },
    onSettled: () => invalidateAll(cache),
  })
}

export function useApplyStatuses() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async () => {
      await ensureCsrf()
      return (await http.post<{ updated: number }>('/integrations/clickup/mappings/apply-statuses')).data
    },
    onSettled: () => invalidateAll(cache),
  })
}
