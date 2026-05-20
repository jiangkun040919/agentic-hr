import { request } from '@/utils/request'
import type { Delivery, DeliveryListParams, DeliveryFormData, DeliveryDetail, ResumeAnalysis } from './types'

// 获取简历列表（HR）
export const getResumeList = (params: DeliveryListParams) => {
  return request.get<{ items: Delivery[]; total: number }>('/delivery/list', { params })
}

// 获取简历详情
export const getResumeDetail = (id: number) => {
  return request.get<DeliveryDetail>(`/delivery/${id}`)
}

// 更新简历状态
export const updateResumeStatus = (id: number, data: { status: number; remark?: string }) => {
  return request.put(`/delivery/${id}/status`, data)
}

// 获取我的投递记录（求职者）
export const getMyDeliveries = () => {
  return request.get<Delivery[]>('/delivery/my')
}

// 提交投递
export const submitDelivery = (data: DeliveryFormData) => {
  return request.post<Delivery>('/delivery', data)
}

// 取消投递
export const cancelDelivery = (id: number) => {
  return request.delete(`/delivery/${id}`)
}

// 获取AI简历分析结果
export const getResumeAnalysis = (deliveryId: number) => {
  return request.post<ResumeAnalysis>(`/ai/resume/analyze`, { deliveryId })
}

// AI匹配度评分
export const getMatchScore = (deliveryId: number) => {
  return request.get(`/ai/resume/score`, { params: { deliveryId } })
}

// AI生成面试题
export const generateInterviewQuestions = (deliveryId: number) => {
  return request.get(`/ai/interview/generate`, { params: { deliveryId } })
}

// 更新投递信息
export const updateDeliveryInfo = (id: number, data: DeliveryFormData) => {
  return request.put(`/delivery/${id}`, data)
}

// 设置AI面试权限（HR操作）
export const setAIInterviewPermission = (id: number, allow: boolean, deadline?: string) => {
  return request.put(`/delivery/${id}/ai-interview-permission`, { allow, deadline })
}

// 批量 AI 打分排序
export const batchScore = (deliveryIds: number[]) => {
  return request.post('/delivery/batch-score', { deliveryIds })
}

// 批量操作（状态变更等）
export const batchOperation = (deliveryIds: number[], status: number, remark?: string) => {
  return request.post('/delivery/batch', { deliveryIds, status, remark })
}

// 开始实习
export const startInternship = (deliveryId: number, data: { startDate?: string; position?: string; mentor?: string }) => {
  return request.put(`/delivery/${deliveryId}/start-internship`, data)
}

// 正式入职
export const formalHire = (deliveryId: number, data: { hireDate?: string; position?: string; salary?: number }) => {
  return request.put(`/delivery/${deliveryId}/formal-hire`, data)
}

// 上传简历文件（PDF/Word）并提取文本
export const uploadResumeFile = (deliveryId: number, fileBase64: string, fileName: string) => {
  return request.post<{ textLength: number }>(`/delivery/${deliveryId}/upload-resume`, { fileBase64, fileName })
}

// 多候选人横向对比
export const compareCandidates = (deliveryIds: number[]) => {
  return request.post('/delivery/compare', { deliveryIds })
}