import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getJobList, getJobDetail, createJob, updateJob, deleteJob, updateJobStatus } from '@/api/job'
import type { Job, JobListParams, JobFormData } from '@/api/job/types'

export const useJobStore = defineStore('job', () => {
  // 状态
  const jobs = ref<Job[]>([])
  const currentJob = ref<Job | null>(null)
  const total = ref(0)
  const loading = ref(false)

  // 获取岗位列表
  async function fetchJobs(params: JobListParams) {
    loading.value = true
    try {
      const res = await getJobList(params)
      jobs.value = res.items
      total.value = res.total
    } finally {
      loading.value = false
    }
  }

  // 获取岗位详情
  async function fetchJobDetail(id: number) {
    loading.value = true
    try {
      const res = await getJobDetail(id)
      currentJob.value = res
      return res
    } finally {
      loading.value = false
    }
  }

  // 创建岗位
  async function create(data: JobFormData) {
    const res = await createJob(data)
    return res
  }

  // 更新岗位
  async function update(id: number, data: JobFormData) {
    const res = await updateJob(id, data)
    return res
  }

  // 删除岗位
  async function remove(id: number) {
    const res = await deleteJob(id)
    return res
  }

  // 更新岗位状态（上下架）
  async function toggleStatus(id: number, status: number) {
    const res = await updateJobStatus(id, status)
    return res
  }

  return {
    jobs,
    currentJob,
    total,
    loading,
    fetchJobs,
    fetchJobDetail,
    create,
    update,
    remove,
    toggleStatus,
  }
})