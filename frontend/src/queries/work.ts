import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http, ensureCsrf } from '../api/http'
import type { SyncImportedTimeResult, WorkItemReview } from '../api/types'

const keys = {
  pending: ['work', 'pending'] as const,
  completed: ['work', 'completed'] as const,
  finalized: ['work', 'finalized'] as const,
}

function invalidateWork(cache: ReturnType<typeof useQueryCache>) {
  cache.invalidateQueries({ key: ['work'] })
  cache.invalidateQueries({ key: ['tasks'] })
}

export function usePendingWork() {
  return useQuery({
    key: keys.pending,
    query: async () => (await http.get<WorkItemReview[]>('/work/pending')).data,
  })
}

export function useCompletedWork() {
  return useQuery({
    key: keys.completed,
    query: async () => (await http.get<WorkItemReview[]>('/work/completed')).data,
  })
}

export function useFinalizedWork() {
  return useQuery({
    key: keys.finalized,
    query: async () => (await http.get<WorkItemReview[]>('/work/finalized')).data,
  })
}

export function useFinalizeWork() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (taskId: string) => {
      await ensureCsrf()
      return (await http.post<WorkItemReview>(`/work/${taskId}/finalize`, {})).data
    },
    onSettled: () => invalidateWork(cache),
  })
}

export function useExcludeWork() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ taskId, reason }: { taskId: string; reason?: string }) => {
      await ensureCsrf()
      return (await http.post<WorkItemReview>(`/work/${taskId}/exclude`, { reason: reason ?? null })).data
    },
    onSettled: () => invalidateWork(cache),
  })
}

export function useSyncImportedTime() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async () => {
      await ensureCsrf()
      return (await http.post<SyncImportedTimeResult>('/time-entries/sync-imported')).data
    },
    onSettled: () => invalidateWork(cache),
  })
}
