<template>
  <div class="smart-screening-container">
    <!-- ═══ 工具栏 ═══ -->
    <div class="toolbar">
      <el-input v-model="searchParams.keyword" placeholder="搜索姓名/手机号" clearable style="width: 200px" @change="fetchResumes" />
      <el-select v-model="searchParams.status" placeholder="投递状态" clearable style="width: 120px" @change="fetchResumes">
        <el-option label="待查看" :value="0" />
        <el-option label="已查看" :value="1" />
        <el-option label="面试中" :value="2" />
        <el-option label="实习中" :value="3" />
        <el-option label="正式入职" :value="4" />
        <el-option label="已淘汰" :value="5" />
      </el-select>
      <el-select v-model="searchParams.jobId" placeholder="投递岗位" clearable style="width: 150px" @change="fetchResumes">
        <el-option v-for="job in jobs" :key="job.jobId" :label="job.title" :value="job.jobId" />
      </el-select>
      <el-button type="primary" @click="fetchResumes">搜索</el-button>
      <el-button type="warning" :loading="sortingLoading" @click="handleSmartSort">
        <el-icon><Sort /></el-icon> AI 智能排序
      </el-button>
      <el-radio-group v-model="viewMode" size="small" style="margin-left:auto">
        <el-radio-button value="table"><el-icon><List /></el-icon></el-radio-button>
        <el-radio-button value="kanban"><el-icon><Grid /></el-icon></el-radio-button>
      </el-radio-group>
    </div>

    <!-- ═══ 批量操作栏 ═══ -->
    <div v-if="selectedIds.length > 0" class="batch-bar">
      <span class="batch-count">已选 {{ selectedIds.length }} 项</span>
      <el-button type="success" size="small" @click="handleBatchOperation(1)">批量已查看</el-button>
      <el-button type="warning" size="small" @click="handleBatchOperation(2)">批量面试中</el-button>
      <el-button type="primary" size="small" @click="handleBatchOperation(3)">批量开始实习</el-button>
      <el-button type="success" size="small" @click="handleBatchOperation(4)">批量正式入职</el-button>
      <el-button type="danger" size="small" @click="handleBatchOperation(5)">批量淘汰</el-button>
      <el-button size="small" @click="selectedIds = []">取消选择</el-button>
    </div>

    <!-- ═══ 表格/看板内容 + 内联AI面板 ═══ -->
    <div class="screening-main" :class="{ 'has-panel': inlinePanelVisible }">
      <div class="screening-content" :class="{ 'panel-open': inlinePanelVisible }">
        <!-- 看板视图 -->
        <div v-if="viewMode === 'kanban'" v-loading="loading" class="kanban-board">
          <div v-for="col in kanbanColumns" :key="col.status" class="kanban-column">
            <div class="kanban-col-header" :style="{ borderTopColor: col.color }">
              <span class="kanban-col-title">{{ col.label }}</span>
              <el-tag :color="col.color" effect="dark" size="small" round>{{ col.items.length }}</el-tag>
            </div>
            <div class="kanban-col-body">
              <div v-for="item in col.items" :key="item.deliveryId" class="kanban-card" @click="openInlinePanel(item)">
                <div class="kanban-card-top">
                  <span class="kanban-card-name">{{ item.candidateName }}</span>
                  <el-tag :type="getStatusTagType(item.status)" size="small" round>{{ getStatusText(item.status) }}</el-tag>
                </div>
                <div class="kanban-card-job">{{ item.jobTitle }}</div>
                <div class="kanban-card-meta">
                  <span>{{ item.education || '-' }}</span>
                  <span v-if="item.workYears">{{ item.workYears }}年</span>
                  <span class="kanban-card-time">{{ formatShortDate(item.deliverTime) }}</span>
                </div>
                <div class="kanban-card-actions" @click.stop>
                  <template v-if="col.status < 4">
                    <el-button size="small" text type="primary" @click="quickChangeStatus(item, col.status + 1)">
                      → {{ kanbanColumns[col.status + 1]?.label.split('（')[0] || '下一阶段' }}
                    </el-button>
                  </template>
                  <el-button v-if="col.status < 5" size="small" text type="danger" @click="quickChangeStatus(item, 5)">淘汰</el-button>
                  <el-button size="small" text type="primary" @click.stop="$router.push(`/admin/resumes/${item.deliveryId}`)">详情 →</el-button>
                </div>
              </div>
              <div v-if="col.items.length === 0" class="kanban-col-empty">暂无候选人</div>
            </div>
          </div>
        </div>

        <!-- 表格视图 -->
        <el-card v-else v-loading="loading">
          <el-table :data="deliveries" stripe @row-click="openInlinePanel" @selection-change="handleSelectionChange" ref="tableRef" highlight-current-row>
            <el-table-column type="selection" width="40" />
            <el-table-column label="#" width="50">
              <template #default="{ $index }">{{ String((searchParams.page - 1) * searchParams.pageSize + $index + 1).padStart(3, '0') }}</template>
            </el-table-column>
            <el-table-column prop="candidateName" label="姓名" width="90" />
            <el-table-column prop="phone" label="手机号" width="125" />
            <el-table-column prop="jobTitle" label="投递岗位" width="170" show-overflow-tooltip />
            <el-table-column prop="education" label="学历" width="70" />
            <el-table-column prop="workYears" label="年限" width="70">
              <template #default="{ row }">{{ row.workYears ? `${row.workYears}年` : '-' }}</template>
            </el-table-column>
            <el-table-column prop="deliverTime" label="投递时间" width="150">
              <template #default="{ row }">{{ formatDate(row.deliverTime) }}</template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="85">
              <template #default="{ row }">
                <el-tag :type="getStatusType(row.status)" size="small">{{ getStatusText(row.status) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="AI操作" width="270" fixed="right">
              <template #default="{ row }">
                <div class="ai-actions">
                  <el-button size="small" type="primary" :loading="aiLoading === row.deliveryId && inlinePanelType === 'analyze'" @click.stop="handleInlineAction(row, 'analyze')">
                    <el-icon><MagicStick /></el-icon>解析
                  </el-button>
                  <el-button size="small" type="success" :loading="aiLoading === row.deliveryId && inlinePanelType === 'score'" @click.stop="handleInlineAction(row, 'score')">
                    <el-icon><TrendCharts /></el-icon>评分
                  </el-button>
                  <el-button size="small" type="primary" plain @click.stop="$router.push(`/admin/resumes/${row.deliveryId}`)">详情</el-button>
                </div>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </div>

      <!-- ═══ 内联AI分析面板 ═══ -->
      <transition name="panel-slide">
        <div v-if="inlinePanelVisible" class="inline-panel">
          <div class="panel-header">
            <div class="panel-candidate-info">
              <span class="panel-candidate-name">{{ activeCandidate?.candidateName }}</span>
              <span class="panel-candidate-job">{{ activeCandidate?.jobTitle }}</span>
            </div>
            <el-button :icon="Close" text @click="closeInlinePanel" />
          </div>

          <el-tabs v-model="inlinePanelType" class="panel-tabs" @tab-change="onPanelTabChange">
            <el-tab-pane label="AI简历解析" name="analyze" />
            <el-tab-pane label="智能匹配评分" name="score" />
            <el-tab-pane label="面试建议" name="question" />
          </el-tabs>

          <div class="panel-body" v-loading="aiLoading === activeCandidate?.deliveryId">
            <!-- AI简历解析 -->
            <template v-if="inlinePanelType === 'analyze' && panelResult">
              <div class="tpr-header">
                <div class="tpr-score-ring">
                  <el-progress type="circle" :percentage="panelResult.matchScore || 0" :color="scoreColor(panelResult.matchScore || 50)" :width="72" :stroke-width="6" />
                  <div class="tpr-score-label">综合匹配</div>
                </div>
                <div class="tpr-meta">
                  <div class="tpr-name">{{ panelResult.name || activeCandidate?.candidateName || '-' }}</div>
                  <div class="tpr-info">{{ panelResult.education || '-' }} · {{ panelResult.workYears || 0 }}年经验</div>
                  <div class="tpr-tags">
                    <el-tag size="small" :type="panelResult.hiringSuggestion?.includes('录用') ? 'success' : 'warning'" round>
                      {{ panelResult.hiringSuggestion || '待评估' }}
                    </el-tag>
                  </div>
                </div>
              </div>

              <div class="tpr-section">
                <div class="tpr-section-title"><el-icon><MagicStick /></el-icon> 技能匹配</div>
                <div class="skill-match-grid">
                  <div class="skill-col matched">
                    <div class="skill-col-title">已匹配 ({{ (panelResult.matchedSkills || []).length }})</div>
                    <el-tag v-for="(s, i) in (panelResult.matchedSkills || []).slice(0, 6)" :key="'m'+i" type="success" effect="plain" size="small" style="margin:2px">
                      {{ typeof s === 'string' ? s : s.skill }}
                    </el-tag>
                  </div>
                  <div class="skill-col missing">
                    <div class="skill-col-title">待提升 ({{ (panelResult.missingSkills || []).length }})</div>
                    <el-tag v-for="(s, i) in (panelResult.missingSkills || []).slice(0, 6)" :key="'x'+i" type="danger" effect="plain" size="small" style="margin:2px">
                      {{ typeof s === 'string' ? s : s.skill || s }}
                    </el-tag>
                  </div>
                </div>
              </div>

              <div class="tpr-section" v-if="panelResult.strengths?.length || panelResult.weaknesses?.length">
                <div class="tpr-section-title"><el-icon><TrendCharts /></el-icon> 匹配分析</div>
                <div class="sw-grid">
                  <div class="sw-col" v-if="panelResult.strengths?.length">
                    <div class="sw-col-title">优势</div>
                    <div v-for="(s, i) in panelResult.strengths.slice(0, 4)" :key="'st'+i" class="sw-item">{{ s }}</div>
                  </div>
                  <div class="sw-col" v-if="panelResult.weaknesses?.length">
                    <div class="sw-col-title">关注点</div>
                    <div v-for="(w, i) in panelResult.weaknesses.slice(0, 4)" :key="'wk'+i" class="sw-item">{{ w }}</div>
                  </div>
                </div>
              </div>

              <div class="tpr-section" v-if="panelResult.interviewQuestions?.length">
                <div class="tpr-section-title"><el-icon><ChatDotRound /></el-icon> 面试建议</div>
                <div v-for="(q, i) in panelResult.interviewQuestions.slice(0, 3)" :key="'q'+i" class="iq-item">
                  <div class="iq-num">{{ i + 1 }}</div>
                  <div class="iq-body">
                    <div class="iq-question">{{ q.question }}</div>
                    <div class="iq-meta"><el-tag size="small">{{ q.category }}</el-tag> {{ q.purpose }}</div>
                  </div>
                </div>
              </div>

              <div class="tpr-section" v-if="panelResult.workExperience?.length">
                <div class="tpr-section-title"><el-icon><Briefcase /></el-icon> 工作经历</div>
                <el-timeline>
                  <el-timeline-item v-for="(exp, i) in panelResult.workExperience.slice(0, 3)" :key="'we'+i" :timestamp="exp.startDate || exp.duration || ''" placement="top">
                    <b>{{ exp.company || exp.position }}</b>
                    <span v-if="exp.company && exp.position"> — {{ exp.position }}</span>
                    <div style="color:var(--color-text-secondary);font-size:12px;margin-top:4px">{{ exp.description }}</div>
                  </el-timeline-item>
                </el-timeline>
              </div>
            </template>

            <!-- 智能匹配评分 -->
            <template v-else-if="inlinePanelType === 'score' && panelResult">
              <div class="score-main-card">
                <div class="score-ring-wrapper">
                  <el-progress type="circle" :percentage="panelResult.score || 0" :color="scoreColor(panelResult.score || 0)" :width="120" :stroke-width="10" :show-text="false" />
                  <div class="score-content">
                    <span class="score-value" :style="{ color: scoreColor(panelResult.score || 0) }">{{ panelResult.score || 0 }}</span>
                    <span class="score-unit">分</span>
                    <span class="score-text">综合匹配度</span>
                  </div>
                </div>
              </div>

              <div class="detail-items" style="margin-top:16px">
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">技能匹配</span>
                    <span class="item-value skill-score">{{ panelResult.skillScore || panelResult.strengths?.length * 10 || 70 }}%</span>
                  </div>
                  <el-progress :percentage="scorePercent.skill" color="var(--color-success)" :show-text="false" :stroke-width="6" />
                </div>
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">经验匹配</span>
                    <span class="item-value exp-score">{{ panelResult.expScore || 75 }}%</span>
                  </div>
                  <el-progress :percentage="scorePercent.exp" color="var(--color-primary)" :show-text="false" :stroke-width="6" />
                </div>
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">学历匹配</span>
                    <span class="item-value edu-score">{{ panelResult.eduScore || 85 }}%</span>
                  </div>
                  <el-progress :percentage="scorePercent.edu" color="var(--color-warning)" :show-text="false" :stroke-width="6" />
                </div>
              </div>

              <div v-if="panelResult.strengths?.length" style="margin-top:16px">
                <div class="section-label success">优势</div>
                <el-tag v-for="(s, i) in panelResult.strengths" :key="'st'+i" type="success" effect="light" style="margin:2px">{{ s }}</el-tag>
              </div>
              <div v-if="panelResult.weaknesses?.length" style="margin-top:12px">
                <div class="section-label danger">风险点</div>
                <el-tag v-for="(w, i) in panelResult.weaknesses" :key="'wk'+i" type="warning" effect="light" style="margin:2px">{{ w }}</el-tag>
              </div>
            </template>

            <!-- 面试建议 -->
            <template v-else-if="inlinePanelType === 'question' && panelResult">
              <div v-for="(qs, cat) in (panelResult.questions || panelResult)" :key="cat" style="margin-bottom:16px">
                <h4 style="color:var(--color-primary);margin:0 0 8px">{{ catLabels[String(cat)] || String(cat) }}</h4>
                <div v-for="(q, i) in (Array.isArray(qs) ? qs : [])" :key="i" class="question-item">
                  <span class="q-num">{{ i + 1 }}</span>
                  <span class="q-text">{{ typeof q === 'string' ? q : q.question || q }}</span>
                </div>
              </div>
            </template>

            <!-- 空状态 -->
            <el-empty v-if="!panelResult && aiLoading !== activeCandidate?.deliveryId" description="点击上方按钮开始AI分析" :image-size="60" />
          </div>

          <!-- 操作栏 -->
          <div class="panel-actions" v-if="panelResult">
            <el-button type="success" size="small" :loading="actionLoading === 'review'" @click="handlePanelAction('review')">
              <el-icon><Check /></el-icon>通过筛选
            </el-button>
            <el-button type="primary" size="small" :loading="actionLoading === 'interview'" @click="handlePanelAction('interview')">
              <el-icon><Calendar /></el-icon>安排面试
            </el-button>
            <el-button type="danger" size="small" plain :loading="actionLoading === 'reject'" @click="handlePanelAction('reject')">
              <el-icon><Close /></el-icon>淘汰
            </el-button>
          </div>
        </div>
      </transition>
    </div>

    <!-- ═══ 分页 ═══ -->
    <div class="pagination">
      <el-pagination
        v-model:current-page="searchParams.page"
        v-model:page-size="searchParams.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50]"
        layout="total, sizes, prev, pager, next"
        @change="fetchResumes"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { MagicStick, TrendCharts, ChatDotRound, Sort, Grid, List, Close, Check, Calendar, Briefcase } from '@element-plus/icons-vue'
import { useResumeStore } from '@/stores/resume'
import { useJobStore } from '@/stores/job'
import { analyzeResume, scoreResume, generateQuestions } from '@/api/ai'
import { batchScore, batchOperation } from '@/api/delivery'
import type { DeliveryStatus } from '@/api/delivery/types'
import { parseAIResponse } from '@/utils/ai-parse-helper'
import dayjs from 'dayjs'

const resumeStore = useResumeStore()
const jobStore = useJobStore()

const loading = computed(() => resumeStore.loading)
const deliveries = computed(() => resumeStore.deliveries)
const total = computed(() => resumeStore.total)
const jobs = computed(() => jobStore.jobs)

const viewMode = ref<'table' | 'kanban'>('table')
const aiLoading = ref<number | null>(null)
const sortingLoading = ref(false)
const tableRef = ref()
const selectedIds = ref<number[]>([])

const searchParams = reactive({
  page: 1,
  pageSize: 15,
  keyword: '',
  status: undefined as DeliveryStatus | undefined,
  jobId: undefined as number | undefined,
})

// ── 内联面板状态 ──
const inlinePanelVisible = ref(false)
const inlinePanelType = ref('analyze')
const panelResult = ref<any>(null)
const activeCandidate = ref<any>(null)
const actionLoading = ref<string | null>(null)

onMounted(() => {
  fetchResumes()
  jobStore.fetchJobs({ page: 1, pageSize: 100, status: 1 })
})

const fetchResumes = () => {
  resumeStore.fetchResumes(searchParams)
}

// ── 批量操作 ──
const handleSelectionChange = (rows: any[]) => {
  selectedIds.value = rows.map(r => r.deliveryId)
}

const handleBatchOperation = async (status: number) => {
  if (selectedIds.value.length === 0) { ElMessage.warning('请先选择候选人'); return }
  try {
    await batchOperation(selectedIds.value, status)
    ElMessage.success('批量操作成功')
    selectedIds.value = []
    fetchResumes()
  } catch (e: any) {
    ElMessage.error(e.message || '批量操作失败')
  }
}

// ── AI 智能排序 ──
const handleSmartSort = async () => {
  const ids = deliveries.value.slice(0, 20).map((d: any) => d.deliveryId)
  if (ids.length === 0) { ElMessage.warning('当前列表无候选人'); return }
  sortingLoading.value = true
  try {
    const res = await batchScore(ids)
    if (res && Array.isArray(res)) {
      const scoreMap = new Map(res.map((item: any) => [item.deliveryId, item.score]))
      deliveries.value.sort((a: any, b: any) => (scoreMap.get(b.deliveryId) || 0) - (scoreMap.get(a.deliveryId) || 0))
      ElMessage.success('已按 AI 匹配度从高到低排序')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '排序失败')
  } finally {
    sortingLoading.value = false
  }
}

// ── 看板列 ──
const kanbanColumns = computed(() => {
  const cols = [
    { status: 0, label: '待查看', color: '#F59E0B', items: [] as any[] },
    { status: 1, label: '已查看', color: '#3B82F6', items: [] as any[] },
    { status: 2, label: '面试中', color: '#8B5CF6', items: [] as any[] },
    { status: 3, label: '实习中', color: '#06B6D4', items: [] as any[] },
    { status: 4, label: '正式入职', color: '#059669', items: [] as any[] },
    { status: 5, label: '已淘汰', color: '#DC2626', items: [] as any[] },
  ]
  deliveries.value.forEach((d: any) => {
    const col = cols.find(c => c.status === d.status)
    if (col) col.items.push(d)
  })
  return cols
})

const getStatusTagType = (status: number): 'warning' | 'info' | 'success' | 'danger' | 'primary' | undefined => {
  const types: Array<'warning' | 'info' | 'success' | 'danger' | 'primary' | undefined> = ['warning', 'info', undefined, 'primary', 'success', 'danger']
  return types[status]
}

const quickChangeStatus = async (item: any, newStatus: number) => {
  try {
    await batchOperation([item.deliveryId], newStatus)
    ElMessage.success(`已移至「${kanbanColumns.value[newStatus]?.label || ''}」`)
    fetchResumes()
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
  }
}

const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' | undefined => {
  const types: Array<'primary' | 'success' | 'warning' | 'info' | 'danger' | undefined> = ['info', undefined, 'warning', 'primary', 'success', 'danger']
  return types[status]
}

const getStatusText = (status: number) => {
  const texts = ['待查看', '已查看', '面试中', '实习中', '正式入职', '已淘汰']
  return texts[status] || '未知'
}

const scoreColor = (s: number) => s >= 80 ? '#10B981' : s >= 60 ? '#F59E0B' : '#EF4444'

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')
const formatShortDate = (date: string) => date ? dayjs(date).format('MM-DD') : '-'
const catLabels: Record<string, string> = { technical: '技术能力', behavioral: '行为面试', scenario: '情景题' }

// ── 评分面板计算属性 ──
const scorePercent = computed(() => {
  const v = panelResult.value
  if (!v) return { skill: 70, exp: 75, edu: 85 }
  return {
    skill: Number(v.skillScore || v.strengths?.length * 10 || 70),
    exp: Number(v.expScore || 75),
    edu: Number(v.eduScore || 85),
  }
})

// ── 内联面板操作 ──
const openInlinePanel = (row: any) => {
  activeCandidate.value = row
  inlinePanelVisible.value = true
  panelResult.value = null
  inlinePanelType.value = 'analyze'
}

const closeInlinePanel = () => {
  inlinePanelVisible.value = false
  panelResult.value = null
  activeCandidate.value = null
}

const onPanelTabChange = (tab: string | number) => {
  const tabName = String(tab)
  if (tabName !== inlinePanelType.value) {
    inlinePanelType.value = tabName
    panelResult.value = null
  }
}

const handleInlineAction = (row: any, type: string) => {
  openInlinePanel(row)
  inlinePanelType.value = type
  if (type === 'analyze') runAnalyze()
  else if (type === 'score') runScore()
  else if (type === 'question') runQuestion()
}

const runAnalyze = async () => {
  if (!activeCandidate.value) return
  const id = activeCandidate.value.deliveryId
  aiLoading.value = id
  panelResult.value = null
  try {
    const res = await analyzeResume(id)
    let data = res
    if (typeof data === 'string') { try { data = JSON.parse(data) } catch {} }
    panelResult.value = data
    ElMessage.success('AI分析完成')
  } catch (e: any) {
    ElMessage.error(e.message || '解析失败')
  } finally {
    aiLoading.value = null
  }
}

const runScore = async () => {
  if (!activeCandidate.value) return
  const id = activeCandidate.value.deliveryId
  aiLoading.value = id
  panelResult.value = null
  try {
    const res = await scoreResume(id)
    const parsed = parseAIResponse(res)
    panelResult.value = parsed || res
    ElMessage.success('评分完成')
  } catch (e: any) {
    ElMessage.error(e.message || '评分失败')
  } finally {
    aiLoading.value = null
  }
}

const runQuestion = async () => {
  if (!activeCandidate.value) return
  const id = activeCandidate.value.deliveryId
  aiLoading.value = id
  panelResult.value = null
  try {
    const res = await generateQuestions(id)
    const parsed = parseAIResponse(res)
    panelResult.value = parsed?.questions || parsed || res
    ElMessage.success('面试题已生成')
  } catch (e: any) {
    ElMessage.error(e.message || '生成失败')
  } finally {
    aiLoading.value = null
  }
}

const handlePanelAction = async (action: string) => {
  if (!activeCandidate.value) return
  const statusMap: Record<string, number> = { review: 1, interview: 1, reject: 5 }
  const msgMap: Record<string, string> = { review: '已标记为通过筛选', interview: '已通过筛选，请安排面试', reject: '已淘汰' }
  actionLoading.value = action
  try {
    await batchOperation([activeCandidate.value.deliveryId], statusMap[action])
    ElMessage.success(msgMap[action])
    closeInlinePanel()
    fetchResumes()
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
  } finally {
    actionLoading.value = null
  }
}
</script>

<style scoped lang="scss">
.smart-screening-container {
  max-width: var(--content-max-width);

  .toolbar {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
    padding: var(--space-2) 0;
    flex-wrap: wrap;
  }

  // AI操作按钮行
  .ai-actions {
    display: flex;
    flex-direction: row;
    align-items: center;
    gap: 6px;
    flex-wrap: nowrap;
  }

  .batch-bar {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 16px;
    margin-bottom: 12px;
    background: var(--color-primary-bg);
    border: 1px solid var(--color-border-glow);
    border-radius: var(--radius-md);

    .batch-count {
      font-weight: var(--weight-semibold);
      color: var(--color-text);
      margin-right: 4px;
    }
  }

  // ── 主区域：表格 + 内联面板 ──
  .screening-main {
    display: flex;
    gap: 0;
    position: relative;
  }

  .screening-content {
    flex: 1;
    min-width: 0;
    transition: margin-right var(--duration-slow) var(--ease-out);

    &.panel-open {
      margin-right: 0;
    }
  }

  // ── 内联AI面板 ──
  .inline-panel {
    width: 460px;
    flex-shrink: 0;
    background: var(--color-surface);
    backdrop-filter: blur(20px);
    -webkit-backdrop-filter: blur(20px);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    box-shadow: var(--shadow-glow);
    margin-left: var(--space-4);
    display: flex;
    flex-direction: column;
    max-height: calc(100vh - 240px);
    overflow: hidden;
  }

  .panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--space-3) var(--space-4);
    border-bottom: 1px solid var(--color-border-light);
    background: linear-gradient(135deg, var(--color-primary-bg), var(--color-surface));

    .panel-candidate-info {
      display: flex;
      align-items: center;
      gap: var(--space-2);

      .panel-candidate-name {
        font-weight: var(--weight-semibold);
        color: var(--color-text);
      }
      .panel-candidate-job {
        font-size: var(--text-xs);
        color: var(--color-text-secondary);
      }
    }
  }

  .panel-tabs {
    :deep(.el-tabs__header) {
      margin: 0 var(--space-3);
    }
  }

  .panel-body {
    flex: 1;
    overflow-y: auto;
    padding: var(--space-3) var(--space-4);
  }

  .panel-actions {
    padding: var(--space-3) var(--space-4);
    border-top: 1px solid var(--color-border-light);
    display: flex;
    gap: var(--space-2);
    background: var(--color-bg-alt);
  }

  // ── 面板滑入动画 ──
  .panel-slide-enter-active,
  .panel-slide-leave-active {
    transition: all var(--duration-normal) var(--ease-out);
  }
  .panel-slide-enter-from {
    opacity: 0;
    transform: translateX(20px);
    width: 0;
  }
  .panel-slide-leave-to {
    opacity: 0;
    transform: translateX(20px);
    width: 0;
  }

  // ── 看板视图 ──
  .kanban-board {
    display: grid;
    grid-template-columns: repeat(6, 1fr);
    gap: var(--space-3);
    overflow-x: auto;
    padding-bottom: var(--space-3);

    @media (max-width: 1400px) { grid-template-columns: repeat(3, 1fr); }
    @media (max-width: 768px) { grid-template-columns: repeat(2, 1fr); }
  }

  .kanban-column {
    background: var(--color-bg-alt);
    border-radius: var(--radius-lg);
    border: 1px solid var(--color-border);
    display: flex;
    flex-direction: column;
    min-height: 300px;
    min-width: 200px;
  }

  .kanban-col-header {
    padding: var(--space-3) var(--space-4);
    background: var(--color-surface);
    border-top: 3px solid var(--color-border);
    border-radius: var(--radius-lg) var(--radius-lg) 0 0;
    display: flex;
    justify-content: space-between;
    align-items: center;

    .kanban-col-title {
      font-size: var(--text-sm);
      font-weight: var(--weight-semibold);
      color: var(--color-text);
    }
  }

  .kanban-col-body {
    flex: 1;
    padding: var(--space-2);
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    overflow-y: auto;
    max-height: 60vh;
  }

  .kanban-card {
    background: var(--color-surface);
    border-radius: var(--radius-md);
    padding: var(--space-3);
    border: 1px solid var(--color-border);
    cursor: pointer;
    transition: all var(--duration-fast) var(--ease-out);

    &:hover {
      border-color: var(--color-border-glow);
      box-shadow: var(--shadow-glow);
      transform: translateY(-1px);
    }

    .kanban-card-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--space-1);
    }

    .kanban-card-name {
      font-size: var(--text-sm);
      font-weight: var(--weight-semibold);
      color: var(--color-text);
    }

    .kanban-card-job {
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
      margin-bottom: var(--space-2);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .kanban-card-meta {
      display: flex;
      gap: var(--space-2);
      font-size: 11px;
      color: var(--color-text-muted);
      margin-bottom: var(--space-2);
      .kanban-card-time { margin-left: auto; }
    }

    .kanban-card-actions {
      display: flex;
      gap: var(--space-1);
      border-top: 1px solid var(--color-border-light);
      padding-top: var(--space-2);
      .el-button { font-size: 11px; padding: 2px 6px; }
    }
  }

  .kanban-col-empty {
    padding: var(--space-8) var(--space-3);
    text-align: center;
    font-size: var(--text-xs);
    color: var(--color-text-muted);
  }

  // ── 表格高亮行 ──
  :deep(.el-table) {
    cursor: pointer;
  }

  // ── 面板内TAB内容样式 ──
  .tpr-header {
    display: flex;
    gap: var(--space-4);
    align-items: center;
    padding: var(--space-3);
    background: linear-gradient(135deg, var(--color-primary-bg), var(--color-surface));
    border-radius: var(--radius-md);
    border: 1px solid var(--color-border-light);
    margin-bottom: var(--space-3);

    .tpr-score-ring {
      text-align: center;
      flex-shrink: 0;
      .tpr-score-label { font-size: 11px; color: var(--color-text-muted); margin-top: 2px; }
    }

    .tpr-meta {
      flex: 1;
      min-width: 0;
      .tpr-name { font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--color-text); }
      .tpr-info { font-size: var(--text-xs); color: var(--color-text-secondary); margin-top: 2px; }
      .tpr-tags { margin-top: var(--space-2); display: flex; gap: var(--space-1); flex-wrap: wrap; }
    }
  }

  .tpr-section {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border-light);
    border-radius: var(--radius-md);
    padding: var(--space-3);
    margin-bottom: var(--space-2);
  }

  .tpr-section-title {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    color: var(--color-text);
    margin-bottom: var(--space-2);
    padding-bottom: var(--space-1);
    border-bottom: 1px solid var(--color-border-light);
  }

  .skill-match-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-2); }

  .skill-col {
    background: var(--color-surface);
    border-radius: var(--radius-sm);
    padding: var(--space-2);
    &.matched { border-left: 3px solid var(--color-success); }
    &.missing { border-left: 3px solid var(--color-danger); }
    .skill-col-title { font-size: 11px; font-weight: var(--weight-semibold); margin-bottom: var(--space-1); color: var(--color-text-secondary); }
  }

  .sw-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-2); }

  .sw-col {
    background: var(--color-surface);
    border-radius: var(--radius-sm);
    padding: var(--space-2);
    .sw-col-title { font-size: 11px; font-weight: var(--weight-semibold); margin-bottom: var(--space-1); color: var(--color-text-muted); }
    .sw-item { font-size: var(--text-xs); color: var(--color-text-secondary); line-height: 1.5; }
  }

  .iq-item {
    display: flex;
    gap: var(--space-2);
    padding: var(--space-1) 0;
    border-bottom: 1px solid var(--color-border-light);
    &:last-child { border-bottom: none; }
    .iq-num {
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: var(--gradient-primary);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 11px;
      font-weight: var(--weight-bold);
      flex-shrink: 0;
    }
    .iq-body {
      flex: 1;
      .iq-question { font-size: var(--text-xs); color: var(--color-text); line-height: 1.4; }
      .iq-meta { font-size: 10px; color: var(--color-text-muted); margin-top: 2px; }
    }
  }

  // ── 评分卡片 ──
  .score-main-card {
    display: flex;
    justify-content: center;
    padding: var(--space-4);
    background: linear-gradient(135deg, var(--color-primary-bg), var(--color-surface));
    border-radius: var(--radius-lg);
    border: 1px solid var(--color-border-light);
  }

  .score-ring-wrapper {
    position: relative;
    width: 120px;
    height: 120px;
  }

  .score-content {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    text-align: center;
    .score-value { font-size: 36px; font-weight: var(--weight-bold); }
    .score-unit { font-size: 14px; color: var(--color-text-muted); display: block; }
    .score-text { font-size: 11px; color: var(--color-text-secondary); display: block; }
  }

  .detail-items {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    .detail-item {
      .item-header {
        display: flex;
        justify-content: space-between;
        margin-bottom: var(--space-1);
        .item-label { font-size: var(--text-xs); color: var(--color-text-secondary); }
        .item-value { font-size: var(--text-xs); font-weight: var(--weight-semibold); }
      }
    }
  }

  // ── 面试题 ──
  .question-item {
    display: flex;
    gap: var(--space-2);
    padding: var(--space-2);
    background: var(--color-surface);
    border-radius: var(--radius-sm);
    border: 1px solid var(--color-border-light);
    margin-bottom: var(--space-1);
    .q-num {
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: var(--gradient-primary);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 11px;
      font-weight: var(--weight-semibold);
      flex-shrink: 0;
    }
    .q-text { font-size: var(--text-xs); color: var(--color-text); line-height: 1.5; }
  }

  // ── 分页 ──
  .pagination {
    margin-top: 20px;
    display: flex;
    justify-content: center;
  }

  // ── 评分项颜色（跟随主题）──
  .skill-score { color: var(--color-success); }
  .exp-score { color: var(--color-primary); }
  .edu-score { color: var(--color-warning); }

  .section-label {
    font-weight: 600;
    margin-bottom: 8px;
    &.success { color: var(--color-success); }
    &.danger { color: var(--color-danger); }
  }
}
</style>
