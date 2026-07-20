import { useQuery } from '@pinia/colada'
import { http } from '../api/http'
import type { Agency } from '../api/types'

export function useAgency() {
  return useQuery({
    key: ['agency'],
    query: async () => (await http.get<Agency>('/agency')).data,
  })
}
