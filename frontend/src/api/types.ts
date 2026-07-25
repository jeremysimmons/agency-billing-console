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
  description: string | null
  status: string
  active: boolean
  billFieldAvailable: boolean
}

export interface Project {
  id: string
  clientId: string
  name: string
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
  children: ClickUpHierarchyNode[]
}

export interface ClickUpSyncResult {
  syncedAt: string
  containersUpserted: number
  tasksCreated: number
  tasksUpdated: number
  clientsCreated: number
  summary: string
}

export interface CsvImportResult {
  imported: number
  updated: number
  skipped: number
  summary: string
}
