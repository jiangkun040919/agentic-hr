import { request } from '@/utils/request'

// 获取工作台数据
export const getDashboardData = () => {
  return request.get('/stat/dashboard')
}

// 获取招聘漏斗数据
export const getFunnelData = (params?: { startDate?: string; endDate?: string; jobId?: number }) => {
  return request.get('/stat/funnel', { params })
}

// 获取岗位投递统计
export const getJobStats = (params?: { startDate?: string; endDate?: string; hrId?: number }) => {
  return request.get('/stat/job', { params })
}

// 获取简历来源统计
export const getResumeSourceStats = (params?: { startDate?: string; endDate?: string }) => {
  return request.get('/stat/source', { params })
}

// 获取热门岗位
export const getHotJobs = (params?: { limit?: number }) => {
  return request.get('/stat/hot-jobs', { params })
}

// 导出统计数据
export const exportStatistics = (params: { type: string; startDate: string; endDate: string }) => {
  return request.get('/stat/export', { params })
}

// 获取每日趋势
export const getTrendData = (params?: { days?: number; type?: string }) => {
  return request.get('/stat/trend', { params })
}

// 获取人才流动池数据
export const getFlowPoolData = () => {
  return request.get('/stat/flow-pool')
}

// 获取多维度趋势数据
export const getMultiTrendData = (params?: { dimension?: string }) => {
  return request.get('/stat/multi-trend', { params })
}

// 获取入职率数据
export const getHireRateData = (params?: { dimension?: string }) => {
  return request.get('/stat/hire-rate', { params })
}

// 获取热门岗位详情
export const getHotJobDetails = () => {
  return request.get('/stat/hot-jobs')
}