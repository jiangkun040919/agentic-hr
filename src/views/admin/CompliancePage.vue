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
          <el-progress type="dashboard" :percentage="fairnessData.overallScore" :color="scoreColor(fairnessData.overallScore)">
            <template #default="{ percentage }">
              <span class="cpo-score-num">{{ percentage }}</span>
              <span class="cpo-score-unit">分</span>
            </template>
          </el-progress>
        </div>
        <div class="cpo-time">最近审计: {{ formatTime(fairnessData.auditTime) }}</div>
      </div>

      <!-- 各维度卡片 -->
      <div class="cp-dim-card card-tech" v-for="(dim, i) in fairnessData.dimensions" :key="i">
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
        <el-button type="primary" size="small" @click="refreshLogs" :loading="logLoading">
          <el-icon><Refresh /></el-icon>刷新
        </el-button>
      </div>
      <div class="content-card" style="overflow: hidden;">
        <el-table :data="fairnessData.aiDecisions" stripe max-height="360">
          <el-table-column prop="id" label="ID" width="60" />
          <el-table-column prop="type" label="决策类型" width="110">
            <template #default="{ row }">
              <el-tag :type="typeTag(row.type)" size="small">{{ row.type }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="candidate" label="候选人" width="90" />
          <el-table-column prop="job" label="岗位" min-width="160" show-overflow-tooltip />
          <el-table-column prop="score" label="评分" width="80">
            <template #default="{ row }">
              <span :style="{ color: scoreTextColor(row.score), fontWeight: 'bold' }">{{ row.score }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="reason" label="决策理由" min-width="180" show-overflow-tooltip />
          <el-table-column prop="timestamp" label="时间" width="160">
            <template #default="{ row }">{{ formatTime(row.timestamp) }}</template>
          </el-table-column>
        </el-table>
      </div>
    </div>

    <!-- ═══ 数据来源声明 ═══ -->
    <div class="cp-section">
      <div class="cp-section-header">
        <h3><el-icon color="var(--color-gold)"><Document /></el-icon> 数据来源声明</h3>
      </div>
      <div class="cp-source-grid">
        <div class="cp-source-card content-card" v-for="(ds, i) in fairnessData.dataSources" :key="i">
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
        <div v-for="(right, i) in fairnessData.userRights" :key="i" class="cpr-item">
          <div class="cpr-num">{{ i + 1 }}</div>
          <div class="cpr-text">{{ right }}</div>
        </div>
      </div>
    </div>

    <!-- ═══ 合规声明 ═══ -->
    <div class="cp-footer-note content-card">
      <el-icon color="var(--color-text-muted)" :size="16"><InfoFilled /></el-icon>
      <span>本系统遵循《个人信息保护法》《数据安全法》相关规定，AI决策过程全程可追溯。系统每季度自动进行公平性审计，审计结果对监管机构及用户公开。如有疑问请联系合规团队：compliance@ai-recruit.com</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import dayjs from 'dayjs'
import { getFairnessStaticData, type FairnessStaticData } from '@/api/fairness'
import {
  CircleCheckFilled, Notebook, Refresh, Document, DataBoard, Lock, InfoFilled, School, Star, Location, User, Calendar, Setting
} from '@element-plus/icons-vue'

const logLoading = ref(false)

const iconMap: Record<string, any> = {
  School, Star, Location, User, Calendar, Setting,
}

const fairnessData = reactive<FairnessStaticData>({
  auditTime: '',
  overallScore: 0,
  dimensions: [],
  aiDecisions: [],
  dataSources: [],
  userRights: [],
})

const scoreColor = (score: number) => {
  if (score >= 85) return '#2DD4A3'
  if (score >= 70) return '#F5A623'
  return '#F4586D'
}

const dimColor = (status: string) => {
  if (status === 'good') return 'var(--color-success)'
  if (status === 'warning') return 'var(--color-warning)'
  return 'var(--color-danger)'
}

const scoreTextColor = (score: number) => {
  if (score >= 85) return 'var(--color-success)'
  if (score >= 70) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

const typeTag = (type: string): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
  const map: Record<string, any> = {
    '简历筛选': 'primary', '匹配评分': 'success', '自动推荐': 'warning', '面试评估': 'info',
  }
  return map[type] || 'info'
}

const formatTime = (t: string) => {
  if (!t) return '--'
  return dayjs(t).format('YYYY-MM-DD HH:mm:ss')
}

const refreshLogs = async () => {
  logLoading.value = true
  try {
    const data = await getFairnessStaticData()
    fairnessData.aiDecisions = data.aiDecisions
  } catch { /* use existing */ }
  finally { logLoading.value = false }
}

onMounted(async () => {
  try {
    const data = await getFairnessStaticData()
    Object.assign(fairnessData, data)
  } catch { /* fallback already loaded */ }
})
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
