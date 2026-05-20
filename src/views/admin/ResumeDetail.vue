<template>
  <div class="resume-detail-container">
    <el-button @click="$router.back()" class="back-btn"><el-icon><ArrowLeft /></el-icon>返回</el-button>

    <el-card v-loading="loading" v-if="delivery">
      <template #header>
        <div class="card-header">
          <span>简历详情 — {{ delivery.candidateName }}</span>
          <div class="header-actions">
            <el-button type="success" v-if="delivery.status < 2" @click="handleScheduleInterview">安排面试</el-button>
            <el-button type="primary" v-if="delivery.status === 2" @click="internshipDialogVisible = true">
              <el-icon><Promotion /></el-icon> 开始实习
            </el-button>
            <el-button type="success" v-if="delivery.status === 3" @click="hireDialogVisible = true">
              <el-icon><Medal /></el-icon> 正式入职
            </el-button>
            <el-tag v-if="delivery.status === 4" type="success" size="large">已正式入职</el-tag>
            <el-tag v-if="delivery.status >= 5" type="danger" size="large">已淘汰</el-tag>
            <el-button v-if="delivery.status < 4" type="danger" @click="handleEliminate">淘汰</el-button>
            <el-divider direction="vertical" />
            <el-button :type="delivery.allowAIInterview ? 'warning' : 'info'" @click="handleToggleAIInterview" :loading="aiInterviewLoading">
              <el-icon><VideoCamera /></el-icon>
              {{ delivery.allowAIInterview ? '取消AI面试' : '允许AI面试' }}
            </el-button>
          </div>
        </div>
      </template>

      <el-tabs v-model="activeTab">
        <el-tab-pane label="基本信息" name="basic">
          <el-descriptions :column="2" border>
            <el-descriptions-item label="姓名">{{ delivery.candidateName }}</el-descriptions-item>
            <el-descriptions-item label="手机号">{{ delivery.phone }}</el-descriptions-item>
            <el-descriptions-item label="邮箱">{{ delivery.email || '-' }}</el-descriptions-item>
            <el-descriptions-item label="学历">{{ delivery.education || '-' }}</el-descriptions-item>
            <el-descriptions-item label="工作年限">{{ delivery.workYears ? `${delivery.workYears}年` : '-' }}</el-descriptions-item>
            <el-descriptions-item label="投递岗位">{{ delivery.jobTitle }}</el-descriptions-item>
            <el-descriptions-item label="投递时间">{{ formatDate(delivery.deliverTime) }}</el-descriptions-item>
            <el-descriptions-item label="状态">
              <el-tag :type="getStatusType(delivery.status)">{{ getStatusText(delivery.status) }}</el-tag>
            </el-descriptions-item>
          </el-descriptions>

          <template v-if="delivery.interview">
            <el-divider content-position="left">面试信息</el-divider>
            <el-descriptions :column="2" border size="small">
              <el-descriptions-item label="面试时间"><el-text type="primary">{{ formatDate(delivery.interview.scheduleTime) }}</el-text></el-descriptions-item>
              <el-descriptions-item label="面试轮次"><el-tag size="small">{{ delivery.interview.round || '初试' }}</el-tag></el-descriptions-item>
              <el-descriptions-item label="面试官">{{ delivery.interview.interviewerName }}</el-descriptions-item>
              <el-descriptions-item label="面试形式"><el-tag size="small" type="info">{{ delivery.interview.interviewType || '线上面试' }}</el-tag></el-descriptions-item>
            </el-descriptions>
          </template>
        </el-tab-pane>

        <!-- AI简历解析 -->
        <el-tab-pane label="AI简历解析" name="ai-parse">
          <div class="ai-tab-content" v-loading="resumeAiStore.parseLoading">
            <!-- 骨架屏 -->
            <template v-if="resumeAiStore.parseLoading && !parseResult">
              <div class="skeleton-block" v-for="i in 3" :key="i">
                <el-skeleton animated><el-skeleton-item variant="text" /><el-skeleton-item variant="text" style="width:60%" /></el-skeleton>
              </div>
            </template>
            <!-- 解析结果 -->
            <template v-else-if="parseResult">
              <div class="candidate-hero">
                <el-avatar :size="56">{{ parseResult.name?.charAt(0) || '?' }}</el-avatar>
                <div class="candidate-hero-info">
                  <div class="candidate-hero-name">{{ parseResult.name || '-' }}</div>
                  <div class="candidate-hero-meta">
                    {{ parseResult.education?.level || '-' }}
                    · {{ parseResult.workYears }}年经验
                    · {{ parseResult.education?.school || '' }}
                  </div>
                </div>
              </div>
              <!-- 基本信息 -->
              <div class="ai-card">
                <h4 class="ai-card-title">基本信息</h4>
                <div class="info-grid">
                  <div class="info-item"><span class="info-label">手机</span><span class="info-val">{{ parseResult.phone || '-' }}</span></div>
                  <div class="info-item"><span class="info-label">邮箱</span><span class="info-val">{{ parseResult.email || '-' }}</span></div>
                  <div class="info-item"><span class="info-label">学历</span><span class="info-val">{{ parseResult.education?.level || '-' }}</span></div>
                  <div class="info-item"><span class="info-label">专业</span><span class="info-val">{{ parseResult.education?.major || '-' }}</span></div>
                  <div class="info-item"><span class="info-label">学校</span><span class="info-val">{{ parseResult.education?.school || '-' }}</span></div>
                  <div class="info-item"><span class="info-label">工作年限</span><span class="info-val">{{ parseResult.workYears }}年</span></div>
                </div>
              </div>
              <!-- 技能 -->
              <div class="ai-card" v-if="parseResult.skills?.length">
                <h4 class="ai-card-title">技能识别</h4>
                <div class="skill-chips">
                  <el-tag v-for="s in parseResult.skills" :key="s"
                    type="primary" effect="light" size="default" style="margin:4px">{{ s }}</el-tag>
                </div>
              </div>
              <!-- 工作经历 -->
              <div class="ai-card" v-if="parseResult.workExperience?.length">
                <h4 class="ai-card-title">工作经历</h4>
                <el-timeline>
                  <el-timeline-item v-for="(exp, i) in parseResult.workExperience" :key="i"
                    :timestamp="exp.startDate + ' ~ ' + exp.endDate" placement="top">
                    <b>{{ exp.company }}</b> — {{ exp.title }}
                    <div class="exp-desc">{{ exp.description }}</div>
                  </el-timeline-item>
                </el-timeline>
              </div>
            </template>
            <el-empty v-else description="解析失败，请重试" :image-size="60" @click="loadParseResult">
              <el-button type="primary" @click="loadParseResult">重新解析</el-button>
            </el-empty>
          </div>
        </el-tab-pane>

        <!-- 智能匹配评分 -->
        <el-tab-pane label="智能匹配评分" name="ai-match">
          <div class="ai-tab-content" v-loading="resumeAiStore.matchLoading">
            <template v-if="resumeAiStore.matchLoading && !matchResult">
              <div class="skeleton-block"><el-skeleton animated><el-skeleton-item variant="circle" style="width:140px;height:140px" /></el-skeleton></div>
            </template>
            <template v-else-if="matchResult">
              <div class="match-hero">
                <svg class="score-ring" viewBox="0 0 140 140">
                  <circle cx="70" cy="70" r="62" fill="none" stroke="var(--color-border)" stroke-width="10" />
                  <circle cx="70" cy="70" r="62" fill="none" :stroke="matchScoreColor" stroke-width="10"
                    stroke-linecap="round" :stroke-dasharray="2 * Math.PI * 62"
                    :stroke-dashoffset="2 * Math.PI * 62 * (1 - matchResult.overall / 100)"
                    transform="rotate(-90 70 70)" style="transition: stroke-dashoffset 1s ease" />
                  <text x="70" y="65" text-anchor="middle" :fill="matchScoreColor" font-size="36" font-weight="700">{{ matchResult.overall }}</text>
                  <text x="70" y="90" text-anchor="middle" fill="var(--color-text-secondary)" font-size="13">综合分</text>
                </svg>
                <div class="match-sub-scores">
                  <div class="sub-score-item" v-for="s in subScores" :key="s.label">
                    <div class="sub-score-header"><span class="sub-label">{{ s.label }}</span><span class="sub-val">{{ s.value }}%</span></div>
                    <el-progress :percentage="s.value" :color="s.color" :show-text="false" :stroke-width="6" />
                  </div>
                </div>
              </div>
              <div class="ai-card">
                <h4 class="ai-card-title">AI 匹配分析</h4>
                <div class="match-analysis">
                  <div v-if="matchResult.strengths?.length" class="match-section">
                    <span class="match-dot green"></span><b>优势</b>
                    <ul><li v-for="s in matchResult.strengths" :key="s">{{ s }}</li></ul>
                  </div>
                  <div v-if="matchResult.gaps?.length" class="match-section">
                    <span class="match-dot red"></span><b>风险点</b>
                    <ul><li v-for="g in matchResult.gaps" :key="g">{{ g }}</li></ul>
                  </div>
                  <div v-if="matchResult.recommendation" class="match-section">
                    <span class="match-dot blue"></span><b>建议</b>
                    <p>{{ matchResult.recommendation }}</p>
                  </div>
                </div>
              </div>
            </template>
            <el-empty v-else description="评分失败，请重试" :image-size="60" @click="loadMatchResult">
              <el-button type="primary" @click="loadMatchResult">重新评分</el-button>
            </el-empty>
          </div>
        </el-tab-pane>

        <!-- 面试建议 -->
        <el-tab-pane label="面试建议" name="ai-guide">
          <div class="ai-tab-content" v-loading="resumeAiStore.guideLoading">
            <template v-if="resumeAiStore.guideLoading && !guideResult">
              <div class="skeleton-block" v-for="i in 3" :key="i">
                <el-skeleton animated><el-skeleton-item variant="text" /><el-skeleton-item variant="text" style="width:40%" /></el-skeleton>
              </div>
            </template>
            <template v-else-if="guideResult">
              <div class="ai-card guide-strategy-card">
                <h4 class="ai-card-title">面试策略建议</h4>
                <p>{{ guideResult.strategy }}</p>
                <div v-if="guideResult.focusTags?.length" style="margin-top:12px">
                  <span class="sub-label">重点考察：</span>
                  <el-tag v-for="t in guideResult.focusTags" :key="t" type="primary" effect="light" size="default" style="margin:2px">{{ t }}</el-tag>
                </div>
              </div>
              <div v-if="guideResult.warnings?.length" class="ai-card guide-warning-card">
                <h4 class="ai-card-title" style="color:var(--color-danger)">风险提示</h4>
                <ul><li v-for="w in guideResult.warnings" :key="w">{{ w }}</li></ul>
              </div>
              <div class="ai-card" v-if="guideResult.questions?.length">
                <h4 class="ai-card-title">面试问题</h4>
                <div v-for="(cat, catIdx) in groupedQuestions" :key="catIdx" style="margin-bottom:16px">
                  <h5 style="color:var(--color-primary);margin:0 0 8px">{{ cat.label }}</h5>
                  <div v-for="(q, qi) in cat.items" :key="qi" class="guide-q-item">
                    <span class="guide-q-num">{{ qi + 1 }}</span>
                    <div class="guide-q-body">
                      <div class="guide-q-text">{{ q.question }}</div>
                      <div class="guide-q-meta">
                        <el-tag size="small" :type="q.type === 'tech' ? 'success' : q.type === 'experience' ? 'info' : 'warning'">{{ typeLabel(q.type) }}</el-tag>
                        <span>{{ q.purpose }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </template>
            <el-empty v-else description="生成失败，请重试" :image-size="60" @click="loadGuideResult">
              <el-button type="primary" @click="loadGuideResult">重新生成</el-button>
            </el-empty>
          </div>
        </el-tab-pane>

        <el-tab-pane label="操作记录" name="logs">
          <el-timeline>
            <el-timeline-item v-for="(log, index) in logs" :key="index" :timestamp="log.time" placement="top">
              {{ log.action }}
            </el-timeline-item>
          </el-timeline>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-empty v-else-if="!loading" description="简历不存在" />

    <ScheduleInterviewDialog v-model="scheduleDialogVisible" :delivery="delivery" mode="create" @success="handleScheduleSuccess" />

    <!-- 开始实习对话框 -->
    <el-dialog v-model="internshipDialogVisible" title="开始实习" width="420px" destroy-on-close>
      <el-form :model="internshipForm" label-width="100px">
        <el-form-item label="实习岗位">
          <el-input v-model="internshipForm.position" :placeholder="delivery?.jobTitle || '请输入岗位'" />
        </el-form-item>
        <el-form-item label="开始日期">
          <el-date-picker v-model="internshipForm.startDate" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="导师/汇报人">
          <el-input v-model="internshipForm.mentor" placeholder="请输入导师姓名" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="internshipDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="internshipLoading" @click="handleStartInternship">确认开始实习</el-button>
      </template>
    </el-dialog>

    <!-- 正式入职对话框 -->
    <el-dialog v-model="hireDialogVisible" title="正式入职" width="420px" destroy-on-close>
      <el-form :model="hireForm" label-width="100px">
        <el-form-item label="正式职位">
          <el-input v-model="hireForm.position" :placeholder="delivery?.jobTitle || '请输入职位'" />
        </el-form-item>
        <el-form-item label="入职日期">
          <el-date-picker v-model="hireForm.hireDate" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="转正薪资(K)">
          <el-input-number v-model="hireForm.salary" :min="0" :max="200" :precision="1" placeholder="请输入转正薪资" style="width:100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="hireDialogVisible = false">取消</el-button>
        <el-button type="success" :loading="hireLoading" @click="handleFormalHire">确认入职</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useResumeStore } from '@/stores/resume'
import { useResumeAiStore } from '@/stores/resume-ai'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, VideoCamera, Promotion, Medal } from '@element-plus/icons-vue'
import dayjs from 'dayjs'
import { updateResumeStatus, cancelDelivery, setAIInterviewPermission, startInternship, formalHire } from '@/api/delivery'
import ScheduleInterviewDialog from '@/components/interview/ScheduleInterviewDialog.vue'
import type { ParseResult, MatchScoreResult, InterviewGuideResult } from '@/api/resume-ai'

const route = useRoute()
const router = useRouter()
const resumeStore = useResumeStore()
const resumeAiStore = useResumeAiStore()

const loading = computed(() => resumeStore.loading)
const delivery = computed(() => resumeStore.currentDelivery)
const activeTab = ref('basic')
const aiInterviewLoading = ref(false)
const scheduleDialogVisible = ref(false)

// ── AI Tab 懒加载状态 ──
const parseResult = ref<ParseResult | null>(null)
const matchResult = ref<MatchScoreResult | null>(null)
const guideResult = ref<InterviewGuideResult | null>(null)
const parseAttempted = ref(false)
const matchAttempted = ref(false)
const guideAttempted = ref(false)

const matchScoreColor = computed(() => {
  if (!matchResult.value) return 'var(--color-border)'
  const s = matchResult.value.overall
  if (s >= 80) return 'var(--color-success)'
  if (s >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
})

const subScores = computed(() => {
  if (!matchResult.value) return []
  const m = matchResult.value
  return [
    { label: '技能匹配', value: m.skillMatch, color: 'var(--color-success)' },
    { label: '经验匹配', value: m.experienceMatch, color: 'var(--color-primary)' },
    { label: '学历匹配', value: m.educationMatch, color: 'var(--color-warning)' },
    { label: '综合适配', value: m.fitScore, color: 'var(--color-accent)' },
  ]
})

const groupedQuestions = computed(() => {
  if (!guideResult.value?.questions) return []
  const groups: Record<string, { label: string; items: typeof guideResult.value.questions }> = {
    tech: { label: '技术能力', items: [] },
    experience: { label: '项目经验', items: [] },
    star: { label: 'STAR行为面试', items: [] },
  }
  guideResult.value.questions.forEach(q => {
    const t = q.type || 'tech'
    if (groups[t]) groups[t].items.push(q)
    else (groups['tech'] ??= { label: '其他', items: [] }).items.push(q)
  })
  return Object.values(groups).filter(g => g.items.length > 0)
})

function typeLabel(t: string) {
  return ({ tech: '技术', experience: '经验', star: 'STAR' } as Record<string, string>)[t] || t
}

const loadParseResult = async () => {
  if (!delivery.value || parseAttempted.value) return
  parseAttempted.value = true
  try { parseResult.value = await resumeAiStore.fetchParse(delivery.value.deliveryId) } catch { parseResult.value = null }
}

const loadMatchResult = async () => {
  if (!delivery.value || matchAttempted.value) return
  matchAttempted.value = true
  try { matchResult.value = await resumeAiStore.fetchMatch(delivery.value.deliveryId, delivery.value.jobId) } catch { matchResult.value = null }
}

const loadGuideResult = async () => {
  if (!delivery.value || guideAttempted.value) return
  guideAttempted.value = true
  try { guideResult.value = await resumeAiStore.fetchGuide(delivery.value.deliveryId, delivery.value.jobId) } catch { guideResult.value = null }
}

// Tab 切换懒加载
watch(activeTab, (tab) => {
  if (tab === 'ai-parse') loadParseResult()
  else if (tab === 'ai-match') loadMatchResult()
  else if (tab === 'ai-guide') loadGuideResult()
})

// 实习 & 正式入职
const internshipDialogVisible = ref(false)
const internshipLoading = ref(false)
const internshipForm = reactive({ position: '', startDate: '', mentor: '' })
const hireDialogVisible = ref(false)
const hireLoading = ref(false)
const hireForm = reactive({ position: '', hireDate: '', salary: undefined as number | undefined })

// ── 操作日志 ──
const logs = ref([{ time: dayjs().format('YYYY-MM-DD HH:mm'), action: '简历投递成功' }])

const fetchDetail = async (id: number) => {
  await resumeStore.fetchResumeDetail(id)
  if (delivery.value && delivery.value.status === 0) {
    try { await updateResumeStatus(id, { status: 1 }); resumeStore.fetchResumeDetail(id) } catch {}
  }
  if (route.query.schedule === 'true') scheduleDialogVisible.value = true
}

onMounted(async () => {
  const id = Number(route.params.id)
  await fetchDetail(id)
})

watch(() => route.params.id, async (newId) => {
  if (newId) await fetchDetail(Number(newId))
})

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')
const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  return (['info','info','warning','primary','success','danger'] as const)[status] || 'info'
}
const getStatusText = (status: number) => ['待查看','已查看','面试中','实习中','正式入职','已淘汰'][status] || '未知'
const downloadResume = () => { if (delivery.value?.resumeUrl) window.open(delivery.value.resumeUrl) }
const handleScheduleInterview = () => { scheduleDialogVisible.value = true }
const handleScheduleSuccess = () => {
  resumeStore.fetchResumeDetail(Number(route.params.id))
  logs.value.unshift({ time: dayjs().format('YYYY-MM-DD HH:mm'), action: '已安排面试' })
}

const handleEliminate = async () => {
  try {
    await ElMessageBox.confirm('确定要淘汰该简历吗？', '淘汰确认', { confirmButtonText: '确定淘汰', cancelButtonText: '取消', type: 'warning' })
    const id = Number(route.params.id)
    await updateResumeStatus(id, { status: 5, remark: 'HR淘汰' })
    await cancelDelivery(id)
    ElMessage.success('简历已淘汰')
    router.push('/admin/resumes')
  } catch (error: any) { if (error !== 'cancel') ElMessage.error('淘汰失败') }
}

const handleToggleAIInterview = async () => {
  if (!delivery.value) return
  const currentAllow = delivery.value.allowAIInterview
  try {
    await ElMessageBox.confirm(`确定要${currentAllow ? '取消' : '允许'}该候选人的AI面试吗？`, `${currentAllow ? '取消' : '允许'}AI面试确认`, { confirmButtonText: '确定', cancelButtonText: '取消', type: currentAllow ? 'warning' : 'info' })
    aiInterviewLoading.value = true
    let deadline: string | undefined
    if (!currentAllow) { const d = new Date(); d.setDate(d.getDate() + 7); deadline = d.toISOString() }
    await setAIInterviewPermission(delivery.value.deliveryId, !currentAllow, deadline)
    await resumeStore.fetchResumeDetail(Number(route.params.id))
    logs.value.unshift({ time: dayjs().format('YYYY-MM-DD HH:mm'), action: currentAllow ? '已取消AI面试权限' : '已允许AI面试' })
    ElMessage.success(currentAllow ? '已取消AI面试权限' : '已允许AI面试')
  } catch (error: any) { if (error !== 'cancel') ElMessage.error('操作失败') }
  finally { aiInterviewLoading.value = false }
}

const handleStartInternship = async () => {
  if (!delivery.value) return
  internshipLoading.value = true
  try {
    await startInternship(delivery.value.deliveryId, {
      position: internshipForm.position || undefined,
      startDate: internshipForm.startDate || undefined,
      mentor: internshipForm.mentor || undefined,
    })
    ElMessage.success('已开始实习')
    internshipDialogVisible.value = false
    logs.value.unshift({ time: dayjs().format('YYYY-MM-DD HH:mm'), action: '开始实习' })
    await resumeStore.fetchResumeDetail(Number(route.params.id))
  } catch (error: any) {
    ElMessage.error(error.message || '操作失败')
  } finally { internshipLoading.value = false }
}

const handleFormalHire = async () => {
  if (!delivery.value) return
  hireLoading.value = true
  try {
    await formalHire(delivery.value.deliveryId, {
      position: hireForm.position || undefined,
      hireDate: hireForm.hireDate || undefined,
      salary: hireForm.salary,
    })
    ElMessage.success('已正式入职')
    hireDialogVisible.value = false
    logs.value.unshift({ time: dayjs().format('YYYY-MM-DD HH:mm'), action: '正式入职' })
    await resumeStore.fetchResumeDetail(Number(route.params.id))
  } catch (error: any) {
    ElMessage.error(error.message || '操作失败')
  } finally { hireLoading.value = false }
}
</script>

<style scoped lang="scss">
.resume-detail-container {
  .back-btn { margin-bottom: var(--space-5); }
  .card-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: var(--space-3); }
}

// ── AI Tab 通用样式 ──
.ai-tab-content {
  min-height: 300px;
}

.skeleton-block {
  padding: var(--space-4);
  background: var(--color-bg-alt);
  border-radius: var(--radius-md);
  margin-bottom: var(--space-3);
}

// ── 候选人卡片 Hero ──
.candidate-hero {
  display: flex; align-items: center; gap: var(--space-4);
  padding: var(--space-5);
  background: var(--gradient-primary);
  border-radius: var(--radius-lg);
  margin-bottom: var(--space-4);
  .candidate-hero-info {
    .candidate-hero-name { font-size: 20px; font-weight: 700; color: var(--color-text-inverse); }
    .candidate-hero-meta { font-size: 13px; color: rgba(255,255,255,0.7); margin-top: 2px; }
  }
}

// ── AI卡片 ──
.ai-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-4);
  margin-bottom: var(--space-3);
  .ai-card-title {
    font-size: 15px; font-weight: 600; color: var(--color-text);
    margin: 0 0 var(--space-3); padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--color-border-light);
  }
}

// ── 基本信息网格 ──
.info-grid {
  display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--space-3);
  .info-item {
    padding: var(--space-2);
    background: var(--color-bg-alt);
    border-radius: var(--radius-sm);
    .info-label { display: block; font-size: 12px; color: var(--color-text-muted); margin-bottom: 2px; }
    .info-val { font-size: 14px; color: var(--color-text); font-weight: 500; }
  }
}

.skill-chips { display: flex; flex-wrap: wrap; }

.exp-desc { font-size: 13px; color: var(--color-text-secondary); margin-top: 4px; line-height: 1.5; }

// ── 匹配评分 Hero ──
.match-hero {
  display: flex; align-items: center; gap: var(--space-6);
  padding: var(--space-5);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  margin-bottom: var(--space-4);
  flex-wrap: wrap;
  .score-ring { width: 140px; height: 140px; flex-shrink: 0; }
  .match-sub-scores { flex: 1; min-width: 220px; display: flex; flex-direction: column; gap: var(--space-3); }
}

.sub-score-item {
  .sub-score-header { display: flex; justify-content: space-between; margin-bottom: 2px;
    .sub-label { font-size: 13px; color: var(--color-text-secondary); }
    .sub-val { font-size: 13px; font-weight: 600; color: var(--color-text); }
  }
}

// ── 匹配分析 ──
.match-analysis {
  .match-section {
    margin-bottom: var(--space-3);
    b { font-size: 14px; color: var(--color-text); display: block; margin-bottom: 4px; }
    ul { margin: 0; padding-left: 20px;
      li { font-size: 13px; color: var(--color-text-secondary); line-height: 1.6; }
    }
    p { font-size: 13px; color: var(--color-text-secondary); line-height: 1.6; margin: 4px 0 0; }
  }
}

.match-dot {
  display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px;
  &.green { background: var(--color-success); }
  &.red { background: var(--color-danger); }
  &.blue { background: var(--color-primary); }
}

// ── 面试建议 ──
.guide-strategy-card {
  p { font-size: 14px; color: var(--color-text-secondary); line-height: 1.7; margin: 0; }
}
.guide-warning-card {
  border-color: var(--color-danger-light);
  ul { margin: 0; padding-left: 20px;
    li { color: var(--color-danger); font-size: 13px; line-height: 1.6; }
  }
}

.guide-q-item {
  display: flex; gap: var(--space-3); padding: var(--space-2) 0;
  border-bottom: 1px solid var(--color-border-light);
  &:last-child { border-bottom: none; }
  .guide-q-num {
    width: 26px; height: 26px; border-radius: 50%;
    background: var(--color-primary-bg); color: var(--color-primary);
    display: flex; align-items: center; justify-content: center;
    font-size: 12px; font-weight: 600; flex-shrink: 0;
  }
  .guide-q-body {
    flex: 1;
    .guide-q-text { font-size: 14px; color: var(--color-text); line-height: 1.5; }
    .guide-q-meta { display: flex; align-items: center; gap: var(--space-2); margin-top: 4px;
      span { font-size: 12px; color: var(--color-text-muted); }
    }
  }
}

.sub-label { font-size: 13px; color: var(--color-text-secondary); }
</style>
