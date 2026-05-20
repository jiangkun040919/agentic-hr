import { request } from '@/utils/request'

export interface LoginParams {
  username: string
  password: string
}

export interface RegisterParams {
  username: string
  password: string
  role: 'hr' | 'candidate'
  realName: string
  phone?: string
  email?: string
}

export interface LoginResult {
  token: string
  role: string
  userId: number
  username: string
}

export interface UserInfo {
  userId: number
  username: string
  role: string
  realName: string
  phone?: string
  email?: string
  avatar?: string
}

export const login = (data: LoginParams) => {
  return request.post<LoginResult>('/auth/login', data)
}

export const register = (data: RegisterParams) => {
  return request.post<LoginResult>('/auth/register', data)
}

export const logout = () => {
  return request.post('/auth/logout')
}

export const getUserInfo = () => {
  return request.get<UserInfo>('/auth/info')
}

export const refreshToken = () => {
  return request.post<{ token: string }>('/auth/refresh')
}

export const changePassword = (data: { oldPassword: string; newPassword: string }) => {
  return request.post('/auth/change-password', data)
}

export const updateProfile = (data: { realName?: string; phone?: string; email?: string }) => {
  return request.put('/auth/profile', data)
}