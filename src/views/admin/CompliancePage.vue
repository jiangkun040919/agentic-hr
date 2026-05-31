<template>
  <div class="compliance-page">
    <div class="page-header">
      <h2>AI合规与透明性</h2>
      <p>公平性审计 · AI决策日志 · 数据来源声明 · 用户权利</p>
    </div>

    <!-- ═══ 公平性审计总览 ═══ -->
    <div class="cp-summary-row">
      <div class="cp-overall-card card-tech">
        <div class="cpo-header">
          <el-icon :size="22" color="var(--color-success)"><CircleCheckFilled /></el-icon>
          <span>公平性综合评分</span>
        </div>
        <div class="cpo-score">
          <el-progress type="dashboard" :percentage="auditReport?.overallRating?.score ?? 0" :color="scoreColor(auditReport?.overallRating?.score ?? 0)">
            <template #default="{ percentage }">
              <span class="cpo-score-num">{{ percentage }}</span>
              <span class="cpo-score-unit">分</span>
            </template>
          </el-progress>
        </div>
        <div class="cpo-time">最近审计: {{ formatTime(auditReport?.generatedAt ?? "") }}</div>
      </div>

      <!-- 各维度卡片 -->
      <div class="cp-dim-card card-tech" v-for="(dim, i) in dimensions" :key="i">
        <div class="cpd-header">
          <div class="cpd-icon" :style="{ background: dimColor(dim.status) }">
            <el-icon :size="16" color="#fff"><component :is="iconMap[dim.icon] || 'Setting'" /></el-icon>
          </div>
          <div class="cpd-info">
            <div class="cpd-name">{{ dim.name }}</div>
            <div class="cpd-score">{{ dim.score }}分</div>
          </div>
          <el-tag :type="dim.status === 'good' ? 'success' : dim.status === 'warning' ? 'warning' : 'danger'" size="small" round>
            {{ dim.status === 'good' ? '良好' : dim.status === 'warning' ? '关注' : '严重' }}
          </el-tag>
        </div>
        <div class="cpd-detail">{{ dim.detail }}</div>
        <div class="cpd-bar">
          <div class="cpd-bar-fill" :style="{ width: dim.score + '%', background: dimColor(dim.status) }" />
        </div>
      </div>
    </div>

    <!-- ═══ AI决策日志 ═══ -->
    <div class="cp-section">
      <div class="cp-section-header">
        <h3><el-icon color="var(--color-accent)"><Notebook /></el-icon> AI决策日志</h3>
        <el-button type="primary" size="small" @click="loadAudit" :loading="auditLoading">
          <el-icon><Refresh /></el-icon>刷新
        </el-button>
      </div>
      <div class="content-card" style="overflow: hidden;">
        <el-table :data="auditReport?.scoreDistribution?.distribution ?? []" stripe max-height="360">
          <el-table-column prop="status" label="状态" width="80" />
          <el-table-column prop="label" label="阶段" width="120" />
          <el-table-column prop="count" label="人数" width="100">
            <template #default="{ row }">
              <span style="font-weight: bold">{{ row.count }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="percentage" label="占比" width="100">
            <template #default="{ row }">
              <el-progress :percentage="row.percentage" :stroke-width="8" :show-text="true" />
            </template>
          </el-table-column>
        </el-table>
      </div>
    </div>

    <!-- ═══ 改进建议 ═══ -->
    <div v-if="auditReport?.recommendations?.length" class="cp-section">
      <div class="cp-section-header">
        <h3><el-icon color="var(--color-gold)"><Star /></el-icon> 改进建议</h3>
      </div>
      <div class="cp-rights-card content-card">
        <div v-for="(rec, i) in auditReport.recommendations" :key="i" class="cpr-item">
          <div class="cpr-num">{{ i + 1 }}</div>
          <div class="cpr-text">{{ rec }}</div>
        </div>
      </div>
    </div>

    <!-- ═══ 数据来源声明 ═══ -->
    <div class="cp-section">
      <div class="cp-section-header">
        <h3><el-icon color="var(--color-gold)"><Document /></el-icon> 数据来源声明</h3>
      </div>
      <div class="cp-source-grid">
        <div class="cp-source-card content-card" v-for="(ds, i) in dataSources" :key="i">
          <div class="cps-name">
            <el-icon color="var(--color-primary)"><DataBoard /></el-icon>
            {{ ds.name }}
          </div>
          <div class="cps-row">
            <span class="cps-label">数据来源</span>
            <span class="cps-value">{{ ds.source }}</span>
          </div>
          <div class="cps-row">
            <span class="cps-label">使用目的</span>
            <span class="cps-value">{{ ds.purpose }}</span>
          </div>
          <div class="cps-row">
            <span class="cps-label">存储策略</span>
            <span class="cps-value cps-storage">{{ ds.storage }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 用户权利说明 ═══ -->
    <div class="cp-section">
      <div class="cp-section-header">
        <h3><el-icon color="var(--color-rose)"><Lock /></el-icon> 您的权利</h3>
      </div>
      <div class="cp-rights-card content-card">
        <div v-for="(right, i) in userRights" :key="i" class="cpr-item">
          <div class="cpr-num">{{ i + 1 }}</div>
          <div class="cpr-text">{{ right }}</div>
        </div>
      </div>
    </div>


  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import dayjs from 'dayjs'
import { runFairnessAudit, type FairnessAuditReport, type GroupStat } from '@/api/fairness'
import {
  CircleCheckFilled, Notebook, Refresh, Document, DataBoard, Lock, InfoFilled, School, Star, Location, User, Calendar, Setting
} from '@element-plus/icons-vue'

const auditLoading = ref(false)
const auditReport = ref<FairnessAuditReport | null>(null)
const auditError = ref('')

const iconMap: Record<string, any> = { School, Star, Location, User, Calendar, Setting }

// 从审计报告映射维度卡片
const dimensions = computed(() => {
  if (!auditReport.value) return []
  const r = auditReport.value
  return [
    { name: '学历偏差', score: Math.max(0, 100 - r.educationBias.biasRatio * 30), status: r.educationBias.isBiased ? 'warning' : 'good' as const, detail: r.educationBias.summary, icon: 'School', groups: r.educationBias.groups },
    { name: '经验偏差', score: Math.max(0, 100 - r.experienceBias.biasRatio * 30), status: r.experienceBias.isBiased ? 'warning' : 'good' as const, detail: r.experienceBias.summary, icon: 'Star', groups: r.experienceBias.groups },
    { name: '地域偏差', score: Math.max(0, 100 - r.locationBias.biasRatio * 30), status: r.locationBias.isBiased ? 'warning' : 'good' as const, detail: r.locationBias.summary, icon: 'Location', groups: r.locationBias.groups },
    { name: '评分分布', score: Math.min(100, Math.max(0, 100 - Math.abs(r.scoreDistribution.averageStatus - 2) * 15)), status: 'good' as const, detail: `共${r.scoreDistribution.totalCount}条投递，平均状态${r.scoreDistribution.averageStatus}`, icon: 'Calendar', groups: [] },
  ]
})

const scoreColor = (score: number) => score >= 85 ? '#7A8B5E' : score >= 70 ? '#C4945A' : '#B8605A'
const dimColor = (status: string) => status === 'good' ? 'var(--color-success)' : status === 'warning' ? 'var(--color-warning)' : 'var(--color-danger)'
const formatTime = (t: string) => t ? dayjs(t).format('YYYY-MM-DD HH:mm:ss') : '--'

// 数据来源声明（静态）
const dataSources = [
  { name: '投递记录', source: '候选人投递简历时填写的表单信息', purpose: '候选人技能、教育、工作经历提取', storage: '加密存储，保留至招聘流程结束' },
  { name: 'AI匹配评分', source: 'MiniMax API + 本地规则引擎', purpose: '候选人与岗位匹配度评估', storage: '评分结果存储30天' },
  { name: '知识图谱', source: 'Neo4j图数据库', purpose: '技能关系推理与反幻觉验证', storage: '永久存储（脱敏后）' },
  { name: '公平性审计', source: '系统自动统计分析', purpose: '检测招聘各环节的偏差指标', storage: '汇总报告存储90天' },
]

const userRights = [
  '您有权查看AI系统对您简历的评分依据',
  '您有权要求人工复核AI的筛选决定',
  '您有权请求删除个人数据（部分数据受法律法规保护）',
  '您有权对不公平的筛选结果提出申诉',
  '系统每季度进行一次公平性审计，结果公开可查',
]

const loadAudit = async () => {
  auditLoading.value = true
  auditError.value = ''
  try {
    const res = await runFairnessAudit() as any
    auditReport.value = res
  } catch (e: any) {
    auditError.value = e?.message || '审计数据加载失败'
  } finally {
    auditLoading.value = false
  }
}

onMounted(loadAudit)
</script>

<style scoped lang="scss">
.compliance-page {
  max-width: var(--content-max-width);
  padding-bottom: var(--space-8);
}

.page-header {
  margin-bottom: var(--space-5);
  h2 { font-size: var(--text-xl); font-weight: var(--weight-bold); color: var(--color-text); margin: 0 0 var(--space-1); }
  p { color: var(--color-text-secondary); font-size: var(--text-sm); margin: 0; }
}

// ====== 总览行 ======
.cp-summary-row {
  display: grid; grid-template-columns: 240px repeat(5, 1fr); gap: var(--space-4);
  margin-bottom: var(--space-5);

  @media (max-width: 1400px) { grid-template-columns: repeat(3, 1fr); }
  @media (max-width: 768px) { grid-template-columns: 1fr; }
}

.cp-overall-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-lg); padding: var(--space-4);
  display: flex; flex-direction: column; align-items: center; gap: var(--space-3);
}

.cpo-header {
  display: flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text);
}

.cpo-score {
  :deep(.el-progress) { width: 140px; }
  .cpo-score-num { font-size: 28px; font-weight: var(--weight-bold); font-family: var(--font-mono); color: var(--color-text); }
  .cpo-score-unit { font-size: var(--text-sm); color: var(--color-text-muted); }
}

.cpo-time { font-size: var(--text-xs); color: var(--color-text-muted); }

// ====== 维度卡片 ======
.cp-dim-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-lg); padding: var(--space-4);
  display: flex; flex-direction: column; gap: var(--space-2);
}

.cpd-header {
  display: flex; align-items: center; gap: var(--space-3);
  .cpd-icon {
    width: 32px; height: 32px; border-radius: var(--radius-md);
    display: flex; align-items: center; justify-content: center; flex-shrink: 0;
  }
  .cpd-info { flex: 1;
    .cpd-name { font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text); }
    .cpd-score { font-size: 11px; color: var(--color-text-muted); font-family: var(--font-mono); }
  }
}

.cpd-detail { font-size: 11px; color: var(--color-text-muted); line-height: 1.5; flex: 1; }

.cpd-bar { height: 4px; background: var(--color-bg-alt); border-radius: 2px; overflow: hidden; margin-top: auto;
  .cpd-bar-fill { height: 100%; border-radius: 2px; transition: width 1s var(--ease-out); }
}

// ====== 区块 ======
.cp-section {
  margin-bottom: var(--space-5);
}

.cp-section-header {
  display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-3);
  h3 { font-size: var(--text-md); font-weight: var(--weight-semibold); color: var(--color-text); margin: 0; display: flex; align-items: center; gap: var(--space-2); }
}

// ====== 数据来源 ======
.cp-source-grid {
  display: grid; grid-template-columns: repeat(2, 1fr); gap: var(--space-4);
  @media (max-width: 768px) { grid-template-columns: 1fr; }
}

.cp-source-card {
  .cps-name {
    display: flex; align-items: center; gap: var(--space-2);
    font-size: var(--text-sm); font-weight: var(--weight-semibold); color: var(--color-text);
    margin-bottom: var(--space-3); padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--color-border-light);
  }
  .cps-row {
    display: flex; justify-content: space-between; padding: var(--space-1) 0; font-size: var(--text-xs);
    .cps-label { color: var(--color-text-muted); flex-shrink: 0; }
    .cps-value { color: var(--color-text-secondary); text-align: right; }
    .cps-storage { color: var(--color-warning); font-weight: var(--weight-medium); }
  }
}

// ====== 用户权利 ======
.cp-rights-card {
  padding: var(--space-5) !important;
}

.cpr-item {
  display: flex; align-items: flex-start; gap: var(--space-4); padding: var(--space-3) 0;
  border-bottom: 1px solid var(--color-border-light);
  &:last-child { border-bottom: none; }

  .cpr-num {
    width: 28px; height: 28px; border-radius: 50%;
    background: var(--gradient-primary); color: #fff;
    display: flex; align-items: center; justify-content: center;
    font-size: var(--text-xs); font-weight: var(--weight-bold); flex-shrink: 0;
  }
  .cpr-text { font-size: var(--text-sm); color: var(--color-text-secondary); line-height: 1.6; padding-top: 4px; }
}

// ====== 底部声明 ======
.cp-footer-note {
  display: flex; align-items: flex-start; gap: var(--space-3);
  font-size: var(--text-xs); color: var(--color-text-muted); line-height: 1.7;
  background: var(--color-bg-alt) !important; border-color: var(--color-border-light) !important;
}
</style>
