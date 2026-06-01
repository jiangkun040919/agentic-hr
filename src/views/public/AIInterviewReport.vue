<template>
  <div class="interview-report-page">
    <!-- 顶部导航 -->
    <div class="report-header">
      <div class="header-left">
        <el-icon class="back-icon" @click="router.push('/my/deliveries')"><ArrowLeft /></el-icon>
        <div class="header-title">
          <span class="title-main">AI面试报告</span>
          <span class="title-sub">{{ jobTitle }}</span>
        </div>
      </div>
      <div class="header-right">
        <el-tag :type="statusType" size="large">{{ statusText }}</el-tag>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-wrapper">
      <el-skeleton :rows="8" animated />
    </div>

    <!-- 内容区 -->
    <div v-else-if="reportData" class="report-content">
      
      <!-- 完成提示横幅 -->
      <el-alert
        title="您已完成本次AI面试，感谢您的参与！HR将尽快查阅您的面试结果，请耐心等候通知。"
        type="success"
        :closable="false"
        show-icon
        class="completion-alert"
      />

      <!-- 顶部信息卡 -->
      <div class="info-cards">
        <el-card class="info-card" shadow="never">
          <div class="info-card-inner">
            <div class="score-ring">
              <el-progress
                type="circle"
                :percentage="reportData.totalScore || 0"
                :color="getScoreColor(reportData.totalScore || 0)"
                :width="110"
                :stroke-width="8"
                :show-text="false"
              />
              <div class="ring-text">
                <span class="ring-num" :style="{ color: getScoreColor(reportData.totalScore || 0) }">
                  {{ reportData.totalScore || '--' }}
                </span>
                <span class="ring-label">综合评分</span>
              </div>
            </div>
            <div class="info-stats">
              <div class="stat-item">
                <el-icon><Clock /></el-icon>
                <span class="stat-label">面试时长</span>
                <span class="stat-value">{{ formatDuration(reportData.totalDuration) }}</span>
              </div>
              <div class="stat-item">
                <el-icon><ChatDotRound /></el-icon>
                <span class="stat-label">问答轮数</span>
                <span class="stat-value">{{ questionCount }} 轮</span>
              </div>
              <div class="stat-item">
                <el-icon><Calendar /></el-icon>
                <span class="stat-label">面试时间</span>
                <span class="stat-value">{{ formatDate(reportData.startTime) }}</span>
              </div>
            </div>
          </div>
        </el-card>

        <!-- 维度评分 -->
        <el-card class="score-card" shadow="never" v-if="scoresData">
          <template #header>
            <div class="card-section-title">
              <el-icon><DataAnalysis /></el-icon>
              <span>能力维度评分</span>
            </div>
          </template>
          <div class="score-dimensions">
            <div class="score-dim-item" v-for="(val, key) in scoresData" :key="key">
              <div class="dim-header">
                <span class="dim-name">{{ scoreLabels[key] || key }}</span>
                <span class="dim-val" :style="{ color: getScoreColor(val) }">{{ val }}分</span>
              </div>
              <el-progress
                :percentage="val"
                :color="getScoreColor(val)"
                :stroke-width="10"
                :show-text="false"
                class="dim-bar"
              />
            </div>
          </div>
        </el-card>
      </div>

      <!-- 面试对话记录 -->
      <el-card class="chat-record-card" shadow="never">
        <template #header>
          <div class="card-section-title">
            <el-icon><Comment /></el-icon>
            <span>面试对话记录</span>
            <span class="record-count">共 {{ messages.length }} 条</span>
          </div>
        </template>
        
        <div class="chat-timeline">
          <div
            v-for="(msg, i) in messages"
            :key="i"
            :class="['timeline-item', msg.role === 'ai' ? 'ai-item' : 'candidate-item']"
          >
            <div class="tl-avatar">
              <el-icon v-if="msg.role === 'ai'"><Service /></el-icon>
              <el-icon v-else><UserFilled /></el-icon>
            </div>
            <div class="tl-content">
              <div class="tl-meta">
                <span class="tl-role">{{ msg.role === 'ai' ? 'AI面试官' : '我的回答' }}</span>
                <el-tag
                  v-if="msg.messageType === 'evaluation'"
                  type="success"
                  size="small"
                  style="margin-left: 8px"
                >综合评价</el-tag>
                <span class="tl-time">{{ formatTime(msg.createdAt) }}</span>
              </div>
              <div :class="['tl-bubble', msg.role === 'ai' ? 'ai-bubble' : 'candidate-bubble']">
                {{ msg.content }}
              </div>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 底部操作 -->
      <div class="report-footer">
        <el-button size="large" @click="router.push('/my/deliveries')">
          <el-icon><Back /></el-icon>
          返回我的投递
        </el-button>
        <el-button size="large" type="primary" @click="router.push('/jobs')">
          继续浏览岗位
          <el-icon class="el-icon--right"><Position /></el-icon>
        </el-button>
      </div>
    </div>

    <!-- 无数据 -->
    <div v-else class="no-data">
      <el-empty description="暂无面试数据">
        <el-button type="primary" @click="router.push('/my/deliveries')">返回我的投递</el-button>
      </el-empty>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft, Clock, ChatDotRound, Calendar,
  DataAnalysis, Comment, Service, UserFilled, Back, Position
} from '@element-plus/icons-vue'
import { getAIInterviewResult, getAISessionStatus } from '@/api/interview-ai'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const reportData = ref<any>(null)
const messages = ref<any[]>([])

const jobTitle = computed(() => reportData.value?.jobTitle || '未知岗位')

const statusType = computed(() => {
  const s = reportData.value?.status
  if (s === 2 || s === 3) return 'success'
  if (s === 1) return 'warning'
  return 'info'
})

const statusText = computed(() => {
  const s = reportData.value?.status
  if (s === 2) return '已完成'
  if (s === 3) return '已评估'
  if (s === 1) return '进行中'
  return '未知'
})

const questionCount = computed(() => {
  return messages.value.filter(m => m.role === 'ai' && m.messageType === 'question').length
})

const scoresData = computed(() => {
  if (!reportData.value?.scoresJson) return null
  try { return JSON.parse(reportData.value.scoresJson) } catch { return null }
})

const scoreLabels: Record<string, string> = {
  professional: '专业能力',
  communication: '沟通表达',
  problemSolving: '问题解决',
  cultureFit: '文化适配'
}

onMounted(async () => {
  const sessionId = Number(route.params.sessionId)
  if (!sessionId) {
    loading.value = false
    return
  }
  // 分别获取状态和结果，各自独立 try-catch
  try {
    const statusRes = await getAISessionStatus(sessionId)
    reportData.value = statusRes || {}
  } catch (e) {
    console.error('加载面试状态失败', e)
  }

  try {
    const resultRes = await getAIInterviewResult(sessionId)
    if (resultRes) {
      messages.value = resultRes.messages || []
      // 补充 scoresJson / totalScore（result 接口可能更完整）
      if (!reportData.value.scoresJson && resultRes.scoresJson) {
        reportData.value.scoresJson = resultRes.scoresJson
      }
      if (!reportData.value.totalScore && resultRes.totalScore) {
        reportData.value.totalScore = resultRes.totalScore
      }
      if (!reportData.value.startTime && resultRes.startTime) {
        reportData.value.startTime = resultRes.startTime
      }
      if (!reportData.value.jobTitle && resultRes.jobTitle) {
        reportData.value.jobTitle = resultRes.jobTitle
      }
    }
  } catch (e) {
    console.error('加载面试结果失败', e)
  } finally {
    loading.value = false
  }
})

const getScoreColor = (score: number) => {
  if (score >= 80) return 'var(--color-success)'
  if (score >= 60) return '#E6A23C'
  return 'var(--color-danger)'
}

const formatDate = (d: string) => d ? dayjs(d).format('YYYY-MM-DD HH:mm') : '--'
const formatTime = (d: string) => d ? dayjs(d).format('HH:mm') : ''
const formatDuration = (seconds: number) => {
  if (!seconds) return '--'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}分${String(s).padStart(2, '0')}秒`
}
</script>

<style scoped lang="scss">
.interview-report-page {
  min-height: 100vh;
  background: var(--color-bg);
  display: flex;
  flex-direction: column;
}

.report-header {
  background: var(--gradient-primary);
  color: #fff;
  padding: 16px 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 2px 16px rgba(255, 107, 107, 0.25);
  flex-shrink: 0;

  .header-left {
    display: flex;
    align-items: center;
    gap: 16px;

    .back-icon {
      font-size: 22px;
      cursor: pointer;
      opacity: 0.85;
      transition: opacity 0.2s;
      &:hover { opacity: 1; }
    }

    .header-title {
      display: flex;
      flex-direction: column;
      gap: 2px;

      .title-main {
        font-size: 20px;
        font-weight: 700;
      }

      .title-sub {
        font-size: 13px;
        opacity: 0.8;
      }
    }
  }
}

.loading-wrapper {
  padding: 40px 32px;
  max-width: 900px;
  margin: 0 auto;
  width: 100%;
}

.report-content {
  max-width: 900px;
  margin: 0 auto;
  width: 100%;
  padding: 24px 32px 48px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.completion-alert {
  border-radius: 12px;
  :deep(.el-alert__title) {
    font-size: 15px;
  }
}

.info-cards {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;

  @media (max-width: 700px) {
    grid-template-columns: 1fr;
  }

  .info-card {
    border-radius: 12px;

    .info-card-inner {
      display: flex;
      align-items: center;
      gap: 24px;

      .score-ring {
        position: relative;
        flex-shrink: 0;
        width: 110px;
        height: 110px;

        .ring-text {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          text-align: center;

          .ring-num {
            font-size: 28px;
            font-weight: 700;
            display: block;
            line-height: 1;
          }

          .ring-label {
            font-size: 11px;
            color: var(--color-text-secondary);
            margin-top: 4px;
            display: block;
          }
        }
      }

      .info-stats {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 12px;

        .stat-item {
          display: flex;
          align-items: center;
          gap: 8px;
          font-size: 14px;

          .el-icon {
            color: var(--color-primary);
            flex-shrink: 0;
          }

          .stat-label {
            color: var(--color-text-secondary);
            width: 60px;
            flex-shrink: 0;
          }

          .stat-value {
            color: var(--color-text);
            font-weight: 500;
          }
        }
      }
    }
  }

  .score-card {
    border-radius: 12px;

    .score-dimensions {
      display: flex;
      flex-direction: column;
      gap: 14px;

      .score-dim-item {
        .dim-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 6px;

          .dim-name {
            font-size: 13px;
            color: var(--color-text-secondary);
          }

          .dim-val {
            font-size: 14px;
            font-weight: 600;
          }
        }

        .dim-bar {
          :deep(.el-progress-bar__outer) {
            border-radius: 8px;
          }
        }
      }
    }
  }
}

.card-section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-primary);

  .record-count {
    margin-left: 8px;
    font-size: 12px;
    color: var(--color-text-secondary);
    font-weight: normal;
  }
}

.chat-record-card {
  border-radius: 12px;
}

.chat-timeline {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 8px 0;

  .timeline-item {
    display: flex;
    gap: 14px;
    align-items: flex-start;

    &.candidate-item {
      flex-direction: row-reverse;

      .tl-meta {
        justify-content: flex-end;
      }

      .tl-content {
        align-items: flex-end;
      }
    }

    .tl-avatar {
      width: 38px;
      height: 38px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #fff;
      flex-shrink: 0;
      font-size: 18px;
    }

    &.ai-item .tl-avatar {
      background: var(--gradient-primary);
    }

    &.candidate-item .tl-avatar {
      background: linear-gradient(135deg, var(--color-accent), var(--color-secondary));
    }

    .tl-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 6px;
      max-width: 80%;

      .tl-meta {
        display: flex;
        align-items: center;
        gap: 8px;

        .tl-role {
          font-size: 13px;
          font-weight: 600;
          color: var(--color-text);
        }

        .tl-time {
          font-size: 12px;
          color: var(--color-text-muted);
          margin-left: auto;
        }
      }

      .tl-bubble {
        padding: 12px 16px;
        border-radius: 12px;
        font-size: 14px;
        line-height: 1.7;
        white-space: pre-wrap;
        word-break: break-word;
      }

      .ai-bubble {
        background: var(--color-bg);
        color: var(--color-text);
        border-top-left-radius: 4px;
      }

      .candidate-bubble {
        background: var(--gradient-primary);
        color: #fff;
        border-top-right-radius: 4px;
      }
    }
  }
}

.report-footer {
  display: flex;
  justify-content: center;
  gap: 16px;
  margin-top: 8px;
}

.no-data {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
}
</style>
