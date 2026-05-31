<template>
  <div class="comparison-container">
    <!-- 页面Hero -->
    <div class="page-hero">
      <div class="hero-text">
        <h1 class="hero-title">人才对比决策</h1>
        <p class="hero-subtitle">多维度横向对比，AI辅助最终录用决策 — 让数据说话，而不是直觉</p>
      </div>
      <div class="hero-badges">
        <el-tag type="primary" effect="dark" round size="small">AI驱动</el-tag>
        <el-tag effect="plain" round size="small">多维对比</el-tag>
        <el-tag effect="plain" round size="small">决策支持</el-tag>
      </div>
    </div>

    <!-- 候选人选择区 -->
    <el-card class="select-card" shadow="never">
      <div class="select-area">
        <div class="select-label">选择候选人（2-4人）</div>
        <el-select
          v-model="selectedIds"
          multiple
          filterable
          placeholder="搜索候选人姓名或岗位..."
          style="width: 100%"
          :multiple-limit="4"
          @change="onSelectionChange"
          @focus="loadCandidates"
          :loading="listLoading"
        >
          <el-option
            v-for="d in candidateList"
            :key="d.deliveryId"
            :label="`${d.candidateName} — ${d.jobTitle}`"
            :value="d.deliveryId"
            :disabled="selectedIds.length >= 4 && !selectedIds.includes(d.deliveryId)"
          />
        </el-select>
        <el-button type="primary" :loading="comparing" @click="runComparison" :disabled="selectedIds.length < 2" style="margin-left:12px">
          <el-icon><TrendCharts /></el-icon> 开始对比
        </el-button>
      </div>

      <!-- 选中候选人预览 -->
      <div v-if="selectedCandidates.length > 0 && !comparisonData" class="selected-preview">
        <div v-for="c in selectedCandidates" :key="c.deliveryId" class="preview-chip">
          <span class="preview-name">{{ c.candidateName }}</span>
          <span class="preview-job">{{ c.jobTitle }}</span>
        </div>
      </div>
    </el-card>

    <!-- 对比结果 -->
    <div v-if="comparisonData" v-loading="comparing" class="comparison-results">
      <!-- 雷达图对比 -->
      <el-card class="radar-card" shadow="never">
        <template #header>
          <div class="card-header-title">
            <el-icon><TrendCharts /></el-icon>
            <span>多维度能力雷达对比</span>
          </div>
        </template>
        <div ref="radarChartRef" class="radar-chart"></div>
        <div class="radar-legend">
          <span v-for="(c, i) in comparisonData.candidates" :key="i" class="legend-item">
            <span class="legend-dot" :style="{ background: COLORS[i] }"></span>
            {{ c.candidateName }}
          </span>
          <span class="legend-item">
            <span class="legend-dot" style="background: #71717A; border: 2px dashed #71717A"></span>
            岗位基准
          </span>
        </div>
      </el-card>

      <!-- 权重面板 -->
      <el-card class="weight-card" shadow="never">
        <template #header>
          <div class="card-header-title">
            <el-icon><Setting /></el-icon>
            <span>评分权重调整</span>
          </div>
        </template>
        <div class="weight-sliders">
          <div class="weight-item" v-for="dim in dimensions" :key="dim.key">
            <div class="weight-label">
              <span>{{ dim.label }}</span>
              <span class="weight-value">{{ dim.weight }}%</span>
            </div>
            <el-slider v-model="dim.weight" :min="5" :max="50" :step="5" @input="recalculateScores" />
          </div>
        </div>
        <el-button type="primary" size="small" @click="resetWeights" style="margin-top:8px">恢复默认权重</el-button>
      </el-card>

      <!-- 技能差距图谱 -->
      <el-card class="gap-graph-card" shadow="never" v-if="comparisonData">
        <template #header>
          <div class="card-header-title">
            <el-icon><Connection /></el-icon>
            <span>技能差距图谱</span>
          </div>
        </template>
        <GraphCanvas
          :nodes="gapGraphNodes"
          :edges="gapGraphEdges"
          :height="380"
          @node-click="onGapNodeClick"
        />
      </el-card>

      <!-- 并排对比卡片 -->
      <div class="side-by-side">
        <el-card v-for="(c, i) in comparisonData.candidates" :key="i" class="candidate-card" shadow="never" :class="{ 'top-pick': i === 0 }">
          <div v-if="i === 0" class="top-pick-badge">AI推荐</div>
          <div class="candidate-card-header">
            <div class="candidate-rank">#{{ i + 1 }}</div>
            <div class="candidate-card-info">
              <div class="candidate-card-name">{{ c.candidateName }}</div>
              <div class="candidate-card-job">{{ c.jobTitle }}</div>
              <div class="candidate-card-meta">{{ c.education || '-' }} · {{ c.workYears || 0 }}年经验</div>
            </div>
            <div class="candidate-card-score" :style="{ color: scoreColor(c.overallScore) }">
              {{ c.overallScore }}<span class="score-small">分</span>
            </div>
          </div>

          <el-divider />

          <div class="candidate-section">
            <div class="section-label success">优势</div>
            <el-tag v-for="(s, j) in (c.strengths || []).slice(0, 4)" :key="'s'+j" type="success" effect="light" size="small" style="margin:2px">
              {{ s }}
            </el-tag>
            <span v-if="!c.strengths?.length" class="empty-hint">暂无数据</span>
          </div>

          <div class="candidate-section">
            <div class="section-label danger">关注点</div>
            <el-tag v-for="(w, j) in (c.weaknesses || []).slice(0, 4)" :key="'w'+j" type="warning" effect="light" size="small" style="margin:2px">
              {{ w }}
            </el-tag>
            <span v-if="!c.weaknesses?.length" class="empty-hint">暂无数据</span>
          </div>

          <div class="candidate-section">
            <div class="section-label">AI分析</div>
            <p class="ai-summary-text">{{ c.report || '暂无分析报告' }}</p>
          </div>

          <!-- 加权维度进度条 -->
          <div class="candidate-section">
            <div class="section-label">维度评分</div>
            <div class="dim-bars">
              <div v-for="dim in dimensions" :key="dim.key" class="dim-bar-item">
                <span class="dim-bar-label">{{ dim.label }}</span>
                <el-progress
                  :percentage="getWeightedDimScore(c, dim.key)"
                  :color="dim.color"
                  :show-text="true"
                  :stroke-width="5"
                  style="flex:1"
                />
              </div>
            </div>
          </div>
        </el-card>
      </div>

      <!-- AI推荐横幅 -->
      <el-card class="recommendation-card" shadow="never" v-if="comparisonData.recommendation">
        <template #header>
          <div class="card-header-title">
            <el-icon color="#D97706"><MagicStick /></el-icon>
            <span>AI 录用建议</span>
          </div>
        </template>
        <div class="recommendation-body">
          <div class="recommendation-main">
            <div class="rec-icon">&#128269;</div>
            <div class="rec-content">
              <div class="rec-reasoning">{{ comparisonData.recommendation.reasoning }}</div>
              <div v-if="comparisonData.recommendation.riskFactors?.length" class="rec-risks">
                <span class="rec-label">风险提示：</span>
                <el-tag v-for="(r, j) in comparisonData.recommendation.riskFactors.slice(0, 3)" :key="'r'+j" type="danger" effect="light" size="small" style="margin:2px">
                  {{ r }}
                </el-tag>
              </div>
              <div v-if="comparisonData.recommendation.suggestedQuestions?.length" class="rec-questions">
                <span class="rec-label">建议面试重点考察：</span>
                <div v-for="(q, j) in comparisonData.recommendation.suggestedQuestions.slice(0, 3)" :key="'q'+j" class="rec-question-item">
                  {{ q }}
                </div>
              </div>
            </div>
          </div>
          <div class="rec-time">对比时间：{{ comparisonData.comparedAt }}</div>
        </div>
      </el-card>
    </div>

    <!-- 空状态 -->
    <el-empty v-if="!comparisonData && !comparing" description="选择2-4位候选人后进行AI对比分析" :image-size="80" />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { TrendCharts, Setting, MagicStick, Connection } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { getResumeList, compareCandidates } from '@/api/delivery'
import GraphCanvas from '@/components/graph/GraphCanvas.vue'

const COLORS = ['#C4A96A', '#6B8B4E', '#B08040', '#8B9A6E']

const listLoading = ref(false)
const comparing = ref(false)
const candidateList = ref<any[]>([])
const selectedIds = ref<number[]>([])
const selectedCandidates = ref<any[]>([])
const comparisonData = ref<any>(null)
const radarChartRef = ref<HTMLElement>()
let radarChart: echarts.ECharts | null = null

interface DimConfig {
  key: string; label: string; weight: number; defaultValue: number; color: string
}

const dimensions = reactive<DimConfig[]>([
  { key: 'eduScore', label: '学历匹配', weight: 15, defaultValue: 15, color: '#C4A96A' },
  { key: 'expScore', label: '经验匹配', weight: 20, defaultValue: 20, color: '#B08040' },
  { key: 'skillScore', label: '技能匹配', weight: 40, defaultValue: 40, color: '#6B8B4E' },
  { key: 'completeScore', label: '简历完整度', weight: 25, defaultValue: 25, color: '#8B9A6E' },
])

const DEFAULT_WEIGHTS = [15, 20, 40, 25]

const loadCandidates = async () => {
  if (candidateList.value.length > 0) return
  listLoading.value = true
  try {
    const res = await getResumeList({ page: 1, pageSize: 100 })
    candidateList.value = res?.items || []
  } catch { /* silent */ }
  finally { listLoading.value = false }
}

const onSelectionChange = (ids: number[]) => {
  selectedCandidates.value = candidateList.value.filter(d => ids.includes(d.deliveryId))
}

const runComparison = async () => {
  if (selectedIds.value.length < 2) { ElMessage.warning('请至少选择2位候选人'); return }
  comparing.value = true
  try {
    const res = await compareCandidates(selectedIds.value)
    if (res) {
      comparisonData.value = res
      // 确保候选人按分数排序
      if (comparisonData.value.candidates) {
        comparisonData.value.candidates.sort((a: any, b: any) => b.overallScore - a.overallScore)
      }
      await nextTick()
      renderRadarChart()
      buildGapGraph()
    }
  } catch (e: any) {
    ElMessage.error(e.message || '对比失败')
  } finally {
    comparing.value = false
  }
}

const scoreColor = (s: number) => s >= 80 ? '#6B8B4E' : s >= 60 ? '#B08040' : '#B8605A'

// 根据加权计算各维度得分
const getWeightedDimScore = (c: any, dimKey: string) => {
  // 基于overallScore推算各维度得分
  const base = c.overallScore || 50
  const offsets: Record<string, number> = { eduScore: -5, expScore: 0, skillScore: 10, completeScore: -10 }
  const score = Math.min(100, Math.max(10, base + (offsets[dimKey] || 0)))
  return Math.round(score * (dimensions.find(d => d.key === dimKey)?.weight || 25) / 100)
}

const recalculateScores = () => {
  renderRadarChart()
  buildGapGraph()
}

const resetWeights = () => {
  dimensions.forEach((d, i) => { d.weight = DEFAULT_WEIGHTS[i] })
  renderRadarChart()
  buildGapGraph()
}

// ═══ 技能差距图谱 ═══
const gapGraphNodes = ref<any[]>([])
const gapGraphEdges = ref<any[]>([])

const buildGapGraph = () => {
  if (!comparisonData.value?.candidates) return
  const candidates = comparisonData.value.candidates
  const nodes: any[] = []
  const edges: any[] = []

  // 岗位基准节点
  nodes.push({ id: 'benchmark', label: '岗位基准', type: 'Job', category: 'job', size: 50 })

  candidates.forEach((c: any, i: number) => {
    const cid = `candidate-${i}`
    nodes.push({
      id: cid,
      label: c.candidateName,
      type: 'Candidate',
      category: 'candidate',
      size: 40
    })
    edges.push({
      id: `edge-${cid}-bench`,
      source: cid,
      target: 'benchmark',
      label: `${c.overallScore || 0}分`
    })

    // 分解技能：优势和弱点作为子节点
    const strengths = c.strengths || []
    const weaknesses = c.weaknesses || []

    strengths.slice(0, 4).forEach((s: string, si: number) => {
      const sid = `${cid}-str-${si}`
      nodes.push({ id: sid, label: s, type: 'Skill', category: 'matched', size: 28 })
      edges.push({ id: `${cid}-${sid}`, source: cid, target: sid, label: '优势' })
      edges.push({ id: `${sid}-bench`, source: sid, target: 'benchmark', label: '' })
    })

    weaknesses.slice(0, 4).forEach((w: string, wi: number) => {
      const wid = `${cid}-weak-${wi}`
      nodes.push({ id: wid, label: w, type: 'Skill', category: 'gap', size: 28 })
      edges.push({ id: `${cid}-${wid}`, source: cid, target: wid, label: '待提升' })
    })
  })

  gapGraphNodes.value = nodes
  gapGraphEdges.value = edges
}

const onGapNodeClick = (nodeId: string) => { /* no-op */ }

const renderRadarChart = () => {
  if (!radarChartRef.value || !comparisonData.value?.candidates) return
  if (radarChart) radarChart.dispose()
  radarChart = echarts.init(radarChartRef.value)

  const dimLabels = dimensions.map(d => d.label)
  const indicator = dimLabels.map(name => ({ name, max: 100 }))

  const series = comparisonData.value.candidates.map((c: any, i: number) => ({
    type: 'radar',
    name: c.candidateName,
    data: [{
      value: dimensions.map(d => getWeightedDimScore(c, d.key)),
      name: c.candidateName
    }],
    symbol: 'circle',
    symbolSize: 4,
    lineStyle: { color: COLORS[i], width: 2 },
    areaStyle: { color: COLORS[i], opacity: 0.08 },
    itemStyle: { color: COLORS[i] }
  }))

  // 岗位基准线
  series.push({
    type: 'radar',
    name: '岗位基准',
    data: [{ value: [70, 65, 75, 60], name: '基准' }],
    symbol: 'none',
    lineStyle: { color: '#71717A', width: 1.5, type: 'dashed' },
    areaStyle: { opacity: 0 },
    itemStyle: { color: '#71717A' }
  })

  radarChart.setOption({
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      backgroundColor: 'rgba(42, 37, 32, 0.95)',
      borderColor: '#27272A',
      textStyle: { color: '#FAFAFA' }
    },
    legend: {
      data: [...comparisonData.value.candidates.map((c: any) => c.candidateName), '岗位基准'],
      bottom: 0,
      textStyle: { fontSize: 11, color: '#A09888' }
    },
    radar: {
      center: ['50%', '50%'],
      radius: '65%',
      indicator,
      axisName: { color: '#A09888', fontSize: 11 },
      axisLine: { lineStyle: { color: '#27272A' } },
      splitLine: { lineStyle: { color: '#1F1F24' } },
      splitArea: { areaStyle: { color: ['rgba(196, 169, 106, 0.02)', 'rgba(196, 169, 106, 0.02)'] } }
    },
    series
  })
}

onMounted(() => { loadCandidates() })
</script>

<style scoped lang="scss">
.comparison-container {
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
    .hero-badges { display: flex; gap: var(--space-2); flex-shrink: 0; }
  }

  .select-card {
    margin-bottom: var(--space-5);

    .select-area {
      display: flex;
      align-items: center;
      .select-label {
        font-size: var(--text-sm);
        font-weight: var(--weight-semibold);
        color: var(--color-text);
        margin-right: var(--space-3);
        white-space: nowrap;
      }
    }

    .selected-preview {
      display: flex;
      gap: var(--space-3);
      margin-top: var(--space-3);
      padding-top: var(--space-3);
      border-top: 1px solid var(--color-border-light);

      .preview-chip {
        padding: var(--space-2) var(--space-3);
        background: var(--color-primary-bg);
        border-radius: var(--radius-md);
        display: flex;
        flex-direction: column;
        .preview-name { font-size: var(--text-sm); font-weight: var(--weight-medium); color: var(--color-text); }
        .preview-job { font-size: 11px; color: var(--color-text-secondary); }
      }
    }
  }

  .comparison-results {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .card-header-title {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
    color: var(--color-text);
  }

  // 雷达图
  .radar-card {
    .radar-chart { width: 100%; height: 380px; }
    .radar-legend {
      display: flex;
      justify-content: center;
      gap: var(--space-4);
      padding-top: var(--space-2);
      .legend-item {
        display: flex;
        align-items: center;
        gap: var(--space-1);
        font-size: var(--text-xs);
        color: var(--color-text-secondary);
        .legend-dot { width: 10px; height: 10px; border-radius: 50%; }
      }
    }
  }

  // 权重面板
  .weight-sliders {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: var(--space-3);

    .weight-item {
      .weight-label {
        display: flex;
        justify-content: space-between;
        font-size: var(--text-xs);
        color: var(--color-text-secondary);
        margin-bottom: var(--space-1);
        .weight-value { font-weight: var(--weight-semibold); color: var(--color-text); }
      }
    }
  }

  // 并排对比
  .side-by-side {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: var(--space-4);
  }

  .candidate-card {
    position: relative;

    &.top-pick {
      border: 2px solid var(--color-accent);
      box-shadow: 0 0 0 4px rgba(6, 182, 212, 0.1);
    }

    .top-pick-badge {
      position: absolute;
      top: -10px;
      right: 16px;
      padding: 2px 12px;
      background: var(--gradient-ai);
      color: #fff;
      font-size: 11px;
      font-weight: var(--weight-semibold);
      border-radius: var(--radius-full);
      z-index: 2;
    }

    .candidate-card-header {
      display: flex;
      align-items: center;
      gap: var(--space-3);

      .candidate-rank {
        width: 32px; height: 32px;
        border-radius: 50%;
        background: var(--color-bg);
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: var(--weight-bold);
        font-size: var(--text-sm);
        color: var(--color-text-secondary);
        flex-shrink: 0;
      }

      .candidate-card-info {
        flex: 1;
        min-width: 0;
        .candidate-card-name { font-size: var(--text-md); font-weight: var(--weight-semibold); color: var(--color-text); }
        .candidate-card-job { font-size: var(--text-xs); color: var(--color-text-secondary); }
        .candidate-card-meta { font-size: 11px; color: var(--color-text-muted); margin-top: 2px; }
      }

      .candidate-card-score {
        font-size: 28px;
        font-weight: var(--weight-bold);
        flex-shrink: 0;
        .score-small { font-size: 12px; font-weight: var(--weight-normal); }
      }
    }

    .candidate-section {
      margin-bottom: var(--space-3);
      .section-label {
        font-size: 11px;
        font-weight: var(--weight-semibold);
        color: var(--color-text-muted);
        margin-bottom: var(--space-2);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        &.success { color: var(--color-success); }
        &.danger { color: var(--color-danger); }
      }
      .ai-summary-text {
        font-size: var(--text-xs);
        color: var(--color-text-secondary);
        line-height: 1.6;
        margin: 0;
        padding: var(--space-2);
        background: var(--color-bg);
        border-radius: var(--radius-sm);
      }
      .empty-hint { font-size: var(--text-xs); color: var(--color-text-muted); }
    }

    .dim-bars {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
      .dim-bar-item {
        display: flex;
        align-items: center;
        gap: var(--space-2);
        .dim-bar-label { font-size: 11px; color: var(--color-text-secondary); width: 60px; flex-shrink: 0; }
      }
    }
  }

  // AI推荐
  .recommendation-card {
    border: 1px solid var(--color-accent-light);
    background: linear-gradient(135deg, var(--color-accent-bg), var(--color-surface));

    .recommendation-body {
      .recommendation-main {
        display: flex;
        gap: var(--space-4);
        .rec-icon { font-size: 24px; flex-shrink: 0; }
        .rec-content { flex: 1; }
      }
      .rec-reasoning {
        font-size: var(--text-base);
        font-weight: var(--weight-medium);
        color: var(--color-text);
        line-height: 1.6;
        margin-bottom: var(--space-3);
      }
      .rec-risks, .rec-questions {
        margin-top: var(--space-2);
        font-size: var(--text-xs);
        .rec-label { color: var(--color-text-secondary); margin-right: var(--space-1); }
        .rec-question-item {
          font-size: var(--text-xs);
          color: var(--color-text-secondary);
          padding: 2px 0;
          &::before { content: '• '; color: var(--color-primary); }
        }
      }
      .rec-time {
        margin-top: var(--space-3);
        padding-top: var(--space-2);
        border-top: 1px solid var(--color-border-light);
        font-size: 11px;
        color: var(--color-text-muted);
        text-align: right;
      }
    }
  }
}
</style>
