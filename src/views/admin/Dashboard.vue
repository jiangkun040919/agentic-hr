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
        <el-button size="large" @click="$router.push('/admin/resumes')">查看简历</el-button>
      </div>
    </div>

    <!-- ═══ 统计卡片 ═══ -->
    <div class="stat-row">
      <div class="stat-card" style="--card-color: var(--color-primary)">
        <div class="sc-icon"><el-icon :size="22"><Briefcase /></el-icon></div>
        <div class="sc-body">
          <div class="sc-num">{{ animated.jobs }}</div>
          <div class="sc-label">开放岗位</div>
          <div class="sc-sub">共 {{ stats.openJobs }} 个在招</div>
        </div>
      </div>
      <div class="stat-card" style="--card-color: var(--color-accent)">
        <div class="sc-icon"><el-icon :size="22"><Document /></el-icon></div>
        <div class="sc-body">
          <div class="sc-num">{{ animated.deliveries }}</div>
          <div class="sc-label">简历投递</div>
          <div class="sc-sub">共 {{ stats.totalDeliveries }} 份</div>
        </div>
      </div>
      <div class="stat-card" style="--card-color: var(--color-success)">
        <div class="sc-icon"><el-icon :size="22"><VideoCamera /></el-icon></div>
        <div class="sc-body">
          <div class="sc-num">{{ animated.interviews }}</div>
          <div class="sc-label">面试安排</div>
          <div class="sc-sub">共 {{ stats.interviews }} 场</div>
        </div>
      </div>
      <div class="stat-card" style="--card-color: #8B5CF6">
        <div class="sc-icon"><el-icon :size="22"><Medal /></el-icon></div>
        <div class="sc-body">
          <div class="sc-num">{{ animated.hired }}</div>
          <div class="sc-label">已录用/入职</div>
          <div class="sc-sub">共 {{ stats.hired }} 人</div>
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

      <!-- 最近投递 -->
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
              <el-tag :type="getStatusType(d.status)" size="small" round>{{ getStatusText(d.status) }}</el-tag>
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
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getDashboardData } from '@/api/stat'
import { Briefcase, Document, VideoCamera, Medal, Plus, WarningFilled, Calendar, Clock, ArrowRight } from '@element-plus/icons-vue'
import dayjs from 'dayjs'

const router = useRouter()
const today = dayjs().format('YYYY年MM月DD日 dddd')

const stats = reactive({ openJobs: 0, totalDeliveries: 0, interviews: 0, hired: 0 })
const animated = reactive({ jobs: 0, deliveries: 0, interviews: 0, hired: 0 })
const pendingResumes = ref<any[]>([])
const todayInterviews = ref<any[]>([])
const recentDeliveries = ref<any[]>([])
const pendingCount = computed(() => pendingResumes.value.length)

const animate = (key: keyof typeof animated, target: number) => {
  let current = 0
  const step = Math.max(1, Math.ceil(target / 40))
  const timer = setInterval(() => { current += step; if (current >= target) { animated[key] = target; clearInterval(timer) } else { animated[key] = current } }, 30)
}

const pipelineStages = reactive([
  { label: '待查看', count: 0 },
  { label: '面试中', count: 0 },
  { label: '实习中', count: 0 },
  { label: '正式入职', count: 0 },
  { label: '已淘汰', count: 0 },
])

onMounted(() => fetchDashboardData())

const fetchDashboardData = async () => {
  try {
    const response = await getDashboardData()
    const data = response.data || response
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
    animate('jobs', stats.openJobs)
    animate('deliveries', stats.totalDeliveries)
    animate('interviews', stats.interviews)
    animate('hired', stats.hired)
  } catch (e) { console.error('Dashboard load failed', e) }
}

const formatDate = (date: string) => dayjs(date).format('MM-DD HH:mm')

const getStatusType = (status: number): 'info' | 'warning' | 'primary' | 'success' | 'danger' => (['info', 'info', 'warning', 'primary', 'success', 'danger'] as const)[status] || 'info'
const getStatusText = (status: number) => ['待查看', '已查看', '面试中', '实习中', '正式入职', '已淘汰'][status] || '未知'
const goToResume = (id: number) => router.push(`/admin/resumes/${id}`)
</script>

<style scoped lang="scss">
.dashboard {
  max-width: var(--content-max-width);
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

// ====== 统计卡片行 ======
.stat-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--space-4);
  margin-bottom: var(--space-5);

  @media (max-width: 1024px) { grid-template-columns: repeat(2, 1fr); }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  box-shadow: var(--shadow-card);
  border: 1px solid var(--color-border);
  transition: all var(--duration-fast) var(--ease-out);
  position: relative;
  overflow: hidden;

  &::before {
    content: '';
    position: absolute;
    left: 0; top: 12px; bottom: 12px;
    width: 3px;
    background: var(--card-color);
    border-radius: 0 3px 3px 0;
  }

  &:hover {
    transform: translateY(-2px);
    border-color: var(--color-border-glow);
    box-shadow: var(--shadow-glow);
  }

  .sc-icon {
    width: 48px; height: 48px;
    border-radius: var(--radius-md);
    background: color-mix(in srgb, var(--card-color) 12%, transparent);
    color: var(--card-color);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  .sc-body { flex: 1; }

  .sc-num {
    font-size: 28px;
    font-weight: var(--weight-bold);
    color: var(--color-text);
    font-family: var(--font-mono);
    font-variant-numeric: tabular-nums;
    line-height: 1;
  }

  .sc-label {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    margin-top: var(--space-1);
  }

  .sc-sub {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    margin-top: 2px;
  }
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
  display: flex;
  align-items: center;
  flex: 1;
}

.pipe-card {
  flex: 1;
  text-align: center;
  padding: var(--space-3) var(--space-2);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  transition: all var(--duration-fast) var(--ease-out);

  &:hover {
    background: var(--color-surface-hover);
  }

  &.active {
    background: var(--color-primary-bg);
    box-shadow: 0 0 0 1px var(--color-border-glow);
    .pipe-num { color: var(--color-primary); }
  }

  .pipe-num {
    font-size: 22px;
    font-weight: var(--weight-bold);
    color: var(--color-text);
    font-family: var(--font-mono);
    font-variant-numeric: tabular-nums;
  }

  .pipe-name {
    font-size: var(--text-xs);
    color: var(--color-text-secondary);
    margin-top: 2px;
  }
}

.pipe-arrow {
  color: var(--color-text-muted);
  margin: 0 var(--space-1);
  flex-shrink: 0;
}

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
    flex: 1;
    min-width: 0;

    .iv-name { font-size: var(--text-sm); font-weight: var(--weight-medium); color: var(--color-text); }
    .iv-job {
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
      margin-top: 2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
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
    width: 34px; height: 34px;
    border-radius: 50%;
    background: var(--gradient-primary);
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    flex-shrink: 0;
  }

  .ri-info {
    flex: 1; min-width: 0;

    .ri-name { font-size: var(--text-sm); color: var(--color-text); font-weight: var(--weight-medium); }
    .ri-job {
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
      margin-top: 2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  }

  .ri-right {
    text-align: right;
    flex-shrink: 0;

    .ri-time { font-size: 11px; color: var(--color-text-muted); margin-top: var(--space-1); }
  }
}
</style>