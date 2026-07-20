export interface AuthUser {
  id: string
  username: string
  email: string
  displayName: string
  roles: string[]
  isContractorSide: boolean
}

export interface Agency {
  id: string
  name: string
  billingEmail: string | null
  currency: string
  paymentTermsDays: number
  active: boolean
}

export interface Client {
  id: string
  name: string
  code: string | null
  description: string | null
  status: string
  active: boolean
}

export interface Project {
  id: string
  clientId: string
  name: string
  code: string | null
  description: string | null
  status: string
  billingType: string
  hourlyRate: number | null
  fixedFee: number | null
  budgetMinutes: number | null
  budgetAmount: number | null
  startDate: string | null
  endDate: string | null
  active: boolean
}

export interface WorkTask {
  id: string
  clientId: string
  projectId: string | null
  parentTaskId: string | null
  title: string
  description: string | null
  workStatus: string
  billingStatus: string
  billingType: string
  billable: boolean
  hourlyRate: number | null
  fixedFee: number | null
  estimatedMinutes: number | null
  estimateRollupMode: string
  actualRollupMode: string
  billingRollupMode: string
  dueDate: string | null
  completedAt: string | null
  finalizedAt: string | null
  sortOrder: number
}

export interface UnmappedContainer {
  containerId: string
  externalId: string
  containerType: string
  name: string
  url: string | null
  parentExternalId: string | null
  mappingId: string | null
  mappingStatus: string | null
  suggestedClientId: string | null
  suggestedClientName: string | null
  suggestedProjectId: string | null
  suggestedProjectName: string | null
}

export interface UnmappedWorkItem {
  workItemId: string
  externalId: string
  name: string
  statusName: string | null
  url: string | null
  parentExternalId: string | null
  containerId: string | null
  containerName: string | null
  mappingId: string | null
  mappingStatus: string | null
  suggestedTaskId: string | null
  suggestedTaskTitle: string | null
  suggestedClientId: string | null
  suggestedProjectId: string | null
}

export interface StatusMapping {
  id: string
  externalStatusName: string
  externalStatusType: string | null
  internalStatus: string
  treatedAsCompleted: boolean
  treatedAsBillable: boolean
  active: boolean
}

export interface SuggestMappingsResult {
  containerSuggestions: number
  taskSuggestions: number
  statusSeeded: number
}

export interface TimeEntry {
  id: string
  taskId: string
  contractorId: string
  workDate: string
  durationMinutes: number
  description: string | null
  billable: boolean
  approvalStatus: string
  hourlyRate: number | null
  billingAmount: number | null
  startedAt: string | null
  endedAt: string | null
  fromImport: boolean
}

export interface TaskRollup {
  taskId: string
  title: string
  estimateRollupMode: string
  actualRollupMode: string
  directEstimateMinutes: number | null
  rolledUpEstimateMinutes: number
  directActualMinutes: number
  rolledUpActualMinutes: number
  descendantCount: number
}

export interface WorkItemReview {
  taskId: string
  clientId: string
  clientName: string
  projectId: string | null
  projectName: string | null
  title: string
  workStatus: string
  billingStatus: string
  completedAt: string | null
  estimatedMinutes: number | null
  actualMinutes: number
  billableMinutes: number
  billingAmountEstimate: number | null
  clickUpUrl: string | null
  clickUpStatus: string | null
}

export interface SyncImportedTimeResult {
  linked: number
  skipped: number
  failed: number
}
