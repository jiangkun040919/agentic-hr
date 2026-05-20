import { request } from '@/utils/request'

// AI分析简历
export const analyzeResume = (deliveryId: number) => {
  return request.post(`/ai/resume/analyze`, { deliveryId })
}

// AI评分（匹配度）
export const scoreResume = (deliveryId: number) => {
  return request.get(`/ai/resume/score`, { params: { deliveryId } })
}

// AI生成面试题
export const generateQuestions = (deliveryId: number) => {
  return request.get(`/ai/interview/generate`, { params: { deliveryId } })
}

// AI招聘洞察
export const getRecruitmentInsights = (hrId: number, period: string = 'week') => {
  return request.get('/ai/insights', { params: { hrId, period } })
}

// 获取最近AI分析记录
export const getRecentAnalyses = (limit: number = 10) => {
  return request.get('/ai/recent', { params: { limit } })
}

// 流式获取AI响应（打字机效果）
export const streamAIResponse = (url: string, data: any): Promise<ReadableStream> => {
  const token = localStorage.getItem('token')
  return fetch(`${import.meta.env.VITE_API_BASE_URL || '/api'}${url}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(data),
  }).then(res => res.body!)
}