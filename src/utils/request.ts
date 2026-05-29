import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import router from '@/router'
import Cookies from 'js-cookie'

// 创建axios实例
const service: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 90000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// 请求拦截器
service.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // 从Cookie获取token
    const token = Cookies.get('token') || localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    console.error('请求错误:', error)
    return Promise.reject(error)
  }
)

// 响应拦截器
service.interceptors.response.use(
  (response: AxiosResponse) => {
    // 文件下载（blob/arraybuffer）直接返回
    if (response.config.responseType === 'blob' || response.config.responseType === 'arraybuffer') {
      return response
    }
    
    const res = response.data
    
    // 根据业务状态码处理
    if (res.code === 200 || res.success) {
      return res.data ?? res
    }
    
    // 特定错误码处理
    if (res.code === 401) {
      ElMessage.error('登录已过期，请重新登录')
      localStorage.removeItem('token')
      localStorage.removeItem('role')
      router.push('/login')
      return Promise.reject(new Error(res.message || '登录已过期'))
    }
    
    if (res.code === 403) {
      ElMessage.error('没有权限访问')
      return Promise.reject(new Error(res.message || '没有权限'))
    }
    
    // 其他错误
    ElMessage.error(res.message || '请求失败')
    return Promise.reject(new Error(res.message || '请求失败'))
  },
  (error) => {
    // 网络错误处理
    if (error.code === 'ECONNABORTED') {
      ElMessage.error('请求超时，请稍后重试')
    } else if (error.response) {
      switch (error.response.status) {
        case 404:
          ElMessage.error('请求的资源不存在')
          break
        case 500:
          ElMessage.error('服务器错误，请稍后重试')
          break
        case 502:
          ElMessage.error('网关错误，请稍后重试')
          break
        case 503:
          ElMessage.error('服务不可用，请稍后重试')
          break
        default:
          ElMessage.error(error.response.data?.message || '请求失败')
      }
    } else {
      ElMessage.error('网络连接失败，请检查网络')
    }
    return Promise.reject(error)
  }
)

// 扩展request方法支持FormData
export const request = {
  get<T = any>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return service.get(url, config).then(res => res as T)
  },
  post<T = any>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    return service.post(url, data, config).then(res => res as T)
  },
  put<T = any>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    return service.put(url, data, config).then(res => res as T)
  },
  delete<T = any>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return service.delete(url, config).then(res => res as T)
  },
  patch<T = any>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    return service.patch(url, data, config).then(res => res as T)
  },
  // 上传文件
  upload<T = any>(url: string, formData: FormData, config?: AxiosRequestConfig): Promise<T> {
    return service.post(url, formData, {
      ...config,
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }).then(res => res as T)
  },
  // 流式请求（用于AI流式输出）
  stream(url: string, data?: any): EventSource {
    const fullUrl = data
      ? `${url}?${new URLSearchParams(data).toString()}`
      : url
    const eventSource = new EventSource(`${service.defaults.baseURL}${fullUrl}`, {
      withCredentials: true,
    })
    return eventSource
  }
}

export default service