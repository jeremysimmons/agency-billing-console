<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ToggleSwitch from 'primevue/toggleswitch'
import {
  useInvoices,
  useUpdateInvoice,
  useInvoiceLines,
  useCreateInvoiceLine,
  useUpdateInvoiceLine,
  useDeleteInvoiceLine,
  useReorderInvoiceLines,
} from '../queries/invoices'
import { useTasks, useUpdateTaskDiscount } from '../queries/tasks'
import { useClients } from '../queries/clients'
import { useAllProjects, useProjects } from '../queries/projects'
import type { IncludeNonBillableTasks, InvoiceLine, InvoiceStatus, WorkTask } from '../api/types'

const props = defineProps<{ id: string }>()

const INCLUDE_NON_BILLABLE_OPTIONS: { value: IncludeNonBillableTasks; label: string }[] = [
  { value: 'none', label: 'None' },
  { value: 'detail', label: 'Detail' },
  { value: 'summary', label: 'Summary' },
]

const { data: invoices, isLoading: invoicesLoading, error: invoicesError } = useInvoices()
const invoice = computed(() => invoices.value?.find((i) => i.id === props.id))
const updateInvoice = useUpdateInvoice()

const taskFilters = computed(() => ({
  invoiceLabel: invoice.value?.name,
}))
const tasksEnabled = computed(() => !!invoice.value?.name)
const { data: tasks, isLoading: tasksLoading, error: tasksError } = useTasks(taskFilters, tasksEnabled)
const updateDiscount = useUpdateTaskDiscount(taskFilters)

const invoiceId = () => props.id
const linesEnabled = computed(() => !!invoice.value)
const { data: invoiceLines, isLoading: linesLoading, error: linesError } = useInvoiceLines(
  () => (linesEnabled.value ? props.id : undefined),
)
const createLine = useCreateInvoiceLine(invoiceId)
const updateLine = useUpdateInvoiceLine(invoiceId)
const deleteLine = useDeleteInvoiceLine(invoiceId)
const reorderLines = useReorderInvoiceLines(invoiceId)

const { data: clients } = useClients()
const { data: allProjects } = useAllProjects()

const savingDiscountId = ref<string | null>(null)
const discountErrors = ref<Record<string, string>>({})
const applyingDiscounts = ref(false)
const applyDiscountsError = ref('')
const showDiscounts = ref(false)
const savingRate = ref(false)
const rateError = ref('')
const savingIncludeNonBillable = ref(false)
const includeNonBillableError = ref('')
const savingLineId = ref<string | null>(null)
const lineErrors = ref<Record<string, string>>({})
const editingManualId = ref<string | null>(null)
const confirmDeleteManualId = ref<string | null>(null)
const formError = ref('')
const reorderError = ref('')
const localManualOrder = ref<InvoiceLine[]>([])
const draggingId = ref<string | null>(null)
const dragOverId = ref<string | null>(null)

const formClientId = ref('')
const formProjectId = ref('')
const formTitle = ref('')
const formBillingMode = ref<'hours' | 'flat'>('hours')
const formHours = ref('')
const formFlatFee = ref('')
const formDiscount = ref('0')

const { data: formProjectList } = useProjects(
  () => formClientId.value || undefined,
  () => ({ includeShared: true }),
)

watch(formClientId, () => {
  formProjectId.value = ''
})

watch(
  invoiceLines,
  (list) => {
    if (!list) {
      localManualOrder.value = []
      return
    }
    localManualOrder.value = list
      .slice()
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.title.localeCompare(b.title))
  },
  { immediate: true },
)

function toApiStatus(status: string): InvoiceStatus {
  const s = status.trim().toLowerCase().replaceAll(' ', '-').replaceAll('_', '-')
  if (s === 'sent') return 'sent'
  if (s === 'partially-paid' || s === 'partiallypaid') return 'partially-paid'
  if (s === 'fully-paid' || s === 'fullypaid') return 'fully-paid'
  return 'preparing'
}

function toIncludeNonBillable(value: string | null | undefined): IncludeNonBillableTasks {
  const v = (value ?? '').trim().toLowerCase()
  if (v === 'detail' || v === 'summary') return v
  return 'none'
}

function isNonBillableTask(task: WorkTask) {
  return (task.bill ?? '').trim().toLowerCase() === 'no'
}

const VOLLEY_URL_RE = /https?:\/\/app\.meetvolley\.com\/\S*/gi

function displayTaskTitle(title: string) {
  return title.replace(VOLLEY_URL_RE, '').replace(/\s{2,}/g, ' ').trim()
}

interface LineRow {
  key: string
  kind: 'task' | 'manual' | 'summary'
  task: WorkTask | null
  line: InvoiceLine | null
  projectName: string | null
  title: string
  hours: number
  rate: number
  discountPercent: number
  subtotal: number
  isFlatFee: boolean
  isNonBillable: boolean
  allowDiscount: boolean
  allowEdit: boolean
  allowDrag: boolean
}

interface ClientGroup {
  clientId: string
  clientName: string
  rows: LineRow[]
  hours: number
  subtotal: number
}

function compareDate(a: string | null, b: string | null) {
  if (!a && !b) return 0
  if (!a) return 1
  if (!b) return -1
  return a.localeCompare(b)
}

function lineSubtotal(units: number, rate: number, discountPercent: number) {
  return units * rate * (1 - discountPercent / 100)
}

function lineDiscountAmount(units: number, rate: number, discountPercent: number) {
  return units * rate * (discountPercent / 100)
}

const invoiceRate = computed(() => invoice.value?.effectiveRate ?? null)
const includeMode = computed(() => toIncludeNonBillable(invoice.value?.includeNonBillableTasks))

const activeClients = computed(() =>
  (clients.value ?? []).filter((c) => c.active).sort((a, b) => a.name.localeCompare(b.name)),
)

const sharedClientIds = computed(() =>
  new Set(
    (clients.value ?? [])
      .filter((c) => c.name.trim().toLowerCase() === 'shared')
      .map((c) => c.id),
  ),
)

function projectsForClient(clientId: string) {
  return (allProjects.value ?? []).filter(
    (p) => p.clientId === clientId || sharedClientIds.value.has(p.clientId),
  )
}

const clientGroups = computed((): ClientGroup[] => {
  const list = tasks.value ?? []
  const manuals = localManualOrder.value
  const hourlyRate = invoiceRate.value
  const mode = includeMode.value

  const clientIds = new Set<string>()
  for (const t of list) clientIds.add(t.clientId)
  for (const m of manuals) clientIds.add(m.clientId)

  const clientNames = new Map<string, string>()
  for (const t of list) clientNames.set(t.clientId, t.clientName)
  for (const m of manuals) clientNames.set(m.clientId, m.clientName)

  const byClientTasks = new Map<string, WorkTask[]>()
  for (const t of list) {
    const bucket = byClientTasks.get(t.clientId)
    if (bucket) bucket.push(t)
    else byClientTasks.set(t.clientId, [t])
  }

  const byClientManuals = new Map<string, InvoiceLine[]>()
  for (const m of manuals) {
    const bucket = byClientManuals.get(m.clientId)
    if (bucket) bucket.push(m)
    else byClientManuals.set(m.clientId, [m])
  }

  const groups: ClientGroup[] = []
  for (const clientId of clientIds) {
    const clientTasks = byClientTasks.get(clientId) ?? []
    const clientManuals = byClientManuals.get(clientId) ?? []
    const sorted = clientTasks.slice().sort((a, b) => {
      const projectCmp = (a.projectName ?? '\uffff').localeCompare(b.projectName ?? '\uffff')
      if (projectCmp !== 0) return projectCmp
      return compareDate(a.dateDone, b.dateDone)
    })

    const billable = sorted.filter((t) => !isNonBillableTask(t))
    const nonBillable = sorted.filter((t) => isNonBillableTask(t))

    const rows: LineRow[] = []
    for (const line of clientManuals) {
      const discountPercent = line.discountPercent ?? 0
      if (line.flatFee != null) {
        rows.push({
          key: `manual-${line.id}`,
          kind: 'manual',
          task: null,
          line,
          projectName: line.projectName,
          title: line.title,
          hours: 1,
          rate: line.flatFee,
          discountPercent,
          subtotal: lineSubtotal(1, line.flatFee, discountPercent),
          isFlatFee: true,
          isNonBillable: false,
          allowDiscount: true,
          allowEdit: true,
          allowDrag: true,
        })
        continue
      }
      if (hourlyRate == null) continue
      const hours = line.hours ?? 0
      rows.push({
        key: `manual-${line.id}`,
        kind: 'manual',
        task: null,
        line,
        projectName: line.projectName,
        title: line.title,
        hours,
        rate: hourlyRate,
        discountPercent,
        subtotal: lineSubtotal(hours, hourlyRate, discountPercent),
        isFlatFee: false,
        isNonBillable: false,
        allowDiscount: true,
        allowEdit: true,
        allowDrag: true,
      })
    }

    for (const task of billable) {
      const discountPercent = task.discountPercent ?? 0
      if (task.flatFee != null) {
        rows.push({
          key: `task-${task.id}`,
          kind: 'task',
          task,
          line: null,
          projectName: task.projectName,
          title: displayTaskTitle(task.title),
          hours: 1,
          rate: task.flatFee,
          discountPercent,
          subtotal: lineSubtotal(1, task.flatFee, discountPercent),
          isFlatFee: true,
          isNonBillable: false,
          allowDiscount: true,
          allowEdit: false,
          allowDrag: false,
        })
        continue
      }
      if (hourlyRate == null) continue
      const hours = task.billableHours ?? 0
      rows.push({
        key: `task-${task.id}`,
        kind: 'task',
        task,
        line: null,
        projectName: task.projectName,
        title: displayTaskTitle(task.title),
        hours,
        rate: hourlyRate,
        discountPercent,
        subtotal: lineSubtotal(hours, hourlyRate, discountPercent),
        isFlatFee: false,
        isNonBillable: false,
        allowDiscount: true,
        allowEdit: false,
        allowDrag: false,
      })
    }

    if (mode === 'detail') {
      for (const task of nonBillable) {
        const hours = task.nonBillableHours ?? 0
        rows.push({
          key: `task-${task.id}`,
          kind: 'task',
          task,
          line: null,
          projectName: task.projectName,
          title: displayTaskTitle(task.title),
          hours,
          rate: 0,
          discountPercent: 0,
          subtotal: 0,
          isFlatFee: false,
          isNonBillable: true,
          allowDiscount: false,
          allowEdit: false,
          allowDrag: false,
        })
      }
    } else if (mode === 'summary' && nonBillable.length > 0) {
      const hours = nonBillable.reduce((sum, t) => sum + (t.nonBillableHours ?? 0), 0)
      const count = nonBillable.length
      rows.push({
        key: `non-billable-summary-${clientId}`,
        kind: 'summary',
        task: null,
        line: null,
        projectName: null,
        title: `${count} non-billable task${count === 1 ? '' : 's'}`,
        hours,
        rate: 0,
        discountPercent: 0,
        subtotal: 0,
        isFlatFee: false,
        isNonBillable: true,
        allowDiscount: false,
        allowEdit: false,
        allowDrag: false,
      })
    }

    if (rows.length === 0) continue
    const hours = rows.reduce((sum, r) => sum + (r.isNonBillable ? 0 : r.hours), 0)
    const subtotal = rows.reduce((sum, r) => sum + r.subtotal, 0)
    groups.push({
      clientId,
      clientName: clientNames.get(clientId) ?? 'Unknown',
      rows,
      hours,
      subtotal,
    })
  }

  return groups.sort((a, b) => a.clientName.localeCompare(b.clientName))
})

const grandHours = computed(() => clientGroups.value.reduce((sum, g) => sum + g.hours, 0))
const grandTotal = computed(() => {
  if (clientGroups.value.length === 0) return null
  return clientGroups.value.reduce((sum, g) => sum + g.subtotal, 0)
})

const money = new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' })
const hoursFmt = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 })

function formatMoney(n: number | null) {
  if (n == null) return '—'
  return money.format(n)
}

function formatHours(n: number) {
  return hoursFmt.format(n)
}

function formatRate(n: number | null) {
  if (n == null) return '—'
  return money.format(n)
}

/** Full calendar months strictly between completion month and current month. */
function monthsSinceDone(dateDone: string | null | undefined): number | null {
  if (!dateDone) return null
  const done = new Date(dateDone)
  if (Number.isNaN(done.getTime())) return null
  const today = new Date()
  const monthDiff =
    (today.getFullYear() - done.getFullYear()) * 12 + (today.getMonth() - done.getMonth())
  // e.g. 2026-04-01 → 2026-07-26: May + June = 2 (exclude start + current months)
  return Math.max(0, monthDiff - 1)
}

function formatDateYmd(value: string | null | undefined): string | null {
  if (!value) return null
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return null
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function formatDoneAge(dateDone: string | null | undefined): string {
  const ymd = formatDateYmd(dateDone)
  if (!ymd) return '—'
  const months = monthsSinceDone(dateDone)
  return months == null ? ymd : `${ymd} · ${months}`
}

function parseDiscount(raw: string | number): number | undefined {
  const trimmed = String(raw ?? '').trim()
  if (!trimmed) return 0
  const n = Number(trimmed)
  if (!Number.isFinite(n) || n < 0 || n > 100) return undefined
  return n
}

function parseRate(raw: string | number): number | null | undefined {
  const trimmed = String(raw ?? '').trim()
  if (!trimmed) return null
  const n = Number(trimmed)
  if (!Number.isFinite(n) || n < 0) return undefined
  return n
}

function parseNonNegative(raw: string | number): number | undefined {
  const trimmed = String(raw ?? '').trim()
  if (!trimmed) return 0
  const n = Number(trimmed)
  if (!Number.isFinite(n) || n < 0) return undefined
  return n
}

async function persistInvoice(patch: {
  rate?: number | null
  includeNonBillableTasks?: IncludeNonBillableTasks
}) {
  const inv = invoice.value
  if (!inv) return
  await updateInvoice.mutateAsync({
    id: inv.id,
    name: inv.name,
    status: toApiStatus(inv.status),
    isDefault: !!inv.isDefault,
    rate: patch.rate !== undefined ? patch.rate : (inv.rate ?? null),
    includeNonBillableTasks: patch.includeNonBillableTasks
      ?? toIncludeNonBillable(inv.includeNonBillableTasks),
  })
}

async function onRateChange(raw: string) {
  const inv = invoice.value
  if (!inv) return
  const rate = parseRate(raw)
  if (rate === undefined) {
    rateError.value = 'Rate must be a non-negative number.'
    return
  }
  if ((inv.rate ?? null) === rate) {
    rateError.value = ''
    return
  }
  savingRate.value = true
  rateError.value = ''
  try {
    await persistInvoice({ rate })
  } catch (e: any) {
    rateError.value = e?.response?.data?.error ?? 'Could not update rate.'
  } finally {
    savingRate.value = false
  }
}

async function onIncludeNonBillableChange(value: string) {
  const inv = invoice.value
  if (!inv) return
  const mode = toIncludeNonBillable(value)
  if (toIncludeNonBillable(inv.includeNonBillableTasks) === mode) return
  savingIncludeNonBillable.value = true
  includeNonBillableError.value = ''
  try {
    await persistInvoice({ includeNonBillableTasks: mode })
  } catch (e: any) {
    includeNonBillableError.value = e?.response?.data?.error ?? 'Could not update setting.'
  } finally {
    savingIncludeNonBillable.value = false
  }
}

async function onDiscountChange(task: WorkTask, raw: string) {
  const discountPercent = parseDiscount(raw)
  if (discountPercent === undefined) {
    discountErrors.value[task.id] = 'Discount must be 0–100.'
    return
  }
  if ((task.discountPercent ?? 0) === discountPercent) {
    delete discountErrors.value[task.id]
    return
  }
  savingDiscountId.value = task.id
  delete discountErrors.value[task.id]
  try {
    await updateDiscount.mutateAsync({ id: task.id, discountPercent })
  } catch (e: any) {
    discountErrors.value[task.id] = e?.response?.data?.error ?? 'Could not update discount.'
  } finally {
    savingDiscountId.value = null
  }
}

async function applyAgeDiscounts() {
  applyDiscountsError.value = ''
  const list = (tasks.value ?? []).filter((t) => !isNonBillableTask(t))
  const updates: { id: string; discountPercent: number }[] = []
  for (const task of list) {
    const months = monthsSinceDone(task.dateDone)
    if (months == null || months <= 0) continue
    const discountPercent = Math.min(100, months * 5)
    if ((task.discountPercent ?? 0) === discountPercent) continue
    updates.push({ id: task.id, discountPercent })
  }
  if (updates.length === 0) return

  applyingDiscounts.value = true
  try {
    const results = await Promise.allSettled(
      updates.map((u) => updateDiscount.mutateAsync(u)),
    )
    const failed = results.filter((r) => r.status === 'rejected').length
    if (failed > 0) {
      applyDiscountsError.value = `Could not update discount on ${failed} of ${updates.length} tasks.`
    }
  } finally {
    applyingDiscounts.value = false
  }
}

async function addManualLine() {
  formError.value = ''
  if (!formClientId.value) {
    formError.value = 'Client is required.'
    return
  }
  if (!formTitle.value.trim()) {
    formError.value = 'Title is required.'
    return
  }
  const discountPercent = parseDiscount(formDiscount.value)
  if (discountPercent === undefined) {
    formError.value = 'Discount must be 0–100.'
    return
  }

  let hours = 0
  let flatFee: number | null = null
  if (formBillingMode.value === 'flat') {
    const fee = parseNonNegative(formFlatFee.value)
    if (fee === undefined || fee <= 0) {
      formError.value = 'Flat fee must be a positive number.'
      return
    }
    flatFee = fee
  } else {
    const h = parseNonNegative(formHours.value)
    if (h === undefined || h <= 0) {
      formError.value = 'Hours must be a positive number.'
      return
    }
    hours = h
  }

  try {
    await createLine.mutateAsync({
      clientId: formClientId.value,
      projectId: formProjectId.value || null,
      title: formTitle.value.trim(),
      hours,
      flatFee,
      discountPercent,
    })
    formTitle.value = ''
    formHours.value = ''
    formFlatFee.value = ''
    formDiscount.value = '0'
    formBillingMode.value = 'hours'
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not add line.'
  }
}

async function persistManualLine(line: InvoiceLine, patch: Partial<{
  clientId: string
  projectId: string | null
  title: string
  hours: number
  flatFee: number | null
  discountPercent: number
}>) {
  savingLineId.value = line.id
  delete lineErrors.value[line.id]
  try {
    await updateLine.mutateAsync({
      id: line.id,
      clientId: patch.clientId ?? line.clientId,
      projectId: patch.projectId !== undefined ? patch.projectId : line.projectId,
      title: patch.title ?? line.title,
      hours: patch.hours ?? line.hours,
      flatFee: patch.flatFee !== undefined ? patch.flatFee : line.flatFee,
      discountPercent: patch.discountPercent ?? line.discountPercent,
    })
  } catch (e: any) {
    lineErrors.value[line.id] = e?.response?.data?.error ?? 'Could not update line.'
  } finally {
    savingLineId.value = null
  }
}

async function onManualTitleChange(line: InvoiceLine, raw: string) {
  const title = raw.trim()
  if (!title) {
    lineErrors.value[line.id] = 'Title is required.'
    return
  }
  if (title === line.title) {
    delete lineErrors.value[line.id]
    return
  }
  await persistManualLine(line, { title })
}

async function onManualDiscountChange(line: InvoiceLine, raw: string) {
  const discountPercent = parseDiscount(raw)
  if (discountPercent === undefined) {
    lineErrors.value[line.id] = 'Discount must be 0–100.'
    return
  }
  if ((line.discountPercent ?? 0) === discountPercent) {
    delete lineErrors.value[line.id]
    return
  }
  await persistManualLine(line, { discountPercent })
}

async function onManualHoursChange(line: InvoiceLine, raw: string) {
  const hours = parseNonNegative(raw)
  if (hours === undefined || hours <= 0) {
    lineErrors.value[line.id] = 'Hours must be a positive number.'
    return
  }
  if (line.flatFee == null && (line.hours ?? 0) === hours) {
    delete lineErrors.value[line.id]
    return
  }
  await persistManualLine(line, { hours, flatFee: null })
}

async function onManualFlatFeeChange(line: InvoiceLine, raw: string) {
  const flatFee = parseNonNegative(raw)
  if (flatFee === undefined || flatFee <= 0) {
    lineErrors.value[line.id] = 'Flat fee must be a positive number.'
    return
  }
  if (line.flatFee != null && line.flatFee === flatFee) {
    delete lineErrors.value[line.id]
    return
  }
  await persistManualLine(line, { flatFee, hours: 0 })
}

async function onManualBillingModeChange(line: InvoiceLine, mode: 'hours' | 'flat') {
  if (mode === 'flat' && line.flatFee != null) return
  if (mode === 'hours' && line.flatFee == null) return
  if (mode === 'flat') {
    await persistManualLine(line, { flatFee: line.flatFee && line.flatFee > 0 ? line.flatFee : 1, hours: 0 })
  } else {
    await persistManualLine(line, { flatFee: null, hours: line.hours > 0 ? line.hours : 1 })
  }
}

async function onManualProjectChange(line: InvoiceLine, projectId: string) {
  const next = projectId || null
  if ((line.projectId ?? null) === next) return
  await persistManualLine(line, { projectId: next })
}

async function onDeleteManualLine(line: InvoiceLine) {
  savingLineId.value = line.id
  delete lineErrors.value[line.id]
  try {
    await deleteLine.mutateAsync(line.id)
    confirmDeleteManualId.value = null
    editingManualId.value = null
  } catch (e: any) {
    lineErrors.value[line.id] = e?.response?.data?.error ?? 'Could not delete line.'
    confirmDeleteManualId.value = null
  } finally {
    savingLineId.value = null
  }
}

function startEditManual(line: InvoiceLine) {
  editingManualId.value = line.id
  confirmDeleteManualId.value = null
}

function stopEditManual() {
  editingManualId.value = null
  confirmDeleteManualId.value = null
}

function isEditingManual(line: InvoiceLine | null) {
  return !!line && editingManualId.value === line.id
}

function onDragStart(id: string, event: DragEvent) {
  draggingId.value = id
  event.dataTransfer?.setData('text/plain', id)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onDragOver(id: string, event: DragEvent) {
  if (!draggingId.value) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  dragOverId.value = id
}

function onDragLeave(id: string) {
  if (dragOverId.value === id) dragOverId.value = null
}

async function onDrop(targetId: string, event: DragEvent) {
  event.preventDefault()
  const fromId = event.dataTransfer?.getData('text/plain') || draggingId.value
  draggingId.value = null
  dragOverId.value = null
  if (!fromId || fromId === targetId) return

  const order = localManualOrder.value.map((l) => l.id)
  const fromIdx = order.indexOf(fromId)
  const toIdx = order.indexOf(targetId)
  if (fromIdx < 0 || toIdx < 0) return
  order.splice(fromIdx, 1)
  order.splice(toIdx, 0, fromId)

  const byId = new Map(localManualOrder.value.map((l) => [l.id, l]))
  localManualOrder.value = order.map((id, i) => {
    const line = byId.get(id)!
    return { ...line, sortOrder: i }
  })

  reorderError.value = ''
  try {
    await reorderLines.mutateAsync(order)
  } catch (e: any) {
    reorderError.value = e?.response?.data?.error ?? 'Could not save line order.'
  }
}

function onDragEnd() {
  draggingId.value = null
  dragOverId.value = null
}

const contentLoading = computed(() => tasksLoading.value || linesLoading.value)
const contentError = computed(() => tasksError.value || linesError.value)
</script>

<template>
  <section data-testid="invoice-detail-view">
    <p><RouterLink to="/invoices" data-testid="invoice-detail-back">← Invoices</RouterLink></p>

    <p v-if="invoicesLoading" data-testid="invoice-detail-loading">Loading…</p>
    <p v-else-if="invoicesError" class="error" data-testid="invoice-detail-error">Failed to load invoice.</p>
    <p v-else-if="!invoice" data-testid="invoice-not-found">Invoice not found.</p>

    <template v-else>
      <h1 data-testid="invoice-detail-name">{{ invoice.name }}</h1>
      <p class="meta" data-testid="invoice-detail-meta">
        Status: {{ invoice.status }}
      </p>
      <div class="settings-row" data-testid="invoice-detail-settings">
        <div class="setting" data-testid="invoice-detail-rate-row">
          <label class="setting-label" for="invoice-detail-rate">Hourly rate</label>
          <input
            id="invoice-detail-rate"
            type="number"
            step="0.01"
            min="0"
            class="rate-input"
            :value="invoice.rate ?? ''"
            :placeholder="String(invoice.effectiveRate)"
            :disabled="savingRate"
            data-testid="invoice-detail-rate"
            aria-label="Invoice hourly rate"
            @blur="onRateChange(($event.target as HTMLInputElement).value)"
          />
          <span
            v-if="invoice.rate == null"
            class="muted setting-hint"
            data-testid="invoice-detail-rate-default-hint"
          >Using default ({{ formatRate(invoice.effectiveRate) }})</span>
          <span
            v-if="rateError"
            class="error inline"
            data-testid="invoice-detail-rate-error"
          >{{ rateError }}</span>
        </div>
        <div class="setting" data-testid="invoice-detail-include-non-billable-row">
          <label class="setting-label" for="invoice-detail-include-non-billable">Include Non-Billable Tasks</label>
          <select
            id="invoice-detail-include-non-billable"
            class="setting-select"
            :value="includeMode"
            :disabled="savingIncludeNonBillable"
            data-testid="invoice-detail-include-non-billable"
            aria-label="Include non-billable tasks"
            @change="onIncludeNonBillableChange(($event.target as HTMLSelectElement).value)"
          >
            <option
              v-for="opt in INCLUDE_NON_BILLABLE_OPTIONS"
              :key="opt.value"
              :value="opt.value"
            >{{ opt.label }}</option>
          </select>
          <span
            v-if="includeNonBillableError"
            class="error inline"
            data-testid="invoice-detail-include-non-billable-error"
          >{{ includeNonBillableError }}</span>
        </div>
        <div class="setting" data-testid="invoice-detail-discounts-toggle-row">
          <span id="invoice-detail-discounts-label" class="setting-label">Discounts</span>
          <ToggleSwitch
            v-model="showDiscounts"
            aria-labelledby="invoice-detail-discounts-label"
            :pt="{ input: { 'data-testid': 'invoice-detail-discounts-toggle' } }"
          />
        </div>
        <div
          v-if="showDiscounts"
          class="setting"
          data-testid="invoice-detail-apply-discounts-row"
        >
          <button
            type="button"
            class="apply-discounts-btn"
            :disabled="applyingDiscounts || contentLoading"
            data-testid="invoice-detail-apply-discounts"
            @click="applyAgeDiscounts"
          >{{ applyingDiscounts ? 'Applying…' : 'Apply Discounts' }}</button>
          <span class="muted setting-hint">5% × months for tasks with Months &gt; 0</span>
          <span
            v-if="applyDiscountsError"
            class="error inline"
            data-testid="invoice-detail-apply-discounts-error"
          >{{ applyDiscountsError }}</span>
        </div>
      </div>

      <form class="manual-form" data-testid="invoice-manual-line-form" @submit.prevent="addManualLine">
        <h2 class="manual-heading">Add manual line</h2>
        <div class="manual-fields">
          <select
            v-model="formClientId"
            required
            data-testid="invoice-manual-client"
            aria-label="Client"
          >
            <option value="">Client…</option>
            <option v-for="c in activeClients" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
          <select
            v-model="formProjectId"
            :disabled="!formClientId"
            data-testid="invoice-manual-project"
            aria-label="Project"
          >
            <option value="">Project…</option>
            <option v-for="p in formProjectList ?? []" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
          <input
            v-model="formTitle"
            placeholder="Task name"
            required
            data-testid="invoice-manual-title"
          />
          <select
            v-model="formBillingMode"
            data-testid="invoice-manual-billing-mode"
            aria-label="Billing mode"
          >
            <option value="hours">Hours</option>
            <option value="flat">Flat fee</option>
          </select>
          <input
            v-if="formBillingMode === 'hours'"
            v-model="formHours"
            type="number"
            step="0.01"
            min="0"
            placeholder="Hours"
            required
            data-testid="invoice-manual-hours"
          />
          <input
            v-else
            v-model="formFlatFee"
            type="number"
            step="0.01"
            min="0"
            placeholder="Flat fee"
            required
            data-testid="invoice-manual-flat-fee"
          />
          <input
            v-model="formDiscount"
            type="number"
            step="0.01"
            min="0"
            max="100"
            placeholder="Discount %"
            data-testid="invoice-manual-discount"
            aria-label="Discount percent"
          />
          <button
            type="submit"
            :disabled="createLine.isLoading.value"
            data-testid="invoice-manual-submit"
          >Add line</button>
        </div>
        <p v-if="formError" class="error" data-testid="invoice-manual-error">{{ formError }}</p>
      </form>
      <p v-if="reorderError" class="error" data-testid="invoice-manual-reorder-error">{{ reorderError }}</p>

      <p v-if="contentLoading" data-testid="invoice-detail-tasks-loading">Loading tasks…</p>
      <p v-else-if="contentError" class="error" data-testid="invoice-detail-tasks-error">Failed to load invoice lines.</p>
      <template v-else-if="tasksEnabled">
        <p v-if="clientGroups.length === 0" class="muted" data-testid="invoice-detail-empty">
          No tasks or manual lines on this invoice.
        </p>

        <table
          v-else
          class="grid"
          data-testid="invoice-detail-table"
        >
          <thead>
            <tr>
              <th class="drag-col" aria-label="Reorder"></th>
              <th>Project</th>
              <th>Task</th>
              <th class="num">Hours / Fee</th>
              <th class="num">Rate</th>
              <th v-if="showDiscounts" class="num" title="Months since completion date">Months</th>
              <th class="num">Discount %</th>
              <th class="num">Discount</th>
              <th class="num">Subtotal</th>
              <th class="actions-col" aria-label="Actions"></th>
            </tr>
          </thead>
          <tbody
            v-for="group in clientGroups"
            :key="group.clientId"
            :data-testid="`invoice-client-group-${group.clientId}`"
          >
            <tr class="client-header">
              <th
                :colspan="showDiscounts ? 10 : 9"
                :data-testid="`invoice-client-name-${group.clientId}`"
              >{{ group.clientName }}</th>
            </tr>
            <tr
              v-for="row in group.rows"
              :key="row.key"
              :class="{
                'non-billable-row': row.isNonBillable,
                'manual-row': row.kind === 'manual',
                'manual-row--dragging': row.line && draggingId === row.line.id,
                'manual-row--drag-over': row.line && dragOverId === row.line.id && draggingId !== row.line.id,
              }"
              :data-testid="row.task
                ? `invoice-task-row-${row.task.id}`
                : row.line
                  ? `invoice-manual-row-${row.line.id}`
                  : `invoice-non-billable-summary-${group.clientId}`"
              @dragover="row.line ? onDragOver(row.line.id, $event) : undefined"
              @dragleave="row.line ? onDragLeave(row.line.id) : undefined"
              @drop="row.line ? onDrop(row.line.id, $event) : undefined"
            >
              <td class="drag-col">
                <span
                  v-if="row.allowDrag && row.line && isEditingManual(row.line)"
                  class="drag-handle"
                  draggable="true"
                  title="Drag to reorder"
                  :data-testid="`invoice-manual-drag-${row.line.id}`"
                  @dragstart="onDragStart(row.line.id, $event)"
                  @dragend="onDragEnd"
                >⠿</span>
              </td>
              <td :data-testid="row.task
                ? `invoice-task-project-${row.task.id}`
                : row.line
                  ? `invoice-manual-project-cell-${row.line.id}`
                  : undefined"
              >
                <template v-if="row.line && isEditingManual(row.line)">
                  <select
                    class="project-select"
                    :value="row.line.projectId ?? ''"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-project-${row.line.id}`"
                    :aria-label="`Project for ${row.title}`"
                    @change="onManualProjectChange(row.line!, ($event.target as HTMLSelectElement).value)"
                  >
                    <option value="">—</option>
                    <option
                      v-for="p in projectsForClient(row.line.clientId)"
                      :key="p.id"
                      :value="p.id"
                    >{{ p.name }}</option>
                  </select>
                </template>
                <template v-else>{{ row.projectName ?? '—' }}</template>
              </td>
              <td :data-testid="row.task
                ? `invoice-task-title-${row.task.id}`
                : row.line
                  ? `invoice-manual-title-cell-${row.line.id}`
                  : undefined"
              >
                <template v-if="row.task">
                  <span :data-testid="`invoice-task-title-text-${row.task.id}`">{{ row.title }}</span>
                  <RouterLink
                    v-if="row.task.clickUpTaskId"
                    class="task-clickup-id"
                    :to="{ path: '/tasks', query: { clickUpId: row.task.clickUpTaskId } }"
                    :data-testid="`invoice-task-clickup-id-${row.task.id}`"
                  >{{ row.task.clickUpTaskId }}</RouterLink>
                </template>
                <template v-else-if="row.line && isEditingManual(row.line)">
                  <input
                    type="text"
                    class="title-input"
                    :value="row.line.title"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-title-${row.line.id}`"
                    :aria-label="`Title for ${row.title}`"
                    @blur="onManualTitleChange(row.line!, ($event.target as HTMLInputElement).value)"
                  />
                </template>
                <template v-else-if="row.line">
                  <span :data-testid="`invoice-manual-title-${row.line.id}`">{{ row.line.title }}</span>
                </template>
                <template v-else>{{ row.title }}</template>
              </td>
              <td class="num billing-cell">
                <template v-if="row.line && isEditingManual(row.line)">
                  <select
                    class="billing-mode-select"
                    :value="row.isFlatFee ? 'flat' : 'hours'"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-mode-${row.line.id}`"
                    :aria-label="`Billing mode for ${row.title}`"
                    @change="onManualBillingModeChange(row.line!, ($event.target as HTMLSelectElement).value as 'hours' | 'flat')"
                  >
                    <option value="hours">Hours</option>
                    <option value="flat">Flat</option>
                  </select>
                  <input
                    v-if="row.isFlatFee"
                    type="number"
                    step="0.01"
                    min="0"
                    class="fee-input"
                    :value="row.line.flatFee ?? ''"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-fee-${row.line.id}`"
                    :aria-label="`Flat fee for ${row.title}`"
                    @blur="onManualFlatFeeChange(row.line!, ($event.target as HTMLInputElement).value)"
                  />
                  <input
                    v-else
                    type="number"
                    step="0.01"
                    min="0"
                    class="fee-input"
                    :value="row.line.hours"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-hours-${row.line.id}`"
                    :aria-label="`Hours for ${row.title}`"
                    @blur="onManualHoursChange(row.line!, ($event.target as HTMLInputElement).value)"
                  />
                </template>
                <template v-else-if="row.line">
                  <span
                    v-if="row.isFlatFee"
                    :data-testid="`invoice-manual-fee-${row.line.id}`"
                  >{{ formatMoney(row.rate) }}</span>
                  <span
                    v-else
                    :data-testid="`invoice-manual-hours-${row.line.id}`"
                  >{{ formatHours(row.hours) }}</span>
                </template>
                <template v-else>
                  <span :data-testid="row.task ? `invoice-task-hours-${row.task.id}` : undefined">
                    {{ formatHours(row.hours) }}
                  </span>
                </template>
              </td>
              <td class="num" :data-testid="row.task
                ? `invoice-task-rate-${row.task.id}`
                : row.line
                  ? `invoice-manual-rate-${row.line.id}`
                  : undefined"
              >
                {{ formatRate(row.rate) }}<span v-if="row.isFlatFee" class="muted flat-fee-tag"> flat</span>
              </td>
              <td
                v-if="showDiscounts"
                class="num"
                :data-testid="row.task ? `invoice-task-months-${row.task.id}` : undefined"
              >
                {{ row.task ? formatDoneAge(row.task.dateDone) : '—' }}
              </td>
              <td class="num discount-cell">
                <template v-if="showDiscounts && row.allowDiscount && row.task">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="100"
                    class="discount-input"
                    :value="row.discountPercent"
                    :disabled="savingDiscountId === row.task.id"
                    :data-testid="`invoice-task-discount-${row.task.id}`"
                    :aria-label="`Discount for ${row.title}`"
                    @blur="onDiscountChange(row.task, ($event.target as HTMLInputElement).value)"
                  />
                  <span
                    v-if="discountErrors[row.task.id]"
                    class="error inline"
                    :data-testid="`invoice-task-discount-error-${row.task.id}`"
                  >{{ discountErrors[row.task.id] }}</span>
                </template>
                <template v-else-if="showDiscounts && row.allowDiscount && row.line && isEditingManual(row.line)">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="100"
                    class="discount-input"
                    :value="row.discountPercent"
                    :disabled="savingLineId === row.line.id"
                    :data-testid="`invoice-manual-discount-${row.line.id}`"
                    :aria-label="`Discount for ${row.title}`"
                    @blur="onManualDiscountChange(row.line!, ($event.target as HTMLInputElement).value)"
                  />
                  <span
                    v-if="lineErrors[row.line.id]"
                    class="error inline"
                    :data-testid="`invoice-manual-error-${row.line.id}`"
                  >{{ lineErrors[row.line.id] }}</span>
                </template>
                <template v-else-if="row.allowDiscount && (row.task || row.line)">
                  <span
                    :data-testid="row.task
                      ? `invoice-task-discount-${row.task.id}`
                      : `invoice-manual-discount-${row.line!.id}`"
                  >{{ row.discountPercent ? row.discountPercent : '—' }}</span>
                  <span
                    v-if="row.line && lineErrors[row.line.id]"
                    class="error inline"
                    :data-testid="`invoice-manual-error-${row.line.id}`"
                  >{{ lineErrors[row.line.id] }}</span>
                </template>
                <span v-else class="muted">—</span>
              </td>
              <td
                class="num"
                :data-testid="row.task
                  ? `invoice-task-discount-amount-${row.task.id}`
                  : row.line
                    ? `invoice-manual-discount-amount-${row.line.id}`
                    : undefined"
              >
                <template v-if="row.allowDiscount && row.discountPercent">
                  {{ formatMoney(lineDiscountAmount(row.hours, row.rate, row.discountPercent)) }}
                </template>
                <span v-else class="muted">—</span>
              </td>
              <td class="num" :data-testid="row.task
                ? `invoice-task-subtotal-${row.task.id}`
                : row.line
                  ? `invoice-manual-subtotal-${row.line.id}`
                  : undefined"
              >
                {{ formatMoney(row.subtotal) }}
              </td>
              <td class="actions-cell">
                <template v-if="row.line">
                  <template v-if="isEditingManual(row.line)">
                    <template v-if="confirmDeleteManualId === row.line.id">
                      <button
                        type="button"
                        class="link-btn danger"
                        :disabled="savingLineId === row.line.id"
                        :data-testid="`invoice-manual-delete-confirm-${row.line.id}`"
                        @click="onDeleteManualLine(row.line)"
                      >Confirm</button>
                      <button
                        type="button"
                        class="link-btn"
                        :disabled="savingLineId === row.line.id"
                        :data-testid="`invoice-manual-delete-cancel-${row.line.id}`"
                        @click="confirmDeleteManualId = null"
                      >Cancel</button>
                    </template>
                    <template v-else>
                      <button
                        type="button"
                        class="link-btn"
                        :disabled="savingLineId === row.line.id"
                        :data-testid="`invoice-manual-done-${row.line.id}`"
                        @click="stopEditManual"
                      >Done</button>
                      <button
                        type="button"
                        class="link-btn danger"
                        :disabled="savingLineId === row.line.id"
                        :data-testid="`invoice-manual-delete-${row.line.id}`"
                        @click="confirmDeleteManualId = row.line.id"
                      >Delete</button>
                    </template>
                  </template>
                  <button
                    v-else
                    type="button"
                    class="link-btn"
                    :data-testid="`invoice-manual-edit-${row.line.id}`"
                    @click="startEditManual(row.line)"
                  >Edit</button>
                </template>
              </td>
            </tr>
            <tr class="group-subtotal" :data-testid="`invoice-client-subtotal-${group.clientId}`">
              <td colspan="3">Client subtotal</td>
              <td class="num">{{ formatHours(group.hours) }}</td>
              <td class="num"></td>
              <td v-if="showDiscounts" class="num"></td>
              <td class="num"></td>
              <td class="num"></td>
              <td class="num">{{ formatMoney(group.subtotal) }}</td>
              <td></td>
            </tr>
          </tbody>
          <tfoot data-testid="invoice-grand-total">
            <tr class="grand">
              <td colspan="3">Grand total</td>
              <td class="num">{{ formatHours(grandHours) }}</td>
              <td class="num"></td>
              <td v-if="showDiscounts" class="num"></td>
              <td class="num"></td>
              <td class="num"></td>
              <td class="num">{{ formatMoney(grandTotal) }}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
      </template>
    </template>
  </section>
</template>

<style scoped>
.meta, .muted { color: #6b7280; font-size: 0.9rem; }
.error { color: #dc2626; }
.error.inline { display: block; margin-top: 0.25rem; font-size: 0.85rem; }
.settings-row {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem 2rem;
  margin: 0.75rem 0 1.25rem;
}
.setting {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
}
.setting-label { font-weight: 600; }
.rate-input {
  width: 6rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  text-align: right;
}
.setting-select {
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  background: #fff;
}
.setting-hint { font-size: 0.85rem; }
.apply-discounts-btn {
  padding: 0.4rem 0.8rem;
  border: none;
  border-radius: 6px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
  font: inherit;
}
.apply-discounts-btn:disabled { opacity: 0.6; cursor: default; }
.manual-form {
  margin: 0 0 1.25rem;
  padding: 0.75rem 0;
  border-top: 1px solid #e5e7eb;
}
.manual-heading {
  font-size: 1rem;
  font-weight: 600;
  margin: 0 0 0.5rem;
}
.manual-fields {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}
.manual-fields input,
.manual-fields select {
  padding: 0.4rem 0.55rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
}
.manual-fields button {
  padding: 0.4rem 0.8rem;
  border: none;
  border-radius: 6px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
.manual-fields button:disabled { opacity: 0.6; cursor: default; }
.flat-fee-tag { font-size: 0.8rem; margin-left: 0.25rem; }
.task-clickup-id { margin-left: 0.4em; font-size: 0.85rem; font-variant-numeric: tabular-nums; }
.grid { width: 100%; border-collapse: separate; border-spacing: 0; table-layout: fixed; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: none; vertical-align: middle; }
.grid th.num, .grid td.num { text-align: right; font-variant-numeric: tabular-nums; }
.grid thead tr { border-bottom: 2px solid #e5e7eb; }
.grid tbody tr { border-bottom: 1px solid #eee; }
.grid thead th { font-weight: 600; }
.client-header th {
  padding-top: 1.25rem;
  font-size: 1.1rem;
  font-weight: 600;
  background: transparent;
}
.client-header { border-bottom: 1px solid #e5e7eb; }
.drag-col { width: 1.75rem; }
.actions-col, .actions-cell { width: 7.5rem; text-align: right; white-space: nowrap; }
.actions-cell { white-space: nowrap; }
.actions-cell .link-btn + .link-btn { margin-left: 0.5rem; }
.drag-handle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  color: #9ca3af;
  cursor: grab;
  user-select: none;
}
.drag-handle:active { cursor: grabbing; }
.manual-row--dragging { opacity: 0.55; }
.manual-row--drag-over {
  box-shadow: inset 0 2px 0 #059669;
}
.title-input,
.fee-input,
.discount-input,
.project-select,
.billing-mode-select {
  padding: 0.35rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font: inherit;
  background: #fff;
}
.title-input { width: 100%; min-width: 0; }
.billing-cell {
  white-space: nowrap;
}
.billing-cell > * + * { margin-left: 0.35rem; }
.billing-mode-select { width: 4.75rem; vertical-align: middle; }
.fee-input { width: 4.5rem; text-align: right; vertical-align: middle; }
.discount-cell { width: 6.5rem; }
.discount-input {
  width: 4.5rem;
  text-align: right;
}
.project-select { width: 100%; max-width: 10rem; }
.link-btn {
  border: none;
  background: none;
  color: #10b981;
  cursor: pointer;
  padding: 0;
  font: inherit;
}
.link-btn.danger { color: #dc2626; }
.link-btn:disabled { opacity: 0.5; cursor: default; }
.group-subtotal { border-bottom: none; }
.group-subtotal td { font-weight: 600; padding-top: 0.75rem; }
.grand { border-bottom: none; border-top: 2px solid #e5e7eb; }
.grand td { font-weight: 700; font-size: 1.05rem; padding-top: 1rem; }
a { color: #10b981; }
</style>
