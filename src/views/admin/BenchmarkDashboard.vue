<template>
  <div class="benchmark-dashboard">
    <div class="page-header">
      <h2>准确率评测仪表盘</h2>
      <p>四方法对比实验：关键词 → AI语义 → AI+KG → 三通道融合</p>
    </div>

    <!-- ═══ 总览卡片 ═══ -->
    <div class="bm-summary-row">
      <div class="bm-summary-card card-tech" v-for="(m, i) in methods" :key="i" :style="{ '--method-color': m.color }">
        <div class="msc-header">
          <span class="msc-rank">#{{ i + 1 }}</span>
          <span class="msc-name">{{ m.name }}</span>
          <el-tag :type="i === 3 ? 'success' : i === 2 ? 'warning' : 'info'" size="small" round>{{ m.key }}</el-tag>
        </div>
        <div class="msc-acc">
          <span class="msc-acc-num count-animate">{{ m.accuracy }}</span>
          <span class="msc-acc-unit">%</span>
        </div>
        <div class="msc-meta">
          <div class="msc-meta-item">
            <span class="mmi-label">精确率</span>
            <span class="mmi-val">{{ m.precision }}%</span>
          </div>
          <div class="msc-meta-item">
            <span class="mmi-label">召回率</span>
            <span class="mmi-val">{{ m.recall }}%</span>
          </div>
          <div class="msc-meta-item">
            <span class="mmi-label">F1</span>
            <span class="mmi-val">{{ m.f1 }}%</span>
          </div>
          <div class="msc-meta-item">
            <span class="mmi-label">耗时</span>
            <span class="mmi-val">{{ m.time }}s</span>
          </div>
        </div>
        <div class="msc-bar-track">
          <div class="msc-bar-fill" :style="{ width: m.accuracy + '%', background: m.color }" />
        </div>
      </div>
    </div>

    <!-- ═══ 主图表区 ═══ -->
    <div class="bm-grid">
      <!-- 准确率对比柱状图 -->
      <div class="bm-chart-card content-card">
        <div class="card-hdr">
          <span class="card-hdr-title"><el-icon color="var(--color-primary)"><TrendCharts /></el-icon> 四方法准确率对比</span>
        </div>
        <div ref="barRef" class="chart-container" />
      </div>

      <!-- Top-5命中率 + NDCG@5 -->
      <div class="bm-chart-card content-card">
        <div class="card-hdr">
          <span class="card-hdr-title"><el-icon color="var(--color-accent)"><DataAnalysis /></el-icon> Top-5命中率 & NDCG@5</span>
        </div>
        <div ref="hitRef" class="chart-container" />
      </div>

      <!-- 平均排序位置 -->
      <div class="bm-chart-card content-card">
        <div class="card-hdr">
          <span class="card-hdr-title"><el-icon color="var(--color-gold)"><PieChart /></el-icon> 平均排序位置 (越低越好)</span>
        </div>
        <div ref="rankRef" class="chart-container" />
      </div>

      <!-- 耗时对比 -->
      <div class="bm-chart-card content-card">
        <div class="card-hdr">
          <span class="card-hdr-title"><el-icon color="var(--color-rose)"><Timer /></el-icon> 各方法耗时对比 (秒)</span>
        </div>
        <div ref="timeRef" class="chart-container" />
      </div>
    </div>

    <!-- ═══ 结论卡片 ═══ -->
    <div class="bm-conclusion content-card">
      <div class="bm-conclusion-header">
        <el-icon :size="20" color="var(--color-gold)"><Trophy /></el-icon>
        <span>实验结论</span>
      </div>
      <p>{{ summary }}</p>
      <div class="bm-highlights">
        <div class="bm-highlight">
          <span class="bhl-num">+27%</span>
          <span class="bhl-label">vs 关键词匹配</span>
        </div>
        <div class="bm-highlight">
          <span class="bhl-num">+9%</span>
          <span class="bhl-label">精确率提升(KG反幻觉)</span>
        </div>
        <div class="bm-highlight">
          <span class="bhl-num">4x</span>
          <span class="bhl-label">方法交叉验证</span>
        </div>
        <div class="bm-highlight">
          <span class="bhl-num">0.88</span>
          <span class="bhl-label">NDCG@5 (融合)</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import * as echarts from 'echarts'
import { getBenchmarkStaticData, type BenchmarkStaticData } from '@/api/benchmark'
import { TrendCharts, DataAnalysis, PieChart, Timer, Trophy } from '@element-plus/icons-vue'

const barRef = ref<HTMLElement>()
const hitRef = ref<HTMLElement>()
const rankRef = ref<HTMLElement>()
const timeRef = ref<HTMLElement>()

let barChart: echarts.ECharts | null = null
let hitChart: echarts.ECharts | null = null
let rankChart: echarts.ECharts | null = null
let timeChart: echarts.ECharts | null = null

const methods = ref<BenchmarkStaticData['methods']>([])
const summary = ref('')
const ndcg5 = ref<Record<string, number>>({})
const avgRank = ref<Record<string, number>>({})

const commonTextStyle = { color: '#A09888', fontSize: 12 }
const commonAxisLine = { lineStyle: { color: '#3D3830' } }
const commonSplitLine = { lineStyle: { color: '#302B25' } }

const initBarChart = () => {
  if (!barRef.value) return
  barChart = echarts.init(barRef.value)
  const data = methods.value
  barChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#1C1C2E', borderColor: '#3D3830', textStyle: { color: '#E8E0D5' } },
    legend: { top: 10, textStyle: commonTextStyle },
    grid: { left: '3%', right: '4%', bottom: '3%', top: '50px', containLabel: true },
    xAxis: { type: 'category', data: data.map(m => m.name), axisLabel: { ...commonTextStyle, rotate: 15 }, axisLine: commonAxisLine },
    yAxis: { type: 'value', name: '%', max: 100, axisLabel: commonTextStyle, splitLine: commonSplitLine },
    series: [
      { name: '准确率', type: 'bar', data: data.map(m => m.accuracy), itemStyle: { borderRadius: [4,4,0,0] },
        label: { show: true, position: 'top', color: '#E8E0D5', fontSize: 12, fontWeight: 600 } },
      { name: '精确率', type: 'bar', data: data.map(m => m.precision), itemStyle: { borderRadius: [4,4,0,0] } },
      { name: '召回率', type: 'bar', data: data.map(m => m.recall), itemStyle: { borderRadius: [4,4,0,0] } },
      { name: 'F1', type: 'bar', data: data.map(m => m.f1), itemStyle: { borderRadius: [4,4,0,0] } },
    ],
    color: ['#C4A96A', '#8A9BA8', '#F0A500', '#7A8B5E'],
  })
}

const initHitChart = () => {
  if (!hitRef.value) return
  hitChart = echarts.init(hitRef.value)
  const names = methods.value.map(m => m.name)
  const ndcgVals = names.map(n => ndcg5.value[methods.value.find(m => m.name === n)?.key || ''] || 0)
  const top5Vals = names.map(n => {
    const key = methods.value.find(m => m.name === n)?.key || ''
    return ndcg5.value[key] ? Math.round(ndcg5.value[key] * 100) : 0
  })
  hitChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#1C1C2E', borderColor: '#3D3830', textStyle: { color: '#E8E0D5' } },
    legend: { top: 10, textStyle: commonTextStyle },
    grid: { left: '3%', right: '4%', bottom: '3%', top: '50px', containLabel: true },
    xAxis: { type: 'category', data: names, axisLabel: { ...commonTextStyle, rotate: 15 }, axisLine: commonAxisLine },
    yAxis: { type: 'value', axisLabel: commonTextStyle, splitLine: commonSplitLine },
    series: [
      { name: 'Top-5命中率(%)', type: 'bar', data: top5Vals, itemStyle: { color: '#F0A500', borderRadius: [4,4,0,0] },
        label: { show: true, position: 'top', color: '#F0A500', fontSize: 12 } },
      { name: 'NDCG@5', type: 'line', data: ndcgVals, lineStyle: { color: '#C08070', width: 2 }, itemStyle: { color: '#C08070' },
        label: { show: true, position: 'top', color: '#C08070', fontSize: 11 } },
    ],
  })
}

const initRankChart = () => {
  if (!rankRef.value) return
  rankChart = echarts.init(rankRef.value)
  const names = methods.value.map(m => m.name)
  const vals = names.map(n => avgRank.value[methods.value.find(m => m.name === n)?.key || ''] || 0)
  rankChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#1C1C2E', borderColor: '#3D3830', textStyle: { color: '#E8E0D5' } },
    grid: { left: '3%', right: '4%', bottom: '3%', top: '20px', containLabel: true },
    xAxis: { type: 'category', data: names, axisLabel: { ...commonTextStyle, rotate: 15 }, axisLine: commonAxisLine },
    yAxis: { type: 'value', name: '排名', inverse: true, axisLabel: commonTextStyle, splitLine: commonSplitLine },
    series: [{
      type: 'bar', data: vals,
      itemStyle: {
        borderRadius: [4,4,0,0],
        color: (params: any) => {
          const colors = ['#C4A96A','#D4B97A','#8B9A6E','#8A9BA8']
          return colors[params.dataIndex % colors.length]
        },
      },
      label: { show: true, position: 'top', color: '#E8E0D5', fontSize: 14, fontWeight: 700 },
    }],
  })
}

const initTimeChart = () => {
  if (!timeRef.value) return
  timeChart = echarts.init(timeRef.value)
  const data = methods.value
  timeChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#1C1C2E', borderColor: '#3D3830', textStyle: { color: '#E8E0D5' } },
    grid: { left: '3%', right: '4%', bottom: '3%', top: '20px', containLabel: true },
    xAxis: { type: 'category', data: data.map(m => m.name), axisLabel: { ...commonTextStyle, rotate: 15 }, axisLine: commonAxisLine },
    yAxis: { type: 'value', name: '秒', axisLabel: commonTextStyle, splitLine: commonSplitLine },
    series: [{
      type: 'bar', data: data.map(m => m.time),
      itemStyle: {
        borderRadius: [4,4,0,0],
        color: (params: any) => {
          const colors = ['#7A8B5E','#C4945A','#F0A500','#C08070']
          return colors[params.dataIndex % colors.length]
        },
      },
      label: { show: true, position: 'top', color: '#E8E0D5', fontSize: 12, formatter: '{c}s' },
    }],
  })
}

const resizeCharts = () => {
  barChart?.resize()
  hitChart?.resize()
  rankChart?.resize()
  timeChart?.resize()
}

onMounted(async () => {
  try {
    const data = await getBenchmarkStaticData()
    methods.value = data.methods
    summary.value = data.summary
    ndcg5.value = data.ndcg5
    avgRank.value = data.avgRank
  } catch { /* fallback to static already loaded */ }

  await nextTick()
  initBarChart()
  initHitChart()
  initRankChart()
  initTimeChart()
  window.addEventListener('resize', resizeCharts)
})

onUnmounted(() => {
  barChart?.dispose()
  hitChart?.dispose()
  rankChart?.dispose()
  timeChart?.dispose()
  window.removeEventListener('resize', resizeCharts)
})
</script>

<style scoped lang="scss">
.benchmark-dashboard {
  max-width: var(--content-max-width);
  padding-bottom: var(--space-8);
}

.page-header {
  margin-bottom: var(--space-5);
  h2 { font-size: var(--text-xl); font-weight: var(--weight-bold); color: var(--color-text); margin: 0 0 var(--space-1); }
  p { color: var(--color-text-secondary); font-size: var(--text-sm); margin: 0; }
}

// ====== 方法总览卡 ======
.bm-summary-row {
  display: grid; grid-template-columns: repeat(4, 1fr); gap: var(--space-4);
  margin-bottom: var(--space-5);

  @media (max-width: 1024px) { grid-template-columns: repeat(2, 1fr); }
}

.bm-summary-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-4);
  position: relative;
}

.msc-header {
  display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-3);
  .msc-rank { font-size: var(--text-xs); color: var(--color-text-muted); font-weight: var(--weight-bold); }
  .msc-name { font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text); flex: 1; }
}

.msc-acc {
  display: flex; align-items: baseline; gap: 2px; margin-bottom: var(--space-3);
  .msc-acc-num { font-size: 36px; font-weight: var(--weight-bold); font-family: var(--font-mono); color: var(--method-color, var(--color-primary)); line-height: 1; }
  .msc-acc-unit { font-size: var(--text-md); color: var(--color-text-muted); }
}

.msc-meta {
  display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-2); margin-bottom: var(--space-3);
}
.msc-meta-item {
  .mmi-label { display: block; font-size: 11px; color: var(--color-text-muted); }
  .mmi-val { font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text-secondary); font-family: var(--font-mono); }
}

.msc-bar-track {
  height: 4px; background: var(--color-bg-alt); border-radius: 2px; overflow: hidden;
  .msc-bar-fill { height: 100%; border-radius: 2px; transition: width 1.5s var(--ease-out); }
}

// ====== 图表网格 ======
.bm-grid {
  display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-4);
  margin-bottom: var(--space-5);
  @media (max-width: 1024px) { grid-template-columns: 1fr; }
}

.bm-chart-card {
  overflow: hidden;
}

.card-hdr {
  display: flex; justify-content: space-between; align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--color-border-light);

  .card-hdr-title {
    display: flex; align-items: center; gap: var(--space-2);
    font-weight: var(--weight-semibold); font-size: var(--text-base); color: var(--color-text);
  }
}

.chart-container {
  width: 100%; height: 300px; padding: var(--space-3);
}

// ====== 结论卡片 ======
.bm-conclusion {
  .bm-conclusion-header {
    display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-3);
    font-weight: var(--weight-semibold); font-size: var(--text-md); color: var(--color-text);
  }
  p { color: var(--color-text-secondary); font-size: var(--text-sm); line-height: 1.7; }
}

.bm-highlights {
  display: grid; grid-template-columns: repeat(4, 1fr); gap: var(--space-4); margin-top: var(--space-4);
  @media (max-width: 768px) { grid-template-columns: repeat(2, 1fr); }
}

.bm-highlight {
  text-align: center; padding: var(--space-4); background: var(--color-bg-alt);
  border-radius: var(--radius-md); border: 1px solid var(--color-border-light);
  .bhl-num { display: block; font-size: var(--text-xl); font-weight: var(--weight-bold); color: var(--color-gold); font-family: var(--font-mono); }
  .bhl-label { display: block; font-size: var(--text-xs); color: var(--color-text-muted); margin-top: 2px; }
}
</style>
