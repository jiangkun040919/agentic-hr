<template>
  <div class="resume-management-container">
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
      <el-button type="warning" :loading="sortingLoading" @click="handleSmartSort" style="margin-left: 8px">
        <el-icon><Sort /></el-icon> AI 智能排序
      </el-button>
      <el-radio-group v-model="viewMode" size="small" style="margin-left:auto">
        <el-radio-button value="table"><el-icon><List /></el-icon></el-radio-button>
        <el-radio-button value="kanban"><el-icon><Grid /></el-icon></el-radio-button>
      </el-radio-group>
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedIds.length > 0" class="batch-bar">
      <span class="batch-count">已选 {{ selectedIds.length }} 项</span>
      <el-button type="success" size="small" @click="handleBatchOperation(1)">批量已查看</el-button>
      <el-button type="warning" size="small" @click="handleBatchOperation(2)">批量面试中</el-button>
      <el-button type="primary" size="small" @click="handleBatchOperation(3)">批量开始实习</el-button>
      <el-button type="success" size="small" @click="handleBatchOperation(4)">批量正式入职</el-button>
      <el-button type="danger" size="small" @click="handleBatchOperation(5)">批量淘汰</el-button>
      <el-button size="small" @click="selectedIds = []">取消选择</el-button>
    </div>

    <el-alert
      v-if="aiMode"
      :title="aiModeTitle"
      type="info"
      :closable="false"
      show-icon
      class="ai-mode-bar"
    >
      <template #default>
        请选择一位候选人进行AI分析
      </template>
    </el-alert>

    <!-- ═══ 看板视图 ═══ -->
    <div v-if="viewMode === 'kanban'" v-loading="loading" class="kanban-board">
      <div v-for="col in kanbanColumns" :key="col.status" class="kanban-column">
        <div class="kanban-col-header" :style="{ borderTopColor: col.color }">
          <span class="kanban-col-title">{{ col.label }}</span>
          <el-tag :color="col.color" effect="dark" size="small" round>{{ col.items.length }}</el-tag>
        </div>
        <div class="kanban-col-body">
          <div v-for="item in col.items" :key="item.deliveryId" class="kanban-card" @click="goToDetail(item.deliveryId)">
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
            </div>
          </div>
          <div v-if="col.items.length === 0" class="kanban-col-empty">暂无候选人</div>
        </div>
      </div>
    </div>

    <el-card v-else v-loading="loading">
      <el-table :data="deliveries" stripe @row-click="aiMode ? handleAISelect : goToDetail" @selection-change="handleSelectionChange" ref="tableRef">
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
        <el-table-column v-if="aiMode" label="AI操作" width="280" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="aiMode === 'analyze'"
              size="small"
              type="primary"
              :loading="aiLoading === row.deliveryId"
              @click.stop="handleAIAnalyze(row.deliveryId)"
            >
              <el-icon><MagicStick /></el-icon>解析
            </el-button>
            <el-button
              v-if="aiMode === 'score'"
              size="small"
              type="success"
              :loading="aiLoading === row.deliveryId"
              @click.stop="handleAIScore(row.deliveryId)"
            >
              <el-icon><TrendCharts /></el-icon>评分
            </el-button>
            <el-button
              v-if="aiMode === 'question'"
              size="small"
              type="warning"
              :loading="aiLoading === row.deliveryId"
              @click.stop="handleAIQuestions(row.deliveryId)"
            >
              <el-icon><ChatDotRound /></el-icon>出题
            </el-button>
            <el-button size="small" @click.stop="goToDetail(row.deliveryId)">查看</el-button>
          </template>
        </el-table-column>
        <el-table-column v-else label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click.stop="goToDetail(row.deliveryId)">查看</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

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

    <el-dialog v-model="showResultDialog" :title="resultDialogTitle" width="750px" destroy-on-close>
      <div v-if="aiLoading && !aiResult" style="text-align: center; padding: 40px">
        <el-icon class="is-loading" :size="40"><Loading /></el-icon>
        <p style="margin-top: 16px; color: var(--color-text-secondary)">AI正在分析中，请稍候...</p>
      </div>
      <div v-else-if="aiResult" class="ai-result-content">
        <!-- AI简历解析 -->
        <template v-if="aiResultType === 'analyze'">
          <div class="analyze-container">
            <!-- 头部信息卡片 -->
            <div class="info-header-card">
              <div class="avatar-wrapper">
                <div class="avatar">
                  <User :size="48" />
                </div>
                <div class="verification-badge" v-if="aiResult.phone">
                  <Check :size="12" />
                </div>
              </div>
              <div class="basic-info">
                <div class="name-row">
                  <h2 class="name">{{ aiResult.name || '未填写' }}</h2>
                  <el-tag v-if="aiResult.workYears" type="success" size="small">
                    {{ aiResult.workYears }}年经验
                  </el-tag>
                </div>
                <div class="contact-info">
                  <span class="contact-item" v-if="aiResult.phone">
                    <Phone :size="14" />
                    {{ aiResult.phone }}
                  </span>
                  <span class="contact-item" v-if="aiResult.email">
                    <Message :size="14" />
                    {{ aiResult.email }}
                  </span>
                </div>
                <div class="education-tag">
                  <Briefcase :size="14" />
                  {{ aiResult.education || '未填写' }}
                </div>
              </div>
              <div class="quick-stats">
                <div class="stat-item">
                  <span class="stat-value">{{ (aiResult.workExperience || []).length }}</span>
                  <span class="stat-label">工作经历</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ (aiResult.projects || []).length }}</span>
                  <span class="stat-label">项目经验</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ (aiResult.skills || []).length }}</span>
                  <span class="stat-label">核心技能</span>
                </div>
              </div>
            </div>

            <!-- 个人信息扩展 -->
            <div class="info-expand-card">
              <div class="card-header">
                <div class="header-icon bg-green">
                  <User :size="18" />
                </div>
                <h3>个人信息</h3>
              </div>
              <div class="info-grid">
                <div class="info-item">
                  <span class="info-label">年龄</span>
                  <span class="info-value">{{ aiResult.age || '未填写' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">性别</span>
                  <span class="info-value">{{ aiResult.gender || '未填写' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">籍贯</span>
                  <span class="info-value">{{ aiResult.nativePlace || '未填写' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">期望薪资</span>
                  <span class="info-value">{{ aiResult.expectedSalary || '未填写' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">求职状态</span>
                  <span class="info-value">{{ aiResult.jobStatus || '未填写' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">到岗时间</span>
                  <span class="info-value">{{ aiResult.availableTime || '未填写' }}</span>
                </div>
              </div>
            </div>

            <!-- 核心技能 -->
            <div class="skill-card">
              <div class="card-header">
                <div class="header-icon">
                  <Star :size="18" />
                </div>
                <h3>核心技能</h3>
                <span class="skill-count">{{ (aiResult.skills || []).length }}项技能</span>
              </div>
              <div class="skill-tags">
                <div v-for="(skill, i) in (aiResult.skills || [])" :key="i" class="skill-item">
                  <el-tag 
                    class="skill-tag"
                    type="primary"
                    effect="light"
                  >
                    {{ skill.name || skill }}
                  </el-tag>
                  <span v-if="skill.level" class="skill-level">{{ skill.level }}</span>
                </div>
              </div>
            </div>

            <!-- AI分析总结 -->
            <div class="summary-card">
              <div class="card-header">
                <div class="header-icon bg-orange">
                  <MagicStick :size="18" />
                </div>
                <h3>AI分析总结</h3>
              </div>
              <div class="summary-content">
                <div class="summary-highlight">
                  <span class="highlight-label">综合评分</span>
                  <span class="highlight-score" :style="{ color: getScoreColor(aiResult.summaryScore || 70) }">
                    {{ aiResult.summaryScore || 70 }}分
                  </span>
                </div>
                <p class="summary-text">{{ aiResult.summary || 'AI已完成简历分析，该候选人具备良好的专业背景和工作经验。' }}</p>
                <div class="summary-tags">
                  <el-tag v-for="(tag, i) in (aiResult.tags || ['潜力候选人', '经验丰富'])" :key="i" size="small">
                    {{ tag }}
                  </el-tag>
                </div>
              </div>
            </div>

            <!-- 工作经历 -->
            <div class="timeline-card">
              <div class="card-header">
                <div class="header-icon bg-blue">
                  <Briefcase :size="18" />
                </div>
                <h3>工作经历</h3>
                <span class="exp-count">{{ (aiResult.workExperience || []).length }}段经历</span>
              </div>
              <div v-if="aiResult.workExperience && aiResult.workExperience.length" class="timeline">
                <div v-for="(exp, i) in aiResult.workExperience" :key="i" class="timeline-item">
                  <div class="timeline-dot"></div>
                  <div class="timeline-content">
                    <div class="timeline-header">
                      <span class="company">{{ exp.company }}</span>
                      <span class="position">{{ exp.position }}</span>
                      <span class="duration">{{ exp.duration }}</span>
                    </div>
                    <p class="timeline-desc">{{ exp.description }}</p>
                    <div v-if="exp.achievements && exp.achievements.length" class="achievements">
                      <h4>主要成就</h4>
                      <ul>
                        <li v-for="(achievement, j) in exp.achievements" :key="j">{{ achievement }}</li>
                      </ul>
                    </div>
                  </div>
                </div>
              </div>
              <div v-else class="empty-state">
                <Briefcase :size="32" />
                <p>暂无工作经历信息</p>
              </div>
            </div>

            <!-- 项目经验 -->
            <div class="project-card">
              <div class="card-header">
                <div class="header-icon bg-purple">
                  <FolderOpened :size="18" />
                </div>
                <h3>项目经验</h3>
                <span class="proj-count">{{ (aiResult.projects || []).length }}个项目</span>
              </div>
              <div v-if="aiResult.projects && aiResult.projects.length" class="project-list">
                <div v-for="(proj, i) in aiResult.projects" :key="i" class="project-item">
                  <div class="project-header">
                    <span class="project-name">{{ proj.name }}</span>
                    <span class="project-role">{{ proj.role }}</span>
                  </div>
                  <span class="project-duration">{{ proj.duration }}</span>
                  <p class="project-desc">{{ proj.description }}</p>
                  <div v-if="proj.techStack" class="tech-stack">
                    <span class="tech-label">技术栈：</span>
                    <el-tag v-for="(tech, j) in proj.techStack.split(',').map((t: string) => t.trim())" :key="j" size="small">{{ tech }}</el-tag>
                  </div>
                </div>
              </div>
              <div v-else class="empty-state">
                <FolderOpened :size="32" />
                <p>暂无项目经验信息</p>
              </div>
            </div>
          </div>
        </template>

        <!-- 智能匹配评分 -->
        <template v-else-if="aiResultType === 'score'">
          <div class="score-container">
            <div class="score-main-card">
              <div class="score-ring-wrapper">
                <div class="score-ring-bg"></div>
                <el-progress
                  type="circle"
                  :percentage="aiResult.score || 0"
                  :color="getScoreColor(aiResult.score || 0)"
                  :width="140"
                  :show-text="false"
                  :stroke-width="12"
                />
                <div class="score-content">
                  <span class="score-value" :style="{ color: getScoreColor(aiResult.score || 0) }">
                    {{ aiResult.score || 0 }}
                  </span>
                  <span class="score-unit">分</span>
                  <div class="score-text">综合匹配度</div>
                  <div class="score-level" :class="getScoreLevel(aiResult.score || 0)">
                    {{ getScoreLevelText(aiResult.score || 0) }}
                  </div>
                </div>
              </div>
            </div>

            <div class="analysis-card">
              <div class="analysis-header">
                <el-icon :size="20"><Document /></el-icon>
                <h4>匹配分析报告</h4>
              </div>
              <div class="analysis-content">
                <p>{{ aiResult.reason || aiResult.report || '暂无分析报告' }}</p>
              </div>
            </div>

            <div class="detail-card">
              <div class="detail-header">
                <el-icon :size="20"><TrendCharts /></el-icon>
                <h4>分项匹配度</h4>
              </div>
              <div class="detail-items">
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">技能匹配</span>
                    <span class="item-value skill-score">{{ aiResult.strengths && aiResult.strengths.length > 0 ? 85 : 0 }}%</span>
                  </div>
                  <el-progress :percentage="aiResult.strengths && aiResult.strengths.length > 0 ? 85 : 0" color="var(--color-success)" :show-text="false" :stroke-width="6" />
                </div>
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">经验匹配</span>
                    <span class="item-value exp-score">78%</span>
                  </div>
                  <el-progress :percentage="78" color="var(--color-primary)" :show-text="false" :stroke-width="6" />
                </div>
                <div class="detail-item">
                  <div class="item-header">
                    <span class="item-label">学历匹配</span>
                    <span class="item-value edu-score">90%</span>
                  </div>
                  <el-progress :percentage="90" color="var(--color-warning)" :show-text="false" :stroke-width="6" />
                </div>
              </div>

              <div v-if="aiResult.strengths && aiResult.strengths.length > 0" class="strengths-section">
                <div class="strengths-header">
                  <el-icon :size="16" color="#67C23A"><Check /></el-icon>
                  <span>优势</span>
                </div>
                <div class="strengths-list">
                  <el-tag v-for="(s, i) in aiResult.strengths" :key="i" type="success" effect="light" class="strength-tag">
                    {{ s }}
                  </el-tag>
                </div>
              </div>

              <div v-if="aiResult.weaknesses && aiResult.weaknesses.length > 0" class="weaknesses-section">
                <div class="weaknesses-header">
                  <el-icon :size="16" color="#F56C6C"><Warning /></el-icon>
                  <span>风险点</span>
                </div>
                <div class="weaknesses-list">
                  <el-tag v-for="(w, i) in aiResult.weaknesses" :key="i" type="warning" effect="light" class="weakness-tag">
                    {{ w }}
                  </el-tag>
                </div>
              </div>
            </div>
          </div>
        </template>

        <!-- 面试题生成 -->
        <template v-else-if="aiResultType === 'question'">
          <el-tabs type="card" class="question-tabs">
            <el-tab-pane label="技术问题">
              <div v-for="(q, i) in aiResult.technical" :key="i" class="question-item">
                <span class="q-num">{{ i + 1 }}</span>
                <span class="q-text">{{ q }}</span>
              </div>
            </el-tab-pane>
            <el-tab-pane label="行为问题">
              <div v-for="(q, i) in aiResult.behavioral" :key="i" class="question-item">
                <span class="q-num">{{ i + 1 }}</span>
                <span class="q-text">{{ q }}</span>
              </div>
            </el-tab-pane>
            <el-tab-pane label="场景问题">
              <div v-for="(q, i) in aiResult.scenario" :key="i" class="question-item">
                <span class="q-num">{{ i + 1 }}</span>
                <span class="q-text">{{ q }}</span>
              </div>
            </el-tab-pane>
          </el-tabs>
        </template>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { MagicStick, TrendCharts, ChatDotRound, Loading, User, Phone, Message, Briefcase, FolderOpened, Document, Star, Check, Warning, Sort, Grid, List } from '@element-plus/icons-vue'
import { useResumeStore } from '@/stores/resume'
import { useJobStore } from '@/stores/job'
import { analyzeResume, scoreResume, generateQuestions } from '@/api/ai'
import { batchScore, batchOperation } from '@/api/delivery'
import { parseAIResponse } from '@/utils/ai-parse-helper'
import dayjs from 'dayjs'

const router = useRouter()
const route = useRoute()
const resumeStore = useResumeStore()
const jobStore = useJobStore()

const loading = computed(() => resumeStore.loading)
const deliveries = computed(() => resumeStore.deliveries)
const total = computed(() => resumeStore.total)
const jobs = computed(() => jobStore.jobs)

const viewMode = ref<'table' | 'kanban'>('table')
const aiMode = ref('')
const aiModeTitle = ref('')
const aiLoading = ref<number | null>(null)
const showResultDialog = ref(false)
const resultDialogTitle = ref('')
const aiResult = ref<any>(null)
const aiResultType = ref('')

const searchParams = reactive({
  page: 1,
  pageSize: 15,
  keyword: '',
  status: undefined as any,
  jobId: undefined as number | undefined,
})

onMounted(() => {
  fetchResumes()
  jobStore.fetchJobs({ page: 1, pageSize: 100, status: 1 })

  const aiParam = route.query.ai as string
  if (aiParam === 'analyze') {
    aiMode.value = 'analyze'
    aiModeTitle.value = 'AI简历解析模式'
  } else if (aiParam === 'score') {
    aiMode.value = 'score'
    aiModeTitle.value = '智能匹配评分模式'
  } else if (aiParam === 'question') {
    aiMode.value = 'question'
    aiModeTitle.value = '面试题生成模式'
  }
})

const fetchResumes = () => {
  resumeStore.fetchResumes(searchParams)
}

// ── 批量操作 ──────────────────────────────────────────
const tableRef = ref()
const selectedIds = ref<number[]>([])

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

// ── AI 智能排序 ──────────────────────────────────────
const sortingLoading = ref(false)
const handleSmartSort = async () => {
  const ids = deliveries.value.slice(0, 20).map(d => d.deliveryId)
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

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD HH:mm')
const formatShortDate = (date: string) => date ? dayjs(date).format('MM-DD') : '-'

// ── 看板列定义 ──
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

const getStatusTagType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  return (['warning', 'info', 'info', 'primary', 'success', 'danger'] as const)[status] || 'info'
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

const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  const types: ('primary' | 'success' | 'warning' | 'info' | 'danger')[] = ['info', 'info', 'warning', 'primary', 'success', 'danger']
  return types[status] || 'info'
}

const getStatusText = (status: number) => {
  const texts = ['待查看', '已查看', '面试中', '实习中', '正式入职', '已淘汰']
  return texts[status] || '未知'
}

const getScoreColor = (score: number) => {
  if (score >= 80) return '#67C23A'
  if (score >= 60) return '#E6A23C'
  return '#F56C6C'
}

const getScoreLevel = (score: number) => {
  if (score >= 80) return 'level-excellent'
  if (score >= 60) return 'level-good'
  return 'level-poor'
}

const getScoreLevelText = (score: number) => {
  if (score >= 80) return '优秀匹配'
  if (score >= 60) return '良好匹配'
  return '需重点评估'
}

const goToDetail = (id: number) => {
  router.push(`/admin/resumes/${id}`)
}

const handleAISelect = (row: any) => {
  if (aiMode.value === 'analyze') {
    handleAIAnalyze(row.deliveryId)
  } else if (aiMode.value === 'score') {
    handleAIScore(row.deliveryId)
  } else if (aiMode.value === 'question') {
    handleAIQuestions(row.deliveryId)
  }
}

const handleAIAnalyze = async (deliveryId: number) => {
  aiLoading.value = deliveryId
  aiResult.value = null
  aiResultType.value = 'analyze'
  resultDialogTitle.value = 'AI简历解析结果'
  showResultDialog.value = true

  try {
    const res = await analyzeResume(deliveryId)
    const parsed = parseAIResponse(res)
    if (parsed) {
      aiResult.value = parsed
    }
  } catch (error: any) {
    ElMessage.error(error.message || 'AI解析失败')
    showResultDialog.value = false
  } finally {
    aiLoading.value = null
  }
}

const handleAIScore = async (deliveryId: number) => {
  aiLoading.value = deliveryId
  aiResult.value = null
  aiResultType.value = 'score'
  resultDialogTitle.value = '智能匹配评分结果'
  showResultDialog.value = true

  try {
    const res = await scoreResume(deliveryId)
    const parsed = parseAIResponse(res)
    if (parsed) {
      aiResult.value = parsed
    }
  } catch (error: any) {
    ElMessage.error(error.message || error.response?.data?.message || 'AI评分失败')
    showResultDialog.value = false
  } finally {
    aiLoading.value = null
  }
}

const handleAIQuestions = async (deliveryId: number) => {
  aiLoading.value = deliveryId
  aiResult.value = null
  aiResultType.value = 'question'
  resultDialogTitle.value = '面试题生成结果'
  showResultDialog.value = true

  try {
    const res = await generateQuestions(deliveryId)
    const parsed = parseAIResponse(res)
    if (parsed) {
      aiResult.value = parsed.questions || parsed
    }
  } catch (error: any) {
    ElMessage.error(error.message || '生成面试题失败')
    showResultDialog.value = false
  } finally {
    aiLoading.value = null
  }
}

const formatAnalyzeResult = (data: any) => {
  let str = '【简历解析结果】\n\n'
  str += `姓名：${data.name || '-'}\n`
  str += `电话：${data.phone || '-'}\n`
  str += `邮箱：${data.email || '-'}\n`
  str += `学历：${data.education || '-'}\n\n`

  if (data.skills && data.skills.length > 0) {
    str += `技能标签：${data.skills.join(', ')}\n\n`
  }

  if (data.workExperience && data.workExperience.length > 0) {
    str += '工作经历：\n'
    data.workExperience.forEach((exp: any, i: number) => {
      str += `${i + 1}. ${exp.company} - ${exp.position} (${exp.duration})\n`
      str += `   ${exp.description}\n`
    })
    str += '\n'
  }

  if (data.projects && data.projects.length > 0) {
    str += '项目经验：\n'
    data.projects.forEach((proj: any, i: number) => {
      str += `${i + 1}. ${proj.name} - ${proj.role} (${proj.duration})\n`
      str += `   ${proj.description}\n`
    })
  }

  return str
}
</script>

<style scoped lang="scss">
.resume-management-container {
  max-width: var(--content-max-width);

  .toolbar {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
    padding: var(--space-2) 0;
    flex-wrap: wrap;
  }

  // ====== 看板视图 ======
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
    background: var(--color-bg);
    border-radius: var(--radius-lg);
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
      border-color: var(--color-primary);
      box-shadow: var(--shadow-sm);
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

  .batch-bar {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 16px;
    margin-bottom: 12px;
    background: var(--color-success-bg);
    border: 1px solid var(--color-success-light);
    border-radius: 8px;
    .batch-count { font-weight: 600; color: var(--color-text); margin-right: 4px; }
  }

  .ai-mode-bar {
    margin-bottom: 12px;
    border-radius: 8px;
  }

  .pagination {
    margin-top: 20px;
    display: flex;
    justify-content: center;
  }

  :deep(.el-table) {
    cursor: pointer;
  }

  .ai-result-content {
    .empty-tip {
      color: var(--color-text-muted);
      text-align: center;
      padding: 20px;
      background: var(--color-bg-alt);
      border-radius: 8px;
    }

    // AI简历解析样式
    .analyze-container {
      display: flex;
      flex-direction: column;
      gap: 20px;

      .info-header-card {
        background: var(--gradient-primary);
        border-radius: 16px;
        padding: 24px;
        display: flex;
        align-items: center;
        gap: 20px;
        color: var(--color-text-inverse);

        .avatar-wrapper {
          position: relative;
          flex-shrink: 0;

          .verification-badge {
            position: absolute;
            bottom: 2px;
            right: 2px;
            width: 20px;
            height: 20px;
            background: var(--color-success);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            border: 2px solid var(--color-surface);
          }
        }

        .avatar {
          width: 80px;
          height: 80px;
          border-radius: 50%;
          background: rgba(255, 255, 255, 0.2);
          display: flex;
          align-items: center;
          justify-content: center;
          color: var(--color-text-inverse);
        }

        .basic-info {
          flex: 1;

          .name-row {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 12px;

            .name {
              font-size: 24px;
              font-weight: 700;
              margin: 0;
            }
          }

          .contact-info {
            display: flex;
            gap: 24px;
            margin-bottom: 8px;

            .contact-item {
              display: flex;
              align-items: center;
              gap: 6px;
              font-size: 14px;
              opacity: 0.9;
            }
          }

          .education-tag {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            background: rgba(255, 255, 255, 0.2);
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 13px;
          }
        }

        .quick-stats {
          display: flex;
          flex-direction: column;
          gap: 12px;
          padding-left: 20px;
          border-left: 1px solid rgba(255, 255, 255, 0.2);

          .stat-item {
            text-align: center;

            .stat-value {
              display: block;
              font-size: 24px;
              font-weight: 700;
            }

            .stat-label {
              font-size: 12px;
              opacity: 0.8;
            }
          }
        }
      }

      .skill-card,
      .timeline-card,
      .project-card,
      .info-expand-card,
      .summary-card {
        background: var(--color-surface);
        border-radius: 12px;
        padding: 20px;
        box-shadow: var(--shadow-md);

        .card-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 16px;

          .header-icon {
            width: 36px;
            height: 36px;
            border-radius: 10px;
            background: var(--gradient-primary);
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--color-text-inverse);

            &.bg-blue {
              background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            }

            &.bg-purple {
              background: linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%);
            }

            &.bg-green {
              background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            }

            &.bg-orange {
              background: linear-gradient(135deg, #fc4a1a 0%, #f7b733 100%);
            }
          }

          h3 {
            font-size: 16px;
            font-weight: 600;
            color: var(--color-text);
            margin: 0;
          }
        }
      }

      .info-expand-card {
        .info-grid {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 16px;

          .info-item {
            display: flex;
            flex-direction: column;
            padding: 12px;
            background: var(--color-bg-alt);
            border-radius: 8px;

            .info-label {
              font-size: 12px;
              color: var(--color-text-muted);
              margin-bottom: 4px;
            }

            .info-value {
              font-size: 14px;
              font-weight: 500;
              color: var(--color-text);
            }
          }
        }
      }

      .skill-card {
        .card-header {
          justify-content: space-between;

          .skill-count {
            font-size: 13px;
            color: var(--color-text-muted);
          }
        }

        .skill-tags {
          display: flex;
          flex-wrap: wrap;
          gap: 10px;

          .skill-item {
            display: flex;
            align-items: center;
            gap: 6px;

            .skill-tag {
              padding: 6px 14px;
              font-size: 13px;
              border-radius: 20px;
            }

            .skill-level {
              font-size: 12px;
              color: var(--color-text-muted);
              background: var(--color-bg-alt);
              padding: 2px 8px;
              border-radius: 4px;
            }
          }
        }
      }

      .summary-card {
        background: var(--color-bg-alt);
        border: 1px solid var(--color-border-light);

        .summary-content {
          .summary-highlight {
            display: flex;
            align-items: baseline;
            gap: 12px;
            margin-bottom: 16px;

            .highlight-label {
              font-size: 14px;
              color: var(--color-text-secondary);
            }

            .highlight-score {
              font-size: 36px;
              font-weight: 700;
            }
          }

          .summary-text {
            font-size: 14px;
            color: var(--color-text-secondary);
            line-height: 1.7;
            margin: 0 0 16px 0;
          }

          .summary-tags {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
          }
        }
      }

      .timeline-card {
        .card-header {
          justify-content: space-between;

          .exp-count {
            font-size: 13px;
            color: var(--color-text-muted);
          }
        }

        .timeline {
          padding-left: 8px;

          .timeline-item {
            position: relative;
            padding-left: 28px;
            padding-bottom: 24px;
            border-left: 2px solid var(--color-border);
            margin-left: 6px;

            &:last-child {
              padding-bottom: 0;
              border-left-color: transparent;
            }

            .timeline-dot {
              position: absolute;
              left: -8px;
              top: 4px;
              width: 12px;
              height: 12px;
              border-radius: 50%;
              background: var(--gradient-primary);
              border: 3px solid var(--color-surface);
              box-shadow: var(--glow-primary);
            }

            .timeline-content {
              .timeline-header {
                display: flex;
                align-items: center;
                gap: 12px;
                margin-bottom: 8px;
                flex-wrap: wrap;

                .company {
                  font-weight: 600;
                  color: var(--color-text);
                }

                .position {
                  font-weight: 500;
                  color: var(--color-primary);
                }

                .duration {
                  font-size: 13px;
                  color: var(--color-text-muted);
                  margin-left: auto;
                }
              }

              .timeline-desc {
                font-size: 14px;
                color: var(--color-text-secondary);
                line-height: 1.6;
                margin: 0;
                padding: 12px;
                background: var(--color-bg-alt);
                border-radius: 8px;
                margin-bottom: 12px;
              }

              .achievements {
                background: var(--color-primary-bg);
                border-left: 3px solid var(--color-primary);
                padding: 12px 16px;
                border-radius: 0 8px 8px 0;

                h4 {
                  font-size: 14px;
                  font-weight: 600;
                  color: var(--color-text);
                  margin: 0 0 8px 0;
                }

                ul {
                  margin: 0;
                  padding-left: 20px;

                  li {
                    font-size: 13px;
                    color: var(--color-text-secondary);
                    line-height: 1.6;
                    margin-bottom: 4px;

                    &:last-child {
                      margin-bottom: 0;
                    }
                  }
                }
              }
            }
          }
        }

        .empty-state {
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          padding: 30px;
          color: var(--color-text-muted);

          p {
            margin-top: 12px;
            font-size: 14px;
          }
        }
      }

      .project-card {
        .card-header {
          justify-content: space-between;

          .proj-count {
            font-size: 13px;
            color: var(--color-text-muted);
          }
        }

        .project-list {
          display: flex;
          flex-direction: column;
          gap: 16px;

          .project-item {
            padding: 16px;
            background: var(--color-bg-alt);
            border-radius: 10px;
            border-left: 4px solid var(--color-secondary);

            .project-header {
              display: flex;
              align-items: center;
              gap: 10px;
              margin-bottom: 6px;

              .project-name {
                font-weight: 600;
                color: var(--color-text);
              }

              .project-role {
                font-size: 13px;
                color: var(--color-text-muted);
                padding: 2px 8px;
                background: var(--color-surface);
                border-radius: 4px;
              }
            }

            .project-duration {
              font-size: 12px;
              color: var(--color-text-muted);
              margin-bottom: 10px;
              display: block;
            }

            .project-desc {
              font-size: 14px;
              color: var(--color-text-secondary);
              line-height: 1.6;
              margin: 0 0 12px 0;
            }

            .tech-stack {
              display: flex;
              flex-wrap: wrap;
              align-items: center;
              gap: 8px;

              .tech-label {
                font-size: 12px;
                color: var(--color-text-muted);
                margin-right: 4px;
              }
            }
          }
        }

        .empty-state {
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          padding: 30px;
          color: var(--color-text-muted);

          p {
            margin-top: 12px;
            font-size: 14px;
          }
        }
      }
    }

    // 智能匹配评分样式
    .score-container {
      display: flex;
      flex-direction: column;
      gap: 20px;

      .score-main-card {
        background: var(--gradient-primary);
        border-radius: 16px;
        padding: 32px;
        display: flex;
        justify-content: center;

        .score-ring-wrapper {
          position: relative;
          width: 140px;
          height: 140px;

          .score-ring-bg {
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            border-radius: 50%;
            background: rgba(255, 255, 255, 0.2);
          }

          :deep(.el-progress) {
            position: absolute;
            top: 0;
            left: 0;
          }

          .score-content {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            text-align: center;

            .score-value {
              font-size: 48px;
              font-weight: 700;
              color: var(--color-text-inverse);
            }

            .score-unit {
              font-size: 18px;
              color: rgba(255, 255, 255, 0.8);
            }

            .score-text {
              font-size: 14px;
              color: rgba(255, 255, 255, 0.9);
              margin-top: 4px;
            }

            .score-level {
              display: inline-block;
              margin-top: 8px;
              padding: 4px 16px;
              border-radius: 20px;
              font-size: 13px;
              font-weight: 500;
              background: rgba(255, 255, 255, 0.3);
              color: var(--color-text-inverse);
            }
          }
        }
      }

      .analysis-card {
        background: var(--color-surface);
        border-radius: 12px;
        padding: 20px;
        box-shadow: var(--shadow-md);

        .analysis-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 16px;

          h4 {
            font-size: 16px;
            font-weight: 600;
            color: var(--color-text);
            margin: 0;
          }
        }

        .analysis-content {
          p {
            color: var(--color-text-secondary);
            line-height: 1.7;
            margin: 0;
            padding: 12px;
            background: var(--color-bg-alt);
            border-radius: 8px;
          }
        }
      }

      .detail-card {
        background: var(--color-surface);
        border-radius: 12px;
        padding: 20px;
        box-shadow: var(--shadow-md);

        .detail-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 20px;

          h4 {
            font-size: 16px;
            font-weight: 600;
            color: var(--color-text);
            margin: 0;
          }
        }

        .detail-items {
          display: flex;
          flex-direction: column;
          gap: 16px;

          .detail-item {
            .item-header {
              display: flex;
              justify-content: space-between;
              align-items: center;
              margin-bottom: 8px;

              .item-label {
                font-size: 14px;
                color: var(--color-text-secondary);
              }

              .item-value {
                font-weight: 600;
                font-size: 14px;
              }
            }

            :deep(.el-progress-bar) {
              border-radius: 3px;
            }
          }
        }

        .strengths-section {
          margin-top: 20px;
          padding-top: 16px;
          border-top: 1px solid var(--color-border-light);

          .strengths-header {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-bottom: 12px;
            font-weight: 600;
            color: var(--color-success);
          }

          .strengths-list {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;

            .strength-tag {
              background: #f0f9eb;
              color: #67C23A;
              border: none;
            }
          }
        }

        .weaknesses-section {
          margin-top: 16px;
          padding-top: 16px;
          border-top: 1px solid #eee;

          .weaknesses-header {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-bottom: 12px;
            font-weight: 600;
            color: #F56C6C;
          }

          .weaknesses-list {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;

            .weakness-tag {
              background: #fef0f0;
              color: #F56C6C;
              border: none;
            }
          }
        }
      }
    }

    // 面试题生成样式
    .question-tabs {
      :deep(.el-tabs__header) {
        margin-bottom: 16px;
      }

      .question-item {
        display: flex;
        gap: 12px;
        padding: 12px 16px;
        background: #fafafa;
        border-radius: 8px;
        margin-bottom: 8px;

        .q-num {
          width: 28px;
          height: 28px;
          border-radius: 50%;
          background: #1F4E78;
          color: #fff;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 14px;
          font-weight: 600;
          flex-shrink: 0;
        }

        .q-text {
          color: var(--color-text);
          line-height: 1.6;
        }
      }
    }
  }

  // ── 评分项颜色（跟随主题）──
  .skill-score { color: var(--color-success); }
  .exp-score { color: var(--color-primary); }
  .edu-score { color: var(--color-warning); }
}
</style>
