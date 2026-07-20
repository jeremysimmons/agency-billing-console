import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { storeToRefs } from 'pinia'
import { http, ensureCsrf } from '../api/http'
import type { Agency } from '../api/types'
import { useAuthStore } from '../stores/auth'

export interface AgencyInput {
  name: string
  billingEmail?: string | null
  billingAddress?: string | null
  currency: string
  paymentTermsDays: number
  active: boolean
}

export function useAgency() {
  const { user } = storeToRefs(useAuthStore())
  return useQuery({
    key: ['agency'],
    query: async () => (await http.get<Agency>('/agency')).data,
    enabled: () => !!user.value,
  })
}

export function useUpdateAgency() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: AgencyInput) => {
      await ensureCsrf()
      return (await http.put<Agency>('/agency', input)).data
    },
    onSettled: () => cache.invalidateQueries({ key: ['agency'] }),
  })
}
