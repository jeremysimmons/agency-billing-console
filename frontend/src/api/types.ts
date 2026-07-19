export interface AuthUser {
  id: string
  username: string
  email: string
  displayName: string
  roles: string[]
  isContractorSide: boolean
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
