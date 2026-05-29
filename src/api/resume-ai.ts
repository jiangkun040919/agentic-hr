import { request } from '@/utils/request'

export interface ParseRequest { resumeId: number }
export interface MatchRequest { resumeId: number; jobId?: number }
export interface InterviewGuideRequest { resumeId: number; jobId?: number }

// ═══ 简历解析 ═══
export interface ResumeSkill {
  name: string; level: string; years: number; confidence: string
}
export interface ResumeProject {
  name: string; role: string; techStack: string[]; description: string
}
export interface EduHistory {
  school: string; degree: string; major: string; startYear: number; endYear: number
}
export interface WorkExp {
  company: string; title: string; startDate: string; endDate: string; description: string
}
export interface ResumeLanguage { name: string; level: string }

export interface ParseResult {
  name: string; phone: string; email: string
  education: { level: string; major: string; school: string } | null
  workYears: number
  skills: ResumeSkill[]
  workExperience: WorkExp[]
  projects: ResumeProject[]
  educationHistory: EduHistory[]
  certifications: string[]
  languages: ResumeLanguage[]
  extractionQuality: string
  analysisMode: string
  analyzedAt: string
}

// ═══ 匹配评分 ═══
export interface MatchScoreResult {
  overall: number; skillMatch: number; experienceMatch: number
  educationMatch: number; fitScore: number
  strengths: string[]; gaps: string[]
  recommendation: string
  hiringSuggestion: string
  levelEstimate: string
  interviewFocus: string[]
}

// ═══ 面试建议 ═══
export interface IQItem {
  type: string; category: string; question: string
  purpose: string; expectedAnswer?: string
}
export interface EvalRubric {
  technicalWeight: number; experienceWeight: number
  communicationWeight: number; cultureFitWeight: number
}
export interface InterviewGuideResult {
  strategy: string; focusTags: string[]; warnings: string[]
  questions: IQItem[]; suggestedDuration: string
  evaluation: EvalRubric | null
}

export const parseResume = (data: ParseRequest) =>
  request.post<ParseResult>('/resume/ai-parse', data)

export const scoreMatch = (data: MatchRequest) =>
  request.post<MatchScoreResult>('/resume/ai-match', data)

export const generateInterviewGuide = (data: InterviewGuideRequest) =>
  request.post<InterviewGuideResult>('/resume/ai-interview-guide', data)
