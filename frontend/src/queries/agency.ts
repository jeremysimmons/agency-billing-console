import { useQuery } from '@pinia/colada'
import { storeToRefs } from 'pinia'
import { http } from '../api/http'
import type { Agency } from '../api/types'
import { useAuthStore } from '../stores/auth'

export function useAgency() {
  const { user } = storeToRefs(useAuthStore())
  return useQuery({
    key: ['agency'],
    query: async () => (await http.get<Agency>('/agency')).data,
    enabled: () => !!user.value,
  })
}
