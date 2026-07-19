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
