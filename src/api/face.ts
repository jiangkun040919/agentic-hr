import axios from 'axios'
import Cookies from 'js-cookie'

/**
 * 腾讯云人脸表情分析（后端代理）
 * 使用原始 axios 直接发请求，不经过全局响应拦截器，
 * 避免 code:500 时弹出"表情分析服务异常"全局提示。
 * @param imageBase64 摄像头截图的 base64 字符串
 */
export async function analyzeExpression(imageBase64: string): Promise<{ data?: { expression?: string } } | null> {
  try {
    const token = Cookies.get('token') || localStorage.getItem('token')
    const baseURL = import.meta.env.VITE_API_BASE_URL || '/api'
    const res = await axios.post(
      `${baseURL}/face/analyze`,
      { imageBase64 },
      {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        timeout: 10000
      }
    )
    // 仅在成功时返回数据，否则静默返回 null
    if (res.data?.code === 200) {
      return res.data
    }
    return null
  } catch {
    return null
  }
}

