import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type { ClickUpHierarchyNode, ClickUpSyncResult, CsvImportResult } from '../api/types'

export function useClickUpHierarchy() {
  return useQuery({
    key: ['clickup', 'hierarchy'],
    query: async () => (await http.get<ClickUpHierarchyNode[]>('/clickup/hierarchy')).data,
  })
}

export function useClickUpSync() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async () => (await http.post<ClickUpSyncResult>('/clickup/sync')).data,
    onSettled: () => {
      cache.invalidateQueries({ key: ['clickup'] })
      cache.invalidateQueries({ key: ['tasks'] })
      cache.invalidateQueries({ key: ['clients'] })
      cache.invalidateQueries({ key: ['agency'] })
    },
  })
}

export function useCsvImport() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return (await http.post<CsvImportResult>('/clickup/import-csv', form)).data
    },
    onSettled: () => {
      cache.invalidateQueries({ key: ['tasks'] })
      cache.invalidateQueries({ key: ['clients'] })
    },
  })
}
