export interface GraphNode {
  id: string
  label: string
  properties: Record<string, string>
}

export interface GraphEdge {
  id: string
  label: string
  source: string
  target: string
}

export interface GraphData {
  nodes: GraphNode[]
  edges: GraphEdge[]
}

export interface GapAnalysisResult {
  targetJob: string
  requiredSkills: string[]
  matchedSkills: string[]
  missingSkills: string[]
  matchRate: number
}

export interface LearningStep {
  skill: string
  suggestion: string
}

export interface LearningPathResult {
  targetJob: string
  currentMatchRate: number
  steps: LearningStep[]
  missingSkills: string[]
}

export interface HallucinationCheckResult {
  verifiedSkills: string[]
  unverifiedSkills: string[]
  verificationRate: number
}

export interface MatchDimension {
  name: string
  score: number
  weight: number
  analysis: string
  strengths: string[]
  weaknesses: string[]
}

export interface EnhancedMatchResult {
  jobTitle: string
  overallScore: number
  dimensions: MatchDimension[]
  gapAnalysis: GapAnalysisResult
  learningPath: LearningPathResult
  verification: HallucinationCheckResult
  suggestions: string[]
  matchedAt: string
}

export interface DiscoveredJob {
  name: string
  responsibilities: string
  requiredSkills: string[]
  plusSkills: string[]
  scenarios: string
  demandLevel: string
  discoveredAt: string
  sourceSkills: string[]
}

export interface EmergingJobReport {
  generatedAt: string
  emergingSkills: string[]
  discoveredJobs: DiscoveredJob[]
  totalDiscovered: number
}

export interface JobEvolutionReport {
  jobTitle: string
  analyzedAt: string
  addedSkills: string[]
  removedSkills: string[]
  upgradedSkills: string[]
  trendSummary: string
}

export interface SkillTrendPoint {
  skill: string
  period: string
  demandScore: number
}

export interface SkillTrendData {
  jobName: string
  periods: string[]
  points: SkillTrendPoint[]
}

export interface SkillChange {
  job: string
  skill: string
  changeType: string
  oldValue: string
  newValue: string
}

export interface SnapshotComparison {
  period1: string
  period2: string
  changes: SkillChange[]
}

export interface TestResult {
  label: string
  score: number
  isAccurate: boolean
}

export interface AccuracyReport {
  totalTests: number
  accurate: number
  inaccurate: number
  accuracy: number
  results: TestResult[]
}
