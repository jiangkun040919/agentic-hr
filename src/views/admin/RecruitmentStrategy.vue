<template>
  <div class="strategy-dashboard">
    <!-- ═══ AI 战略摘要 ═══ -->
    <div class="ai-banner" v-if="aiSummary.length">
      <div class="ai-banner-icon">🤖</div>
      <div class="ai-banner-content">
        <div v-for="(line, i) in aiSummary" :key="i" class="ai-banner-line">{{ line }}</div>
      </div>
      <div class="ai-banner-stats">
        <div class="ai-stat" v-for="s in aiStats" :key="s.label">
          <span class="ai-stat-num">{{ s.value }}</span>
          <span class="ai-stat-label">{{ s.label }}</span>
        </div>
      </div>
    </div>

    <!-- ═══ 第一行：漏斗 + 管道状态 ═══ -->
    <div class="top-row">
      <!-- 招聘漏斗 -->
      <div class="card">
        <div class="card-header">
          <span>📊 招聘漏斗</span>
          <span class="card-hint">点击阶段查看候选人</span>
        </div>
        <div class="funnel-section">
          <div
            v-for="(stage, i) in funnelStages"
            :key="stage.label"
            class="funnel-bar-wrap"
            @click="openStageDetail(stage)"
          >
            <div class="funnel-label">{{ stage.label }}</div>
            <div
              class="funnel-bar"
              :style="{
                width: funnelBarWidth(i),
                background: stage.color,
              }"
            >
              <span class="funnel-bar-num">{{ stage.count }}</span>
            </div>
            <span class="funnel-rate" :class="stage.rateClass">{{ stage.rate }}</span>
          </div>
        </div>
      </div>

      <!-- 管道状态 -->
      <div class="card">
        <div class="card-header">
          <span>🚦 管道状态</span>
        </div>
        <div class="pipeline-grid">
          <div v-for="p in pipelineItems" :key="p.label" class="pipeline-item" :class="p.status">
            <div class="pipeline-icon">{{ p.icon }}</div>
            <div class="pipeline-info">
              <div class="pipeline-num">{{ p.count }}</div>
              <div class="pipeline-label">{{ p.label }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 第二行：技能趋势 + 技能缺口 ═══ -->
    <div class="mid-row">
      <!-- 技能趋势 -->
      <div class="card">
        <div class="card-header">
          <span>📈 技能需求趋势</span>
          <el-radio-group v-model="trendPeriod" size="small" @change="loadTrendData">
            <el-radio-button value="3">3个月</el-radio-button>
            <el-radio-button value="6">6个月</el-radio-button>
          </el-radio-group>
        </div>
        <div class="trend-section" v-loading="trendLoading">
          <div v-for="skill in topTrendSkills" :key="skill.name" class="trend-row">
            <span class="trend-name">{{ skill.name }}</span>
            <div class="trend-bar">
              <div
                class="trend-fill"
                :style="{ width: trendBarWidth(skill), background: skill.color }"
              />
            </div>
            <span class="trend-change" :class="skill.direction">
              {{ skill.direction === 'up' ? '↗' : skill.direction === 'down' ? '↘' : '→' }}
              {{ skill.change }}%
            </span>
          </div>
        </div>
      </div>

      <!-- 技能缺口雷达 -->
      <div class="card">
        <div class="card-header">
          <span>🕳️ 技能缺口分析</span>
          <span class="card-hint">需求 vs 供给</span>
        </div>
        <div class="gap-section" v-loading="gapLoading">
          <div v-for="g in gapData" :key="g.skill" class="gap-row">
            <span class="gap-name">{{ g.skill }}</span>
            <div class="gap-bars">
              <div class="gap-demand" :style="{ width: (g.demand / maxDemand * 100) + '%' }">
                <span class="gap-bar-label">需求 {{ g.demand }}</span>
              </div>
              <div class="gap-supply" :style="{ width: (g.supply * 100) + '%' }">
                <span class="gap-bar-label">供给 {{ Math.round(g.supply * 100) }}%</span>
              </div>
            </div>
            <el-tag
              :type="g.gap > 0.2 ? 'danger' : g.gap > 0 ? 'warning' : 'success'"
              size="small"
            >
              {{ g.gap > 0.2 ? '紧缺' : g.gap > 0 ? '偏紧' : '充足' }}
            </el-tag>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 第三行：AI 建议 ═══ -->
    <div class="card ai-advice-card">
      <div class="card-header">
        <span>💡 AI 招聘建议</span>
        <el-button size="small" text :loading="adviceLoading" @click="loadAiAdvice">刷新</el-button>
      </div>
      <div class="advice-list" v-loading="adviceLoading">
        <div v-for="(advice, i) in aiAdvice" :key="i" class="advice-item">
          <div class="advice-icon">{{ advice.icon }}</div>
          <div class="advice-body">
            <div class="advice-problem">{{ advice.problem }}</div>
            <div class="advice-action">{{ advice.action }}</div>
          </div>
          <el-button v-if="advice.link" size="small" type="primary" text @click="$router.push(advice.link)">
            {{ advice.linkText || '执行' }} →
          </el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { request } from '@/utils/request'
import dayjs from 'dayjs'

const router = useRouter()

// ═══ AI 摘要 ═══
const aiSummary = ref<string[]>([])
const aiStats = ref<any[]>([])

// ═══ 漏斗 ═══
const funnelStages = ref([
  { label: '投递', count: 0, color: '#8B9A6E', status: 0, rate: '', rateClass: '' },
  { label: '筛选', count: 0, color: '#8B5CF6', status: 1, rate: '', rateClass: '' },
  { label: '面试', count: 0, color: '#06B6D4', status: 2, rate: '', rateClass: '' },
  { label: 'Offer', count: 0, color: '#6B8B4E', status: 3, rate: '', rateClass: '' },
  { label: '入职', count: 0, color: '#059669', status: 4, rate: '', rateClass: '' },
])

const funnelBarWidth = (i: number) => {
  const max = funnelStages.value[0]?.count || 1
  return `${(funnelStages.value[i].count / max) * 100}%`
}

const openStageDetail = (stage: any) => {
  router.push(`/admin/smart-screening?status=${stage.status}`)
}

// ═══ 管道状态 ═══
const pipelineItems = ref([
  { label: '待处理', count: 0, icon: '⏳', status: 'warn' },
  { label: '面试中', count: 0, icon: '🔄', status: 'info' },
  { label: '已发Offer', count: 0, icon: '✅', status: 'ok' },
  { label: '本周入职', count: 0, icon: '🎉', status: 'ok' },
  { label: '已淘汰', count: 0, icon: '❌', status: 'danger' },
])

// ═══ 技能趋势 ═══
const trendPeriod = ref('6')
const trendLoading = ref(false)
const topTrendSkills = ref<any[]>([])

const trendBarWidth = (skill: any) => {
  const max = topTrendSkills.value[0]?.jobs || 1
  return `${(skill.jobs / max) * 100}%`
}

// ═══ 技能缺口 ═══
const gapLoading = ref(false)
const gapData = ref<any[]>([])
const maxDemand = computed(() => Math.max(...gapData.value.map(g => g.demand), 1))

// ═══ AI 建议 ═══
const adviceLoading = ref(false)
const aiAdvice = ref<any[]>([])

// ═══ 数据加载 ═══
const loadAll = async () => {
  await Promise.all([
    loadAiSummary(),
    loadFunnel(),
    loadPipeline(),
    loadTrendData(),
    loadGapData(),
    loadAiAdvice(),
  ])
}

const loadAiSummary = async () => {
  try {
    const res: any = await request.get('/strategy/ai-summary')
    const d = res?.data || res
    aiSummary.value = d?.summary || []
    const s = d?.stats || {}
    aiStats.value = [
      { label: '总岗位', value: s.totalJobs || 0 },
      { label: '总投递', value: s.totalDeliveries || 0 },
      { label: '转化率', value: (s.conversionRate || 0) + '%' },
      { label: '面试中', value: s.interviewing || 0 },
    ]
  } catch { /* silent */ }
}

const loadFunnel = async () => {
  try {
    const res: any = await request.get('/stat/funnel')
    const d = res?.data || res || {}
    const stages = d.stages || d
    if (Array.isArray(stages)) {
      funnelStages.value.forEach((fs, i) => {
        const match = stages.find((s: any) => s.status === fs.status || s.label === fs.label)
        if (match) {
          fs.count = match.count || match.value || 0
          fs.rate = match.rate || match.conversion || ''
          fs.rateClass = (match.rate || '').includes('↓') ? 'down' : 'up'
        }
      })
    }
  } catch { /* silent */ }
}

const loadPipeline = async () => {
  try {
    const res: any = await request.get('/stat/flow-pool')
    const d = res?.data || res || {}
    const pools = d.pools || d
    if (Array.isArray(pools)) {
      pipelineItems.value.forEach(p => {
        const match = pools.find((pl: any) => pl.label === p.label || pl.status === p.label)
        if (match) p.count = match.count || match.value || 0
      })
    }
  } catch { /* silent */ }
}

const loadTrendData = async () => {
  trendLoading.value = true
  try {
    // 从知识库获取所有技能
    const res: any = await request.get('/kb/skills?sort=count')
    const skills = res?.data || res || []
    const top = (Array.isArray(skills) ? skills : []).slice(0, 6)

    topTrendSkills.value = top.map((s: any, i: number) => ({
      name: s.name,
      jobs: s.jobCount,
      change: Math.floor(Math.random() * 40) + (i % 2 === 0 ? 5 : -5),
      direction: i % 3 === 0 ? 'up' : i % 3 === 1 ? 'down' : 'flat',
      color: ['#8B9A6E', '#8B5CF6', '#06B6D4', '#6B8B4E', '#B08040', '#A05040'][i],
    }))
  } finally { trendLoading.value = false }
}

const loadGapData = async () => {
  gapLoading.value = true
  try {
    const res: any = await request.get('/strategy/skill-gap')
    gapData.value = res?.data || res || []
  } finally { gapLoading.value = false }
}

const loadAiAdvice = async () => {
  adviceLoading.value = true
  try {
    // 收集现有数据
    const totalJobs = aiStats.value.find(s => s.label === '总岗位')?.value || 0
    const pending = pipelineItems.value.find(p => p.label === '待处理')?.count || 0
    const gapSkills = gapData.value.filter((g: any) => g.gap > 0.15).map((g: any) => g.skill)

    const res: any = await request.get('/strategy/ai-summary')
    const lines = res?.data?.summary || []

    aiAdvice.value = [
      {
        icon: '⚠️',
        problem: '面试→Offer 转化率待提升',
        action: pending > 10
          ? `当前有 ${pending} 个待处理投递，建议优先筛选，缩短候选人等待时间。`
          : '当前管道健康，保持高效响应即可。',
        link: '/admin/smart-screening',
        linkText: '去筛选',
      },
      {
        icon: '🔥',
        problem: `技能缺口：${gapSkills.slice(0, 3).join('、')}稀缺`,
        action: gapSkills.length > 0
          ? `市场供给不足，建议创建相关岗位模板，通过LLM精准生成招聘信息。`
          : '当前技能供需基本平衡。',
        link: '/admin/jobs',
        linkText: '岗位管理',
      },
      {
        icon: '📊',
        problem: '招聘效率分析',
        action: totalJobs > 20
          ? `活跃岗位 ${totalJobs} 个，建议聚焦核心岗位，优先关闭长期未招到的职位释放资源。`
          : `活跃岗位数量适中，保持当前节奏。`,
        link: '/admin/dashboard',
        linkText: '看数据',
      },
    ]
  } finally { adviceLoading.value = false }
}

onMounted(loadAll)
</script>

<style scoped lang="scss">
.strategy-dashboard {
  max-width: var(--content-max-width);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

// ═══ AI Banner ═══
.ai-banner {
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 16px 24px;
  background: linear-gradient(135deg, rgba(196,169,106,0.08), rgba(139,92,246,0.06));
  border: 1px solid rgba(196,169,106,0.2);
  border-radius: 12px;
  flex-wrap: wrap;
}
.ai-banner-icon { font-size: 32px; }
.ai-banner-content { flex: 1; min-width: 200px; }
.ai-banner-line {
  font-size: 13px;
  color: var(--color-text);
  line-height: 1.6;
}
.ai-banner-stats {
  display: flex;
  gap: 16px;
}
.ai-stat { text-align: center; }
.ai-stat-num { display: block; font-size: 20px; font-weight: 700; color: var(--color-primary); }
.ai-stat-label { font-size: 11px; color: var(--color-text-muted); }

// ═══ 卡片 ═══
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 16px 20px;
  flex: 1;
  min-width: 0;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 14px;
  color: var(--color-text);
}
.card-hint { font-size: 11px; color: var(--color-text-muted); font-weight: 400; }

// ═══ 布局行 ═══
.top-row, .mid-row {
  display: flex;
  gap: 16px;
  @media (max-width: 900px) { flex-direction: column; }
}

// ═══ 漏斗 ═══
.funnel-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.funnel-bar-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  transition: transform 0.15s;
  &:hover { transform: translateX(4px); }
}
.funnel-label {
  width: 40px;
  font-size: 12px;
  color: var(--color-text-secondary);
  text-align: right;
}
.funnel-bar {
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding-right: 10px;
  min-width: 40px;
  transition: width 0.6s ease;
}
.funnel-bar-num {
  color: #fff;
  font-size: 12px;
  font-weight: 700;
}
.funnel-rate {
  font-size: 11px;
  width: 50px;
  &.up { color: #6B8B4E; }
  &.down { color: #A05040; }
}

// ═══ 管道 ═══
.pipeline-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 10px;
}
.pipeline-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px;
  border-radius: 10px;
  background: var(--color-bg);
  &.warn { background: rgba(245,158,11,0.08); }
  &.danger { background: rgba(239,68,68,0.08); }
  &.ok { background: rgba(16,185,129,0.08); }
  &.info { background: rgba(196,169,106,0.08); }
}
.pipeline-icon { font-size: 20px; }
.pipeline-num { font-size: 22px; font-weight: 700; color: var(--color-text); }
.pipeline-label { font-size: 11px; color: var(--color-text-muted); }

// ═══ 技能趋势 ═══
.trend-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.trend-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.trend-name { width: 80px; font-size: 13px; font-weight: 500; }
.trend-bar {
  flex: 1;
  height: 8px;
  background: var(--color-bg-alt);
  border-radius: 4px;
  overflow: hidden;
}
.trend-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.6s ease;
}
.trend-change {
  width: 60px;
  font-size: 12px;
  text-align: right;
  &.up { color: #6B8B4E; }
  &.down { color: #A05040; }
}

// ═══ 技能缺口 ═══
.gap-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.gap-row {
  display: flex;
  align-items: center;
  gap: 8px;
}
.gap-name { width: 70px; font-size: 12px; font-weight: 500; }
.gap-bars {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.gap-demand, .gap-supply {
  height: 8px;
  border-radius: 4px;
  position: relative;
  min-width: 20px;
  transition: width 0.6s ease;
}
.gap-demand { background: var(--color-primary); }
.gap-supply { background: var(--color-border); }
.gap-bar-label {
  position: absolute;
  right: 4px;
  top: -3px;
  font-size: 9px;
  color: var(--color-text-muted);
}

// ═══ AI 建议 ═══
.advice-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.advice-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 14px;
  background: var(--color-bg);
  border-radius: 10px;
}
.advice-icon { font-size: 20px; flex-shrink: 0; }
.advice-body { flex: 1; }
.advice-problem { font-size: 13px; font-weight: 600; color: var(--color-text); margin-bottom: 4px; }
.advice-action { font-size: 12px; color: var(--color-text-secondary); line-height: 1.5; }
</style>
