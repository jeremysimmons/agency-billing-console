import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type { IncludeNonBillableTasks, Invoice, InvoiceLine, InvoiceStatus } from '../api/types'

export function useInvoices() {
  return useQuery({
    key: ['invoices'],
    query: async () => (await http.get<Invoice[]>('/invoices')).data,
  })
}

export function useCreateInvoice() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: {
      name: string
      status?: InvoiceStatus
      isDefault?: boolean
      includeNonBillableTasks?: IncludeNonBillableTasks
    }) =>
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
      rate,
      includeNonBillableTasks,
    }: {
      id: string
      name: string
      status: InvoiceStatus
      isDefault: boolean
      rate: number | null
      includeNonBillableTasks: IncludeNonBillableTasks
    }) => (await http.put<Invoice>(`/invoices/${id}`, {
      name,
      status,
      isDefault,
      rate,
      includeNonBillableTasks,
    })).data,
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

export interface InvoiceLineInput {
  clientId: string
  projectId: string | null
  title: string
  hours: number
  flatFee: number | null
  discountPercent: number
}

function invoiceLinesKey(invoiceId: string) {
  return ['invoices', invoiceId, 'lines'] as const
}

export function useInvoiceLines(invoiceId: () => string | undefined, enabled?: () => boolean) {
  return useQuery({
    key: () => invoiceLinesKey(invoiceId() ?? 'none'),
    query: async () =>
      (await http.get<InvoiceLine[]>(`/invoices/${invoiceId()}/lines`)).data,
    enabled: () => !!invoiceId() && (enabled?.() ?? true),
  })
}

function invalidateInvoiceLines(cache: ReturnType<typeof useQueryCache>, invoiceId: string) {
  cache.invalidateQueries({ key: invoiceLinesKey(invoiceId) })
}

export function useCreateInvoiceLine(invoiceId: () => string) {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (input: InvoiceLineInput) =>
      (await http.post<InvoiceLine>(`/invoices/${invoiceId()}/lines`, input)).data,
    onSettled: () => invalidateInvoiceLines(cache, invoiceId()),
  })
}

export function useUpdateInvoiceLine(invoiceId: () => string) {
  const cache = useQueryCache()
  return useMutation({
    mutation: async ({ id, ...input }: InvoiceLineInput & { id: string }) =>
      (await http.put<InvoiceLine>(`/invoices/${invoiceId()}/lines/${id}`, input)).data,
    onSettled: () => invalidateInvoiceLines(cache, invoiceId()),
  })
}

export function useDeleteInvoiceLine(invoiceId: () => string) {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (id: string) => {
      await http.delete(`/invoices/${invoiceId()}/lines/${id}`)
    },
    onSettled: () => invalidateInvoiceLines(cache, invoiceId()),
  })
}

export function useReorderInvoiceLines(invoiceId: () => string) {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (orderedIds: string[]) =>
      (await http.put<InvoiceLine[]>(`/invoices/${invoiceId()}/lines/reorder`, { orderedIds })).data,
    onSuccess: (list) => cache.setQueryData(invoiceLinesKey(invoiceId()), list),
    onSettled: () => invalidateInvoiceLines(cache, invoiceId()),
  })
}
