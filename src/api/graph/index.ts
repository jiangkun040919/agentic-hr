import { request } from '@/utils/request'
import type {
  GraphData, GapAnalysisResult, LearningPathResult, HallucinationCheckResult,
  EnhancedMatchResult, EmergingJobReport, JobEvolutionReport, SkillTrendData,
  SnapshotComparison, AccuracyReport
} from './types'

// ====== 图谱数据 ======
export const getJobSkillGraph = (centerJob?: string, depth = 2) =>
  request.get<GraphData>('/graph/job-skill', { params: { centerJob, depth } })

export const getSkillTrend = (jobName: string) =>
  request.get<SkillTrendData>('/graph/skill-trend', { params: { jobName } })

export const getSkillCooccurrence = () =>
  request.get('/graph/skill-cooccurrence')

export const getJobHotness = () =>
  request.get('/graph/job-hotness')

export const findSimilarJobs = (jobName: string) =>
  request.get<string[]>('/graph/similar-jobs', { params: { jobName } })

// ====== 匹配与差距分析 ======
export const analyzeSkillGap = (candidateSkills: string[], targetJob: string) =>
  request.post<{ result: GapAnalysisResult; aiAdvice?: string }>('/graph/skill-gap', { candidateSkills, targetJob })

export const getLearningPath = (candidateSkills: string[], targetJob: string) =>
  request.post<LearningPathResult>('/graph/learning-path', { candidateSkills, targetJob })

export const enhancedMatch = (resumeText: string, jobId: number) =>
  request.post<EnhancedMatchResult>('/graph/enhanced-match', { resumeText, jobId })

export const runAccuracyTest = (testPairs: { label: string; resumeText: string; jobId: number; isExpectedMatch: boolean }[]) =>
  request.post<AccuracyReport>('/graph/accuracy-test', testPairs)

// ====== 新岗位发现与演化 ======
export const getEmergingJobs = () =>
  request.get<EmergingJobReport>('/graph/emerging-jobs')

export const getJobEvolution = (jobTitle: string) =>
  request.get<JobEvolutionReport>('/graph/job-evolution', { params: { jobTitle } })

// ====== 图谱管理 ======
export const verifySkills = (skills: string[]) =>
  request.post<HallucinationCheckResult>('/graph/verify-skills', { skills })

export const takeSnapshot = (period: string) =>
  request.post('/graph/snapshot', null, { params: { period } })

export const compareSnapshots = (period1: string, period2: string) =>
  request.get<SnapshotComparison>('/graph/snapshot-compare', { params: { period1, period2 } })

export const ingestJob = (jobId: number, jobTitle: string, requirements: string, jd: string) =>
  request.post('/graph/ingest-job', { jobId, jobTitle, requirements, jd })

export const runETL = () =>
  request.post('/graph/etl/run')

export const getTestDataset = () =>
  request.get('/graph/test-dataset')

// ====== 自然语言查询 & 报告 ======
export const nlQuery = (question: string) =>
  request.post<{ answer: string; relatedSkills: string[]; queriedAt: string }>('/graph/nl-query', { question })

export const getMarketReport = () =>
  request.get<MarketReport>('/graph/market-report')

export const evaluateAccuracy = () =>
  request.post<AccuracyEvaluation>('/graph/evaluate-accuracy')

export interface MarketReport {
  generatedAt: string
  totalActiveJobs: number
  departmentDistribution: Record<string, number>
  cityDistribution: Record<string, number>
  avgSalaryMin: number
  avgSalaryMax: number
  salaryRange: string
  topDemandSkills: Record<string, number>
  aiSummary: string
}

export interface AccuracyEvaluation {
  evaluatedAt: string
  resumeParseAccuracy: number
  resumeTotalFields: number
  resumeCorrectFields: number
  matchAccuracy: number
  matchTotal: number
  matchCorrect: number
  passThreshold: boolean
  summary: string
}
