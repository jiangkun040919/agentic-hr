<template>
  <div class="ai-enhance-section">
    <!-- 1. 技能雷达图 -->
    <div class="enhance-card" v-if="skillRadarData.length">
      <h4>📊 技能雷达图</h4>
      <div ref="radarRef" class="radar-chart" style="height:280px"></div>
    </div>

    <!-- 2. 经历时间线 -->
    <div class="enhance-card" v-if="timeline.length">
      <h4>🕐 经历时间线</h4>
      <el-timeline>
        <el-timeline-item
          v-for="(item, i) in timeline"
          :key="i"
          :timestamp="item.period"
          :type="item.type || 'primary'"
          :hollow="item.type === 'gap'"
          placement="top"
        >
          <b>{{ item.title }}</b>
          <div class="timeline-detail">{{ item.detail }}</div>
        </el-timeline-item>
      </el-timeline>
    </div>

    <!-- 3. 风险标签 -->
    <div class="enhance-card" v-if="riskTags.length">
      <h4>⚠️ 风险标签</h4>
      <div class="risk-tags">
        <el-tag
          v-for="tag in riskTags"
          :key="tag.label"
          :type="tag.severity === 'high' ? 'danger' : 'warning'"
          effect="dark"
          size="default"
          style="margin: 4px"
        >
          {{ tag.label }}
        </el-tag>
      </div>
      <div v-for="tag in riskTags" :key="'r-'+tag.label" class="risk-detail">
        <span class="risk-label">{{ tag.label }}：</span>
        <span>{{ tag.reason }}</span>
      </div>
    </div>

    <!-- 4. 逐句对照 -->
    <div class="enhance-card" v-if="sentenceMatches.length">
      <h4>📝 逐句对照</h4>
      <div class="sentence-match-list">
        <div
          v-for="(sm, i) in sentenceMatches"
          :key="i"
          class="sentence-match-item"
          :class="{ matched: sm.matched }"
        >
          <div class="sm-extract">
            <el-tag :type="sm.matched ? 'success' : 'danger'" size="small">
              {{ sm.matched ? '✅' : '❌' }}
            </el-tag>
            <span>{{ sm.extracted }}</span>
          </div>
          <div class="sm-source" v-if="sm.source">
            <el-icon><ArrowRight /></el-icon>
            <span class="sm-source-text">"{{ sm.source }}"</span>
          </div>
        </div>
      </div>
    </div>

    <!-- 5. 竞争力排名 -->
    <div class="enhance-card" v-if="ranking">
      <h4>🏆 竞争力排名</h4>
      <div class="ranking-display">
        <div class="rank-badge">
          <span class="rank-number">#{{ ranking.rank }}</span>
          <span class="rank-total">/ {{ ranking.total }}</span>
        </div>
        <div class="rank-bar-wrapper">
          <el-progress
            :percentage="Math.round((1 - ranking.rank / ranking.total) * 100)"
            :color="ranking.rank <= ranking.total * 0.3 ? '#67C23A' : ranking.rank <= ranking.total * 0.6 ? '#E6A23C' : '#F56C6C'"
          >
            <span>前 {{ Math.round((1 - ranking.rank / ranking.total) * 100) }}%</span>
          </el-progress>
        </div>
      </div>
      <div class="rank-note">
        匹配度 {{ ranking.matchScore }}分，在同岗位 {{ ranking.total }} 名候选人中排名第 {{ ranking.rank }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { ArrowRight } from '@element-plus/icons-vue'
import * as echarts from 'echarts'

const props = defineProps<{
  parseResult: any
  matchResult: any
  delivery: any
}>()

// 1. 技能雷达图数据
const skillRadarData = computed(() => {
  const skills = props.parseResult?.skills || []
  if (!skills.length) return []
  return skills.map((s: any) => ({
    name: s.name,
    value: s.level === '精通' ? 90 : s.level === '熟练' ? 70 : 50
  }))
})

const radarRef = ref<HTMLElement>()
let radarChart: echarts.ECharts | null = null

const renderRadar = () => {
  if (!radarRef.value || !skillRadarData.value.length) return
  if (radarChart) radarChart.dispose()
  radarChart = echarts.init(radarRef.value)
  radarChart.setOption({
    radar: {
      indicator: skillRadarData.value.map((s: any) => ({ name: s.name, max: 100 })),
      center: ['50%', '55%'],
      radius: '65%'
    },
    series: [{
      type: 'radar',
      data: [{ value: skillRadarData.value.map((s: any) => s.value), name: '技能评分' }],
      areaStyle: { opacity: 0.3 },
      itemStyle: { color: '#409EFF' }
    }]
  })
}

watch(skillRadarData, () => nextTick(renderRadar))
onMounted(() => nextTick(renderRadar))

// 2. 经历时间线
const timeline = computed(() => {
  const items: any[] = []
  const work = props.parseResult?.workExperience || []
  const edu = props.parseResult?.educationHistory || []

  // Education
  edu.forEach((e: any) => {
    items.push({
      period: `${e.startYear}-${e.endYear}`,
      title: `${e.school} · ${e.degree}`,
      detail: e.major || '',
      type: 'info'
    })
  })

  // Work experience sorted by date
  const sorted = [...work].sort((a: any, b: any) => {
    return (a.startDate || '').localeCompare(b.startDate || '')
  })

  sorted.forEach((w: any, i: number) => {
    items.push({
      period: `${w.startDate || ''} ~ ${w.endDate || '至今'}`,
      title: `${w.company} — ${w.title}`,
      detail: w.description || '',
      type: 'primary'
    })
    // Check for gaps
    if (i < sorted.length - 1) {
      const currentEnd = w.endDate
      const nextStart = sorted[i + 1]?.startDate
      if (currentEnd && nextStart) {
        const gap = calculateGap(currentEnd, nextStart)
        if (gap > 6) {
          items.push({
            period: `${gap}个月空窗期`,
            title: '⚠️ 经历空窗',
            detail: `从 ${currentEnd} 到 ${nextStart}，间隔约 ${gap} 个月`,
            type: 'gap'
          })
        }
      }
    }
  })

  return items
})

// 3. 风险标签
const riskTags = computed(() => {
  const tags: any[] = []
  const work = props.parseResult?.workExperience || []
  const skills = props.parseResult?.skills || []
  const education = props.parseResult?.education

  // 频繁跳槽
  if (work.length >= 3) {
    const durations = work
      .filter((w: any) => w.startDate && w.endDate)
      .map((w: any) => {
        const s = new Date(w.startDate)
        const e = new Date(w.endDate)
        return (e.getTime() - s.getTime()) / (365 * 24 * 60 * 60 * 1000)
      })
    const avgDuration = durations.length ? durations.reduce((a: number, b: number) => a + b, 0) / durations.length : 999
    if (avgDuration < 1.5) {
      tags.push({ label: '频繁跳槽', reason: `${work.length}段工作经历，平均每段${avgDuration.toFixed(1)}年`, severity: 'high' })
    }
  }

  // 技能断层 - compare with a standard set
  const coreSkills = ['Java', 'Python', 'JavaScript', 'SQL', 'MySQL', 'Git', 'Docker']
  const foundCore = coreSkills.filter(cs => skills.some((s: any) => s.name?.toLowerCase().includes(cs.toLowerCase())))
  if (skills.length > 0 && foundCore.length < coreSkills.length * 0.3) {
    tags.push({ label: '技能断层', reason: `核心技能覆盖不足 (${foundCore.length}/${coreSkills.length})`, severity: 'high' })
  }

  // 学历不符
  if (education?.level && ['高中', '中专', '大专'].includes(education.level)) {
    tags.push({ label: '学历偏低', reason: `当前学历: ${education.level}`, severity: 'high' })
  }

  // 空窗期
  if (timeline.value.some((t: any) => t.type === 'gap')) {
    tags.push({ label: '经历空窗', reason: '工作经历中存在较长空窗期', severity: 'medium' })
  }

  return tags
})

// 4. 逐句对照
const sentenceMatches = computed(() => {
  const match = props.matchResult
  if (!match) return []
  const items: any[] = []

  // From match details
  const matchDetails = match.matchDetails || match.details || []
  matchDetails.forEach((d: any) => {
    items.push({
      extracted: d.skill || d.key || d.name || '',
      source: d.evidence || d.source || '',
      matched: d.matched !== false
    })
  })

  // Add unmatched skills from JD
  const gaps = match.gaps || match.missingSkills || []
  gaps.forEach((g: any) => {
    items.push({
      extracted: typeof g === 'string' ? g : (g.skill || g.name || ''),
      source: '',
      matched: false
    })
  })

  return items
})

// 5. 竞争力排名 (mock data - would come from API)
const ranking = computed(() => {
  const score = props.matchResult?.score || props.matchResult?.matchScore || 0
  if (!score) return null
  // Simulate: rank based on score out of a pool
  const total = 8
  const rank = Math.max(1, Math.round(total * (1 - score / 100)) + 1)
  return { rank: Math.min(rank, total), total, matchScore: score }
})

function calculateGap(endDate: string, startDate: string): number {
  try {
    const e = new Date(endDate)
    const s = new Date(startDate)
    return Math.round((s.getTime() - e.getTime()) / (30 * 24 * 60 * 60 * 1000))
  } catch { return 0 }
}
</script>

<style scoped lang="scss">
.ai-enhance-section {
  margin-top: 16px;
}
.enhance-card {
  margin-bottom: 16px;
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  h4 { margin: 0 0 12px 0; font-size: 15px; }
}
.risk-tags { margin-bottom: 10px; }
.risk-detail { font-size: 13px; margin: 4px 0; color: var(--el-text-color-secondary); }
.risk-label { font-weight: 600; color: var(--el-text-color-primary); }
.sentence-match-item {
  padding: 8px 0;
  border-bottom: 1px dashed var(--el-border-color-extra-light);
  .sm-extract { display: flex; align-items: center; gap: 8px; font-weight: 500; }
  .sm-source { display: flex; align-items: center; gap: 6px; margin-top: 4px; padding-left: 28px; }
  .sm-source-text { color: var(--el-color-primary); font-style: italic; font-size: 13px; }
}
.ranking-display {
  display: flex; align-items: center; gap: 16px;
  .rank-badge {
    text-align: center;
    .rank-number { font-size: 36px; font-weight: 700; color: var(--el-color-primary); }
    .rank-total { font-size: 14px; color: var(--el-text-color-secondary); display: block; }
  }
  .rank-bar-wrapper { flex: 1; }
}
.rank-note { margin-top: 8px; font-size: 13px; color: var(--el-text-color-secondary); }
.timeline-detail { font-size: 12px; color: var(--el-text-color-secondary); margin-top: 2px; }
</style>
