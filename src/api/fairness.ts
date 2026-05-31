import { request } from '@/utils/request'

// ====== 公平性审计 API ======

/** 运行公平性审计（真实后端数据） */
export const runFairnessAudit = () =>
  request.post<FairnessAuditReport>('/fairness/audit')

export interface GroupStat {
  group: string
  count: number
  avgStatus: number
  passedRate: number
}

export interface EducationBiasResult {
  groups: GroupStat[]
  maxPassedRate: number
  minPassedRate: number
  biasRatio: number
  isBiased: boolean
  summary: string
}

export interface ExperienceBiasResult {
  groups: GroupStat[]
  maxPassedRate: number
  minPassedRate: number
  biasRatio: number
  isBiased: boolean
  summary: string
}

export interface LocationBiasResult {
  groups: GroupStat[]
  maxPassedRate: number
  minPassedRate: number
  biasRatio: number
  isBiased: boolean
  summary: string
}

export interface StatusCount {
  status: number
  label: string
  count: number
  percentage: number
}

export interface ScoreDistributionResult {
  distribution: StatusCount[]
  totalCount: number
  averageStatus: number
}

export interface OverallRating {
  level: string
  issueCount: number
  score: number
}

export interface FairnessAuditReport {
  generatedAt: string
  educationBias: EducationBiasResult
  experienceBias: ExperienceBiasResult
  locationBias: LocationBiasResult
  scoreDistribution: ScoreDistributionResult
  overallRating: OverallRating
  recommendations: string[]
}
