import { request } from '@/utils/request'

// 获取常用面试官配置
export const getCommonInterviewers = () => {
  return request.get<number[]>('/sys-config/common-interviewers')
}

// 保存常用面试官配置
export const saveCommonInterviewers = (interviewerIds: number[]) => {
  return request.post('/sys-config/common-interviewers', { interviewerIds })
}
