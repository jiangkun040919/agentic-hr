import { defineStore } from 'pinia'
import { ref } from 'vue'
import { 
  getResumeList, 
  getResumeDetail, 
  updateResumeStatus, 
  getMyDeliveries,
  submitDelivery 
} from '@/api/delivery'
import type { Delivery, DeliveryDetail, DeliveryListParams, DeliveryFormData } from '@/api/delivery/types'

export const useResumeStore = defineStore('resume', () => {
  // 状态
  const deliveries = ref<Delivery[]>([])
  const currentDelivery = ref<DeliveryDetail | null>(null)
  const total = ref(0)
  const loading = ref(false)

  // 获取简历列表（HR）
  async function fetchResumes(params: DeliveryListParams) {
    loading.value = true
    try {
      const res = await getResumeList(params)
      deliveries.value = res.items
      total.value = res.total
    } finally {
      loading.value = false
    }
  }

  // 获取简历详情
  async function fetchResumeDetail(id: number) {
    loading.value = true
    try {
      const res = await getResumeDetail(id)
      currentDelivery.value = res
      return res
    } finally {
      loading.value = false
    }
  }

  // 更新简历状态
  async function updateStatus(id: number, status: number, remark?: string) {
    const res = await updateResumeStatus(id, { status, remark })
    return res
  }

  // 获取我的投递记录（求职者）
  async function fetchMyDeliveries() {
    loading.value = true
    try {
      const res = await getMyDeliveries()
      deliveries.value = res
    } finally {
      loading.value = false
    }
  }

  // 提交投递
  async function submit(data: DeliveryFormData) {
    const res = await submitDelivery(data)
    return res
  }

  return {
    deliveries,
    currentDelivery,
    total,
    loading,
    fetchResumes,
    fetchResumeDetail,
    updateStatus,
    fetchMyDeliveries,
    submit,
  }
})