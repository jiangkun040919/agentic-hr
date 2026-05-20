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