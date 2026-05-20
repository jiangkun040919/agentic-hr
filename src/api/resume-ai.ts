import { request } from '@/utils/request'

export interface ParseRequest { resumeId: number }
export interface MatchRequest { resumeId: number; jobId?: number }
export interface InterviewGuideRequest { resumeId: number; jobId?: number }

export interface EducationInfo { level: string; major: string; school: string }
export interface WorkExp { company: string; title: string; startDate: string; endDate: string; description: string }

export interface ParseResult {
  name: string; phone: string; email: string
  education: EducationInfo | null
  workYears: number
  skills: string[]
  workExperience: WorkExp[]
}

export interface MatchScoreResult {
  overall: number; skillMatch: number; experienceMatch: number
  educationMatch: number; fitScore: number
  strengths: string[]; gaps: string[]
  recommendation: string
}

export interface InterviewQuestion { type: string; question: string; purpose: string }

export interface InterviewGuideResult {
  strategy: string; focusTags: string[]; warnings: string[]; questions: InterviewQuestion[]
}

export const parseResume = (data: ParseRequest) =>
  request.post<ParseResult>('/resume/ai-parse', data)

export const scoreMatch = (data: MatchRequest) =>
  request.post<MatchScoreResult>('/resume/ai-match', data)

export const generateInterviewGuide = (data: InterviewGuideRequest) =>
  request.post<InterviewGuideResult>('/resume/ai-interview-guide', data)
