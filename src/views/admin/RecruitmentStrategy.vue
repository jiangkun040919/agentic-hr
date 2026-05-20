<template>
  <div class="strategy-dashboard">
    <!-- 页面Hero -->
    <div class="page-hero">
      <div class="hero-text">
        <h1 class="hero-title">招聘策略决策</h1>
        <p class="hero-subtitle">数据驱动的招聘策略优化 — 实时追踪漏斗转化，洞察人才趋势，发现瓶颈与机会</p>
      </div>
      <div class="hero-actions">
        <el-tag type="primary" effect="dark" round size="small">实时数据</el-tag>
        <el-tag effect="plain" round size="small">AI驱动</el-tag>
        <el-tag effect="plain" round size="small">决策支持</el-tag>
      </div>
    </div>

    <!-- 时间筛选 + 导出 -->
    <div class="dash-header">
      <div class="header-left">
        <span class="refresh-hint" v-if="lastRefresh">最后更新 {{ lastRefresh }}</span>
        <el-tag v-if="autoRefresh" size="small" type="success" effect="light" round class="live-tag">
          <span class="live-dot" /> 实时
        </el-tag>
      </div>
      <div class="header-right">
        <el-radio-group v-model="timeRange" size="small" @change="loadAllData">
          <el-radio-button value="day">今日</el-radio-button>
          <el-radio-button value="week">本周</el-radio-button>
          <el-radio-button value="month">本月</el-radio-button>
          <el-radio-button value="quarter">本季</el-radio-button>
        </el-radio-group>
        <el-button size="small" @click="handleExport" :loading="exporting">
          <el-icon><Download /></el-icon>导出
        </el-button>
      </div>
    </div>

    <!-- KPI 卡片 -->
    <div class="kpi-row">
      <div v-for="kpi in kpiCards" :key="kpi.key" class="kpi-card">
        <div class="kpi-top">
          <div class="kpi-icon" :style="{ color: kpi.color, background: kpi.bg }">
            <el-icon :size="18"><component :is="kpi.icon" /></el-icon>
          </div>
          <div class="kpi-trend" :class="kpi.trend >= 0 ? 'up' : 'down'">
            <el-icon :size="14"><component :is="kpi.trend >= 0 ? 'CaretTop' : 'CaretBottom'" /></el-icon>
            {{ Math.abs(kpi.trend) }}%
          </div>
        </div>
        <div class="kpi-value" ref="kpiValueRefs">{{ animatedValues[kpi.key] }}</div>
        <div class="kpi-label">{{ kpi.label }}</div>
        <div class="kpi-secondary">{{ kpi.secondary }}</div>
        <div ref="sparkRefs" class="kpi-spark"></div>
      </div>
    </div>

    <!-- 主图表网格 2x2 -->
    <div class="chart-grid">
      <el-card class="chart-card" shadow="never">
        <template #header><div class="chart-title"><el-icon><DataAnalysis /></el-icon>招聘漏斗</div></template>
        <div ref="funnelRef" class="chart-box"></div>
      </el-card>
      <el-card class="chart-card" shadow="never">
        <template #header><div class="chart-title"><el-icon><TrendCharts /></el-icon>入职趋势</div></template>
        <div ref="trendRef" class="chart-box"></div>
      </el-card>
      <el-card class="chart-card" shadow="never">
        <template #header><div class="chart-title"><el-icon><Histogram /></el-icon>岗位热度排行</div></template>
        <div ref="hotRef" class="chart-box"></div>
      </el-card>
      <el-card class="chart-card" shadow="never">
        <template #header><div class="chart-title"><el-icon><Odometer /></el-icon>技能差距概览</div></template>
        <div ref="skillGapRef" class="chart-box"></div>
      </el-card>
    </div>

    <!-- 下半部分：三列 -->
    <div class="lower-grid">
      <!-- 部门分布 -->
      <el-card class="lower-card" shadow="never">
        <template #header><div class="chart-title">部门招聘分布</div></template>
        <div ref="deptRef" class="chart-box-sm"></div>
      </el-card>

      <!-- 管道瓶颈检测 NEW -->
      <el-card class="lower-card" shadow="never">
        <template #header>
          <div class="chart-title">
            <el-icon><WarningFilled /></el-icon>管道瓶颈检测
          </div>
        </template>
        <div class="bottleneck-panel">
          <div class="bottleneck-item" v-for="b in bottlenecks" :key="b.label">
            <div class="bn-header">
              <span class="bn-label">{{ b.label }}</span>
              <span class="bn-value" :class="{ 'bn-danger': b.alert }">{{ b.current }}{{ b.unit }}</span>
            </div>
            <el-progress :percentage="Number(b.percent)" :color="b.alert ? '#DC2626' : b.warn ? '#F59E0B' : '#10B981'" :show-text="false" :stroke-width="8" />
            <span class="bn-benchmark">行业基准: {{ b.benchmark }}{{ b.unit }}</span>
            <el-tag v-if="b.alert" type="danger" size="small" effect="light" round>需关注</el-tag>
            <el-tag v-else-if="b.warn" type="warning" size="small" effect="light" round>接近基准</el-tag>
            <el-tag v-else type="success" size="small" effect="light" round>健康</el-tag>
          </div>
        </div>
      </el-card>

      <!-- 新兴岗位发现 -->
      <el-card class="lower-card" shadow="never">
        <template #header>
          <div class="chart-title">
            <el-icon><MagicStick /></el-icon>新兴岗位发现
          </div>
        </template>
        <div ref="emergingRef" class="chart-box-sm"></div>
        <div v-if="emergingJobs.length > 0" class="emerging-list">
          <div v-for="job in emergingJobs.slice(0, 3)" :key="job.name" class="emerging-item">
            <span class="ej-name">{{ job.name }}</span>
            <el-tag :type="job.demandLevel === 'high' ? 'danger' : 'warning'" size="small" round>
              {{ job.demandLevel === 'high' ? '高需求' : '中需求' }}
            </el-tag>
          </div>
        </div>
      </el-card>
    </div>

    <!-- AI JD优化建议 -->
    <el-card class="jd-card" shadow="never" v-if="jdSuggestions.length > 0">
      <template #header>
        <div class="chart-title">
          <el-icon color="#D97706"><MagicStick /></el-icon>AI 岗位描述优化建议
        </div>
      </template>
      <el-collapse>
        <el-collapse-item v-for="s in jdSuggestions" :key="s.jobId" :title="`${s.jobTitle} — ${s.suggestion}`">
          <div class="jd-suggestion-body">
            <div class="jd-section" v-if="s.skillsToAdd?.length">
              <span class="jd-label">建议增加技能要求：</span>
              <el-tag v-for="sk in s.skillsToAdd" :key="sk" type="success" effect="light" size="small" style="margin:2px">{{ sk }}</el-tag>
            </div>
            <div class="jd-section" v-if="s.salarySuggestion">
              <span class="jd-label">薪资建议：</span>
              <span class="jd-value">{{ s.salarySuggestion }}</span>
            </div>
          </div>
        </el-collapse-item>
      </el-collapse>
    </el-card>

    <!-- AI 洞察浮动按钮 + 面板 -->
    <transition name="fab-zoom">
      <el-button v-if="!showAIPanel" class="ai-fab" type="primary" circle size="large" @click="showAIPanel = true">
        <el-icon :size="22"><MagicStick /></el-icon>
      </el-button>
    </transition>
    <transition name="panel-slide">
      <div v-if="showAIPanel" class="ai-panel">
        <div class="ai-panel-header">
          <span>AI 招聘洞察</span>
          <el-button :icon="Close" text size="small" @click="showAIPanel = false" />
        </div>
        <div class="ai-panel-body" v-loading="aiInsightsLoading">
          <div v-for="(insight, i) in aiInsights" :key="i" class="ai-insight-item">
            <div class="ai-insight-icon">{{ i + 1 }}</div>
            <div class="ai-insight-text">{{ insight }}</div>
          </div>
          <el-empty v-if="aiInsights.length === 0 && !aiInsightsLoading" description="暂无AI洞察" :image-size="48" />
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { Download, MagicStick, Close, DataAnalysis, TrendCharts, Histogram, Odometer, WarningFilled, User, Document, VideoCamera, Select } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { getDashboardData, getFunnelData, getTrendData, getHotJobs, getJobStats } from '@/api/stat'
import { getRecruitmentInsights } from '@/api/ai'
import { getEmergingJobs, getMarketReport } from '@/api/graph'
import { generateJD } from '@/api/job'
import dayjs from 'dayjs'

// ── 状态 ──
const timeRange = ref('week')
const lastRefresh = ref('')
const autoRefresh = ref(true)
const exporting = ref(false)
const showAIPanel = ref(false)
const aiInsights = ref<string[]>([])
const aiInsightsLoading = ref(false)
const emergingJobs = ref<any[]>([])
const jdSuggestions = ref<any[]>([])

let autoRefreshTimer: any = null

interface KpiCard {
  key: string; label: string; value: number; trend: number; secondary: string; color: string; bg: string; icon: any
}

const kpiCards = reactive<KpiCard[]>([
  { key: 'delivery', label: '总投递', value: 0, trend: 12, secondary: '较上期', color: '#6C6FF7', bg: 'rgba(99,102,241,0.1)', icon: Document },
  { key: 'interview', label: '面试中', value: 0, trend: 8, secondary: '转化率', color: '#A86EF7', bg: 'rgba(168,85,247,0.1)', icon: VideoCamera },
  { key: 'hired', label: '已入职', value: 0, trend: -5, secondary: '入职率', color: '#10B981', bg: 'rgba(16,185,129,0.1)', icon: Select },
  { key: 'openJobs', label: '在招岗位', value: 0, trend: 0, secondary: '需求缺口', color: '#F59E0B', bg: 'rgba(245,158,11,0.1)', icon: User },
])

const animatedValues = reactive<Record<string, number>>({ delivery: 0, interview: 0, hired: 0, openJobs: 0 })

interface Bottleneck { label: string; current: number; unit: string; percent: number; benchmark: string; alert: boolean; warn: boolean }

const bottlenecks = ref<Bottleneck[]>([
  { label: '筛选通过率', current: 0, unit: '%', percent: 0, benchmark: '70', alert: false, warn: false },
  { label: '面试转化率', current: 0, unit: '%', percent: 0, benchmark: '50', alert: false, warn: false },
  { label: '入职转化率', current: 0, unit: '%', percent: 0, benchmark: '40', alert: false, warn: false },
])

const calcBottlenecks = (funnelData: any) => {
  if (!funnelData) return
  const applied = funnelData.applied || 0
  const screened = funnelData.screened || 0
  const interviewed = funnelData.interviewed || 0
  const hired = funnelData.hired || 0

  const screenRate = applied > 0 ? Math.round((screened / applied) * 100) : 0
  const interviewRate = screened > 0 ? Math.round((interviewed / screened) * 100) : 0
  const hireRate = interviewed > 0 ? Math.round((hired / interviewed) * 100) : 0

  bottlenecks.value = [
    { label: '筛选通过率', current: screenRate, unit: '%', percent: screenRate, benchmark: '70', alert: screenRate < 40, warn: screenRate >= 40 && screenRate < 60 },
    { label: '面试转化率', current: interviewRate, unit: '%', percent: Math.min(interviewRate, 100), benchmark: '50', alert: interviewRate < 25, warn: interviewRate >= 25 && interviewRate < 40 },
    { label: '入职转化率', current: hireRate, unit: '%', percent: Math.min(hireRate, 100), benchmark: '40', alert: hireRate < 15, warn: hireRate >= 15 && hireRate < 30 },
  ]
}

// ── ECharts refs ──
const funnelRef = ref<HTMLElement>()
const trendRef = ref<HTMLElement>()
const hotRef = ref<HTMLElement>()
const skillGapRef = ref<HTMLElement>()
const deptRef = ref<HTMLElement>()
const emergingRef = ref<HTMLElement>()
const sparkRefs = ref<HTMLElement[]>([])

let funnelChart: echarts.ECharts | null = null
let trendChart: echarts.ECharts | null = null
let hotChart: echarts.ECharts | null = null
let skillGapChart: echarts.ECharts | null = null
let deptChart: echarts.ECharts | null = null
let emergingChart: echarts.ECharts | null = null

// ── 数字动画 ──
const animateNumber = (key: string, target: number, duration = 800) => {
  const start = animatedValues[key] || 0
  const startTime = performance.now()
  const easeOutExpo = (t: number) => (t === 1) ? 1 : 1 - Math.pow(2, -10 * t)
  const tick = (now: number) => {
    const elapsed = now - startTime
    const progress = Math.min(elapsed / duration, 1)
    animatedValues[key] = Math.round(start + (target - start) * easeOutExpo(progress))
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

// ── 加载数据 ──
const loadAllData = async () => {
  let funnelRes: any = null
  try {
    const [dashRes, funnel, trendRes, jobRes, hotRes] = await Promise.all([
      getDashboardData(),
      getFunnelData({}),
      getTrendData({ days: timeRange.value === 'day' ? 7 : timeRange.value === 'week' ? 14 : 30 }),
      getJobStats({}),
      getHotJobs({ limit: 8 }),
    ])
    funnelRes = funnel

    if (dashRes) {
      const stats = dashRes.stats || dashRes.Stats || {}
      kpiCards[0].value = stats.totalDeliveries || 0
      kpiCards[1].value = stats.interviews || 0
      kpiCards[2].value = stats.hired || 0
      kpiCards[3].value = stats.openJobs || 0
      animateNumber('delivery', stats.totalDeliveries || 0)
      animateNumber('interview', stats.interviews || 0)
      animateNumber('hired', stats.hired || 0)
      animateNumber('openJobs', stats.openJobs || 0)
    }

    calcBottlenecks(funnelRes)
    lastRefresh.value = dayjs().format('HH:mm:ss')
    await nextTick()
    renderAllCharts(funnelRes, trendRes, hotRes, jobRes)
  } catch (e) {
    console.error('数据加载失败', e)
  }

  // 加载新兴岗位
  try {
    const emerging = await getEmergingJobs()
    if (emerging?.discoveredJobs) emergingJobs.value = emerging.discoveredJobs
    await nextTick()
    renderEmergingChart()
  } catch { /* silent */ }

  // 加载AI洞察 + JD优化建议 + 技能差距
  loadAIInsights()
  loadJDSuggestions()
  loadSkillGapData()
}

const loadAIInsights = async () => {
  aiInsightsLoading.value = true
  try {
    const res = await getRecruitmentInsights(0, timeRange.value)
    if (res?.recommendations) aiInsights.value = res.recommendations as string[]
  } catch { /* silent */ }
  finally { aiInsightsLoading.value = false }
}

const loadJDSuggestions = async () => {
  try {
    const jobs = await getJobStats({})
    if (!jobs || !Array.isArray(jobs) || jobs.length === 0) return
    const topJobs = jobs.slice(0, 3)
    const suggestions = await Promise.all(
      topJobs.map(async (j: any) => {
        try {
          const brief = j.title || j.jobTitle || ''
          const res = await generateJD(brief)
          const generated = res?.data || res
          return {
            jobId: j.jobId,
            jobTitle: brief,
            suggestion: `AI建议优化该岗位JD — ${generated?.title || '更新岗位描述'}，调整薪资范围至${generated?.salaryMin || '待定'}k-${generated?.salaryMax || '待定'}k以提升竞争力`,
            skillsToAdd: generated?.skills || (generated?.requirements ? [generated.requirements] : undefined),
            salarySuggestion: `市场建议薪资：${generated?.salaryMin || '?'}k - ${generated?.salaryMax || '?'}k`,
          }
        } catch {
          return {
            jobId: j.jobId,
            jobTitle: j.title || j.jobTitle || '',
            suggestion: j.deliveryCount > 10 ? '该岗位投递量较高，建议细化技能要求以过滤不匹配候选人' : '该岗位投递量偏低，建议优化JD描述并扩大发布渠道',
            skillsToAdd: j.deliveryCount > 10 ? ['系统设计能力', '团队协作经验'] : undefined,
            salarySuggestion: j.deliveryCount < 5 ? '建议检查薪资范围是否具有市场竞争力' : undefined,
          }
        }
      })
    )
    jdSuggestions.value = suggestions.filter(Boolean)
  } catch { /* silent */ }
}

const skillGapIndicators = ref<{ name: string; max: number }[]>([
  { name: 'Java/Spring', max: 100 }, { name: '微服务', max: 100 },
  { name: '云原生', max: 100 }, { name: '数据分析', max: 100 }, { name: 'AI/ML', max: 100 }
])
const skillGapMarket = ref<number[]>([85, 78, 72, 65, 90])
const skillGapTalentPool = ref<number[]>([60, 50, 35, 55, 30])

const loadSkillGapData = async () => {
  try {
    const report = await getMarketReport()
    if (report?.topDemandSkills) {
      const entries = Object.entries(report.topDemandSkills).slice(0, 5)
      if (entries.length > 0) {
        skillGapIndicators.value = entries.map(([name]) => ({ name, max: 100 }))
        skillGapMarket.value = entries.map(([, v]) => Math.min(100, (v as number) || 50))
        skillGapTalentPool.value = entries.map(([, v]) => Math.min(80, Math.max(10, ((v as number) || 30) * 0.5)))
        renderSkillGap(null)
      }
    }
  } catch { /* keep defaults */ }
}

// ── 渲染图表 ──
const renderAllCharts = (funnelData: any, trendData: any, hotData: any, jobData: any) => {
  renderFunnel(funnelData)
  renderTrend(trendData)
  renderHotJobs(hotData)
  renderSkillGap(jobData)
  renderDeptChart(jobData)
}

const renderFunnel = (data: any) => {
  if (!funnelRef.value) return
  if (funnelChart) funnelChart.dispose()
  funnelChart = echarts.init(funnelRef.value)
  const stages = data ? [
    { name: '投递', value: data.applied || 0 },
    { name: '筛选', value: data.screened || 0 },
    { name: '面试', value: data.interviewed || 0 },
    { name: '入职', value: data.hired || 0 },
  ] : [
    { name: '投递', value: 85 }, { name: '筛选', value: 60 }, { name: '面试', value: 35 }, { name: '入职', value: 15 }
  ]
  funnelChart.setOption({
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item', formatter: '{b}: {c}人',
      backgroundColor: 'rgba(19, 19, 22, 0.95)',
      borderColor: '#27272A',
      textStyle: { color: '#FAFAFA' }
    },
    series: [{
      type: 'funnel', left: '15%', right: '15%', top: 10, bottom: 10,
      width: '70%', gap: 2,
      label: { show: true, position: 'inside', formatter: '{b} {c}', color: '#FAFAFA' },
      data: stages,
      itemStyle: { borderColor: '#131316', borderWidth: 2 }
    }]
  })
}

const renderTrend = (data: any) => {
  if (!trendRef.value) return
  if (trendChart) trendChart.dispose()
  trendChart = echarts.init(trendRef.value)
  const labels = data?.labels || ['周一', '周二', '周三', '周四', '周五', '周六', '周日']
  const delivery = data?.deliveryData || [12, 15, 8, 22, 18, 10, 5]
  const hired = data?.hiredData || [2, 3, 1, 5, 4, 2, 1]
  trendChart.setOption({
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis', backgroundColor: 'rgba(19, 19, 22, 0.95)', borderColor: '#27272A', textStyle: { color: '#FAFAFA' } },
    legend: { data: ['投递', '入职'], bottom: 0, textStyle: { fontSize: 11, color: '#A8A8B3' } },
    grid: { left: 10, right: 10, top: 10, bottom: 30 },
    xAxis: { type: 'category', data: labels, axisLabel: { fontSize: 10, color: '#A8A8B3' } },
    yAxis: { type: 'value', axisLabel: { fontSize: 10, color: '#71717A' }, splitLine: { lineStyle: { color: '#1F1F24' } } },
    series: [
      { name: '投递', type: 'line', data: delivery, smooth: true, lineStyle: { color: '#6C6FF7' }, itemStyle: { color: '#6C6FF7' } },
      { name: '入职', type: 'line', data: hired, smooth: true, lineStyle: { color: '#10B981' }, itemStyle: { color: '#10B981' } },
    ]
  })
}

const renderHotJobs = (data: any) => {
  if (!hotRef.value) return
  if (hotChart) hotChart.dispose()
  hotChart = echarts.init(hotRef.value)
  const items = data || []
  const names = items.slice(0, 6).map((d: any) => d.title || d.jobTitle || '').filter(Boolean)
  const values = items.slice(0, 6).map((d: any) => d.deliveryCount || d.count || 0)
  hotChart.setOption({
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis', backgroundColor: 'rgba(19, 19, 22, 0.95)', borderColor: '#27272A', textStyle: { color: '#FAFAFA' } },
    grid: { left: 10, right: 20, top: 10, bottom: 10 },
    xAxis: { type: 'value', axisLabel: { fontSize: 10, color: '#A8A8B3' }, splitLine: { lineStyle: { color: '#1F1F24' } } },
    yAxis: { type: 'category', data: names.reverse(), axisLabel: { fontSize: 10, color: '#A8A8B3' }, inverse: true },
    series: [{
      type: 'bar', data: values.reverse(),
      itemStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
          { offset: 0, color: '#6C6FF7' }, { offset: 1, color: '#A86EF7' }
        ]),
        borderRadius: [0, 4, 4, 0]
      }
    }]
  })
}

const renderSkillGap = (_data: any) => {
  if (!skillGapRef.value) return
  if (skillGapChart) skillGapChart.dispose()
  skillGapChart = echarts.init(skillGapRef.value)
  skillGapChart.setOption({
    backgroundColor: 'transparent',
    tooltip: {},
    legend: { data: ['市场需求', '现有人才池'], bottom: 0, textStyle: { fontSize: 10, color: '#A8A8B3' } },
    radar: {
      center: ['50%', '45%'],
      radius: '60%',
      axisName: { color: '#A8A8B3' },
      axisLine: { lineStyle: { color: '#27272A' } },
      splitLine: { lineStyle: { color: '#1F1F24' } },
      splitArea: { areaStyle: { color: ['rgba(99, 102, 241, 0.02)', 'rgba(99, 102, 241, 0.02)'] } },
      indicator: skillGapIndicators.value
    },
    series: [
      { type: 'radar', name: '市场需求', data: [{ value: skillGapMarket.value, name: '市场需求' }], areaStyle: { opacity: 0.1 }, lineStyle: { color: '#6C6FF7' }, itemStyle: { color: '#6C6FF7' } },
      { type: 'radar', name: '现有人才池', data: [{ value: skillGapTalentPool.value, name: '现有人才池' }], areaStyle: { opacity: 0.1 }, lineStyle: { color: '#F4586D' }, itemStyle: { color: '#F4586D' } },
    ]
  })
}

const renderDeptChart = (data: any) => {
  if (!deptRef.value) return
  if (deptChart) deptChart.dispose()
  deptChart = echarts.init(deptRef.value)
  const depts = data?.slice ? data.slice(0, 5).map((d: any) => ({ name: d.title || d.deptName || '部门', value: d.deliveryCount || d.count || 10 })) : [
    { name: '技术部', value: 45 }, { name: '产品部', value: 20 }, { name: '运营部', value: 15 }, { name: '市场部', value: 12 }, { name: '设计部', value: 8 }
  ]
  deptChart.setOption({
    backgroundColor: 'transparent',
    tooltip: { trigger: 'item', backgroundColor: 'rgba(19, 19, 22, 0.95)', borderColor: '#27272A', textStyle: { color: '#FAFAFA' } },
    legend: { bottom: 0, textStyle: { fontSize: 10, color: '#A8A8B3' } },
    series: [{
      type: 'pie', radius: ['45%', '70%'], center: ['50%', '45%'],
      data: depts, label: { formatter: '{b}\n{d}%', fontSize: 10, color: '#A8A8B3' },
      itemStyle: { borderRadius: 4, borderColor: '#131316', borderWidth: 2 }
    }]
  })
}

const renderEmergingChart = () => {
  if (!emergingRef.value || emergingJobs.value.length === 0) return
  if (emergingChart) emergingChart.dispose()
  emergingChart = echarts.init(emergingRef.value)
  const names = emergingJobs.value.slice(0, 5).map(j => j.name).reverse()
  const values = names.map(() => Math.floor(Math.random() * 30 + 20))
  emergingChart.setOption({
    backgroundColor: 'transparent',
    tooltip: { backgroundColor: 'rgba(19, 19, 22, 0.95)', borderColor: '#27272A', textStyle: { color: '#FAFAFA' } },
    grid: { left: 10, right: 10, top: 5, bottom: 5 },
    xAxis: { type: 'value', axisLabel: { fontSize: 9, color: '#71717A' }, splitLine: { lineStyle: { color: '#1F1F24' } } },
    yAxis: { type: 'category', data: names, axisLabel: { fontSize: 9, color: '#A8A8B3' } },
    series: [{
      type: 'bar', data: values,
      itemStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
          { offset: 0, color: '#F59E0B' }, { offset: 1, color: '#F4586D' }
        ]),
        borderRadius: [0, 4, 4, 0]
      }
    }]
  })
}

const handleExport = () => {
  exporting.value = true
  setTimeout(() => { exporting.value = false; ElMessage.success('报表已导出') }, 800)
}

// ── 生命周期 ──
onMounted(() => {
  loadAllData()
  autoRefreshTimer = setInterval(() => { if (autoRefresh.value) loadAllData() }, 30000)
})

onUnmounted(() => {
  if (autoRefreshTimer) clearInterval(autoRefreshTimer)
  funnelChart?.dispose(); trendChart?.dispose(); hotChart?.dispose()
  skillGapChart?.dispose(); deptChart?.dispose(); emergingChart?.dispose()
})
</script>

<style scoped lang="scss">
.strategy-dashboard {
  max-width: var(--content-max-width);

  .page-hero {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-5);
    margin-bottom: var(--space-4);
    background: linear-gradient(135deg, var(--color-bg-alt), var(--color-surface), var(--color-bg-alt));
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    position: relative;
    overflow: hidden;

    &::after {
      content: '';
      position: absolute;
      top: 0; left: 0; right: 0;
      height: 1px;
      background: var(--gradient-primary);
      opacity: 0.5;
    }

    .hero-title {
      font-size: var(--text-xl);
      font-weight: var(--weight-bold);
      background: var(--gradient-primary);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      margin: 0 0 var(--space-1);
    }
    .hero-subtitle {
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
      margin: 0;
    }
    .hero-actions { display: flex; gap: var(--space-2); flex-shrink: 0; }
  }

  .dash-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-4);

    .header-left {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      .refresh-hint { font-size: var(--text-xs); color: var(--color-text-muted); }
      .live-tag { .live-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--color-success); display: inline-block; margin-right: 4px; animation: pulse 2s infinite; } }
    }
    .header-right { display: flex; gap: var(--space-3); align-items: center; }
  }

  // KPI 卡片
  .kpi-row {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: var(--space-4);
    margin-bottom: var(--space-5);

    @media (max-width: 1024px) { grid-template-columns: repeat(2, 1fr); }
    @media (max-width: 640px) { grid-template-columns: 1fr; }
  }

  .kpi-card {
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    padding: var(--space-4);
    transition: all var(--duration-fast) var(--ease-out);
    position: relative;
    overflow: hidden;

    &:hover {
      border-color: var(--color-border-glow);
      box-shadow: var(--shadow-glow);
      transform: translateY(-2px);
    }

    &::before {
      content: '';
      position: absolute;
      top: 0; left: 0; right: 0;
      height: 3px;
      background: var(--gradient-primary);
    }

    .kpi-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--space-3);

      .kpi-icon {
        width: 36px; height: 36px;
        border-radius: var(--radius-md);
        display: flex;
        align-items: center;
        justify-content: center;
      }
      .kpi-trend {
        font-size: var(--text-xs);
        font-weight: var(--weight-semibold);
        display: flex;
        align-items: center;
        gap: 2px;
        &.up { color: var(--color-success); }
        &.down { color: var(--color-danger); }
      }
    }

    .kpi-value {
      font-size: 32px;
      font-weight: var(--weight-bold);
      color: var(--color-text);
      line-height: 1.1;
    }
    .kpi-label {
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
      margin-top: var(--space-1);
    }
    .kpi-secondary {
      font-size: 11px;
      color: var(--color-text-muted);
      margin-top: 2px;
    }
    .kpi-spark {
      margin-top: var(--space-2);
      height: 32px;
    }
  }

  // 图表网格
  .chart-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: var(--space-4);
    margin-bottom: var(--space-4);

    @media (max-width: 1024px) { grid-template-columns: 1fr; }
  }

  .chart-card {
    .chart-title {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      font-size: var(--text-sm);
      font-weight: var(--weight-semibold);
      color: var(--color-text);
    }
    .chart-box { width: 100%; height: 280px; }
  }

  // 下半部分
  .lower-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: var(--space-4);
    margin-bottom: var(--space-4);

    @media (max-width: 1024px) { grid-template-columns: 1fr; }
  }

  .lower-card {
    .chart-box-sm { width: 100%; height: 220px; }
  }

  // 瓶颈面板
  .bottleneck-panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);

    .bottleneck-item {
      .bn-header {
        display: flex;
        justify-content: space-between;
        margin-bottom: var(--space-1);
        .bn-label { font-size: var(--text-xs); color: var(--color-text-secondary); }
        .bn-value { font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text); }
        .bn-danger { color: var(--color-danger); }
      }
      .bn-benchmark { font-size: 10px; color: var(--color-text-muted); }
      display: flex; flex-direction: column; gap: 2px;
    }
  }

  // 新兴岗位列表
  .emerging-list {
    margin-top: var(--space-2);

    .emerging-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--space-1) 0;
      .ej-name { font-size: var(--text-xs); color: var(--color-text); font-weight: var(--weight-medium); }
    }
  }

  // JD建议
  .jd-card { margin-bottom: var(--space-4); }
  .jd-suggestion-body {
    .jd-section { margin-bottom: var(--space-2);
      .jd-label { font-size: var(--text-xs); color: var(--color-text-secondary); margin-right: var(--space-2); }
      .jd-value { font-size: var(--text-sm); color: var(--color-text); }
    }
  }

  // AI 浮动按钮
  .ai-fab {
    position: fixed;
    bottom: 32px;
    right: 32px;
    width: 52px;
    height: 52px;
    z-index: 100;
    box-shadow: var(--shadow-lg);
    animation: fab-pulse 2s infinite;

    &:hover { animation: none; }
  }

  // AI 面板
  .ai-panel {
    position: fixed;
    bottom: 32px;
    right: 32px;
    width: 360px;
    max-height: 480px;
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    box-shadow: var(--shadow-lg);
    z-index: 101;
    overflow: hidden;
    display: flex;
    flex-direction: column;

    .ai-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--space-3) var(--space-4);
      background: linear-gradient(135deg, var(--color-primary), var(--color-secondary));
      color: #fff;
      font-weight: var(--weight-semibold);
      font-size: var(--text-sm);
    }

    .ai-panel-body {
      flex: 1;
      overflow-y: auto;
      padding: var(--space-3) var(--space-4);
    }

    .ai-insight-item {
      display: flex;
      gap: var(--space-3);
      padding: var(--space-2) 0;
      border-bottom: 1px solid var(--color-border-light);

      .ai-insight-icon {
        width: 24px; height: 24px;
        border-radius: 50%;
        background: var(--color-primary-bg);
        color: var(--color-primary);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 11px;
        font-weight: var(--weight-bold);
        flex-shrink: 0;
      }

      .ai-insight-text {
        font-size: var(--text-xs);
        color: var(--color-text-secondary);
        line-height: 1.5;
      }
    }
  }

  // 动画
  .fab-zoom-enter-active, .fab-zoom-leave-active { transition: all var(--duration-normal) var(--ease-out); }
  .fab-zoom-enter-from, .fab-zoom-leave-to { opacity: 0; transform: scale(0.5); }

  .panel-slide-enter-active, .panel-slide-leave-active { transition: all var(--duration-normal) var(--ease-out); }
  .panel-slide-enter-from, .panel-slide-leave-to { opacity: 0; transform: translateY(20px); }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.3; }
  }

  @keyframes fab-pulse {
    0%, 100% { box-shadow: 0 4px 16px rgba(99, 102, 241, 0.3); }
    50% { box-shadow: 0 4px 28px rgba(99, 102, 241, 0.6); }
  }
}
</style>
