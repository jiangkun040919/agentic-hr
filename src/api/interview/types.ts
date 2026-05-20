// 面试状态
export type InterviewStatus = 0 | 1 | 2 | 3 | 4
// 0:待面试 1:已面试 2:通过 3:未通过 4:已取消

// 面试轮次
export type InterviewRound = 'HR初面' | '技术初试' | '技术复试' | '终面'

// 面试形式
export type InterviewType = '线上面试' | '现场面试' | '电话面试'

// 面试列表查询参数
export interface InterviewListParams {
  page?: number
  pageSize?: number
  deliveryId?: number
  interviewerId?: number
  status?: InterviewStatus
  startDate?: string
  endDate?: string
  keyword?: string
}

// 面试表单数据（完整版）
export interface InterviewFormData {
  deliveryId: number
  interviewerId: number
  interviewerIds?: number[]
  scheduleTime: string
  location: string
  round?: string
  interviewType?: string
  duration?: number
  remark?: string
  notifyChannels?: string[]
  notifyContent?: string
}

// 面试结果回填数据
export interface InterviewResultData {
  result: string
  record: string
  score?: number
  scores?: {
    professional: number
    experience: number
    communication: number
    quality: number
  }
}

// 面试记录
export interface Interview {
  interviewId: number
  deliveryId: number
  candidateName: string
  jobTitle: string
  interviewerId: number
  interviewerName: string
  scheduleTime: string
  location: string
  status: InterviewStatus
  result?: string
  record?: string
  createdAt: string
  updatedAt?: string
  // 扩展字段
  round?: string
  interviewType?: string
  duration?: number
  cancelReason?: string
}

// 面试详情
export interface InterviewDetail extends Interview {
  candidatePhone: string
  candidateEmail: string
  resumeUrl?: string
  deliveryStatus: number
}

// 面试官信息
export interface Interviewer {
  userId: number
  realName: string
  roleName: string
  phone?: string
  email?: string
}