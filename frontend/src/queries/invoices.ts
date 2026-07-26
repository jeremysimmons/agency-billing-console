import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type { Invoice, InvoiceStatus } from '../api/types'

export function useInvoices() {
  return useQuery({
    key: ['invoices'],
    query: async () => (await http.get<Invoice[]>('/invoices')).data,
  })
}

export function useCreateInvoice() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: { name: string; status?: InvoiceStatus; isDefault?: boolean }) =>
      (await http.post<Invoice>('/invoices', input)).data,
    onSettled: () => cache.invalidateQueries({ key: ['invoices'] }),
  })
}

export function useUpdateInvoice() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({
      id,
      name,
      status,
      isDefault,
    }: {
      id: string
      name: string
      status: InvoiceStatus
      isDefault: boolean
    }) => (await http.put<Invoice>(`/invoices/${id}`, { name, status, isDefault })).data,
    onSettled: () => cache.invalidateQueries({ key: ['invoices'] }),
  })
}

export function useReorderInvoices() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (orderedIds: string[]) =>
      (await http.put<Invoice[]>('/invoices/reorder', { orderedIds })).data,
    onSuccess: (list) => cache.setQueryData(['invoices'], list),
    onSettled: () => cache.invalidateQueries({ key: ['invoices'] }),
  })
}
