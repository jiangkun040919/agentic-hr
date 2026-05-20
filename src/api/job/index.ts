import { request } from '@/utils/request'
import type { Job, JobListParams, JobFormData, JobDetail } from './types'

// 获取岗位列表
export const getJobList = (params: JobListParams) => {
  return request.get<{ items: Job[]; total: number }>('/job/list', { params })
}

// 获取岗位详情
export const getJobDetail = (id: number) => {
  return request.get<JobDetail>(`/job/${id}`)
}

// 创建岗位
export const createJob = (data: JobFormData) => {
  return request.post<Job>('/job', data)
}

// 更新岗位
export const updateJob = (id: number, data: JobFormData) => {
  return request.put<Job>(`/job/${id}`, data)
}

// 删除岗位
export const deleteJob = (id: number) => {
  return request.delete(`/job/${id}`)
}

// 更新岗位状态
export const updateJobStatus = (id: number, status: number) => {
  return request.put(`/job/${id}/status`, { status })
}

// 获取我的发布岗位列表（HR）
export const getMyJobs = (params?: JobListParams) => {
  return request.get<{ items: Job[]; total: number }>('/job/my', { params })
}

// AI 智能生成 JD
export const generateJD = (brief: string) => {
  return request.post('/job/generate-jd', { brief })
}