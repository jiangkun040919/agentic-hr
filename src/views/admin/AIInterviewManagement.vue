<template>
  <div class="ai-interview-mgmt">
    <div class="page-header">
      <h2>AI面试管理</h2>
      <div class="header-stats">
        <div class="stat-item">
          <span class="stat-value">{{ totalCount }}</span>
          <span class="stat-label">全部记录</span>
        </div>
        <div class="stat-item">
          <span class="stat-value active">{{ activeCount }}</span>
          <span class="stat-label">进行中</span>
        </div>
        <div class="stat-item">
          <span class="stat-value done">{{ doneCount }}</span>
          <span class="stat-label">已完成</span>
        </div>
        <div class="stat-item">
          <span class="stat-value avg">{{ avgScore || '--' }}</span>
          <span class="stat-label">平均评分</span>
        </div>
      </div>
    </div>

    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索候选人/岗位"
        clearable
        style="width: 240px"
        @change="fetchList"
      >
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-select v-model="statusFilter" placeholder="面试状态" clearable style="width: 130px" @change="fetchList">
        <el-option label="全部" :value="-1" />
        <el-option label="未开始" :value="0" />
        <el-option label="进行中" :value="1" />
        <el-option label="已完成" :value="2" />
        <el-option label="已中断" :value="3" />
      </el-select>
      <el-button type="primary" @click="fetchList">搜索</el-button>
      <el-button @click="handleReset">重置</el-button>
    </div>

    <el-card v-loading="loading">
      <el-table :data="sessions" stripe>
        <el-table-column label="ID" width="70">
          <template #default="{ $index }">
            {{ String((searchParams.page - 1) * searchParams.pageSize + $index + 1).padStart(3, '0') }}
          </template>
        </el-table-column>
        <el-table-column prop="candidateName" label="候选人" width="120" />
        <el-table-column prop="jobTitle" label="应聘岗位" min-width="160" />
        <el-table-column label="面试状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">{{ getStatusText(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="综合评分" width="110">
          <template #default="{ row }">
            <span v-if="row.totalScore" class="score-cell" :style="{ color: getScoreColor(row.totalScore) }">
              {{ row.totalScore }}分
            </span>
            <span v-else class="score-empty">--</span>
          </template>
        </el-table-column>
        <el-table-column label="面试时长" width="100">
          <template #default="{ row }">
            {{ row.totalDuration ? formatDuration(row.totalDuration) : '--' }}
          </template>
        </el-table-column>
        <el-table-column label="面试时间" width="160">
          <template #default="{ row }">
            {{ row.startTime ? formatDate(row.startTime) : '--' }}
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="160">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click="viewDetail(row)">查看详情</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="searchParams.page"
          v-model:page-size="searchParams.pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next"
          @change="fetchList"
        />
      </div>
    </el-card>

    <!-- 详情弹窗 -->
    <el-dialog
      v-model="detailVisible"
      title="AI面试详情"
      width="900px"
      destroy-on-close
    >
      <div v-if="currentSession" class="session-detail">
        <!-- 头部信息 -->
        <div class="detail-header-card">
          <div class="header-info">
            <div class="candidate-name">{{ currentSession.candidateName }}</div>
            <div class="job-title">{{ currentSession.jobTitle }}</div>
          </div>
          <div class="header-right">
            <el-tag :type="getStatusType(currentSession.status)" size="large">
              {{ getStatusText(currentSession.status) }}
            </el-tag>
            <div class="duration-info" v-if="currentSession.totalDuration">
              <span>时长：{{ formatDuration(currentSession.totalDuration) }}</span>
            </div>
          </div>
        </div>

        <!-- 评分区 -->
        <div v-if="currentSession.totalScore" class="score-section">
          <div class="score-ring-wrapper">
            <el-progress
              type="circle"
              :percentage="currentSession.totalScore"
              :color="getScoreColor(currentSession.totalScore)"
              :width="110"
              :show-text="false"
            />
            <div class="ring-center">
              <span class="ring-score" :style="{ color: getScoreColor(currentSession.totalScore) }">
                {{ currentSession.totalScore }}
              </span>
              <span class="ring-label">综合评分</span>
            </div>
          </div>
          <div class="score-bars" v-if="currentSession.scoresJson">
            <div class="bar-item" v-for="(val, key) in parsedScores" :key="key">
              <span class="bar-label">{{ scoreLabels[key] || key }}</span>
              <el-progress :percentage="val" :color="getScoreColor(val)" :show-text="false" />
              <span class="bar-value">{{ val }}分</span>
            </div>
          </div>
        </div>

        <!-- 对话记录 -->
        <div class="conversation-section">
          <h4>对话记录</h4>
          <div class="messages-list">
            <template v-if="detailMessages.length > 0">
              <div
                v-for="(msg, i) in detailMessages"
                :key="i"
                :class="['msg-item', msg.role]"
              >
                <div v-if="msg.role === 'ai'" class="ai-avatar">
                  <el-icon><Service /></el-icon>
                </div>
                <div v-else class="candidate-avatar">
                  <el-icon><UserFilled /></el-icon>
                </div>
                <div class="msg-bubble">
                  <div class="msg-content">{{ msg.content }}</div>
                  <div class="msg-time">{{ formatTime(msg.createdAt) }}</div>
                </div>
              </div>
            </template>
            <div v-else class="empty-msgs">
              <el-empty description="暂无对话记录" :image-size="60" />
            </div>
          </div>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, reactive, onMounted, onActivated } from 'vue'
import { Search, Service, UserFilled } from '@element-plus/icons-vue'
import { getAIInterviewList, getAIInterviewMessages } from '@/api/interview-ai'
import dayjs from 'dayjs'
const loading = ref(false)
const sessions = ref<any[]>([])
const total = ref(0)
const keyword = ref('')
const statusFilter = ref(-1)

const searchParams = reactive({
  page: 1,
  pageSize: 10
})

// 详情
const detailVisible = ref(false)
const currentSession = ref<any>(null)
const detailMessages = ref<any[]>([])

const totalCount = computed(() => total.value)
const activeCount = computed(() => sessions.value.filter(s => s.status === 1).length)
const doneCount = computed(() => sessions.value.filter(s => s.status === 2).length)
const avgScore = computed(() => {
  const scores = sessions.value.filter(s => s.totalScore).map(s => s.totalScore)
  if (scores.length === 0) return null
  return Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
})

const parsedScores = computed(() => {
  if (!currentSession.value?.scoresJson) return {}
  try {
    return JSON.parse(currentSession.value.scoresJson)
  } catch {
    return {}
  }
})

const scoreLabels: Record<string, string> = {
  professional: '专业能力',
  communication: '沟通表达',
  problemSolving: '问题解决',
  cultureFit: '文化适配'
}

onMounted(() => {
  fetchList()
})

// 从其他页面切回时自动刷新
onActivated(() => {
  fetchList()
})

const fetchList = async () => {
  loading.value = true
  try {
    // 传递 status 参数给后端（后端若支持则服务端过滤；不支持则忽略，客户端兜底）
    const apiParams: any = {
      page: searchParams.page,
      pageSize: searchParams.pageSize,
      keyword: keyword.value || undefined
    }
    if (statusFilter.value >= 0) {
      apiParams.status = statusFilter.value
    }

    const res = await getAIInterviewList(apiParams)

    // 响应拦截器已解包，兼容多种返回格式
    const data = Array.isArray(res) ? res : (res?.data || res?.data?.data || res || [])
    const list = Array.isArray(data) ? data : []

    if (statusFilter.value >= 0) {
      // 客户端兜底过滤，但 total 保持原始总数（避免分页错乱）
      sessions.value = list.filter((s: any) => s.status === statusFilter.value)
      total.value = list.length
    } else {
      sessions.value = list
      total.value = list.length
    }
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

const handleReset = () => {
  keyword.value = ''
  statusFilter.value = -1
  searchParams.page = 1
  fetchList()
}

const viewDetail = async (row: any) => {
  currentSession.value = { ...row }
  detailMessages.value = []
  detailVisible.value = true

  try {
    const res = await getAIInterviewMessages(row.sessionId)
    // 响应拦截器已解包：res 直接就是 data 对象
    if (res && res.sessionId) {
      currentSession.value.totalScore = res.totalScore
      currentSession.value.scoresJson = res.scoresJson
      currentSession.value.totalDuration = res.totalDuration
      detailMessages.value = res.messages || []
    }
  } catch (e) {
    console.error(e)
  }
}

const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' | undefined => {
  const types: ('primary' | 'success' | 'warning' | 'info' | 'danger')[] = ['info', 'info', 'warning', 'success', 'danger']
  return types[status] || 'info'
}

const getStatusText = (status: number) => {
  const texts = ['', '未开始', '进行中', '已完成', '已中断']
  return texts[status] || '未知'
}

const getScoreColor = (score: number) => {
  if (score >= 80) return 'var(--color-success)'
  if (score >= 60) return '#E6A23C'
  return 'var(--color-danger)'
}

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')
const formatTime = (date: string) => dayjs(date).format('HH:mm')
const formatDuration = (seconds: number) => {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}分${s}秒`
}
</script>

<style scoped lang="scss">
.ai-interview-mgmt {
  .page-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 20px;

    h2 {
      font-size: 20px;
      color: var(--color-text);
      margin: 0;
    }

    .header-stats {
      display: flex;
      gap: 24px;

      .stat-item {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 4px;

        .stat-value {
          font-size: 24px;
          font-weight: 700;
          color: var(--color-primary);

          &.active { color: var(--color-warning); }
          &.done { color: var(--color-success); }
          &.avg { color: var(--color-accent); }
        }

        .stat-label {
          font-size: 12px;
          color: var(--color-text-secondary);
        }
      }
    }
  }

  .toolbar {
    display: flex;
    gap: 12px;
    margin-bottom: 16px;
    flex-wrap: wrap;
  }

  .pagination {
    margin-top: 20px;
    display: flex;
    justify-content: center;
  }

  :deep(.el-table) {
    .score-cell {
      font-weight: 700;
      font-size: 16px;
    }
    .score-empty {
      color: var(--color-text-muted);
    }
  }
}

.session-detail {
  .detail-header-card {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: var(--gradient-primary);
    border-radius: 12px;
    padding: 20px 24px;
    color: #fff;
    margin-bottom: 20px;

    .candidate-name {
      font-size: 20px;
      font-weight: 700;
      margin-bottom: 4px;
    }

    .job-title {
      font-size: 14px;
      opacity: 0.8;
    }

    .header-right {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 8px;

      .duration-info {
        font-size: 13px;
        opacity: 0.8;
      }
    }
  }

  .score-section {
    display: flex;
    gap: 32px;
    align-items: center;
    padding: 20px;
    background: var(--color-bg);
    border-radius: 12px;
    margin-bottom: 20px;

    .score-ring-wrapper {
      position: relative;
      width: 110px;
      height: 110px;
      flex-shrink: 0;

      .ring-center {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        text-align: center;

        .ring-score {
          font-size: 28px;
          font-weight: 700;
          display: block;
        }

        .ring-label {
          font-size: 12px;
          color: var(--color-text-secondary);
        }
      }
    }

    .score-bars {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 10px;

      .bar-item {
        display: flex;
        align-items: center;
        gap: 10px;

        .bar-label {
          width: 70px;
          font-size: 13px;
          color: var(--color-text-secondary);
          flex-shrink: 0;
        }

        :deep(.el-progress) { flex: 1; }

        .bar-value {
          width: 45px;
          font-size: 13px;
          font-weight: 600;
          color: var(--color-text);
          text-align: right;
          flex-shrink: 0;
        }
      }
    }
  }

  .conversation-section {
    h4 {
      font-size: 14px;
      color: var(--color-text-secondary);
      margin: 0 0 12px;
    }

    .messages-list {
      max-height: 400px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding-right: 8px;

      .msg-item {
        display: flex;
        gap: 10px;
        align-items: flex-start;

        &.candidate {
          flex-direction: row-reverse;

          .msg-bubble {
            background: var(--gradient-primary);
            color: #fff;
          }

          .msg-time {
            color: rgba(255,255,255,0.5);
          }
        }

        .ai-avatar, .candidate-avatar {
          width: 36px;
          height: 36px;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          color: #fff;
          flex-shrink: 0;
        }

        .ai-avatar { background: var(--color-primary); }
        .candidate-avatar { background: var(--color-accent); }

        .msg-bubble {
          max-width: 70%;
          padding: 12px 16px;
          border-radius: 12px;
          background: var(--color-bg);

          .msg-content {
            font-size: 14px;
            line-height: 1.6;
            white-space: pre-wrap;
          }

          .msg-time {
            font-size: 11px;
            color: var(--color-text-muted);
            margin-top: 4px;
            display: block;
          }
        }
      }

      .empty-msgs {
        padding: 40px 0;
      }
    }
  }
}
</style>
