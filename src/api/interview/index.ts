import { request } from '@/utils/request'
import type { Interview, InterviewFormData, InterviewListParams, InterviewDetail, InterviewResultData, Interviewer } from './types'

// 获取面试列表
export const getInterviewList = (params: InterviewListParams) => {
  return request.get<{ items: Interview[]; total: number }>('/interview/list', { params })
}

// 获取面试详情
export const getInterviewDetail = (id: number) => {
  return request.get<InterviewDetail>(`/interview/${id}`)
}

// 安排面试
export const scheduleInterview = (data: InterviewFormData) => {
  return request.post<Interview>('/interview', data)
}

// 更新面试信息
export const updateInterview = (id: number, data: Partial<InterviewFormData>) => {
  return request.put<Interview>(`/interview/${id}`, data)
}

// 更新面试状态
export const updateInterviewStatus = (id: number, status: number) => {
  return request.put(`/interview/${id}/status`, { status })
}

// 记录面试结果
export const recordInterviewResult = (id: number, data: InterviewResultData) => {
  return request.put(`/interview/${id}/result`, data)
}

// 取消面试（带原因）
export const cancelInterview = (id: number, reason?: string) => {
  return request.delete(`/interview/${id}`, { data: { reason } })
}

// 检查面试官时间冲突
export const checkInterviewConflict = (interviewerId: number, scheduleTime: string) => {
  return request.get<boolean>('/interview/check-conflict', {
    params: { interviewerId, scheduleTime }
  })
}

// 获取面试官列表
export const getInterviewerList = () => {
  return request.get<Interviewer[]>('/users/interviewers')
}

// 发送面试通知
export const sendInterviewNotification = (interviewId: number, channels: string[]) => {
  return request.post(`/interview/${interviewId}/notify`, { channels })
}