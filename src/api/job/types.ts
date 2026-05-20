// 岗位状态
export type JobStatus = 0 | 1 | 2 // 0:关闭 1:开放 2:已过期

// 岗位列表查询参数
export interface JobListParams {
  page?: number
  pageSize?: number
  keyword?: string
  dept?: string
  location?: string
  salaryMin?: number
  salaryMax?: number
  status?: JobStatus
  sortBy?: 'created_at' | 'salary' | 'views'
  sortOrder?: 'asc' | 'desc'
}

// 岗位表单数据
export interface JobFormData {
  title: string
  dept: string
  location: string
  JD: string
  requirements: string
  salaryMin?: number | null
  salaryMax?: number | null
  headCount?: number | null
  status?: JobStatus
  expiredAt?: string | null
}

// 岗位详情（包含额外信息）
export interface JobDetail extends Job {
  views: number
  deliveries: number
  hrName: string
  hrAvatar?: string
}

// 岗位基本信息
export interface Job {
  jobId: number
  title: string
  dept: string
  location: string
  JD: string
  requirements: string
  salaryMin?: number
  salaryMax?: number
  headCount?: number
  status: JobStatus
  hrId: number
  createdAt: string
  updatedAt?: string
  expiredAt?: string
  skills?: string[]
  deliveryCount?: number
  interviewCount?: number
}