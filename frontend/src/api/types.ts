export interface Agency {
  id: string
  name: string
  lastClickUpSyncAt: string | null
  lastClickUpSyncSummary: string | null
  uiPreferences: AgencyUiPreferences
}

export interface AgencyUiPreferences {
  taskGroupClientOrder: string[]
}

export interface Client {
  id: string
  name: string
  code: string | null
  originalName: string | null
  clickUpFolderId: string | null
  clickUpListId: string | null
  description: string | null
  status: string
  active: boolean
  billFieldAvailable: boolean
}

export interface Project {
  id: string
  clientId: string
  clientName: string
  name: string
}

export type InvoiceStatus = 'preparing' | 'sent' | 'partially-paid' | 'fully-paid'
export type IncludeNonBillableTasks = 'none' | 'detail' | 'summary'

export interface Invoice {
  id: string
  name: string
  status: InvoiceStatus | string
  sortOrder: number
  isDefault: boolean
  rate: number | null
  effectiveRate: number
  includeNonBillableTasks: IncludeNonBillableTasks | string
}

export interface WorkTask {
  id: string
  shortId: number
  clientId: string
  clientName: string
  projectId: string | null
  projectName: string | null
  bill: string | null
  billableHours: number | null
  nonBillableHours: number | null
  invoiceLabel: string | null
  discountPercent: number
  flatFee: number | null
  note: string | null
  clickUpUrl: string | null
  clickUpTaskId: string | null
  clickUpParentId: string | null
  clickUpFolderId: string | null
  clickUpFolderName: string | null
  clickUpListId: string | null
  clickUpListName: string | null
  title: string
  description: string | null
  clickUpStatus: string | null
  tags: string | null
  dateCreated: string | null
  dueDate: string | null
  dateDone: string | null
  dateClosed: string | null
  orderIndex: number | null
  estimatedHours: number | null
  actualHours: number | null
  needsAttention: boolean
}

export interface TaskClientCount {
  clientId: string
  clientName: string
  taskCount: number
  missingCount: number
  uninvoicedCount: number
}

export interface TaskMonthCount {
  month: string
  taskCount: number
  missingCount: number
  uninvoicedCount: number
}

export interface TaskSummary {
  byClient: TaskClientCount[]
  byDoneMonth: TaskMonthCount[]
}

export interface ClickUpHierarchyNode {
  type: string
  id: string
  name: string
  parentType: string | null
  parentId: string | null
  updatedAt: string
  taskCount: number
  children: ClickUpHierarchyNode[]
}

export interface ClickUpSyncResult {
  syncedAt: string
  containersUpserted: number
  tasksCreated: number
  tasksUpdated: number
  clientsCreated: number
  summary: string
  syncRunId?: string | null
  parentsFetched?: number
}

export interface ClickUpSyncRunSummary {
  id: string
  startedAt: string
  finishedAt: string | null
  status: string
  summary: string | null
  containersUpserted: number
  tasksCreated: number
  tasksUpdated: number
  clientsCreated: number
  parentsFetched: number
}

export interface ClickUpSyncRun extends ClickUpSyncRunSummary {
  log: string
}

export type ClickUpSyncPhase =
  | 'started'
  | 'hierarchy'
  | 'page'
  | 'descendants'
  | 'parents'
  | 'bill_fields'
  | 'hours'
  | 'invoices'
  | 'log'
  | 'completed'
  | 'error'

export interface ClickUpSyncProgressEvent {
  phase: ClickUpSyncPhase
  message?: string
  containersUpserted?: number
  page?: number
  tasksCreated?: number
  tasksUpdated?: number
  clientsCreated?: number
  clientsProcessed?: number
  clientsTotal?: number
  parentsFetched?: number
  syncedAt?: string
  summary?: string
  error?: string
  syncRunId?: string
}

export interface CsvImportResult {
  imported: number
  updated: number
  skipped: number
  summary: string
}
