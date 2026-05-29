import { request } from '@/utils/request'

// ====== 数据导出 API ======

/** 导出投递记录 Excel */
export const exportDeliveries = () =>
  request.get('/export/deliveries', { responseType: 'blob' })

/** 导出候选人数据 Excel */
export const exportCandidates = () =>
  request.get('/export/candidates', { responseType: 'blob' })

/** 导出准确率评测报告 Excel */
export const exportBenchmark = () =>
  request.get('/export/benchmark', { responseType: 'blob' })

/** 通用下载处理 */
export const downloadFile = (blob: Blob, filename: string) => {
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  window.URL.revokeObjectURL(url)
}
