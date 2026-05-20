<template>
  <div class="my-deliveries-container">
    <h2 class="page-title">我的投递记录</h2>

    <!-- 站内消息通知提示 -->
    <el-alert
      type="info"
      :closable="false"
      show-icon
      class="notify-banner"
    >
      <template #default>
        <span>
          面试通知将通过<strong>站内消息</strong>实时推送，请注意右上角
          <el-icon style="vertical-align: middle; margin: 0 2px;"><Bell /></el-icon>
          铃铛图标的消息提醒，有新通知时会显示红色角标。
        </span>
      </template>
    </el-alert>

    <!-- AI面试邀请卡片 -->
    <el-card v-if="aiInterviewInvitations.length > 0" class="ai-invitation-card" shadow="hover">
      <template #header>
        <div class="ai-invitation-header">
          <el-icon color="var(--color-success)" size="20"><VideoCamera /></el-icon>
          <span class="invitation-title">AI面试邀请</span>
          <el-badge :value="aiInterviewInvitations.length" type="success" />
        </div>
      </template>
      <div class="ai-invitation-list">
        <div 
          v-for="item in aiInterviewInvitations" 
          :key="item.deliveryId" 
          class="ai-invitation-item"
        >
          <div class="invitation-info">
            <h4>{{ item.jobTitle }}</h4>
            <p class="deadline" v-if="item.aiInterviewDeadline">
              <el-icon><Clock /></el-icon>
              请在 {{ formatDeadline(item.aiInterviewDeadline) }} 前完成面试
            </p>
            <p class="deadline warning" v-else-if="isDeadlinePassed(item.aiInterviewDeadline)">
              <el-icon color="var(--color-danger)"><Clock /></el-icon>
              面试邀请已过期
            </p>
          </div>
          <div class="invitation-actions">
            <el-button 
              type="success" 
              size="large" 
              @click="startAIInterview(item)"
              :disabled="isDeadlinePassed(item.aiInterviewDeadline)"
            >
              <el-icon><VideoPlay /></el-icon>
              参加AI面试
            </el-button>
          </div>
        </div>
      </div>
    </el-card>

    <el-card v-loading="loading">
      <el-tabs v-model="activeTab">
        <el-tab-pane label="我的投递" name="deliveries">
          <el-table :data="deliveries" stripe>
            <el-table-column prop="jobTitle" label="投递岗位" min-width="120" />
            <el-table-column prop="candidateName" label="姓名" width="100" />
            <el-table-column prop="phone" label="手机号" width="120" />
            <el-table-column prop="deliverTime" label="投递时间" width="160">
              <template #default="{ row }">
                {{ formatDate(row.deliverTime) }}
              </template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="100">
              <template #default="{ row }">
                <el-tag :type="getStatusType(row.status)">{{ getStatusText(row.status) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="allowAIInterview" label="AI面试" width="100">
              <template #default="{ row }">
                <el-tag v-if="row.allowAIInterview" type="success" size="small">
                  <el-icon><VideoCamera /></el-icon> 已开放
                </el-tag>
                <el-tag v-else type="info" size="small">未开放</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="updateTime" label="更新时间" width="160">
              <template #default="{ row }">
                {{ row.updateTime ? formatDate(row.updateTime) : '-' }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="200">
              <template #default="{ row }">
                <el-button size="small" type="primary" @click="viewDetail(row)">查看详情</el-button>
                <el-button size="small" @click="editDetail(row)" style="margin-left: 8px;">修改信息</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>
        
        <el-tab-pane label="AI面试记录" name="ai-interviews" lazy>
          <el-empty v-if="aiSessionsLoading" description="加载中..." />
          <el-empty v-else-if="aiSessions.length === 0" description="暂无AI面试记录" />
          <div v-else class="ai-history-list">
            <el-card 
              v-for="item in aiSessions" 
              :key="item.sessionId"
              class="ai-history-card"
              shadow="hover"
            >
              <div class="ai-history-content">
                <div class="history-info">
                  <h4>{{ item.jobTitle || 'AI面试' }}</h4>
                  <p>开始时间：{{ item.startTime ? formatDate(item.startTime) : formatDate(item.createdAt) }}</p>
                  <p v-if="item.endTime">结束时间：{{ formatDate(item.endTime) }}</p>
                  <p v-if="item.totalDuration">面试时长：{{ formatDuration(item.totalDuration) }}</p>
                </div>
                <div class="history-status">
                  <el-tag :type="getSessionStatusType(item.status)" size="large">
                    {{ getSessionStatusText(item.status) }}
                  </el-tag>
                  <div v-if="item.totalScore" class="score-display">
                    <span class="score-label">AI评分</span>
                    <span class="score-value" :class="getScoreClass(item.totalScore)">{{ item.totalScore }}</span>
                  </div>
                </div>
              </div>
            </el-card>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-dialog title="投递详情" v-model="showDetail" width="500px">
      <div v-if="selectedDelivery" class="detail-content">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="投递岗位">{{ selectedDelivery.jobTitle }}</el-descriptions-item>
          <el-descriptions-item label="投递时间">{{ formatDate(selectedDelivery.deliverTime) }}</el-descriptions-item>
          <el-descriptions-item label="姓名">{{ selectedDelivery.candidateName }}</el-descriptions-item>
          <el-descriptions-item label="手机号">{{ selectedDelivery.phone }}</el-descriptions-item>
          <el-descriptions-item label="学历">{{ selectedDelivery.education }}</el-descriptions-item>
          <el-descriptions-item label="工作经验">{{ selectedDelivery.workYears }}年</el-descriptions-item>
          <el-descriptions-item label="邮箱">{{ selectedDelivery.email }}</el-descriptions-item>
          <el-descriptions-item label="当前状态">
            <el-tag :type="getStatusType(selectedDelivery.status)">{{ getStatusText(selectedDelivery.status) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="AI面试">
            <el-tag v-if="selectedDelivery.allowAIInterview" type="success">
              <el-icon><VideoCamera /></el-icon> 已开放
            </el-tag>
            <el-tag v-else type="info">未开放</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="更新时间" :span="2">
            {{ selectedDelivery.updateTime ? formatDate(selectedDelivery.updateTime) : '-' }}
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>

    <el-dialog title="修改投递信息" v-model="showEdit" width="500px">
      <div v-if="editingDelivery" class="edit-content">
        <el-form :model="editForm" label-width="100px">
          <el-form-item label="姓名" required>
            <el-input v-model="editForm.candidateName" placeholder="请输入姓名" />
          </el-form-item>
          <el-form-item label="手机号" required>
            <el-input v-model="editForm.phone" placeholder="请输入手机号" />
          </el-form-item>
          <el-form-item label="邮箱">
            <el-input v-model="editForm.email" placeholder="请输入邮箱" />
          </el-form-item>
          <el-form-item label="学历" required>
            <el-select v-model="editForm.education" placeholder="请选择学历">
              <el-option label="高中" value="高中" />
              <el-option label="大专" value="大专" />
              <el-option label="本科" value="本科" />
              <el-option label="硕士" value="硕士" />
              <el-option label="博士" value="博士" />
            </el-select>
          </el-form-item>
          <el-form-item label="工作经验(年)" required>
            <el-input type="number" v-model="editForm.workYears" placeholder="请输入工作年限" />
          </el-form-item>
        </el-form>
      </div>
      <template #footer>
        <el-button @click="showEdit = false">取消</el-button>
        <el-button type="primary" @click="submitEdit">保存修改</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useResumeStore } from '@/stores/resume'
import { updateDeliveryInfo } from '@/api/delivery'
import { getMyAISessions } from '@/api/interview-ai'
import { VideoCamera, Clock, VideoPlay, Bell } from '@element-plus/icons-vue'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'

const router = useRouter()
const resumeStore = useResumeStore()
const loading = computed(() => resumeStore.loading)
const deliveries = computed(() => resumeStore.deliveries)

// 已完成AI面试的 deliveryId 集合（从 localStorage 读取，避免完成后继续显示邀请）
const getCompletedAIDeliveryIds = (): Set<number> => {
  try {
    const raw = localStorage.getItem('completedAIInterviewDeliveries')
    return new Set(raw ? JSON.parse(raw) : [])
  } catch {
    return new Set()
  }
}
const completedAIDeliveryIds = ref<Set<number>>(getCompletedAIDeliveryIds())

// AI面试邀请列表（投递中已开放AI面试且未完成的记录）
const aiInterviewInvitations = computed(() => {
  return deliveries.value.filter(d => d.allowAIInterview && !completedAIDeliveryIds.value.has(d.deliveryId))
})

const activeTab = ref('deliveries')

const showDetail = ref(false)
const selectedDelivery = ref<any>(null)

const showEdit = ref(false)
const editingDelivery = ref<any>(null)
const editForm = reactive({
  candidateName: '',
  phone: '',
  email: '',
  education: '',
  workYears: 0
})

onMounted(() => {
  resumeStore.fetchMyDeliveries()
})

// AI面试记录
const aiSessions = ref<any[]>([])
const aiSessionsLoading = ref(false)

const fetchAISessions = async () => {
  aiSessionsLoading.value = true
  try {
    const res = await getMyAISessions()
    if (res.data?.code === 200) {
      aiSessions.value = res.data.data || []
    }
  } catch {
    aiSessions.value = []
  } finally {
    aiSessionsLoading.value = false
  }
}

// 切换到AI面试记录tab时加载数据
watch(activeTab, (val) => {
  if (val === 'ai-interviews' && aiSessions.value.length === 0) {
    fetchAISessions()
  }
})

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')

const formatDeadline = (date: string | null | undefined) => {
  if (!date) return ''
  return dayjs(date).format('YYYY-MM-DD HH:mm')
}

const isDeadlinePassed = (deadline: string | null | undefined) => {
  if (!deadline) return false
  return new Date() > new Date(deadline)
}

const getSessionStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  const map: Record<number, 'primary' | 'success' | 'warning' | 'info' | 'danger'> = { 0: 'info', 1: 'warning', 2: 'success', 3: 'danger' }
  return map[status] || 'info'
}

const getSessionStatusText = (status: number) => {
  const map: Record<number, string> = { 0: '未开始', 1: '进行中', 2: '已完成', 3: '已中断' }
  return map[status] || '未知'
}

const formatDuration = (seconds: number) => {
  const min = Math.floor(seconds / 60)
  const sec = seconds % 60
  return min > 0 ? `${min}分${sec}秒` : `${sec}秒`
}

const getScoreClass = (score: number) => {
  if (score >= 80) return 'score-high'
  if (score >= 60) return 'score-medium'
  return 'score-low'
}

const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  const types: Record<number, 'primary' | 'success' | 'warning' | 'info' | 'danger'> = {
    0: 'info',
    1: 'info',
    2: 'warning',
    3: 'primary',
    4: 'success',
    5: 'danger'
  }
  return types[status] || 'info'
}

const getStatusText = (status: number) => {
  const texts = ['待查看', '已查看', '面试中', '实习中', '正式入职', '已淘汰']
  return texts[status] || '未知'
}

const viewDetail = (row: any) => {
  selectedDelivery.value = row
  showDetail.value = true
}

const editDetail = (row: any) => {
  editingDelivery.value = row
  editForm.candidateName = row.candidateName || ''
  editForm.phone = row.phone || ''
  editForm.email = row.email || ''
  editForm.education = row.education || ''
  editForm.workYears = row.workYears || 0
  showEdit.value = true
}

const submitEdit = async () => {
  try {
    await updateDeliveryInfo(editingDelivery.value.deliveryId, {
      jobId: editingDelivery.value.jobId,
      candidateName: editForm.candidateName,
      phone: editForm.phone,
      email: editForm.email,
      education: editForm.education,
      workYears: editForm.workYears,
      resumeUrl: ''
    })
    
    await resumeStore.fetchMyDeliveries()
    showEdit.value = false
    ElMessage.success('信息更新成功')
  } catch (error) {
    ElMessage.error('更新失败，请稍后重试')
  }
}

// 开始AI面试
const startAIInterview = (delivery: any) => {
  const userId = localStorage.getItem('userId') || localStorage.getItem('candidateId') || ''
  if (!userId) {
    ElMessage.warning('无法获取用户信息，请重新登录')
    router.push('/login')
    return
  }
  if (!delivery.jobId || !delivery.deliveryId) {
    ElMessage.warning('投递信息不完整，请联系HR')
    return
  }
  // 记录即将参加（进入面试即视为"已处理"，完成后自动隐藏邀请）
  const ids = getCompletedAIDeliveryIds()
  ids.add(delivery.deliveryId)
  localStorage.setItem('completedAIInterviewDeliveries', JSON.stringify([...ids]))
  completedAIDeliveryIds.value = ids

  router.push({
    name: 'AIInterview',
    params: {
      jobId: String(delivery.jobId),
      deliveryId: String(delivery.deliveryId),
      candidateId: String(userId)
    }
  })
}
</script>

<style scoped lang="scss">
.my-deliveries-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.notify-banner {
  margin-bottom: 16px;
  border-radius: 8px;
}

.page-title {
  margin-bottom: 20px;
  color: var(--color-primary);
}

// AI面试邀请卡片样式
.ai-invitation-card {
  margin-bottom: 20px;
  border: 2px solid var(--color-success);
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.06) 0%, var(--color-surface) 100%);
  
  :deep(.el-card__header) {
    background: linear-gradient(135deg, var(--color-success) 0%, #34D399 100%);
    color: #fff;
    padding: 12px 20px;
  }
  
  .ai-invitation-header {
    display: flex;
    align-items: center;
    gap: 10px;
    
    .invitation-title {
      font-size: 16px;
      font-weight: 600;
      flex: 1;
    }
  }
  
  .ai-invitation-list {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }
  
  .ai-invitation-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 16px;
    background: var(--color-surface);
    border-radius: 8px;
    border: 1px solid var(--color-border);
    
    .invitation-info {
      flex: 1;
      
      h4 {
        margin: 0 0 8px;
        color: var(--color-text);
        font-size: 16px;
      }
      
      .deadline {
        margin: 4px 0 0;
        font-size: 14px;
        color: var(--color-text-secondary);
        display: flex;
        align-items: center;
        gap: 4px;
        
        &.warning {
          color: var(--color-danger);
        }
      }
    }
    
    .invitation-actions {
      .el-button {
        padding: 12px 24px;
        font-size: 15px;
        
        .el-icon {
          margin-right: 6px;
        }
      }
    }
  }
}

// AI面试记录样式
.ai-history-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  
  .ai-history-card {
    .ai-history-content {
      display: flex;
      justify-content: space-between;
      align-items: center;
      
      .history-info {
        h4 {
          margin: 0 0 8px;
          color: var(--color-text);
        }
        
        p {
          margin: 4px 0;
          font-size: 14px;
          color: var(--color-text-secondary);
        }
      }

      .history-status {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 8px;
      }

      .score-display {
        display: flex;
        align-items: center;
        gap: 6px;

        .score-label {
          font-size: 13px;
          color: var(--color-text-secondary);
        }

        .score-value {
          font-size: 24px;
          font-weight: 700;

          &.score-high { color: var(--color-success); }
          &.score-medium { color: #E6A23C; }
          &.score-low { color: var(--color-danger); }
        }
      }
    }
  }
}
</style>
