<template>
  <div class="interview-page">
    <!-- ═══ 统计概览 ═══ -->
    <div class="interview-stats">
      <div class="stat-item" :class="{ active: searchParams.status === undefined }" @click="searchParams.status = undefined; fetchInterviews()">
        <div class="stat-value">{{ stats.total }}</div>
        <div class="stat-label">全部面试</div>
      </div>
      <div class="stat-item accent" :class="{ active: searchParams.status === 0 }" @click="searchParams.status = 0; fetchInterviews()">
        <div class="stat-value">{{ stats.pending }}</div>
        <div class="stat-label">待面试</div>
        <div class="stat-dot" />
      </div>
      <div class="stat-item success" :class="{ active: searchParams.status === 2 }" @click="searchParams.status = 2; fetchInterviews()">
        <div class="stat-value">{{ stats.passed }}</div>
        <div class="stat-label">已通过</div>
      </div>
      <div class="stat-item danger" :class="{ active: searchParams.status === 3 }" @click="searchParams.status = 3; fetchInterviews()">
        <div class="stat-value">{{ stats.failed }}</div>
        <div class="stat-label">未通过</div>
      </div>
    </div>

    <!-- ═══ 工具栏 ═══ -->
    <div class="toolbar">
      <div class="toolbar-left">
        <el-input
          v-model="searchParams.keyword"
          placeholder="搜索候选人姓名..."
          :prefix-icon="Search"
          clearable
          size="default"
          class="search-input"
          @change="fetchInterviews"
        />
        <el-select v-model="searchParams.interviewType" placeholder="面试形式" clearable size="default" @change="fetchInterviews">
          <el-option label="线上面试" value="online" />
          <el-option label="现场面试" value="onsite" />
          <el-option label="电话面试" value="phone" />
        </el-select>
      </div>
      <div class="toolbar-right">
        <el-radio-group v-model="viewMode" size="small">
          <el-radio-button value="list">
            <el-icon><List /></el-icon>
          </el-radio-button>
          <el-radio-button value="calendar">
            <el-icon><Calendar /></el-icon>
          </el-radio-button>
        </el-radio-group>
        <el-button type="primary" @click="handleAdd">
          <el-icon><Plus /></el-icon>安排面试
        </el-button>
      </div>
    </div>

    <!-- ═══ 列表视图 ═══ -->
    <div v-if="viewMode === 'list'" v-loading="loading" class="interview-list-view">
      <div v-if="interviews.length === 0 && !loading" class="empty-state">
        <div class="empty-icon"><Calendar /></div>
        <div class="empty-title">暂无面试安排</div>
        <div class="empty-desc">点击「安排面试」开始创建面试</div>
        <el-button type="primary" @click="handleAdd">安排面试</el-button>
      </div>

      <div v-else class="interview-cards">
        <div
          v-for="item in interviews"
          :key="item.interviewId"
          class="interview-card"
          :class="cardStatusClass(item.status)"
          @click="viewDetail(item)"
        >
          <!-- 左侧时间轴 -->
          <div class="card-time">
            <div class="time-date">{{ formatDay(item.scheduleTime) }}</div>
            <div class="time-hour">{{ formatHour(item.scheduleTime) }}</div>
          </div>

          <!-- 中间信息 -->
          <div class="card-body">
            <div class="card-header">
              <h4 class="card-candidate">{{ item.candidateName }}</h4>
              <el-tag :type="getStatusTagType(item.status)" size="small" effect="light" round>
                {{ getStatusText(item.status) }}
              </el-tag>
            </div>
            <div class="card-meta">
              <span class="meta-item">
                <el-icon><Briefcase /></el-icon>{{ item.jobTitle }}
              </span>
              <span class="meta-item">
                <el-icon><User /></el-icon>{{ item.interviewerName || '待分配' }}
              </span>
              <span class="meta-item">
                <el-icon><VideoCamera /></el-icon>{{ item.interviewType || '线上面试' }}
              </span>
            </div>
            <div class="card-round" v-if="item.round">
              第{{ item.round }}轮面试
            </div>
          </div>

          <!-- 右侧操作 -->
          <div class="card-actions" @click.stop>
            <template v-if="item.status === 0">
              <el-button size="small" @click="handleReschedule(item)">改期</el-button>
              <el-button size="small" type="danger" plain @click="handleCancel(item)">取消</el-button>
              <el-button size="small" type="primary" @click="handleRecordResult(item)">录入结果</el-button>
            </template>
            <template v-else>
              <el-button size="small" @click="viewDetail(item)">查看</el-button>
            </template>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 日历视图 ═══ -->
    <div v-else class="interview-calendar-view">
      <div class="calendar-toolbar">
        <el-button text @click="prevWeek"><el-icon><ArrowLeft /></el-icon></el-button>
        <span class="calendar-range">{{ calendarRange }}</span>
        <el-button text @click="nextWeek"><el-icon><ArrowRight /></el-icon></el-button>
        <el-button size="small" @click="calendarWeekStart = dayjs().startOf('week')" style="margin-left:8px">今天</el-button>
      </div>

      <div class="calendar-grid">
        <div v-for="(day, di) in weekDays" :key="di" class="calendar-day" :class="{ today: day.isToday }">
          <div class="day-header">
            <span class="day-name">{{ day.dayName }}</span>
            <span class="day-date" :class="{ todayDot: day.isToday }">{{ day.dayNum }}</span>
          </div>
          <div class="day-slots">
            <div
              v-for="iv in day.interviews"
              :key="iv.interviewId"
              class="day-slot"
              :class="cardStatusClass(iv.status)"
              @click="viewDetail(iv)"
            >
              <div class="slot-time">{{ formatHour(iv.scheduleTime) }}</div>
              <div class="slot-info">
                <div class="slot-name">{{ iv.candidateName }}</div>
                <div class="slot-job">{{ iv.jobTitle }}</div>
              </div>
            </div>
            <div v-if="day.interviews.length === 0" class="day-empty">空闲</div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 分页 ═══ -->
    <div v-if="viewMode === 'list' && total > 0" class="pagination-wrap">
      <el-pagination
        v-model:current-page="searchParams.page"
        v-model:page-size="searchParams.pageSize"
        :total="total"
        layout="total, prev, pager, next"
        @change="fetchInterviews"
      />
    </div>

    <!-- ═══ 弹窗: 面试详情 ═══ -->
    <InterviewDetailDialog
      v-model="detailDialogVisible"
      :interview="currentInterview"
      :mode="dialogMode"
      @success="handleDetailSuccess"
    />

    <!-- ═══ 弹窗: 选择候选人 ═══ -->
    <el-dialog v-model="selectDialogVisible" title="选择候选人安排面试" width="680px" destroy-on-close>
      <el-input v-model="deliveryKeyword" placeholder="搜索候选人/岗位..." :prefix-icon="Search" clearable size="default" class="select-search" @input="fetchPendingDeliveries" />
      <el-table :data="pendingDeliveries" v-loading="deliveryLoading" stripe max-height="360" @row-dblclick="handleSelectDelivery" highlight-current-row>
        <el-table-column prop="candidateName" label="候选人" width="100" />
        <el-table-column prop="jobTitle" label="应聘岗位" min-width="130" show-overflow-tooltip />
        <el-table-column prop="phone" label="手机号" width="125" />
        <el-table-column prop="education" label="学历" width="80" />
        <el-table-column prop="deliverTime" label="投递时间" width="130">
          <template #default="{ row }">{{ formatDate(row.deliverTime) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click="handleSelectDelivery(row)">选择</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-if="!deliveryLoading && pendingDeliveries.length === 0" description="暂无待安排面试的候选人" :image-size="64" />
    </el-dialog>

    <!-- ═══ 弹窗: 面试安排 ═══ -->
    <ScheduleInterviewDialog
      v-model="scheduleDialogVisible"
      :delivery="selectedDelivery"
      @success="handleScheduleSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getInterviewList } from '@/api/interview'
import type { InterviewStatus } from '@/api/interview/types'
import { getResumeList } from '@/api/delivery'
import dayjs from 'dayjs'
import isoWeek from 'dayjs/plugin/isoWeek'
import 'dayjs/locale/zh-cn'
import InterviewDetailDialog from '@/components/interview/InterviewDetailDialog.vue'
import ScheduleInterviewDialog from '@/components/interview/ScheduleInterviewDialog.vue'
import { Search, Plus, List, Calendar, Briefcase, User, VideoCamera, ArrowLeft, ArrowRight } from '@element-plus/icons-vue'

dayjs.extend(isoWeek)
dayjs.locale('zh-cn')

const route = useRoute()

const loading = ref(false)
const interviews = ref<any[]>([])
const total = ref(0)
const viewMode = ref<'list' | 'calendar'>('list')

// 统计
const stats = reactive({ total: 0, pending: 0, passed: 0, failed: 0 })

// 弹窗
const detailDialogVisible = ref(false)
const scheduleDialogVisible = ref(false)
const selectDialogVisible = ref(false)
const currentInterview = ref<any>(null)
const selectedDelivery = ref<any>(null)
const dialogMode = ref<'view' | 'reschedule' | 'cancel' | 'result'>('view')

const pendingDeliveries = ref<any[]>([])
const deliveryLoading = ref(false)
const deliveryKeyword = ref('')

// 日历
const calendarWeekStart = ref(dayjs().startOf('isoWeek'))

const searchParams = reactive({
  page: 1, pageSize: 10, keyword: '', status: undefined as InterviewStatus | undefined, interviewType: '',
})

onMounted(() => {
  fetchInterviews()
  if (route.query.deliveryId) handleAdd()
})

const fetchInterviews = async () => {
  loading.value = true
  try {
    const res = await getInterviewList(searchParams)
    interviews.value = res.items || []
    total.value = res.total || 0
    // 统计 (从后端拿 or 前端简单算)
    const all = await getInterviewList({ page: 1, pageSize: 1000, status: undefined })
    const items = all.items || []
    stats.total = all.total || items.length
    stats.pending = items.filter((i: any) => i.status === 0).length
    stats.passed = items.filter((i: any) => i.status === 2).length
    stats.failed = items.filter((i: any) => i.status === 3).length
  } catch { /* ignore */ }
  finally { loading.value = false }
}

// ── 日历 ──
const weekDays = computed(() => {
  const start = calendarWeekStart.value
  return Array.from({ length: 7 }, (_, i) => {
    const d = start.add(i, 'day')
    const dayInterviews = interviews.value.filter((iv: any) => {
      if (!iv.scheduleTime) return false
      return dayjs(iv.scheduleTime).format('YYYY-MM-DD') === d.format('YYYY-MM-DD')
    })
    return {
      dayName: d.format('ddd'),
      dayNum: d.date(),
      isToday: d.format('YYYY-MM-DD') === dayjs().format('YYYY-MM-DD'),
      interviews: dayInterviews,
    }
  })
})

const calendarRange = computed(() => {
  const start = calendarWeekStart.value
  const end = start.add(6, 'day')
  return `${start.format('MM月DD日')} - ${end.format('MM月DD日')}`
})

const prevWeek = () => { calendarWeekStart.value = calendarWeekStart.value.subtract(7, 'day') }
const nextWeek = () => { calendarWeekStart.value = calendarWeekStart.value.add(7, 'day') }

// ── 格式化 ──
const formatDate = (d: string) => dayjs(d).format('MM-DD HH:mm')
const formatDay = (d: string) => d ? dayjs(d).format('MM/DD') : '--'
const formatHour = (d: string) => d ? dayjs(d).format('HH:mm') : '--:--'

// ── 状态 ──
const getStatusText = (s: number) => ['待面试', '已完成', '已通过', '未通过', '已取消'][s] || '未知'
const getStatusTagType = (s: number): 'warning' | 'info' | 'success' | 'danger' => {
  return (['warning', 'info', 'success', 'danger', 'info'] as const)[s] || 'info'
}
const cardStatusClass = (s: number) => {
  return ['status-pending', 'status-done', 'status-passed', 'status-failed', 'status-cancelled'][s] || ''
}

// ── 操作 ──
const handleAdd = () => { deliveryKeyword.value = ''; selectDialogVisible.value = true; fetchPendingDeliveries() }

const fetchPendingDeliveries = async () => {
  deliveryLoading.value = true
  try {
    const res = await getResumeList({ page: 1, pageSize: 50, keyword: deliveryKeyword.value || undefined, status: 1 })
    pendingDeliveries.value = Array.isArray(res) ? res : (res?.items || [])
  } catch { pendingDeliveries.value = [] }
  finally { deliveryLoading.value = false }
}

const handleSelectDelivery = (row: any) => {
  selectedDelivery.value = row
  selectDialogVisible.value = false
  scheduleDialogVisible.value = true
}

const viewDetail = (row: any) => { dialogMode.value = 'view'; currentInterview.value = row; detailDialogVisible.value = true }
const handleReschedule = (row: any) => { dialogMode.value = 'reschedule'; currentInterview.value = row; detailDialogVisible.value = true }
const handleCancel = (row: any) => { dialogMode.value = 'cancel'; currentInterview.value = row; detailDialogVisible.value = true }
const handleRecordResult = (row: any) => { dialogMode.value = 'result'; currentInterview.value = row; detailDialogVisible.value = true }
const handleDetailSuccess = () => fetchInterviews()
const handleScheduleSuccess = () => fetchInterviews()
</script>

<style scoped lang="scss">
// ====== 页面容器 ======
.interview-page {
  max-width: var(--content-max-width);
}

// ====== 统计概览 ======
.interview-stats {
  display: flex;
  gap: var(--space-3);
  margin-bottom: var(--space-5);
}

.stat-item {
  flex: 1;
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  padding: var(--space-4) var(--space-5);
  border: 2px solid var(--color-border);
  cursor: pointer;
  transition: all var(--duration-fast) var(--ease-out);
  position: relative;
  overflow: hidden;

  &:hover { border-color: var(--color-primary); }

  &.active {
    border-color: var(--color-primary);
    background: var(--color-primary-bg);
  }

  .stat-value {
    font-size: 28px;
    font-weight: var(--weight-bold);
    color: var(--color-text);
    font-family: var(--font-mono);
    font-variant-numeric: tabular-nums;
  }

  .stat-label {
    font-size: var(--text-xs);
    color: var(--color-text-secondary);
    margin-top: var(--space-1);
  }

  .stat-dot {
    position: absolute;
    right: var(--space-3);
    top: 50%;
    transform: translateY(-50%);
    width: 8px; height: 8px;
    border-radius: 50%;
    background: var(--color-accent);
  }

  &.accent .stat-value { color: var(--color-accent); }
  &.success .stat-value { color: var(--color-success); }
  &.danger .stat-value { color: var(--color-danger); }
}

// ====== 工具栏 ======
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-4);

  .toolbar-left {
    display: flex;
    gap: var(--space-3);
    align-items: center;

    .search-input { width: 240px; }
  }

  .toolbar-right {
    display: flex;
    gap: var(--space-3);
    align-items: center;
  }
}

// ====== 面试卡片列表 ======
.interview-list-view {
  min-height: 200px;
}

.interview-cards {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.interview-card {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  padding: var(--space-4) var(--space-5);
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: all var(--duration-fast) var(--ease-out);

  &:hover {
    box-shadow: var(--shadow-md);
    border-color: var(--color-primary);
    transform: translateY(-1px);
  }

  // 左侧彩色状态条
  border-left: 3px solid var(--color-border);

  &.status-pending { border-left-color: var(--color-accent); }
  &.status-done { border-left-color: var(--color-primary); }
  &.status-passed { border-left-color: var(--color-success); }
  &.status-failed { border-left-color: var(--color-danger); }
  &.status-cancelled { border-left-color: var(--color-text-muted); }
}

.card-time {
  text-align: center;
  min-width: 52px;
  flex-shrink: 0;

  .time-date {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    font-weight: var(--weight-medium);
  }

  .time-hour {
    font-size: var(--text-lg);
    font-weight: var(--weight-bold);
    color: var(--color-text);
    font-family: var(--font-mono);
  }
}

.card-body {
  flex: 1;
  min-width: 0;

  .card-header {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-2);

    .card-candidate {
      font-size: var(--text-md);
      font-weight: var(--weight-semibold);
      color: var(--color-text);
      margin: 0;
    }
  }

  .card-meta {
    display: flex;
    gap: var(--space-4);
    flex-wrap: wrap;

    .meta-item {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);

      .el-icon { font-size: 14px; }
    }
  }

  .card-round {
    margin-top: var(--space-2);
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    background: var(--color-bg);
    display: inline-block;
    padding: 2px 8px;
    border-radius: var(--radius-full);
  }
}

.card-actions {
  display: flex;
  gap: var(--space-2);
  flex-shrink: 0;
}

// ====== 日历视图 ======
.calendar-toolbar {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-4);

  .calendar-range {
    font-size: var(--text-md);
    font-weight: var(--weight-semibold);
    min-width: 180px;
    text-align: center;
  }
}

.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: var(--space-2);
}

.calendar-day {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  padding: var(--space-3);
  min-height: 280px;

  &.today {
    border-color: var(--color-primary);
    background: var(--color-primary-bg);
  }

  .day-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-3);
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--color-border-light);

    .day-name { font-size: var(--text-xs); color: var(--color-text-secondary); font-weight: var(--weight-medium); }
    .day-date {
      font-size: var(--text-base);
      font-weight: var(--weight-semibold);
      width: 26px; height: 26px;
      display: flex; align-items: center; justify-content: center;
      border-radius: 50%;

      &.todayDot {
        background: var(--color-primary);
        color: #fff;
      }
    }
  }
}

.day-slots {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.day-slot {
  background: var(--color-bg);
  border-radius: var(--radius-md);
  padding: var(--space-2);
  cursor: pointer;
  transition: all var(--duration-fast) var(--ease-out);
  border-left: 3px solid var(--color-border);

  &:hover { background: var(--color-primary-bg); }

  &.status-pending { border-left-color: var(--color-accent); }
  &.status-passed { border-left-color: var(--color-success); }
  &.status-failed { border-left-color: var(--color-danger); }

  .slot-time {
    font-size: 11px;
    color: var(--color-text-muted);
    font-family: var(--font-mono);
    margin-bottom: 4px;
  }

  .slot-name {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    color: var(--color-text);
  }

  .slot-job {
    font-size: 11px;
    color: var(--color-text-secondary);
    margin-top: 2px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}

.day-empty {
  text-align: center;
  padding: var(--space-4);
  font-size: var(--text-xs);
  color: var(--color-text-muted);
}

// ====== 分页 ======
.pagination-wrap {
  margin-top: var(--space-6);
  display: flex;
  justify-content: center;
}

// ====== 选择对话框 ======
.select-search {
  margin-bottom: var(--space-3);
}
</style>