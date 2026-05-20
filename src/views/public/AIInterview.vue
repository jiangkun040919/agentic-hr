<template>
  <div class="ai-interview-page">
    <!-- 顶部标题栏 -->
    <div class="interview-header">
      <div class="header-left">
        <el-icon class="back-icon" @click="handleBack"><ArrowLeft /></el-icon>
        <div class="interview-title">
          <span class="job-name">{{ jobTitle }}</span>
          <span class="interview-mode">AI智能面试</span>
        </div>
      </div>
      <div class="header-right">
        <div class="round-indicator" v-if="interviewStarted">
          <span class="round-label">第 {{ currentRound }} 轮</span>
          <el-progress :percentage="roundPercent" :stroke-width="4" :show-text="false" color="var(--color-success)" />
        </div>
        <!-- TTS播放状态 -->
        <div v-if="isSpeaking" class="tts-badge">
          <span class="tts-wave"><span></span><span></span><span></span><span></span></span>
          <span>AI语音播报中</span>
        </div>
        <el-tag :type="statusTagType" size="large">{{ statusText }}</el-tag>
      </div>
    </div>

    <div class="interview-body">
      <!-- 左侧：岗位信息面板 -->
      <div class="left-panel">
        <el-card class="job-info-card">
          <template #header>
            <div class="card-header-title">
              <el-icon><Briefcase /></el-icon>
              <span>岗位信息</span>
            </div>
          </template>
          <div class="job-detail-list">
            <div class="job-item">
              <span class="job-label">岗位名称</span>
              <span class="job-value">{{ jobTitle }}</span>
            </div>
            <div class="job-item">
              <span class="job-label">部门</span>
              <span class="job-value">{{ jobDept || '待定' }}</span>
            </div>
            <div class="job-item">
              <span class="job-label">工作地点</span>
              <span class="job-value">{{ jobLocation || '待定' }}</span>
            </div>
            <div class="job-item">
              <span class="job-label">薪资范围</span>
              <span class="job-value salary">{{ jobSalary || '面议' }}</span>
            </div>
          </div>
        </el-card>

        <!-- 面试指南 -->
        <el-card class="guide-card" shadow="never">
          <template #header>
            <div class="card-header-title">
              <el-icon><InfoFilled /></el-icon>
              <span>面试须知</span>
            </div>
          </template>
          <div class="guide-list">
            <div class="guide-item" v-for="(item, i) in guides" :key="i">
              <div class="guide-num">{{ i + 1 }}</div>
              <div class="guide-text">{{ item }}</div>
            </div>
          </div>
        </el-card>

        <!-- 面试状态 -->
        <el-card class="status-card" v-if="interviewStarted">
          <div class="status-grid">
            <div class="status-item">
              <span class="status-label">面试时长</span>
              <span class="status-value">{{ formatDuration(duration) }}</span>
            </div>
            <div class="status-item">
              <span class="status-label">已回答</span>
              <span class="status-value">{{ answeredCount }} 轮</span>
            </div>
            <div class="status-item" v-if="totalScore">
              <span class="status-label">综合评分</span>
              <span class="status-value score" :style="{ color: getScoreColor(totalScore) }">
                {{ totalScore }}分
              </span>
            </div>
          </div>
        </el-card>
      </div>

      <!-- 右侧：对话区 + 摄像头 -->
      <div class="right-panel-wrapper">
        <!-- 摄像头视频预览 + 行为分析 -->
        <div class="video-panel" v-if="interviewStarted && !isEnded">
          <div class="video-container">
            <video ref="videoRef" autoplay playsinline muted class="camera-video"></video>
            <div class="video-overlay" v-if="!cameraReady">
              <el-icon :size="32"><VideoCamera /></el-icon>
              <span>摄像头未开启</span>
            </div>
            <!-- 行为分析状态浮层 -->
            <div class="behavior-overlay" v-if="cameraReady && behaviorReady">
              <div class="behavior-tag" :class="behaviorState.posture">
                <span>{{ behaviorState.postureLabel }}</span>
              </div>
              <div class="behavior-tag" :class="behaviorState.attention">
                <el-icon :size="12"><View /></el-icon>
                <span>{{ behaviorState.attentionLabel }}</span>
              </div>
            </div>
          </div>
          <div class="video-info">
            <div class="video-label">
              <el-icon v-if="cameraReady" color="var(--color-success)"><VideoCamera /></el-icon>
              <span :style="{ color: cameraReady ? (behaviorReady ? 'var(--color-success)' : '#E6A23C') : 'var(--color-text-secondary)' }">
                {{ cameraReady ? (behaviorReady ? '行为分析已启动' : `模型加载中... ${modelLoadProgress}`) : '摄像头未开启（可选）' }}
              </span>
            </div>
            <!-- 实时状态面板 -->
            <div class="behavior-status-grid" v-if="behaviorReady">
              <div class="bs-item">
                <span class="bs-label">🧍 姿态</span>
                <span class="bs-value" :class="behaviorState.posture">{{ behaviorState.postureLabel }}</span>
              </div>
              <div class="bs-item">
                <span class="bs-label">👀 注意力</span>
                <span class="bs-value" :class="behaviorState.attention">{{ behaviorState.attentionLabel }}</span>
              </div>
              <div class="bs-item">
                <span class="bs-label">🖐️ 手势</span>
                <span class="bs-value">{{ behaviorState.gesture || '无' }}</span>
              </div>
              <div class="bs-item">
                <span class="bs-label">😐 表情</span>
                <span class="bs-value">{{ expressionLabel || '检测中...' }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="right-panel">
          <!-- 对话记录 -->
          <div class="chat-area" ref="chatAreaRef">
            <!-- 开始前欢迎页 -->
            <div class="welcome-message" v-if="!interviewStarted">
              <div class="welcome-avatar">
                <el-icon :size="48"><UserFilled /></el-icon>
              </div>
              <h2>欢迎参加AI智能面试</h2>
              <p>AI面试官将全程语音提问，请根据问题作答</p>
              <p style="font-size:13px;color:var(--color-text-muted);margin-top:-8px;">面试轮数由AI根据您的表现自行判断，结束后直接出分</p>
              <el-button type="primary" size="large" @click="handleStart" :loading="loading" class="start-btn">
                <el-icon><VideoPlay /></el-icon>
                开始面试
              </el-button>
            </div>

            <div v-else class="messages-container">
              <TransitionGroup name="message-fade">
                <div
                  v-for="(msg, i) in messages"
                  :key="i"
                  :class="['message-wrapper', msg.role]"
                >
                  <!-- AI消息 -->
                  <template v-if="msg.role === 'ai'">
                    <div class="ai-avatar" :class="{ speaking: isSpeaking && i === lastAiMsgIndex }">
                      <el-icon><Service /></el-icon>
                    </div>
                    <div class="message-bubble ai-bubble">
                      <div class="message-content">{{ msg.content }}</div>
                      <div class="message-meta">
                        <span class="message-time">{{ formatTime(msg.createdAt) }}</span>
                        <el-button
                          v-if="i === lastAiMsgIndex && msg.messageType === 'question'"
                          size="small" text
                          :type="isSpeaking ? 'danger' : 'primary'"
                          class="replay-btn"
                          @click="isSpeaking ? stopSpeaking() : speakText(msg.content)"
                        >
                          <el-icon><component :is="isSpeaking ? VideoPause : Microphone" /></el-icon>
                          {{ isSpeaking ? '停止' : '重播' }}
                        </el-button>
                      </div>
                    </div>
                  </template>

                  <!-- 候选人消息 -->
                  <template v-else>
                    <div class="message-bubble candidate-bubble">
                      <div class="message-content">{{ msg.content }}</div>
                      <div class="message-meta right">
                        <span class="message-time">{{ formatTime(msg.createdAt) }}</span>
                        <span v-if="msg.inputMode === 'voice'" class="voice-tag">🎙️ 语音</span>
                      </div>
                    </div>
                    <div class="candidate-avatar">
                      <el-icon><UserFilled /></el-icon>
                    </div>
                  </template>
                </div>
              </TransitionGroup>

              <!-- 加载中 -->
              <div v-if="loading" class="message-wrapper ai">
                <div class="ai-avatar">
                  <el-icon><Service /></el-icon>
                </div>
                <div class="message-bubble ai-bubble loading-bubble">
                  <div class="typing-indicator">
                    <span></span><span></span><span></span>
                  </div>
                </div>
              </div>

              <!-- 面试结束：直接出分，无多余文字 -->
              <div v-if="isEnded" class="interview-result-card">
                <div class="result-header">
                  <el-icon :size="32" color="var(--color-success)"><CircleCheck /></el-icon>
                  <h3>面试结束 — 您的评分</h3>
                </div>
                <div class="result-scores" v-if="scoresData">
                  <div class="score-ring-wrapper">
                    <el-progress
                      type="circle"
                      :percentage="totalScore || 0"
                      :color="getScoreColor(totalScore || 0)"
                      :width="100"
                      :show-text="false"
                    />
                    <div class="ring-center">
                      <span class="ring-score" :style="{ color: getScoreColor(totalScore || 0) }">
                        {{ totalScore || 0 }}
                      </span>
                      <span class="ring-label">综合评分</span>
                    </div>
                  </div>
                  <div class="score-bars">
                    <div class="score-bar-item" v-for="(item, k) in scoresData" :key="k">
                      <span class="bar-label">{{ scoreLabels[k] || k }}</span>
                      <el-progress :percentage="item" :color="getScoreColor(item)" :show-text="false" />
                      <span class="bar-value">{{ item }}分</span>
                    </div>
                  </div>
                </div>
                <div class="result-actions">
                  <el-button @click="router.push('/my/deliveries')">返回我的投递</el-button>
                  <el-button type="primary" @click="viewDetail">查看详细报告</el-button>
                </div>
              </div>
            </div>
          </div>

          <!-- 输入区 -->
          <div class="input-area" v-if="interviewStarted && !isEnded">
            <!-- 语音模式（默认开启） -->
            <div v-if="isVoiceMode" class="voice-input-area">
              <div class="voice-mode-header">
                <el-icon color="var(--color-primary)"><Microphone /></el-icon>
                <span>{{ currentRound === 1 ? '请语音进行自我介绍' : '语音回答模式' }}</span>
                <el-switch
                  v-model="autoListenMode"
                  size="small"
                  active-text="免提"
                  inactive-text="手动"
                  style="--el-switch-on-color: var(--color-success)"
                />
                <el-button size="small" text @click="isVoiceMode = false">切换文字输入</el-button>
              </div>

              <div class="voice-record-area">
                <div class="voice-circle" :class="{ active: isRecording, transcribing: isTranscribing }" @click="toggleVoiceRecord">
                  <el-icon :size="32"><component :is="isRecording ? VideoPause : Microphone" /></el-icon>
                  <div class="voice-circle-rings" v-if="isRecording">
                    <div class="ring ring1"></div>
                    <div class="ring ring2"></div>
                  </div>
                </div>
                <div class="voice-status">
                  <template v-if="isTranscribing">
                    <span class="status-transcribing">识别中...</span>
                  </template>
                  <template v-else-if="isRecording">
                    <span class="status-recording">录音中 {{ recordingSeconds }}s &nbsp; 最长60秒，说完后点击停止</span>
                  </template>
                  <template v-else>
                    <span class="status-idle">点击麦克风开始录音</span>
                  </template>
                </div>
              </div>
            </div>

            <!-- 文字模式 -->
            <div v-else class="text-input-area">
              <div class="input-wrapper">
                <el-input
                  v-model="currentAnswer"
                  type="textarea"
                  :rows="3"
                  placeholder="请输入你的回答..."
                  resize="none"
                  @keydown.ctrl.enter="handleSubmit"
                />
                <div class="input-actions">
                  <span class="hint-text">Ctrl+Enter 发送</span>
                  <div class="action-btns">
                    <el-tooltip content="切换语音输入" placement="top">
                      <el-button circle @click="switchToVoice">
                        <el-icon><Microphone /></el-icon>
                      </el-button>
                    </el-tooltip>
                    <el-button
                      type="primary"
                      :loading="loading"
                      :disabled="!currentAnswer.trim()"
                      @click="handleSubmit"
                    >
                      <el-icon v-if="!loading"><Promotion /></el-icon>
                      发送回答
                    </el-button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  ArrowLeft, Briefcase, InfoFilled, UserFilled,
  Service, VideoPlay, Promotion, CircleCheck, Microphone,
  VideoPause, VideoCamera, View
} from '@element-plus/icons-vue'
import {
  startAIInterview, submitAIAnswer,
  getAIInterviewResult, getAISessionStatus,
  speechToText, voiceStartInterview, voiceSubmitAnswer
} from '@/api/interview-ai'
import { getJobDetail } from '@/api/job'
import { analyzeExpression } from '@/api/face'
import {
  initBehaviorAnalysis, startDetection, stopDetection, dispose,
  setModelLoadCallback,
  type BehaviorState
} from '@/utils/behaviorAnalysis'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

// ── 基础数据 ──────────────────────────────────────────
const loading = ref(false)
const sessionId = ref<number | null>(null)
const deliveryId = ref<number>(0)
const candidateId = ref<number>(0)
const jobId = ref<number>(0)

const jobTitle = ref('')
const jobDept = ref('')
const jobLocation = ref('')
const jobSalary = ref('')

const messages = ref<any[]>([])
const currentAnswer = ref('')
const interviewStarted = ref(false)
const isEnded = ref(false)
const totalScore = ref<number | null>(null)
const scoresJson = ref<string | null>(null)

const elapsedSeconds = ref(0)
let timer: number | null = null

const guides = [
  '面试开始后AI会先让您做自我介绍',
  'AI面试官将根据您的回答自行判断面试轮数',
  '可语音或文字作答，语音模式支持实时识别',
  '面试结束后直接出分，无需等待',
  '保持网络畅通，避免刷新页面'
]

// ── 摄像头（getUserMedia）────────────────────────────
const videoRef = ref<HTMLVideoElement>()
const cameraReady = ref(false)
const behaviorReady = ref(false)  // 行为分析模型是否就绪
const modelLoadProgress = ref('')  // 模型加载进度文字

// 行为分析实时状态
const behaviorState = ref<BehaviorState>({
  posture: 'good', postureLabel: '未检测',
  attention: 'unknown', attentionLabel: '未检测到面部',
  gesture: null, confidence: 0
})

// 表情分析（腾讯云，低频调用）
const expressionLabel = ref('')
let expressionTimer: number | null = null

const startCamera = async () => {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 320, height: 240, facingMode: 'user' }, audio: false })
    if (videoRef.value) {
      videoRef.value.srcObject = stream
      cameraReady.value = true
      // 等视频就绪后启动行为分析
      videoRef.value.onloadedmetadata = async () => {
        // 注册模型加载进度回调（用 setModelLoadCallback 避免打包冻结问题）
        setModelLoadCallback((stage: string, current: number, total: number) => {
          modelLoadProgress.value = `${stage}(${current}/${total})`
        })
        const ready = await initBehaviorAnalysis()
        modelLoadProgress.value = ''
        if (ready) {
          behaviorReady.value = true
          startDetection(videoRef.value!, (state: BehaviorState) => {
            behaviorState.value = state
          })
          // 表情分析：每8秒截一次图调腾讯云
          startExpressionPolling()
        }
      }
    }
  } catch (e: any) {
    cameraReady.value = false
    if (e.name === 'NotAllowedError') {
      ElMessage.warning('摄像头权限被拒绝，面试将继续，但无视频和行为分析')
    }
  }
}

const stopCamera = () => {
  stopExpressionPolling()
  stopDetection()
  if (videoRef.value && videoRef.value.srcObject) {
    const stream = videoRef.value.srcObject as MediaStream
    stream.getTracks().forEach(t => t.stop())
    videoRef.value.srcObject = null
  }
  cameraReady.value = false
  behaviorReady.value = false
}

// ── 表情分析轮询（腾讯云 DetectFace）───────────────────
const captureFrame = (): string | null => {
  if (!videoRef.value || !cameraReady.value) return null
  const canvas = document.createElement('canvas')
  canvas.width = 200
  canvas.height = 150
  const ctx = canvas.getContext('2d')
  if (!ctx) return null
  ctx.drawImage(videoRef.value, 0, 0, 200, 150)
  return canvas.toDataURL('image/jpeg', 0.7).split(',')[1]
}

const doExpressionAnalysis = async () => {
  const base64 = captureFrame()
  if (!base64) return
  try {
    const res = await analyzeExpression(base64)
    if (res?.data?.expression) {
      expressionLabel.value = res.data.expression as string
    }
  } catch {
    // 静默失败，表情分析是辅助功能
  }
}

const startExpressionPolling = () => {
  stopExpressionPolling()
  // 首次立即执行
  doExpressionAnalysis()
  expressionTimer = window.setInterval(doExpressionAnalysis, 8000)
}

const stopExpressionPolling = () => {
  if (expressionTimer) { clearInterval(expressionTimer); expressionTimer = null }
}

// ── 语音模式（双通道：浏览器 STT + 云端 MiniMax STT） ──────────
const isVoiceMode = ref(true)            // 默认开启语音模式
const autoListenMode = ref(true)         // 免提模式：AI 说完自动开始听
const voiceIntroText = ref('')           // 语音识别结果（临时显示用）
const isRecording = ref(false)
const isTranscribing = ref(false)
const recordingSeconds = ref(0)
const useCloudSTT = ref(false)           // 是否使用云端 MiniMax STT（浏览器 STT 不可用时自动切换）
let speechRecognition: any = null
let mediaRecorder: MediaRecorder | null = null
let audioChunks: Blob[] = []
let recordingTimer: number | null = null

// ── TTS 语音播报 ──────────────────────────────────────
const isSpeaking = ref(false)
let currentAudio: HTMLAudioElement | null = null

// ── 计算属性 ──────────────────────────────────────────
const currentRound = computed(() => {
  return messages.value.filter(m => m.role === 'ai' && m.messageType === 'question').length
})

const answeredCount = computed(() => {
  return messages.value.filter(m => m.role === 'candidate').length
})

// 动态进度条：基于固定10轮比例显示（不超过100%）
const roundPercent = computed(() => {
  return Math.min(Math.round((currentRound.value / 10) * 100), 100)
})

const statusTagType = computed(() => {
  if (isEnded.value) return 'success'
  if (interviewStarted.value) return 'warning'
  return 'info'
})

const statusText = computed(() => {
  if (isEnded.value) return '已完成'
  if (interviewStarted.value) return '进行中'
  return '未开始'
})

const duration = computed(() => elapsedSeconds.value)

const scoresData = computed(() => {
  if (!scoresJson.value) return null
  try { return JSON.parse(scoresJson.value) } catch { return null }
})

const scoreLabels: Record<string, string> = {
  professional: '专业能力',
  communication: '沟通表达',
  problemSolving: '问题解决',
  cultureFit: '文化适配'
}

// 最后一条 AI 消息的索引（用于显示重播按钮）
const lastAiMsgIndex = computed(() => {
  for (let i = messages.value.length - 1; i >= 0; i--) {
    if (messages.value[i].role === 'ai') return i
  }
  return -1
})

// ── 生命周期 ──────────────────────────────────────────
onMounted(async () => {
  jobId.value = Number(route.params.jobId) || Number(route.query.jobId) || 0
  deliveryId.value = Number(route.params.deliveryId) || Number(route.query.deliveryId) || 0
  candidateId.value = Number(route.params.candidateId) || Number(route.query.candidateId) || 0

  if (route.query.sessionId) {
    sessionId.value = Number(route.query.sessionId)
    await loadSession()
  }

  if (jobId.value) {
    try {
      const jobRes = await getJobDetail(jobId.value)
      if (jobRes) {
        jobTitle.value = jobRes.title || ''
        jobDept.value = jobRes.dept || ''
        jobLocation.value = jobRes.location || ''
        if (jobRes.salaryMin && jobRes.salaryMax) {
          jobSalary.value = `${jobRes.salaryMin}-${jobRes.salaryMax}K`
        }
      }
    } catch (e) {
      console.error('获取岗位信息失败', e)
    }
  }
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
  if (recordingTimer) clearInterval(recordingTimer)
  if (speechRecognition) speechRecognition.stop()
  stopSpeaking()
  stopCamera()
  dispose()
})

// ── TTS：AI提问后自动语音播报 ─────────────────────────
const speakText = async (text: string) => {
  stopSpeaking()
  isSpeaking.value = true

  try {
    const res = await fetch('/api/ai-interview/text-to-speech', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${localStorage.getItem('token') || ''}`
      },
      body: JSON.stringify({ text, voiceId: 'male-qn-qingse' })
    })

    if (res.ok) {
      const data = await res.json()
      if (data.code === 200 && data.data) {
        let audioSrc = ''

        if (data.data.audioHex) {
          const hexStr: string = data.data.audioHex
          const bytes = new Uint8Array(hexStr.match(/.{1,2}/g)!.map((b: string) => parseInt(b, 16)))
          const blob = new Blob([bytes], { type: 'audio/mp3' })
          audioSrc = URL.createObjectURL(blob)
        } else if (data.data.audioBase64) {
          const b64 = data.data.audioBase64
          audioSrc = `data:audio/mp3;base64,${b64}`
        } else if (data.data.audioUrl) {
          audioSrc = data.data.audioUrl
        }

        if (audioSrc) {
          currentAudio = new Audio(audioSrc)
          currentAudio.onended = () => {
            isSpeaking.value = false
            if (audioSrc.startsWith('blob:')) URL.revokeObjectURL(audioSrc)
            // 免提模式：AI 说完自动开始录音
            if (autoListenMode.value && interviewStarted.value && !isEnded.value) {
              setTimeout(() => toggleVoiceRecord(), 600)
            }
          }
          currentAudio.onerror = () => {
            isSpeaking.value = false
            fallbackTTS(text)
            if (autoListenMode.value && interviewStarted.value && !isEnded.value) {
              setTimeout(() => toggleVoiceRecord(), 600)
            }
          }
          currentAudio.play()
          return
        }
      }
    }
    fallbackTTS(text)
  } catch {
    fallbackTTS(text)
  }
}

const fallbackTTS = (text: string) => {
  if (!('speechSynthesis' in window)) { isSpeaking.value = false; return }
  const utter = new SpeechSynthesisUtterance(text)
  utter.lang = 'zh-CN'
  utter.rate = 0.95
  utter.onend = () => {
    isSpeaking.value = false
    if (autoListenMode.value && interviewStarted.value && !isEnded.value) {
      setTimeout(() => toggleVoiceRecord(), 600)
    }
  }
  utter.onerror = () => { isSpeaking.value = false }
  window.speechSynthesis.speak(utter)
}

const stopSpeaking = () => {
  if (currentAudio) { currentAudio.pause(); currentAudio = null }
  if ('speechSynthesis' in window) window.speechSynthesis.cancel()
  isSpeaking.value = false
}

// ── STT：麦克风录音 + 转文字 ──────────────────────────
const switchToVoice = () => {
  isVoiceMode.value = true
  voiceIntroText.value = ''
}

/** 启动云 STT（MediaRecorder → MiniMax ASR） */
const startCloudSTT = async () => {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    audioChunks = []
    mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm;codecs=opus' })
    mediaRecorder.ondataavailable = (e) => { if (e.data.size > 0) audioChunks.push(e.data) }
    mediaRecorder.onstop = async () => {
      stream.getTracks().forEach(t => t.stop())
      if (audioChunks.length === 0) { stopRecording(); return }
      isTranscribing.value = true
      const blob = new Blob(audioChunks, { type: 'audio/webm' })
      const buffer = await blob.arrayBuffer()
      const base64 = btoa(String.fromCharCode(...new Uint8Array(buffer)))
      try {
        const res = await speechToText(base64, 'webm')
        if (res?.text) {
          voiceIntroText.value = res.text
          stopRecording()
          autoSubmitVoiceIfReady()
        } else {
          ElMessage.warning('云端语音识别未返回结果，请重试')
          stopRecording()
        }
      } catch {
        ElMessage.warning('云端语音识别失败，请重试或切换浏览器内置识别')
        stopRecording()
      }
    }
    mediaRecorder.start()
  } catch (e: any) {
    if (e.name === 'NotAllowedError') {
      ElMessage.error('麦克风权限被拒绝')
    } else {
      ElMessage.error('无法启动录音，请切换浏览器内置识别')
    }
    stopRecording()
  }
}

const toggleVoiceRecord = async () => {
  if (isRecording.value) {
    // 手动停止 → 停止所有录制通道
    if (mediaRecorder && mediaRecorder.state === 'recording') mediaRecorder.stop()
    if (speechRecognition) speechRecognition.stop()
    return
  }

  // 先尝试浏览器内置 STT，不可用时自动切云 STT
  const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition

  if (SpeechRecognition && !useCloudSTT.value) {
    await startBrowserSTT(SpeechRecognition)
  } else {
    await startCloudSTT()
  }
}

const startBrowserSTT = async (SpeechRecognition: any) => {
  recordingSeconds.value = 0
  voiceIntroText.value = ''

  speechRecognition = new SpeechRecognition()
  speechRecognition.lang = 'zh-CN'
  speechRecognition.continuous = true
  speechRecognition.interimResults = true

  speechRecognition.onresult = (event: any) => {
    let finalText = ''
    let interimText = ''
    for (let i = event.resultIndex; i < event.results.length; i++) {
      if (event.results[i].isFinal) {
        finalText += event.results[i][0].transcript
      } else {
        interimText += event.results[i][0].transcript
      }
    }
    voiceIntroText.value = finalText || interimText
  }

  speechRecognition.onerror = (event: any) => {
    console.warn('浏览器语音识别错误:', event.error)
    if (event.error === 'no-speech') {
      // 静默等待，不打断用户体验
    } else if (event.error === 'not-allowed' || event.error === 'audio-capture') {
      ElMessage.warning('麦克风权限受限，切换云端识别')
      useCloudSTT.value = true
      stopRecording()
      setTimeout(() => toggleVoiceRecord(), 400)
    } else {
      stopRecording()
    }
  }

  speechRecognition.onend = () => {
    stopRecording()
    autoSubmitVoiceIfReady()
  }

  try {
    speechRecognition.start()
    isRecording.value = true

    recordingTimer = window.setInterval(() => {
      recordingSeconds.value++
      if (recordingSeconds.value >= 60) {
        if (speechRecognition) speechRecognition.stop()
        // 同时如果有 MediaRecorder 也在跑也停掉
        if (mediaRecorder && mediaRecorder.state === 'recording') mediaRecorder.stop()
      }
    }, 1000)
  } catch {
    ElMessage.warning('浏览器语音识别启动失败，切换云端识别')
    useCloudSTT.value = true
    await startCloudSTT()
  }
}

const stopRecording = () => {
  isRecording.value = false
  isTranscribing.value = false
  if (recordingTimer) { clearInterval(recordingTimer); recordingTimer = null }
  // 清理 MediaRecorder
  if (mediaRecorder && mediaRecorder.state === 'recording') {
    mediaRecorder.stop()
  }
  mediaRecorder = null
  audioChunks = []
}

const autoSubmitVoiceIfReady = async () => {
  const text = voiceIntroText.value.trim()
  if (!text) {
    // 无内容时，在免提模式下静默重启录音
    if (autoListenMode.value) {
      setTimeout(() => toggleVoiceRecord(), 1000)
    } else {
      ElMessage.info('未识别到有效语音，请重新录制')
    }
    return
  }
  if (text.length < 5) {
    ElMessage.warning('识别内容过短（需至少5个字），请重新录制')
    voiceIntroText.value = ''
    return
  }
  await doSubmitAnswer(text, 'voice')
  voiceIntroText.value = ''
}

// ── 面试核心逻辑 ──────────────────────────────────────
const loadSession = async () => {
  if (!sessionId.value) return
  try {
    const statusRes = await getAISessionStatus(sessionId.value)
    const data = statusRes || {}
    if (data.status === 2 || data.status === 3) {
      interviewStarted.value = true
      isEnded.value = true
      totalScore.value = data.totalScore
      scoresJson.value = data.scoresJson
      jobTitle.value = data.jobTitle || jobTitle.value
    }
    const msgRes = await getAIInterviewResult(sessionId.value)
    if (msgRes) messages.value = msgRes.messages || []
    if (data.status === 1) {
      interviewStarted.value = true
      startTimer()
    }
  } catch (e) {
    console.error('加载会话失败', e)
  }
}

const startTimer = () => {
  timer = window.setInterval(() => { elapsedSeconds.value++ }, 1000)
}

const handleStart = async () => {
  if (!deliveryId.value || !candidateId.value || !jobId.value) {
    ElMessage.warning('缺少必要的面试信息')
    return
  }

  loading.value = true
  try {
    const res = await startAIInterview({
      deliveryId: deliveryId.value,
      candidateId: candidateId.value,
      jobId: jobId.value
    })

    if (res && res.sessionId) {
      sessionId.value = res.sessionId
      interviewStarted.value = true
      const firstContent = res.firstMessage?.content || '你好！欢迎参加本次AI面试，请先做一个简短的自我介绍。'
      messages.value.push({
        role: 'ai',
        content: firstContent,
        messageType: 'question',
        createdAt: res.firstMessage?.createdAt || new Date().toISOString()
      })
      // 默认语音模式
      isVoiceMode.value = true
      startTimer()
      // 启动摄像头
      startCamera()
      scrollToBottom()
      // AI提问语音播报
      await speakText(firstContent)
    } else {
      ElMessage.error('启动面试失败：返回数据异常')
    }
  } catch (e: any) {
    const errorMsg = e.message || '启动面试失败'
    ElMessage.error(errorMsg)
    if (errorMsg.includes('不可重复') || errorMsg.includes('已完成')) {
      ElMessageBox.alert(errorMsg, '无法开始面试', {
        confirmButtonText: '返回我的投递',
        type: 'warning'
      }).then(() => router.push('/my/deliveries')).catch(() => {})
    } else if (errorMsg.includes('未允许') || errorMsg.includes('不允许') || errorMsg.includes('权限') || errorMsg.includes('过期')) {
      ElMessageBox.confirm(errorMsg, '无法开始面试', {
        confirmButtonText: '返回我的投递',
        cancelButtonText: '我知道了',
        type: 'warning'
      }).then(() => router.push('/my/deliveries')).catch(() => {})
    }
  } finally {
    loading.value = false
  }
}

/** 文字模式提交 */
const handleSubmit = async () => {
  if (!currentAnswer.value.trim() || !sessionId.value) return
  const answerText = currentAnswer.value.trim()
  currentAnswer.value = ''
  await doSubmitAnswer(answerText, 'text')
}

/** 统一提交逻辑 */
const doSubmitAnswer = async (answerText: string, inputMode: 'text' | 'voice') => {
  messages.value.push({
    role: 'candidate',
    content: answerText,
    messageType: 'answer',
    inputMode,
    createdAt: new Date().toISOString()
  })
  scrollToBottom()

  loading.value = true
  try {
    const res = await submitAIAnswer({ sessionId: sessionId.value!, answer: answerText })

    if (res) {
      const aiContent = res.content
      if (res.isEnded) {
        messages.value.push({
          role: 'ai',
          content: aiContent,
          messageType: 'evaluation',
          createdAt: new Date().toISOString()
        })
        isEnded.value = true
        totalScore.value = res.totalScore
        scoresJson.value = res.scoresJson
        if (timer) clearInterval(timer)
        stopCamera()
        // 面试结束，不播报（直接出分）
      } else {
        messages.value.push({
          role: 'ai',
          content: aiContent,
          messageType: 'question',
          createdAt: new Date().toISOString()
        })
        // 保持语音模式
        isVoiceMode.value = true
        voiceIntroText.value = ''
        // AI提问语音播报
        await speakText(aiContent)
      }
      scrollToBottom()
    } else {
      ElMessage.error('提交失败：返回数据异常')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '提交失败')
  } finally {
    loading.value = false
  }
}

const viewDetail = () => {
  if (!sessionId.value) return
  router.push({ name: 'AIInterviewReport', params: { sessionId: sessionId.value } })
}

const handleBack = () => { router.back() }

const scrollToBottom = () => {
  nextTick(() => {
    const el = chatAreaRef.value
    if (el) el.scrollTop = el.scrollHeight
  })
}

const chatAreaRef = ref<HTMLElement>()

const formatTime = (date: string) => dayjs(date).format('HH:mm')

const formatDuration = (seconds: number) => {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${String(s).padStart(2, '0')}`
}

const getScoreColor = (score: number) => {
  if (score >= 80) return 'var(--color-success)'
  if (score >= 60) return '#E6A23C'
  return 'var(--color-danger)'
}
</script>

<style scoped lang="scss">
.ai-interview-page {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--color-bg);
  overflow: hidden;
}

// ── 顶部标题 ────────────────────────────────────────
.interview-header {
  background: var(--gradient-primary);
  color: #fff;
  padding: 16px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-shrink: 0;
  box-shadow: 0 2px 12px rgba(99, 102, 241, 0.3);

  .header-left {
    display: flex;
    align-items: center;
    gap: 16px;

    .back-icon {
      font-size: 20px;
      cursor: pointer;
      opacity: 0.8;
      transition: opacity 0.2s;
      &:hover { opacity: 1; }
    }

    .interview-title {
      display: flex;
      flex-direction: column;
      gap: 2px;

      .job-name { font-size: 18px; font-weight: 600; }
      .interview-mode { font-size: 12px; opacity: 0.8; }
    }
  }

  .header-right {
    display: flex;
    align-items: center;
    gap: 16px;

    .round-indicator {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 4px;

      .round-label { font-size: 13px; opacity: 0.9; }
      :deep(.el-progress) { width: 120px; }
    }

    :deep(.el-tag) { font-size: 13px; }

    .tts-badge {
      display: flex;
      align-items: center;
      gap: 8px;
      background: rgba(255,255,255,0.15);
      border: 1px solid rgba(255,255,255,0.3);
      border-radius: 20px;
      padding: 4px 12px;
      font-size: 12px;

      .tts-wave {
        display: flex;
        align-items: center;
        gap: 2px;

        span {
          display: inline-block;
          width: 3px;
          background: #fff;
          border-radius: 2px;
          animation: wave 0.8s infinite ease-in-out;

          &:nth-child(1) { height: 8px; animation-delay: 0s; }
          &:nth-child(2) { height: 14px; animation-delay: 0.1s; }
          &:nth-child(3) { height: 10px; animation-delay: 0.2s; }
          &:nth-child(4) { height: 16px; animation-delay: 0.3s; }
        }
      }
    }
  }
}

@keyframes wave {
  0%, 100% { transform: scaleY(0.6); }
  50% { transform: scaleY(1.3); }
}

// ── 主体布局 ────────────────────────────────────────
.interview-body {
  flex: 1;
  display: flex;
  gap: 20px;
  padding: 20px;
  overflow: hidden;
}

.left-panel {
  width: 280px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow-y: auto;

  .card-header-title {
    display: flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
    color: var(--color-primary);
  }

  .job-info-card .job-detail-list {
    display: flex;
    flex-direction: column;
    gap: 12px;

    .job-item {
      display: flex;
      flex-direction: column;
      gap: 4px;

      .job-label { font-size: 12px; color: var(--color-text-secondary); }
      .job-value { font-size: 14px; font-weight: 500; color: var(--color-text);
        &.salary { color: var(--color-accent); }
      }
    }
  }

  .guide-card .guide-list {
    display: flex;
    flex-direction: column;
    gap: 12px;

    .guide-item {
      display: flex;
      gap: 10px;
      align-items: flex-start;

      .guide-num {
        width: 20px; height: 20px;
        background: var(--color-primary); color: #fff;
        border-radius: 50%;
        display: flex; align-items: center; justify-content: center;
        font-size: 11px; font-weight: 600; flex-shrink: 0;
      }

      .guide-text { font-size: 13px; color: var(--color-text-secondary); line-height: 1.5; }
    }
  }

  .status-card .status-grid {
    display: flex; flex-direction: column; gap: 12px;

    .status-item {
      display: flex; justify-content: space-between; align-items: center;

      .status-label { font-size: 13px; color: var(--color-text-secondary); }
      .status-value { font-size: 16px; font-weight: 600; color: var(--color-text);
        &.score { font-size: 20px; }
      }
    }
  }
}

// ── 右侧对话区 + 摄像头包裹 ──────────────────────────
.right-panel-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-width: 0;
}

// ── 摄像头视频面板 ──────────────────────────────────
.video-panel {
  flex-shrink: 0;
  display: flex;
  align-items: flex-start;
  gap: 16px;
  background: var(--color-surface);
  border-radius: 16px;
  padding: 12px 16px;
  box-shadow: var(--shadow-card);

  .video-container {
    position: relative;
    width: 240px;
    height: 180px;
    border-radius: 12px;
    overflow: hidden;
    background: var(--color-bg);
    flex-shrink: 0;

    .camera-video {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transform: scaleX(-1); // 镜像
    }

    .video-overlay {
      position: absolute;
      top: 0; left: 0; right: 0; bottom: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 6px;
      background: rgba(9, 9, 11, 0.85);
      color: var(--color-text-secondary);
      font-size: 12px;
    }

    // 行为分析浮层
    .behavior-overlay {
      position: absolute;
      top: 8px;
      left: 8px;
      right: 8px;
      display: flex;
      gap: 6px;
      z-index: 2;

      .behavior-tag {
        display: flex;
        align-items: center;
        gap: 3px;
        padding: 2px 8px;
        border-radius: 10px;
        font-size: 11px;
        font-weight: 600;
        backdrop-filter: blur(4px);
        transition: all 0.3s;

        &.good, &.focused {
          background: rgba(103, 194, 58, 0.85);
          color: #fff;
        }
        &.warning {
          background: rgba(230, 162, 60, 0.85);
          color: #fff;
        }
        &.bad, &.distracted {
          background: rgba(245, 108, 108, 0.85);
          color: #fff;
        }
        &.unknown {
          background: rgba(144, 147, 156, 0.75);
          color: #fff;
        }
      }
    }
  }

  .video-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 10px;
    min-width: 0;
  }

  .video-label {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    font-weight: 500;
  }

  // 行为分析实时状态面板
  .behavior-status-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 6px 12px;

    .bs-item {
      display: flex;
      align-items: center;
      gap: 6px;

      .bs-label {
        font-size: 12px;
        color: var(--color-text-secondary);
        white-space: nowrap;
      }

      .bs-value {
        font-size: 12px;
        font-weight: 600;
        color: var(--color-text);
        transition: color 0.3s;

        &.good, &.focused { color: var(--color-success); }
        &.warning { color: #E6A23C; }
        &.bad, &.distracted { color: var(--color-danger); }
      }
    }
  }
}

// ── 右侧对话区 ───────────────────────────────────────
.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: var(--color-surface);
  border-radius: 16px;
  box-shadow: var(--shadow-card);
  overflow: hidden;
  min-height: 0;
}

.chat-area {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  scroll-behavior: smooth;
}

// 欢迎页
.welcome-message {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  text-align: center;
  gap: 14px;

  .welcome-avatar {
    width: 80px; height: 80px;
    background: var(--gradient-primary);
    border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    color: #fff; margin-bottom: 8px;
  }

  h2 { font-size: 24px; color: var(--color-text); margin: 0; }
  p { color: var(--color-text-secondary); font-size: 14px; margin: 0; }
  .start-btn { margin-top: 12px; }
}

// ── 消息气泡 ─────────────────────────────────────────
.messages-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.message-wrapper {
  display: flex;
  gap: 12px;
  align-items: flex-end;

  &.candidate { flex-direction: row-reverse; }

  .ai-avatar,
  .candidate-avatar {
    width: 40px; height: 40px;
    border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0; color: #fff;
    transition: box-shadow 0.3s;
  }

  .ai-avatar {
    background: var(--gradient-primary);
    &.speaking {
      box-shadow: 0 0 0 4px rgba(99, 102, 241, 0.3), 0 0 0 8px rgba(99, 102, 241, 0.1);
      animation: avatar-pulse 1s infinite;
    }
  }

  .candidate-avatar {
    background: linear-gradient(135deg, var(--color-accent) 0%, var(--color-secondary) 100%);
  }

  .message-bubble {
    padding: 14px 18px;
    border-radius: 16px;
    line-height: 1.6;
    max-width: 75%;

    .message-content {
      font-size: 14px; color: var(--color-text); white-space: pre-wrap;
    }

    .message-meta {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 4px;

      .message-time { font-size: 11px; color: var(--color-text-muted); }
      .replay-btn { padding: 0 6px; height: 22px; font-size: 12px; }
      .voice-tag { font-size: 11px; color: var(--color-text-muted); }

      &.right { justify-content: flex-end; }
    }
  }

  .ai-bubble {
    background: var(--color-bg);
    border-bottom-left-radius: 4px;
  }

  .candidate-bubble {
    background: var(--gradient-primary);
    border-bottom-right-radius: 4px;

    .message-content { color: #fff; }
    .message-time { color: rgba(255,255,255,0.6); }
  }
}

@keyframes avatar-pulse {
  0%, 100% { box-shadow: 0 0 0 4px rgba(99,102,241,0.3), 0 0 0 8px rgba(99,102,241,0.1); }
  50% { box-shadow: 0 0 0 6px rgba(99,102,241,0.4), 0 0 0 12px rgba(99,102,241,0.15); }
}

.loading-bubble { min-width: 80px; }

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 4px 0;

  span {
    width: 8px; height: 8px;
    background: var(--color-primary); border-radius: 50%;
    animation: typing 1.2s infinite;
    &:nth-child(2) { animation-delay: 0.2s; }
    &:nth-child(3) { animation-delay: 0.4s; }
  }
}

@keyframes typing {
  0%, 100% { opacity: 0.3; transform: scale(0.8); }
  50% { opacity: 1; transform: scale(1); }
}

// ── 面试结果卡 ───────────────────────────────────────
.interview-result-card {
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.06) 0%, var(--color-surface) 100%);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 24px;
  margin-top: 16px;

  .result-header {
    display: flex; align-items: center; gap: 12px; margin-bottom: 20px;
    h3 { font-size: 18px; color: var(--color-text); margin: 0; }
  }

  .result-scores {
    display: flex; gap: 32px; align-items: center; flex-wrap: wrap; margin-bottom: 20px;

    .score-ring-wrapper {
      position: relative; width: 100px; height: 100px; flex-shrink: 0;

      .ring-center {
        position: absolute; top: 50%; left: 50%;
        transform: translate(-50%, -50%); text-align: center;

        .ring-score { font-size: 24px; font-weight: 700; display: block; }
        .ring-label { font-size: 11px; color: var(--color-text-secondary); }
      }
    }

    .score-bars {
      flex: 1; display: flex; flex-direction: column; gap: 10px; min-width: 200px;

      .score-bar-item {
        display: flex; align-items: center; gap: 10px;

        .bar-label { width: 70px; font-size: 13px; color: var(--color-text-secondary); flex-shrink: 0; }
        :deep(.el-progress) { flex: 1; }
        .bar-value { width: 45px; font-size: 13px; font-weight: 600; color: var(--color-text); text-align: right; flex-shrink: 0; }
      }
    }
  }

  .result-actions { display: flex; justify-content: center; gap: 12px; }
}

// ── 输入区 ───────────────────────────────────────────
.input-area {
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
  flex-shrink: 0;
}

// ── 语音输入模式 ──────────────────────────────────────
.voice-input-area {
  padding: 20px 24px;

  .voice-mode-header {
    display: flex; align-items: center; gap: 8px;
    font-size: 14px; font-weight: 600; color: var(--color-primary);
    margin-bottom: 20px;

    span { flex: 1; }
  }

  .voice-record-area {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 14px;

    .voice-circle {
      position: relative;
      width: 88px; height: 88px;
      background: var(--gradient-primary);
      border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      color: #fff;
      cursor: pointer;
      transition: transform 0.15s, box-shadow 0.15s;
      box-shadow: 0 6px 20px rgba(99, 102, 241, 0.3);

      &:hover { transform: scale(1.05); }
      &:active { transform: scale(0.97); }

      &.active {
        background: linear-gradient(135deg, #c0392b, #e74c3c);
        box-shadow: 0 6px 20px rgba(231, 76, 60, 0.35);
      }

      &.transcribing {
        background: linear-gradient(135deg, #e6a23c, #f4a261);
        animation: spin-border 1s linear infinite;
      }

      .voice-circle-rings {
        position: absolute; top: 0; left: 0; width: 100%; height: 100%;

        .ring {
          position: absolute; border: 2px solid rgba(231, 76, 60, 0.4);
          border-radius: 50%;
          animation: ripple 1.5s infinite ease-out;

          &.ring1 {
            top: -12px; left: -12px; right: -12px; bottom: -12px;
          }
          &.ring2 {
            top: -22px; left: -22px; right: -22px; bottom: -22px;
            animation-delay: 0.5s;
          }
        }
      }
    }

    .voice-status {
      font-size: 13px;

      .status-idle { color: var(--color-text-secondary); }
      .status-recording { color: var(--color-danger); font-weight: 600; }
      .status-transcribing { color: #E6A23C; }
    }
  }
}

@keyframes ripple {
  0% { transform: scale(1); opacity: 0.8; }
  100% { transform: scale(1.4); opacity: 0; }
}

@keyframes spin-border {
  to { filter: hue-rotate(360deg); }
}

// ── 文字输入模式 ──────────────────────────────────────
.text-input-area {
  padding: 16px 20px;

  .input-wrapper {
    display: flex; flex-direction: column; gap: 10px;

    :deep(.el-textarea__inner) {
      border-radius: 12px; padding: 12px 14px; font-size: 14px;
    }

    .input-actions {
      display: flex; align-items: center; justify-content: space-between;

      .hint-text { font-size: 12px; color: var(--color-text-muted); }
      .action-btns { display: flex; align-items: center; gap: 10px; }
    }
  }
}

// ── 动画 ─────────────────────────────────────────────
.message-fade-enter-active { transition: all 0.3s ease; }
.message-fade-enter-from { opacity: 0; transform: translateY(10px); }
</style>
