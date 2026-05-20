import { request } from '@/utils/request'

// 获取面试官列表
export const getInterviewers = () => {
  return request.get<any[]>('/users/interviewers')
}

// 创建面试官
export const createInterviewer = (data: {
  username: string
  realName?: string
  password?: string
  phone?: string
  email?: string
}) => {
  return request.post('/users/interviewer', data)
}

// 更新面试官
export const updateInterviewer = (id: number, data: {
  realName?: string
  phone?: string
  email?: string
  password?: string
}) => {
  return request.put(`/users/interviewer/${id}`, data)
}

// 删除面试官
export const deleteInterviewer = (id: number) => {
  return request.delete(`/users/interviewer/${id}`)
}
