// 投递状态
export type DeliveryStatus = 0 | 1 | 2 | 3 | 4 | 5
// 0:待查看 1:已查看 2:面试中 3:实习中 4:正式入职 5:已淘汰

// 投递列表查询参数
export interface DeliveryListParams {
  page?: number
  pageSize?: number
  jobId?: number
  hrId?: number
  status?: DeliveryStatus
  keyword?: string
  startDate?: string
  endDate?: string
  sortBy?: 'deliver_time' | 'update_time'
  sortOrder?: 'asc' | 'desc'
}

// 投递表单数据
export interface DeliveryFormData {
  jobId: number
  candidateName: string
  phone: string
  email?: string
  education?: string
  workYears?: number
  resumeUrl?: string
  resumeJson?: string
}

// 投递记录
export interface Delivery {
  deliveryId: number
  jobId: number
  jobTitle: string
  candidateId: number
  candidateName: string
  phone: string
  email?: string
  education?: string
  workYears?: number
  resumeUrl?: string
  resumeText?: string
  status: DeliveryStatus
  hrId: number
  deliverTime: string
  updateTime?: string
  remark?: string
  /** 是否允许进行AI面试 */
  allowAIInterview?: boolean
  /** AI面试截止时间 */
  aiInterviewDeadline?: string | null
}

// 投递详情（包含更多信息）
export interface DeliveryDetail extends Delivery {
  resumeJson?: any
  aiScore?: number
  aiMatchReason?: string
  aiAnalysis?: ResumeAnalysis
  interview?: InterviewInfo
}

// 简历AI分析结果
export interface ResumeAnalysis {
  analysisId: number
  candidateId: number
  parsedJson: {
    name?: string
    phone?: string
    email?: string
    education?: string
    workExperience?: WorkExperience[]
    projects?: Project[]
    skills?: string[]
  }
  skillsTags: string[]
  workExperience: WorkExperience[]
  projects: Project[]
  createdAt: string
}

// 工作经验
export interface WorkExperience {
  company: string
  position: string
  duration: string
  description: string
}

// 项目经验
export interface Project {
  name: string
  role: string
  duration: string
  description: string
}

// 面试信息
export interface InterviewInfo {
  interviewId: number
  interviewerId: number
  interviewerName: string
  scheduleTime: string
  location: string
  status: number
  result?: string
  record?: string
  round?: string
  interviewType?: string
}