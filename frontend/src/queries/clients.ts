import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type { Client } from '../api/types'

export interface ClientInput {
  name: string
  code?: string | null
  originalName?: string | null
  description?: string | null
  status?: string
  active?: boolean
}

export function useClients() {
  return useQuery({
    key: ['clients'],
    query: async () => (await http.get<Client[]>('/clients')).data,
  })
}

export function useCreateClient() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: ClientInput) => (await http.post<Client>('/clients', input)).data,
    onSettled: () => cache.invalidateQueries({ key: ['clients'] }),
  })
}

export function useUpdateClient() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ id, input }: { id: string; input: ClientInput }) =>
      (await http.put<Client>(`/clients/${id}`, input)).data,
    onSettled: () => cache.invalidateQueries({ key: ['clients'] }),
  })
}

export function useDeleteClient() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (id: string) => {
      await http.delete(`/clients/${id}`)
    },
    onSettled: () => cache.invalidateQueries({ key: ['clients'] }),
  })
}
