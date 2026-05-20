import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import router from '@/router'
import { login as loginApi, register as registerApi, getUserInfo } from '@/api/auth'
import type { LoginParams, RegisterParams, UserInfo } from '@/api/auth/types'

export const useUserStore = defineStore('user', () => {
  // 状态 — 初始化时从 localStorage 恢复
  const token = ref<string>(localStorage.getItem('token') || '')
  const userInfo = ref<UserInfo | null>(null)
  const savedRole = localStorage.getItem('role')
  const roles = ref<string[]>(savedRole ? [savedRole] : [])
  const permissions = ref<string[]>([])

  // 计算属性
  const isLoggedIn = computed(() => !!token.value)
  const isHR = computed(() => roles.value.includes('hr'))
  const isAdmin = computed(() => roles.value.includes('admin'))
  const isCandidate = computed(() => roles.value.includes('candidate'))

  // 登录
  async function login(params: LoginParams) {
    const res = await loginApi(params)
    token.value = res.token
    localStorage.setItem('token', res.token)
    localStorage.setItem('role', res.role)
    if (res.userId) {
      localStorage.setItem('userId', String(res.userId))
    }
    roles.value = [res.role]
    await fetchUserInfo()
    return res
  }

  // 注册
  async function register(params: RegisterParams) {
    const res = await registerApi(params)
    token.value = res.token
    localStorage.setItem('token', res.token)
    localStorage.setItem('role', res.role)
    if (res.userId) {
      localStorage.setItem('userId', String(res.userId))
    }
    roles.value = [res.role]
    await fetchUserInfo()
    return res
  }

  // 获取用户信息
  async function fetchUserInfo() {
    if (!token.value) return
    try {
      const res = await getUserInfo()
      userInfo.value = res
      // 从用户信息中提取并保存 userId（后端返回 userId，不是 id）
      const uid = (res as any).userId || (res as any).id
      if (uid) {
        localStorage.setItem('userId', String(uid))
      }
    } catch (_error) {
      // getUserInfo 失败时不要立即登出，保留 token 让用户继续使用
      console.warn('获取用户信息失败，保留当前登录状态')
    }
  }

  // 登出
  function logout() {
    token.value = ''
    userInfo.value = null
    roles.value = []
    permissions.value = []
    localStorage.removeItem('token')
    localStorage.removeItem('role')
    localStorage.removeItem('userId')
    router.push('/login')
  }

  // 设置角色（用于切换账号测试）
  function setRole(role: string) {
    localStorage.setItem('role', role)
    roles.value = [role]
  }

  return {
    token,
    userInfo,
    roles,
    permissions,
    isLoggedIn,
    isHR,
    isAdmin,
    isCandidate,
    login,
    register,
    fetchUserInfo,
    logout,
    setRole,
  }
})