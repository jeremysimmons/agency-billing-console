import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type { Agency } from '../api/types'

export function useAgency() {
  return useQuery({
    key: ['agency'],
    query: async () => (await http.get<Agency>('/agency')).data,
  })
}

export function useUpdateAgencyUiPreferences() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (taskGroupClientOrder: string[]) =>
      (await http.put<Agency>('/agency/ui-preferences', { taskGroupClientOrder })).data,
    onSettled: () => cache.invalidateQueries({ key: ['agency'] }),
  })
}
