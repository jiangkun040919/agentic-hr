<template>
  <div class="dashboard">
    <!-- ═══ 欢迎横幅 ═══ -->
    <div class="welcome-banner">
      <div class="welcome-left">
        <h1>工作台概览</h1>
        <p>{{ today }}</p>
      </div>
      <div class="welcome-actions">
        <el-button type="primary" size="large" @click="$router.push('/admin/jobs/add')">
          <el-icon><Plus /></el-icon>发布岗位
        </el-button>
        <el-button size="large" @click="$router.push('/admin/smart-screening')">查看简历</el-button>
      </div>
    </div>

    <!-- ═══ 统计卡片 + 迷你趋势图 ═══ -->
    <div class="stat-row">
      <div class="stat-card kpi-enhanced" style="--card-color: var(--color-primary)" v-for="(kpi, i) in kpiCards" :key="i">
        <div class="ke-top">
          <div class="sc-icon"><el-icon :size="22"><component :is="kpi.icon" /></el-icon></div>
          <div class="sc-body">
            <div class="sc-num count-animate">{{ animated[i] }}</div>
            <div class="sc-label">{{ kpi.label }}</div>
          </div>
        </div>
        <div class="sparkline-box">
          <svg width="100%" height="28" viewBox="0 0 100 28" preserveAspectRatio="none">
            <defs>
              <linearGradient :id="'spark-grad-' + i" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" :stop-color="kpi.color" stop-opacity="0.3" />
                <stop offset="100%" :stop-color="kpi.color" stop-opacity="0" />
              </linearGradient>
            </defs>
            <polygon :points="kpi.areaPts" :fill="'url(#spark-grad-' + i + ')'" />
            <polyline :points="kpi.linePts" fill="none" :stroke="kpi.color" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </div>
        <div class="kpi-trend-badge" :class="kpi.trend >= 0 ? 'up' : 'down'">
          <el-icon :size="12"><component :is="kpi.trend >= 0 ? 'Top' : 'Bottom'" /></el-icon>
          <span>{{ Math.abs(kpi.trend) }}% {{ kpi.trend >= 0 ? '增长' : '下降' }}</span>
        </div>
      </div>
    </div>

    <!-- ═══ 招聘管道 ═══ -->
    <div class="pipeline-row">
      <div v-for="(stage, i) in pipelineStages" :key="i" class="pipe-wrapper">
        <div class="pipe-card" :class="{ active: i === 0 }">
          <div class="pipe-num">{{ stage.count }}</div>
          <div class="pipe-name">{{ stage.label }}</div>
        </div>
        <div v-if="i < pipelineStages.length - 1" class="pipe-arrow">
          <el-icon><ArrowRight /></el-icon>
        </div>
      </div>
    </div>

    <!-- ═══ ECharts 图表区 ═══ -->
    <div class="chart-row">
      <div class="content-card chart-card">
        <div class="card-hdr">
          <span class="card-hdr-title">
            <el-icon color="var(--color-accent)"><TrendCharts /></el-icon> 招聘趋势 (近30天)
          </span>
        </div>
        <div ref="trendChartRef" class="chart-inner" />
      </div>
      <div class="content-card chart-card">
        <div class="card-hdr">
          <span class="card-hdr-title">
            <el-icon color="var(--color-gold)"><PieChart /></el-icon> 部门岗位分布
          </span>
        </div>
        <div ref="pieChartRef" class="chart-inner" />
      </div>
    </div>

    <!-- ═══ 主内容区 ═══ -->
    <div class="dashboard-grid">
      <!-- 待处理简历 -->
      <div class="content-card">
        <div class="card-hdr">
          <span class="card-hdr-title">
            <el-icon color="var(--color-accent)"><WarningFilled /></el-icon> 待处理简历
          </span>
          <el-tag type="warning" size="small" round>{{ pendingCount }}</el-tag>
        </div>
        <el-table :data="pendingResumes" max-height="340" stripe>
          <el-table-column prop="candidateName" label="姓名" width="90" />
          <el-table-column prop="jobTitle" label="投递岗位" min-width="140" show-overflow-tooltip />
          <el-table-column prop="deliverTime" label="投递时间" width="140">
            <template #default="{ row }">{{ formatDate(row.deliverTime) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="80">
            <template #default="{ row }">
              <el-button size="small" type="primary" link @click="goToResume(row.deliveryId)">查看</el-button>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-if="pendingResumes.length === 0" description="暂无待处理简历" :image-size="60" />
      </div>

      <!-- 今日面试 -->
      <div class="content-card">
        <div class="card-hdr">
          <span class="card-hdr-title">
            <el-icon color="var(--color-primary)"><Calendar /></el-icon> 今日面试
          </span>
        </div>
        <div v-if="todayInterviews.length > 0" class="interview-list">
          <div v-for="(iv, i) in todayInterviews" :key="i" class="iv-item">
            <div class="iv-time">{{ iv.scheduleTime?.split('T')[1]?.slice(0,5) || '--:--' }}</div>
            <div class="iv-info">
              <div class="iv-name">{{ iv.candidateName }}</div>
              <div class="iv-job">{{ iv.jobTitle }} · {{ iv.location || '线上' }}</div>
            </div>
            <el-tag size="small" round>{{ iv.round || '初试' }}</el-tag>
          </div>
        </div>
        <el-empty v-else description="今日无面试安排" :image-size="60" />
      </div>

      <!-- 最近投递 (增强状态标签) -->
      <div class="content-card">
        <div class="card-hdr">
          <span class="card-hdr-title">
            <el-icon color="var(--color-success)"><Clock /></el-icon> 最近投递
          </span>
        </div>
        <div v-if="recentDeliveries.length > 0" class="recent-list">
          <div v-for="(d, i) in recentDeliveries.slice(0, 6)" :key="i" class="recent-item" @click="goToResume(d.deliveryId)">
            <div class="ri-avatar">{{ d.candidateName?.charAt(0) }}</div>
            <div class="ri-info">
              <div class="ri-name">{{ d.candidateName }}</div>
              <div class="ri-job">{{ d.jobTitle }}</div>
            </div>
            <div class="ri-right">
              <span class="status-tag-3d" :class="statusClassFor(d.status)">{{ getStatusText(d.status) }}</span>
              <div class="ri-time">{{ formatDate(d.deliverTime) }}</div>
            </div>
          </div>
        </div>
        <el-empty v-else description="暂无投递" :image-size="60" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import { getDashboardData, getTrendData } from '@/api/stat'
import {
  Briefcase, Document, VideoCamera, Medal, Plus, WarningFilled, Calendar, Clock, ArrowRight,
  Top, Bottom, TrendCharts, PieChart
} from '@element-plus/icons-vue'
import dayjs from 'dayjs'

const router = useRouter()
const today = dayjs().format('YYYY年MM月DD日 dddd')

const stats = reactive({ openJobs: 0, totalDeliveries: 0, interviews: 0, hired: 0 })
const animated = reactive([0, 0, 0, 0])
const pendingResumes = ref<any[]>([])
const todayInterviews = ref<any[]>([])
const recentDeliveries = ref<any[]>([])
const pendingCount = computed(() => pendingResumes.value.length)

// KPI卡片配置
const kpiCards = reactive([
  { label: '开放岗位', key: 'jobs', icon: 'Briefcase', color: 'var(--color-primary)', trend: 0, areaPts: '', linePts: '' },
  { label: '简历投递', key: 'deliveries', icon: 'Document', color: 'var(--color-gold)', trend: 0, areaPts: '', linePts: '' },
  { label: '面试安排', key: 'interviews', icon: 'VideoCamera', color: 'var(--color-success)', trend: 0, areaPts: '', linePts: '' },
  { label: '已录用/入职', key: 'hired', icon: 'Medal', color: 'var(--color-rose)', trend: 0, areaPts: '', linePts: '' },
])

// 生成 sparkline 数据点
const generateSparkPts = (length: number, max: number) => {
  const pts: number[] = []
  for (let i = 0; i < length; i++) {
    pts.push(max - Math.random() * max * 0.7)
  }
  return pts
}

const sparkData = reactive<{ linePts: string; areaPts: string }[]>([
  genSpark(8, 24), genSpark(8, 24), genSpark(8, 24), genSpark(8, 24),
])

function genSpark(len: number, h: number) {
  const pts = generateSparkPts(len, h)
  const step = 100 / (len - 1)
  const line = pts.map((v, i) => `${(i * step).toFixed(1)},${(h - v).toFixed(1)}`).join(' ')
  const area = `0,${h} ${line} 100,${h}`
  return { linePts: line, areaPts: area }
}

// 为 kpiCards 注入 sparkline
kpiCards.forEach((kpi, i) => {
  Object.assign(kpi, sparkData[i])
})

const animateKpi = (index: number, target: number) => {
  let current = 0
  const step = Math.max(1, Math.ceil(target / 40))
  const timer = setInterval(() => {
    current += step
    if (current >= target) { animated[index] = target; clearInterval(timer) }
    else { animated[index] = current }
  }, 30)
}

const pipelineStages = reactive([
  { label: '待查看', count: 0 },
  { label: '面试中', count: 0 },
  { label: '实习中', count: 0 },
  { label: '正式入职', count: 0 },
  { label: '已淘汰', count: 0 },
])

// ====== ECharts ======
const trendChartRef = ref<HTMLElement>()
const pieChartRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null
let pieChart: echarts.ECharts | null = null

const initTrendChart = () => {
  if (!trendChartRef.value) return
  trendChart = echarts.init(trendChartRef.value)
  const days: string[] = []
  for (let i = 29; i >= 0; i--) days.push(dayjs().subtract(i, 'day').format('MM-DD'))
  const deliveries = days.map(() => Math.round(8 + Math.random() * 45))
  const interviews = days.map(() => Math.round(2 + Math.random() * 15))
  const hires = days.map(() => Math.round(0.5 + Math.random() * 5))
  trendChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#1C1C2E', borderColor: '#2A2A35', textStyle: { color: '#E8E8ED' } },
    legend: { top: 0, textStyle: { color: '#A8A8B3', fontSize: 12 } },
    grid: { left: '3%', right: '4%', bottom: '3%', top: '35px', containLabel: true },
    xAxis: { type: 'category', data: days, axisLabel: { color: '#A8A8B3', fontSize: 10, interval: 4 }, axisLine: { lineStyle: { color: '#2A2A35' } } },
    yAxis: { type: 'value', axisLabel: { color: '#A8A8B3' }, splitLine: { lineStyle: { color: '#22222D' } } },
    series: [
      { name: '投递', type: 'line', data: deliveries, smooth: true, lineStyle: { color: '#6C6FF7', width: 2 }, itemStyle: { color: '#6C6FF7' }, areaStyle: { color: 'rgba(108,111,247,0.08)' } },
      { name: '面试', type: 'line', data: interviews, smooth: true, lineStyle: { color: '#F0A500', width: 2 }, itemStyle: { color: '#F0A500' }, areaStyle: { color: 'rgba(240,165,0,0.08)' } },
      { name: '入职', type: 'line', data: hires, smooth: true, lineStyle: { color: '#2DD4A3', width: 2 }, itemStyle: { color: '#2DD4A3' }, areaStyle: { color: 'rgba(45,212,163,0.08)' } },
    ],
  })
}

// 部门颜色映射
const deptColorMap: Record<string, string> = {
  '技术部': '#6C6FF7', '产品部': '#22C5DE', '数据部': '#F0A500',
  '研究院': '#A86EF7', '设计部': '#F472B6', '市场部': '#34D399',
  '运营部': '#8B8EF9', '财务部': '#F4586D', '人力资源部': '#F0A500',
  'AI部': '#9B7ED8',
}

const initPieChart = (deptData?: { name: string; value: number }[]) => {
  if (!pieChartRef.value) return
  pieChart = echarts.init(pieChartRef.value)
  const pieData = (deptData && deptData.length > 0)
    ? deptData.map(d => ({ value: d.value, name: d.name, itemStyle: { color: deptColorMap[d.name] || '#6C6FF7' } }))
    : [{ value: 1, name: '暂无数据', itemStyle: { color: '#666' } }]
  pieChart.setOption({
    tooltip: { trigger: 'item', backgroundColor: '#1C1C2E', borderColor: '#2A2A35', textStyle: { color: '#E8E8ED' } },
    legend: { bottom: 5, textStyle: { color: '#A8A8B3', fontSize: 11 } },
    series: [{
      type: 'pie', radius: ['45%', '75%'], center: ['50%', '45%'],
      avoidLabelOverlap: true, padAngle: 2,
      itemStyle: { borderRadius: 6, borderColor: '#141420', borderWidth: 2 },
      label: { show: false },
      emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' }, itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' } },
      data: pieData,
    }],
  })
}

const resizeCharts = () => { trendChart?.resize(); pieChart?.resize() }

onMounted(() => {
  fetchDashboardData()
  initTrendChart()
  // 饼图先画占位，等 fetchDashboardData 拿到真实数据后再更新
  initPieChart()
  window.addEventListener('resize', resizeCharts)
})

onUnmounted(() => {
  trendChart?.dispose(); pieChart?.dispose()
  window.removeEventListener('resize', resizeCharts)
})

const fetchDashboardData = async () => {
  try {
    const response = await getDashboardData()
    const data = (response as any).data || response
    Object.assign(stats, data.stats)
    pendingResumes.value = data.pendingResumes || []
    todayInterviews.value = data.todayInterviews || []
    recentDeliveries.value = data.recentDeliveries || []
    const all = [...(data.pendingResumes || []), ...(data.recentDeliveries || [])]
    pipelineStages[0].count = all.filter((d: any) => d.status === 0).length
    pipelineStages[1].count = all.filter((d: any) => d.status === 2).length
    pipelineStages[2].count = all.filter((d: any) => d.status === 3).length
    pipelineStages[3].count = all.filter((d: any) => d.status === 4).length
    pipelineStages[4].count = all.filter((d: any) => d.status >= 5).length
    animateKpi(0, stats.openJobs)
    animateKpi(1, stats.totalDeliveries)
    animateKpi(2, stats.interviews)
    animateKpi(3, stats.hired)
    // 用真实部门分布更新饼图
    if ((data as any).deptDistribution) {
      initPieChart((data as any).deptDistribution)
    }
    // 获取趋势数据计算环比变化
    fetchTrendData()
  } catch (e) { console.error('Dashboard load failed', e) }
}

// 获取趋势数据，计算 KPI 环比趋势
const fetchTrendData = async () => {
  try {
    const trendRes = await getTrendData({ days: 14 }) as any
    const td = trendRes?.data || trendRes
    if (!td?.deliveryData) return
    const dData = td.deliveryData as number[]
    const hData = td.hiredData as number[]
    // 前7天 vs 后7天比较
    const mid = Math.floor(dData.length / 2)
    const firstHalf = dData.slice(0, mid)
    const secondHalf = dData.slice(mid)
    const firstSum = firstHalf.reduce((a: number, b: number) => a + b, 0)
    const secondSum = secondHalf.reduce((a: number, b: number) => a + b, 0)
    const firstHired = hData.slice(0, mid).reduce((a: number, b: number) => a + b, 0) || 1
    const secondHired = hData.slice(mid).reduce((a: number, b: number) => a + b, 0) || 1
    kpiCards[1].trend = firstSum > 0 ? Math.round((secondSum - firstSum) / firstSum * 100) : 0
    kpiCards[3].trend = firstHired > 0 ? Math.round((secondHired - firstHired) / firstHired * 100) : 0
  } catch { /* 趋势数据获取失败，保持0 */ }
}

const formatDate = (date: string) => dayjs(date).format('MM-DD HH:mm')

const getStatusText = (status: number) => ['待查看', '已查看', '面试中', '实习中', '正式入职', '已淘汰'][status] || '未知'

const statusClassFor = (status: number) => {
  const map: Record<number, string> = { 0: 'st-pending', 1: 'st-review', 2: 'st-interview', 3: 'st-intern', 4: 'st-hired', 5: 'st-rejected' }
  return map[status] || ''
}

const goToResume = (id: number) => router.push(`/admin/smart-screening?candidateId=${id}`)
</script>

<style scoped lang="scss">
.dashboard {
  width: 100%;
}

// ====== 欢迎横幅 ======
.welcome-banner {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: linear-gradient(135deg, var(--color-bg-alt), var(--color-surface), var(--color-bg-alt));
  border: 1px solid var(--color-border);
  border-radius: var(--radius-xl);
  padding: var(--space-6);
  margin-bottom: var(--space-5);
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

  .welcome-left {
    h1 {
      font-size: var(--text-2xl); font-weight: var(--weight-bold);
      background: var(--gradient-primary);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      margin: 0;
    }
    p { color: var(--color-text-secondary); font-size: var(--text-sm); margin: var(--space-1) 0 0; }
  }

  .welcome-actions { display: flex; gap: var(--space-3); }
}

// ====== KPI增强卡片 ======
.stat-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--space-4);
  margin-bottom: var(--space-5);

  @media (max-width: 1024px) { grid-template-columns: repeat(2, 1fr); }
}

.stat-card.kpi-enhanced {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  box-shadow: var(--shadow-md);
  border: 1px solid transparent;
  transition: all 0.25s var(--ease-out);
  position: relative;
  overflow: hidden;

  &::after {
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0;
    height: 3px;
    background: linear-gradient(90deg, var(--card-color), color-mix(in srgb, var(--card-color) 40%, transparent));
    border-radius: var(--radius-lg) var(--radius-lg) 0 0;
  }

  &:hover {
    transform: translateY(-3px);
    box-shadow: var(--shadow-lg);
    border-color: var(--color-border);
  }

  .ke-top {
    display: flex; align-items: flex-start; gap: var(--space-4);
    .sc-icon {
      width: 48px; height: 48px;
      border-radius: var(--radius-md);
      background: linear-gradient(135deg, color-mix(in srgb, var(--card-color) 18%, transparent), color-mix(in srgb, var(--card-color) 6%, transparent));
      color: var(--card-color);
      display: flex; align-items: center; justify-content: center;
      flex-shrink: 0;
      box-shadow: 0 2px 8px color-mix(in srgb, var(--card-color) 15%, transparent);
    }
    .sc-body {
      .sc-num {
        font-size: 28px; font-weight: var(--weight-bold); color: var(--color-text);
        font-family: var(--font-sans); font-variant-numeric: tabular-nums; line-height: 1.1;
      }
      .sc-label { font-size: var(--text-sm); color: var(--color-text-secondary); margin-top: 3px; font-weight: var(--weight-medium); }
    }
  }

  .sparkline-box {
    margin: var(--space-3) 0 var(--space-2); height: 32px;
    svg { display: block; }
  }

  .kpi-trend-badge {
    display: inline-flex; align-items: center; gap: 4px;
    font-size: 12px; font-weight: var(--weight-medium);
    padding: 2px 8px;
    border-radius: var(--radius-full);
    &.up { color: var(--color-success); background: color-mix(in srgb, var(--color-success) 8%, transparent); }
    &.down { color: var(--color-danger); background: color-mix(in srgb, var(--color-danger) 8%, transparent); }
  }
}

// ====== 图表行 ======
.chart-row {
  display: grid; grid-template-columns: 1.5fr 1fr;
  gap: var(--space-4); margin-bottom: var(--space-5);

  @media (max-width: 1024px) { grid-template-columns: 1fr; }
}

.chart-card {
  overflow: hidden;
  .chart-inner { width: 100%; height: 260px; padding: var(--space-3); }
}

// ====== 招聘管道 ======
.pipeline-row {
  display: flex;
  align-items: center;
  margin-bottom: var(--space-5);
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  padding: var(--space-4) var(--space-5);
  box-shadow: var(--shadow-card);
}

.pipe-wrapper {
  display: flex; align-items: center; flex: 1;
}

.pipe-card {
  flex: 1; text-align: center; padding: var(--space-3) var(--space-2);
  border-radius: var(--radius-md); background: var(--color-bg);
  transition: all var(--duration-fast) var(--ease-out);

  &:hover { background: var(--color-surface-hover); }

  &.active {
    background: var(--color-primary-bg);
    box-shadow: 0 0 0 1px var(--color-border-glow);
    .pipe-num { color: var(--color-primary); }
  }

  .pipe-num {
    font-size: 22px; font-weight: var(--weight-bold); color: var(--color-text);
    font-family: var(--font-mono); font-variant-numeric: tabular-nums;
  }

  .pipe-name {
    font-size: var(--text-xs); color: var(--color-text-secondary); margin-top: 2px;
  }
}

.pipe-arrow { color: var(--color-text-muted); margin: 0 var(--space-1); flex-shrink: 0; }

// ====== 网格布局 ======
.dashboard-grid {
  display: grid;
  grid-template-columns: 1.4fr 1fr;
  gap: var(--space-4);

  @media (max-width: 1024px) { grid-template-columns: 1fr; }
}

// ====== 内容卡片 ======
.content-card {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  box-shadow: var(--shadow-card);
  overflow: hidden;
  transition: border-color var(--duration-fast) var(--ease-out);

  &:hover {
    border-color: var(--color-border-glow);
  }
}

.card-hdr {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--color-border-light);

  .card-hdr-title {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-weight: var(--weight-semibold);
    font-size: var(--text-base);
    color: var(--color-text);
  }
}

// ====== 面试列表 ======
.interview-list {
  padding: var(--space-2) 0;
}

.iv-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-5);
  border-bottom: 1px solid var(--color-border-light);
  transition: background var(--duration-fast) var(--ease-out);

  &:hover { background: var(--color-surface-hover); }
  &:last-child { border-bottom: none; }

  .iv-time {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    color: var(--color-primary);
    font-family: var(--font-mono);
    min-width: 48px;
    flex-shrink: 0;
  }

  .iv-info {
    flex: 1; min-width: 0;

    .iv-name { font-size: var(--text-sm); font-weight: var(--weight-medium); color: var(--color-text); }
    .iv-job {
      font-size: var(--text-xs); color: var(--color-text-secondary);
      margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
  }
}

// ====== 最近投递 ======
.recent-list { padding: var(--space-2) 0; }

.recent-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-5);
  border-bottom: 1px solid var(--color-border-light);
  cursor: pointer;
  transition: background var(--duration-fast) var(--ease-out);

  &:hover { background: var(--color-surface-hover); }
  &:last-child { border-bottom: none; }

  .ri-avatar {
    width: 34px; height: 34px; border-radius: 50%;
    background: var(--gradient-primary); color: #fff;
    display: flex; align-items: center; justify-content: center;
    font-size: var(--text-sm); font-weight: var(--weight-semibold); flex-shrink: 0;
  }

  .ri-info {
    flex: 1; min-width: 0;

    .ri-name { font-size: var(--text-sm); color: var(--color-text); font-weight: var(--weight-medium); }
    .ri-job {
      font-size: var(--text-xs); color: var(--color-text-secondary);
      margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
  }

  .ri-right {
    text-align: right; flex-shrink: 0;

    .ri-time { font-size: 11px; color: var(--color-text-muted); margin-top: var(--space-1); }
  }
}
</style>
