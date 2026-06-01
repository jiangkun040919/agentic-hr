<template>
  <div class="my-deliveries-page">
    <div class="page-header">
      <h1 class="page-title">我的投递记录</h1>
      <p class="page-sub">追踪你的求职进展</p>
    </div>

    <!-- 站内消息通知提示 -->
    <div class="notify-banner">
      <span class="nb-icon">🔔</span>
      <span>面试通知将通过<strong>站内消息</strong>实时推送，请注意右上角铃铛图标的消息提醒。</span>
    </div>

    <!-- AI面试邀请卡片 -->
    <div v-if="aiInterviewInvitations.length > 0" class="ai-invite-card">
      <div class="ai-invite-header">
        <span class="ai-icon">🤖</span>
        <span class="ai-title">AI面试邀请</span>
        <span class="ai-badge">{{ aiInterviewInvitations.length }}</span>
      </div>
      <div class="ai-invite-list">
        <div v-for="item in aiInterviewInvitations" :key="item.deliveryId" class="ai-invite-item">
          <div class="invite-info">
            <h4>{{ item.jobTitle }}</h4>
            <p v-if="item.aiInterviewDeadline" class="deadline">⏰ 请在 {{ formatDeadline(item.aiInterviewDeadline) }} 前完成面试</p>
            <p v-else class="deadline expired">❌ 面试邀请已过期</p>
          </div>
          <VBtn variant="filled" color="mint" size="lg" @click="startAIInterview(item)" :disabled="isDeadlinePassed(item.aiInterviewDeadline)">
            🎬 参加AI面试
          </VBtn>
        </div>
      </div>
    </div>

    <!-- Tab 切换 -->
    <div class="tab-bar">
      <button class="tab-btn" :class="{ active: activeTab === 'deliveries' }" @click="activeTab = 'deliveries'">📋 我的投递</button>
      <button class="tab-btn" :class="{ active: activeTab === 'ai-interviews' }" @click="activeTab = 'ai-interviews'">🤖 AI面试记录</button>
    </div>

    <!-- 投递列表 - 卡片式 -->
    <div v-if="activeTab === 'deliveries'" v-loading="loading" class="delivery-list">
      <div v-for="row in deliveries" :key="row.deliveryId" class="delivery-card" @click="viewDetail(row)">
        <div class="dc-status-bar" :style="{ background: statusBarColors[row.status] || '#8A9BA8' }" />
        <div class="dc-body">
          <div class="dc-header">
            <h3 class="dc-title">{{ row.jobTitle }}</h3>
            <span class="dc-status" :class="`st-${['pending','reviewed','interview','intern','hired','rejected'][row.status]}`">
              {{ ['待查看','已查看','面试中','实习中','正式入职','已淘汰'][row.status] }}
            </span>
          </div>
          <div class="dc-meta">
            <span>{{ row.candidateName }}</span>
            <span>{{ row.phone }}</span>
            <span>{{ formatDate(row.deliverTime) }}</span>
          </div>
          <div class="dc-footer">
            <span v-if="row.allowAIInterview" class="dc-ai-tag">🤖 AI面试已开放</span>
            <span v-else class="dc-ai-tag dc-ai-tag--off">AI面试未开放</span>
            <div class="dc-actions">
              <VBtn variant="ghost" color="gray" size="sm" @click.stop="viewDetail(row)">详情</VBtn>
              <VBtn variant="ghost" color="coral" size="sm" @click.stop="editDetail(row)">修改</VBtn>
            </div>
          </div>
        </div>
      </div>
      <VEmpty v-if="!loading && deliveries.length === 0" title="暂无投递记录" description="快去浏览岗位，投递你的第一份简历吧！" emoji="📮" />
    </div>

    <!-- AI面试记录 -->
    <div v-if="activeTab === 'ai-interviews'" class="ai-history-section">
      <div v-if="aiSessionsLoading" class="loading-text">加载中...</div>
      <VEmpty v-else-if="aiSessions.length === 0" title="暂无AI面试记录" description="完成AI面试后，记录会显示在这里" emoji="🎙️" />
      <div v-else class="ai-history-list">
        <div v-for="item in aiSessions" :key="item.sessionId" class="ai-history-card">
          <div class="ahc-header">
            <h4>{{ item.jobTitle || 'AI面试' }}</h4>
            <VTag :color="sessionStatusColors[item.status] || 'gray'" size="md">
              {{ ['未开始','进行中','已完成','已中断'][item.status] || '未知' }}
            </VTag>
          </div>
          <div class="ahc-body">
            <div class="ahc-info">
              <p>📅 {{ item.startTime ? formatDate(item.startTime) : formatDate(item.createdAt) }}</p>
              <p v-if="item.totalDuration">⏱️ {{ formatDuration(item.totalDuration) }}</p>
            </div>
            <div v-if="item.totalScore" class="ahc-score">
              <div class="score-ring" :style="{ '--pct': item.totalScore }">
                <span class="score-num" :class="getScoreClass(item.totalScore)">{{ item.totalScore }}</span>
              </div>
              <span class="score-label">AI评分</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 详情弹窗 -->
    <VDialog v-model="showDetail" title="投递详情" max-width="520px">
      <div v-if="selectedDelivery" class="detail-content">
        <div class="detail-grid">
          <div class="dg-item"><span class="dg-label">投递岗位</span><span class="dg-value">{{ selectedDelivery.jobTitle }}</span></div>
          <div class="dg-item"><span class="dg-label">投递时间</span><span class="dg-value">{{ formatDate(selectedDelivery.deliverTime) }}</span></div>
          <div class="dg-item"><span class="dg-label">姓名</span><span class="dg-value">{{ selectedDelivery.candidateName }}</span></div>
          <div class="dg-item"><span class="dg-label">手机号</span><span class="dg-value">{{ selectedDelivery.phone }}</span></div>
          <div class="dg-item"><span class="dg-label">学历</span><span class="dg-value">{{ selectedDelivery.education }}</span></div>
          <div class="dg-item"><span class="dg-label">工作经验</span><span class="dg-value">{{ selectedDelivery.workYears }}年</span></div>
          <div class="dg-item"><span class="dg-label">邮箱</span><span class="dg-value">{{ selectedDelivery.email || '-' }}</span></div>
          <div class="dg-item"><span class="dg-label">当前状态</span><span class="dg-value"><VTag :color="statusBarColors[selectedDelivery.status] === '#C4A96A' ? 'coral' : 'mint'" size="sm">{{ ['待查看','已查看','面试中','实习中','正式入职','已淘汰'][selectedDelivery.status] }}</VTag></span></div>
        </div>

        <div style="margin-top:16px">
          <VBtn variant="filled" color="purple" size="sm" @click="loadCompetitiveness" :loading="compLoading">查看竞争力分析</VBtn>
        </div>
        <div v-if="compResult" class="comp-panel">
          <div class="comp-stats">
            <span>匹配 <b>{{ compResult.matchRate }}%</b></span>
            <span>竞争者 <b>{{ compResult.totalCompetitors }}</b></span>
            <span>排名 <b>#{{ compResult.estimatedRank }}</b></span>
            <VTag color="mint" size="sm">前{{ compResult.percentile }}%</VTag>
          </div>
          <div v-if="compResult.strengths?.length" class="comp-tags">
            <span class="comp-label">优势</span>
            <VTag v-for="s in compResult.strengths" :key="s" color="mint" size="sm">{{ s }}</VTag>
          </div>
          <div v-if="compResult.weaknesses?.length" class="comp-tags">
            <span class="comp-label">待提升</span>
            <VTag v-for="w in compResult.weaknesses.slice(0,5)" :key="w" color="sunny" size="sm">{{ w }}</VTag>
          </div>
        </div>
      </div>
    </VDialog>

    <!-- 编辑弹窗 -->
    <VDialog v-model="showEdit" title="修改投递信息" max-width="480px">
      <div v-if="editingDelivery" class="edit-form">
        <div class="form-group">
          <label class="form-label">姓名</label>
          <input v-model="editForm.candidateName" class="form-input" placeholder="请输入姓名" />
        </div>
        <div class="form-group">
          <label class="form-label">手机号</label>
          <input v-model="editForm.phone" class="form-input" placeholder="请输入手机号" />
        </div>
        <div class="form-group">
          <label class="form-label">邮箱</label>
          <input v-model="editForm.email" class="form-input" placeholder="请输入邮箱" />
        </div>
        <div class="form-group">
          <label class="form-label">学历</label>
          <select v-model="editForm.education" class="form-select">
            <option value="高中">高中</option><option value="大专">大专</option>
            <option value="本科">本科</option><option value="硕士">硕士</option>
            <option value="博士">博士</option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">工作经验(年)</label>
          <input v-model.number="editForm.workYears" type="number" class="form-input" placeholder="工作年限" />
        </div>
      </div>
      <template #footer>
        <VBtn variant="ghost" color="gray" @click="showEdit = false">取消</VBtn>
        <VBtn variant="filled" color="coral" @click="submitEdit">保存修改</VBtn>
      </template>
    </VDialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useResumeStore } from '@/stores/resume'
import { updateDeliveryInfo } from '@/api/delivery'
import { getMyAISessions } from '@/api/interview-ai'
import { getCompetitiveness } from '@/api/graph'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'
import VBtn from '@/components/ui/VBtn.vue'
import VTag from '@/components/ui/VTag.vue'
import VEmpty from '@/components/ui/VEmpty.vue'
import VDialog from '@/components/ui/VDialog.vue'

const router = useRouter()
const resumeStore = useResumeStore()
const loading = computed(() => resumeStore.loading)
const deliveries = computed(() => resumeStore.deliveries)

const statusBarColors: Record<number, string> = { 0: '#C4A96A', 1: '#8A9BA8', 2: '#8B9A6E', 3: '#7A8B5E', 4: '#7A8B5E', 5: '#C4A96A' }
const sessionStatusColors: Record<number, string> = { 0: 'gray', 1: 'sunny', 2: 'mint', 3: 'coral' }

// 获取有活跃或已完成 AI session 的 deliveryId 集合
const aiActiveDeliveryIds = computed(() => {
  const ids = new Set<number>()
  for (const s of aiSessions.value) {
    if (s.deliveryId) ids.add(s.deliveryId)
  }
  return ids
})

const aiInterviewInvitations = computed(() =>
  deliveries.value.filter(d => d.allowAIInterview && !aiActiveDeliveryIds.value.has(d.deliveryId))
)

const activeTab = ref('deliveries')
const showDetail = ref(false)
const selectedDelivery = ref<any>(null)
const compLoading = ref(false)
const compResult = ref<any>(null)

const loadCompetitiveness = async () => {
  if (!selectedDelivery.value || compLoading.value) return
  compLoading.value = true
  try { const res = await getCompetitiveness(selectedDelivery.value.deliveryId) as any; compResult.value = res.data || res }
  catch { ElMessage.warning('分析暂不可用') }
  finally { compLoading.value = false }
}

const showEdit = ref(false)
const editingDelivery = ref<any>(null)
const editForm = reactive({ candidateName: '', phone: '', email: '', education: '', workYears: 0 })

onMounted(() => { resumeStore.fetchMyDeliveries(); fetchAISessions() })

const aiSessions = ref<any[]>([])
const aiSessionsLoading = ref(false)
const fetchAISessions = async () => {
  aiSessionsLoading.value = true
  try {
    const res = await getMyAISessions()
    // 响应拦截器已解包 data，res 直接是 { code, data }
    if (res?.code === 200) aiSessions.value = res.data || []
  }
  catch { aiSessions.value = [] }
  finally { aiSessionsLoading.value = false }
}

watch(activeTab, (val) => { if (val === 'ai-interviews') fetchAISessions() })

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')
const formatDeadline = (date: string | null | undefined) => date ? dayjs(date).format('YYYY-MM-DD HH:mm') : ''
const isDeadlinePassed = (deadline: string | null | undefined) => deadline ? new Date() > new Date(deadline) : false
const formatDuration = (seconds: number) => { const min = Math.floor(seconds / 60); const sec = seconds % 60; return min > 0 ? `${min}分${sec}秒` : `${sec}秒` }
const getScoreClass = (score: number) => score >= 80 ? 'score-high' : score >= 60 ? 'score-medium' : 'score-low'

const viewDetail = (row: any) => { selectedDelivery.value = row; showDetail.value = true; compResult.value = null }
const editDetail = (row: any) => {
  editingDelivery.value = row
  Object.assign(editForm, { candidateName: row.candidateName || '', phone: row.phone || '', email: row.email || '', education: row.education || '', workYears: row.workYears || 0 })
  showEdit.value = true
}

const submitEdit = async () => {
  try {
    await updateDeliveryInfo(editingDelivery.value.deliveryId, { ...editForm, jobId: editingDelivery.value.jobId, resumeUrl: '' })
    await resumeStore.fetchMyDeliveries(); showEdit.value = false; ElMessage.success('信息更新成功')
  } catch { ElMessage.error('更新失败') }
}

const startAIInterview = (delivery: any) => {
  const userId = localStorage.getItem('userId') || localStorage.getItem('candidateId') || ''
  if (!userId) { ElMessage.warning('请重新登录'); router.push('/login'); return }
  router.push({ name: 'AIInterview', params: { jobId: String(delivery.jobId), deliveryId: String(delivery.deliveryId), candidateId: String(userId) } })
}
</script>

<style scoped lang="scss">
.my-deliveries-page { max-width: 1000px; margin: 0 auto; padding: 20px; }

.page-header { margin-bottom: 24px; }
.page-title { font-size: 28px; font-weight: 800; color: var(--color-text); margin: 0 0 4px; }
.page-sub { font-size: 14px; color: var(--color-text-muted); margin: 0; }

.notify-banner {
  display: flex; align-items: center; gap: 10px; padding: 14px 20px;
  background: rgba(138,155,168,0.06); border: 1px solid rgba(138,155,168,0.15);
  border-radius: 14px; margin-bottom: 20px; font-size: 13px; color: var(--color-text-secondary);
  strong { color: var(--color-primary); }
}
.nb-icon { font-size: 18px; }

// AI面试邀请
.ai-invite-card {
  background: var(--color-surface); border: 2px solid transparent;
  border-radius: var(--radius-xl); margin-bottom: 20px; overflow: hidden;
  background-image: linear-gradient(var(--color-surface), var(--color-surface)), var(--gradient-mint);
  background-origin: border-box; background-clip: padding-box, border-box;
}
.ai-invite-header {
  display: flex; align-items: center; gap: 10px; padding: 16px 20px;
  background: linear-gradient(135deg, rgba(122,139,94,0.08), rgba(139,154,110,0.04));
}
.ai-icon { font-size: 22px; }
.ai-title { font-size: 17px; font-weight: 700; flex: 1; }
.ai-badge {
  width: 24px; height: 24px; border-radius: 50%; background: var(--gradient-mint);
  color: #fff; font-size: 12px; font-weight: 700; display: flex; align-items: center; justify-content: center;
}
.ai-invite-list { padding: 0 20px 20px; display: flex; flex-direction: column; gap: 12px; }
.ai-invite-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px; background: var(--color-bg); border-radius: 14px;
}
.invite-info {
  h4 { margin: 0 0 6px; font-size: 16px; }
  .deadline { margin: 0; font-size: 13px; color: var(--color-text-secondary); &.expired { color: var(--color-danger); } }
}

// Tab 栏
.tab-bar {
  display: flex; gap: 4px; background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-full); padding: 4px; margin-bottom: 20px; width: fit-content;
}
.tab-btn {
  padding: 8px 20px; border-radius: var(--radius-full); border: none;
  background: transparent; cursor: pointer; font-size: 14px; font-weight: 500;
  color: var(--color-text-secondary); font-family: var(--font-sans);
  transition: all 0.2s var(--ease-bounce);
  &.active { background: var(--gradient-primary); color: #fff; font-weight: 600; box-shadow: 0 2px 8px rgba(196,169,106,0.2); }
  &:hover:not(.active) { background: var(--color-primary-bg); }
}

// 投递卡片列表
.delivery-list { display: flex; flex-direction: column; gap: 12px; }

.delivery-card {
  display: flex; background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-lg); overflow: hidden; cursor: pointer;
  transition: all 0.2s var(--ease-bounce);
  &:hover { transform: translateY(-2px); box-shadow: var(--shadow-md); border-color: var(--color-border-glow); }
}
.dc-status-bar { width: 4px; flex-shrink: 0; }
.dc-body { flex: 1; padding: 16px 20px; }
.dc-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.dc-title { font-size: 16px; font-weight: 700; margin: 0; }
.dc-status {
  font-size: 12px; font-weight: 600; padding: 3px 12px; border-radius: var(--radius-full);
  &.st-pending { background: rgba(196,169,106,0.1); color: #C4A96A; }
  &.st-reviewed { background: rgba(138,155,168,0.1); color: #8A9BA8; }
  &.st-interview { background: rgba(139,154,110,0.1); color: #8B9A6E; }
  &.st-intern { background: rgba(122,139,94,0.1); color: #7A8B5E; }
  &.st-hired { background: rgba(122,139,94,0.1); color: #7A8B5E; }
  &.st-rejected { background: rgba(196,169,106,0.1); color: #C4A96A; }
}
.dc-meta { display: flex; gap: 16px; font-size: 13px; color: var(--color-text-muted); margin-bottom: 10px; }
.dc-footer { display: flex; justify-content: space-between; align-items: center; }
.dc-ai-tag { font-size: 12px; color: var(--color-accent); font-weight: 500; &--off { color: var(--color-text-muted); } }
.dc-actions { display: flex; gap: 6px; }

// AI面试记录
.ai-history-section { min-height: 200px; }
.loading-text { text-align: center; color: var(--color-text-muted); padding: 40px; }
.ai-history-list { display: flex; flex-direction: column; gap: 12px; }
.ai-history-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-lg); padding: 20px;
  transition: all 0.2s var(--ease-bounce);
  &:hover { transform: translateY(-2px); box-shadow: var(--shadow-md); }
}
.ahc-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; h4 { margin: 0; font-size: 16px; } }
.ahc-body { display: flex; justify-content: space-between; align-items: center; }
.ahc-info { p { margin: 4px 0; font-size: 13px; color: var(--color-text-secondary); } }
.ahc-score { text-align: center; }
.score-ring {
  width: 56px; height: 56px; border-radius: 50%; position: relative;
  background: conic-gradient(var(--color-primary) calc(var(--pct) * 1%), var(--color-bg-alt) 0);
  display: flex; align-items: center; justify-content: center;
  &::before { content: ''; position: absolute; inset: 4px; border-radius: 50%; background: var(--color-surface); }
}
.score-num { font-size: 18px; font-weight: 800; position: relative; z-index: 1; &.score-high { color: #7A8B5E; } &.score-medium { color: #C4A96A; } &.score-low { color: #C4A96A; } }
.score-label { font-size: 11px; color: var(--color-text-muted); display: block; margin-top: 4px; }

// 详情/编辑
.detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.dg-item { display: flex; flex-direction: column; gap: 4px; }
.dg-label { font-size: 12px; color: var(--color-text-muted); }
.dg-value { font-size: 14px; color: var(--color-text); font-weight: 500; }

.comp-panel { margin-top: 16px; padding: 16px; background: var(--color-bg); border-radius: 14px; }
.comp-stats { display: flex; gap: 16px; font-size: 13px; align-items: center; flex-wrap: wrap; b { color: var(--color-primary); } }
.comp-tags { margin-top: 10px; display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.comp-label { font-size: 12px; color: var(--color-text-muted); margin-right: 4px; }

.edit-form { display: flex; flex-direction: column; gap: 14px; }
.form-group { display: flex; flex-direction: column; gap: 5px; }
.form-label { font-size: 12px; font-weight: 600; color: var(--color-text-secondary); }
.form-input {
  height: 40px; padding: 0 14px; border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md); background: var(--color-bg);
  font-size: 14px; color: var(--color-text); font-family: var(--font-sans);
  outline: none; transition: border-color 0.2s;
  &:focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.1); }
}
.form-select {
  height: 40px; padding: 0 14px; border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md); background: var(--color-bg);
  font-size: 14px; color: var(--color-text); font-family: var(--font-sans);
  outline: none; cursor: pointer;
}
</style>
