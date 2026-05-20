<template>
  <div class="job-list-page">
    <!-- 顶部 Hero 横幅 -->
    <div class="hero-banner">
      <div class="hero-content">
        <div class="hero-text">
          <h1>发现你的<span class="gradient-text">理想岗位</span></h1>
          <p>AI 驱动岗位能力图谱 · 实时追踪技术趋势 · 精准匹配人才 <span v-if="lastUpdated" class="update-badge">🕐 数据更新于 {{ lastUpdated }}</span></p>
        </div>
        <div class="hero-stats">
          <div class="hero-stat">
            <div class="hs-num">{{ displayStats.jobs || heroStats[0].value }}</div>
            <div class="hs-label">{{ heroStats[0].label }}</div>
          </div>
          <div class="hero-stat">
            <div class="hs-num">{{ displayStats.depts || heroStats[1].value }}</div>
            <div class="hs-label">{{ heroStats[1].label }}</div>
          </div>
          <div class="hero-stat">
            <div class="hs-num">{{ heroStats[2].value }}</div>
            <div class="hs-label">{{ heroStats[2].label }}</div>
          </div>
          <div class="hero-stat">
            <div class="hs-num">{{ heroStats[3].value }}</div>
            <div class="hs-label">{{ heroStats[3].label }}</div>
          </div>
        </div>
      </div>
      <div class="hero-decor">
        <div class="decor-circle c1" /><div class="decor-circle c2" /><div class="decor-circle c3" />
      </div>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <div class="search-row">
        <el-input v-model="searchParams.keyword" placeholder="搜索岗位名称、技能..." clearable size="large" class="search-input" @change="fetchJobs">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="searchParams.dept" placeholder="部门" clearable @change="fetchJobs" style="width: 130px">
          <el-option v-for="d in deptOptions" :key="d" :label="d" :value="d" />
        </el-select>
        <el-select v-model="searchParams.location" placeholder="城市" clearable @change="fetchJobs" style="width: 110px">
          <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
        </el-select>
        <div class="salary-range">
          <el-input-number v-model="searchParams.salaryMin" :min="1" :max="100" placeholder="最低" controls-position="right" style="width:90px" />K
          <span class="range-sep">-</span>
          <el-input-number v-model="searchParams.salaryMax" :min="1" :max="100" placeholder="最高" controls-position="right" style="width:90px" />K
        </div>
        <el-select v-model="sortBy" style="width: 140px" @change="fetchJobs">
          <el-option label="🕐 最新发布" value="newest" />
          <el-option label="💰 薪资高→低" value="salary_desc" />
          <el-option label="💰 薪资低→高" value="salary_asc" />
        </el-select>
        <el-button type="primary" size="large" @click="fetchJobs"><el-icon><Search /></el-icon>搜索</el-button>
        <el-button size="large" @click="resetSearch">重置</el-button>
      </div>

      <!-- 热门搜索标签 -->
      <div class="hot-tags">
        <span class="hot-label">热门搜索：</span>
        <el-tag v-for="t in hotTags" :key="t" size="small" effect="plain" class="hot-tag" @click="searchParams.keyword=t;fetchJobs()">{{ t }}</el-tag>
      </div>
    </div>

    <!-- 快速分类 -->
    <div class="category-bar">
      <div class="category-chip" v-for="d in deptChips" :key="d.name"
        :class="{ active: searchParams.dept === d.name }"
        :style="{ '--chip-color': d.color }"
        @click="searchParams.dept = searchParams.dept === d.name ? '' : d.name; fetchJobs()">
        <span class="chip-icon">
          <component :is="deptIconComponents[d.name]" theme="outline" :size="28" :strokeWidth="3" fill="#22C5DE" />
        </span>
        <span class="chip-name">{{ d.name }}</span>
        <span class="chip-count">{{ d.count }}</span>
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="results-toolbar">
      <div class="results-info">
        <span v-if="!loading">共 <b>{{ total }}</b> 个岗位</span>
        <span v-if="searchParams.dept || searchParams.keyword || searchParams.location">
          · 筛选结果
          <el-tag v-if="searchParams.dept" size="small" closable @close="searchParams.dept='';fetchJobs()">{{ searchParams.dept }}</el-tag>
          <el-tag v-if="searchParams.location" size="small" closable @close="searchParams.location='';fetchJobs()">{{ searchParams.location }}</el-tag>
        </span>
      </div>
      <div class="view-toggle">
        <el-button :type="viewMode==='grid'?'primary':''" size="small" circle @click="viewMode='grid'"><el-icon><Grid /></el-icon></el-button>
        <el-button :type="viewMode==='list'?'primary':''" size="small" circle @click="viewMode='list'"><el-icon><List /></el-icon></el-button>
      </div>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="job-cards">
      <div v-for="n in 6" :key="'sk-'+n" class="job-card-skeleton">
        <el-skeleton animated>
          <template #template>
            <div class="sk-inner">
              <div class="sk-row"><el-skeleton-item variant="text" style="width:55%;height:22px" /><el-skeleton-item variant="text" style="width:18%;height:22px" /></div>
              <div class="sk-row" style="margin-top:10px"><el-skeleton-item variant="text" style="width:16%" /><el-skeleton-item variant="text" style="width:22%" /><el-skeleton-item variant="text" style="width:16%" /></div>
              <div style="margin-top:14px;display:flex;gap:10px"><el-skeleton-item variant="text" style="width:14%;height:22px" /><el-skeleton-item variant="text" style="width:14%;height:22px" /><el-skeleton-item variant="text" style="width:14%;height:22px" /></div>
              <div style="margin-top:14px"><el-skeleton-item variant="text" style="width:80%" /><el-skeleton-item variant="text" style="width:55%;margin-top:6px" /></div>
              <div class="sk-footer"><el-skeleton-item variant="button" style="width:80px;height:30px" /><el-skeleton-item variant="button" style="width:80px;height:30px" /></div>
            </div>
          </template>
        </el-skeleton>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else-if="jobs.length === 0" class="empty-state">
      <el-empty description="暂无匹配岗位">
        <el-button type="primary" @click="resetSearch">清空筛选</el-button>
      </el-empty>
    </div>

    <!-- 列表视图 -->
    <div v-else-if="viewMode === 'list'" class="job-list-view">
      <div v-for="(job, idx) in jobs" :key="job.jobId" class="list-item reveal-card"
        :style="{ '--item-delay': idx * 0.03 + 's' }"
        @click="goToDetail(job.jobId)">
        <div class="list-left">
          <div class="list-icon" :style="{background: deptColors[job.dept]||'#409EFF'}">
            <el-icon :size="20" color="#fff"><Briefcase /></el-icon>
          </div>
          <div class="list-main">
            <div class="list-title-row">
              <span class="list-title">{{ job.title }}</span>
              <el-tag v-if="job.deliveryCount && job.deliveryCount>5" type="danger" size="small" effect="dark">热招</el-tag>
            </div>
            <div class="list-meta">
              <span><el-icon><Location /></el-icon>{{ job.location }}</span>
              <span><el-icon><OfficeBuilding /></el-icon>{{ job.dept }}</span>
              <span v-if="job.headCount"><el-icon><User /></el-icon>{{ job.headCount }}人</span>
              <span><el-icon><Clock /></el-icon>{{ formatDate(job.createdAt) }}</span>
            </div>
          </div>
        </div>
        <div class="list-right">
          <div class="list-salary" v-if="job.salaryMin && job.salaryMax">
            <span class="ls-num">{{ job.salaryMin }}-{{ job.salaryMax }}K</span>
            <span class="ls-unit">/月</span>
          </div>
          <div class="list-salary" v-else><span class="ls-num" style="color:#909399">面议</span></div>
          <div class="list-actions">
            <el-button size="small" @click.stop="goToDetail(job.jobId)">查看</el-button>
            <el-button v-if="isLoggedIn && isCandidate" type="primary" size="small" @click.stop="goToSubmit(job.jobId)">投递</el-button>
          </div>
        </div>
      </div>
    </div>

    <!-- 网格视图 -->
    <div v-else class="job-cards">
      <el-card v-for="(job, idx) in jobs" :key="job.jobId" class="job-card reveal-card"
        shadow="hover"
        :style="{ '--card-delay': idx * 0.04 + 's', '--dept-color': deptColors[job.dept] || deptColors['技术部'] }"
        @click="goToDetail(job.jobId)">
        <div class="card-accent" />
        <div class="card-body">
          <!-- 卡片角标区域 -->
          <div class="card-corner">
            <span v-if="isNewJob(job.createdAt)" class="badge-new-3d">NEW</span>
            <span class="badge-glass-3d" :style="{ borderColor: deptColors[job.dept] || '#22C5DE' }">{{ job.dept }}</span>
          </div>

          <!-- 标题行 -->
          <div class="title-row">
            <h3>{{ job.title }}</h3>
            <el-tag v-if="job.deliveryCount && job.deliveryCount > 5" type="danger" size="small" effect="dark" round class="hot-tag">
              <el-icon><TrendCharts /></el-icon> 热招
            </el-tag>
          </div>

          <!-- 薪资区域 -->
          <div v-if="job.salaryMin && job.salaryMax" class="salary-bar-card">
            <span class="salary-num">{{ job.salaryMin }}-{{ job.salaryMax }}K</span>
            <span class="salary-unit">/月</span>
            <div class="salary-track"><div class="salary-fill" :style="{ width: Math.min((job.salaryMax || 0) / 80 * 100, 100) + '%' }" /></div>
          </div>
          <div v-else class="salary-bar-card"><span class="salary-num muted">薪资面议</span></div>

          <!-- 元信息 -->
          <div class="meta-row">
            <span class="meta-item"><el-icon><Location /></el-icon>{{ job.location }}</span>
            <span class="meta-item" v-if="job.headCount"><el-icon><User /></el-icon>{{ job.headCount }}人</span>
            <span class="meta-item"><el-icon><Clock /></el-icon>{{ formatDate(job.createdAt) }}</span>
          </div>

          <!-- 技能标签 -->
          <div class="skill-row" v-if="job.skills?.length">
            <span v-for="s in job.skills.slice(0, 8)" :key="s" class="skill-pill">{{ s }}</span>
          </div>

          <!-- 要求摘要 -->
          <div class="req-text">
            <el-text type="info" :line-clamp="2">{{ job.requirements || job.JD?.slice(0, 120) }}</el-text>
          </div>

          <!-- 底栏 -->
          <div class="card-footer">
            <span class="view-count" v-if="job.deliveryCount"><el-icon><View /></el-icon>{{ job.deliveryCount }}人已投递</span>
            <span v-else />
            <div class="footer-btns">
              <el-button size="small" class="btn-outline-sm" @click.stop="goToDetail(job.jobId)">查看详情</el-button>
              <el-button type="primary" size="small" class="btn-apply" @click.stop="goToSubmit(job.jobId)" v-if="isLoggedIn && isCandidate">立即投递</el-button>
            </div>
          </div>
        </div>
      </el-card>
    </div>

    <!-- 分页 -->
    <div class="pagination" v-if="total > 0">
      <el-pagination
        v-model:current-page="searchParams.page"
        v-model:page-size="searchParams.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50]"
        layout="total, sizes, prev, pager, next"
        @size-change="fetchJobs"
        @current-change="fetchJobs"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { useUserStore } from '@/stores/user'
import { Location, Clock, User, Search, View, TrendCharts, Grid, List, Briefcase, OfficeBuilding } from '@element-plus/icons-vue'
import { Cpu, Brain, ChartHistogram, ApplicationOne, Rocket, TrendTwo, Finance, Peoples } from '@icon-park/vue-next'
import dayjs from 'dayjs'

const router = useRouter()
const jobStore = useJobStore()
const userStore = useUserStore()

const loading = computed(() => jobStore.loading)
const jobs = computed(() => jobStore.jobs)
const total = computed(() => jobStore.total)
const isLoggedIn = computed(() => userStore.isLoggedIn)
const isCandidate = computed(() => userStore.isCandidate)

const sortBy = ref('newest')
const viewMode = ref<'grid' | 'list'>('grid')
const lastUpdated = ref('')

// ====== 动效：数字滚动 ======
const displayStats = reactive({ jobs: 0, depts: 0, avgSalary: '' })
const animateCounter = (target: number, key: 'jobs' | 'depts', duration = 1200) => {
  const start = performance.now()
  const from = 0
  const tick = (now: number) => {
    const elapsed = now - start
    const progress = Math.min(elapsed / duration, 1)
    const eased = 1 - Math.pow(1 - progress, 3) // ease-out cubic
    const current = Math.round(from + (target - from) * eased)
    ;(displayStats as any)[key] = current
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

// ====== 动效：滚动入场 (Intersection Observer) ======
let observer: IntersectionObserver | null = null
const observeCards = () => {
  if (observer) observer.disconnect()
  observer = new IntersectionObserver((entries) => {
    entries.forEach(e => { if (e.isIntersecting) (e.target as HTMLElement).classList.add('visible') })
  }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' })
  nextTick(() => {
    document.querySelectorAll('.job-card, .list-item').forEach(el => observer!.observe(el))
  })
}

const deptOptions = ['技术部', 'AI部', '数据部', '产品部', '运营部', '市场部', '财务部', '人力资源部']
const cityOptions = ['北京', '上海', '广州', '深圳', '杭州', '成都', '武汉', '南京', '西安', '苏州']

const heroStats = reactive([
  { label: '活跃岗位', value: 0 },
  { label: '技术部门', value: 0 },
  { label: '覆盖城市', value: 10 },
  { label: '平均薪资', value: '' },
])

const deptColors: Record<string, string> = {
  '技术部': '#22C5DE', 'AI部': '#9B59B6', '数据部': '#22C5DE', '产品部': '#22C5DE',
  '运营部': '#22C5DE', '市场部': '#22C5DE', '财务部': '#22C5DE', '人力资源部': '#22C5DE',
}

// 部门图标映射 (IconPark 组件)
const deptIconComponents: Record<string, any> = {
  '技术部': Cpu, 'AI部': Brain, '数据部': ChartHistogram,
  '产品部': ApplicationOne, '运营部': Rocket, '市场部': TrendTwo,
  '财务部': Finance, '人力资源部': Peoples,
}

const deptChips = reactive([
  { name: '技术部', color: '#22C5DE', count: 0 },
  { name: 'AI部', color: '#9B59B6', count: 0 },
  { name: '数据部', color: '#22C5DE', count: 0 },
  { name: '产品部', color: '#22C5DE', count: 0 },
  { name: '运营部', color: '#22C5DE', count: 0 },
  { name: '市场部', color: '#22C5DE', count: 0 },
  { name: '财务部', color: '#22C5DE', count: 0 },
  { name: '人力资源部', color: '#22C5DE', count: 0 },
])

const hotTags = ['Java', 'AI工程师', 'Python', '前端', '数据分析', '产品经理', 'DevOps', '大模型']

const searchParams = reactive({
  page: 1, pageSize: 12, keyword: '', dept: '', location: '',
  salaryMin: undefined as number | undefined,
  salaryMax: undefined as number | undefined,
  status: 1,
})

onMounted(() => { fetchJobs(); loadStats() })
onUnmounted(() => { if (observer) observer.disconnect() })

const fetchJobs = () => {
  let backendSortBy: string | undefined
  let backendSortOrder: string | undefined
  switch (sortBy.value) {
    case 'newest':     backendSortBy = 'created_at'; backendSortOrder = 'desc'; break
    case 'salary_desc': backendSortBy = 'salary';    backendSortOrder = 'desc'; break
    case 'salary_asc':  backendSortBy = 'salary';    backendSortOrder = 'asc';  break
  }
  const params: any = { ...searchParams, sortBy: backendSortBy, sortOrder: backendSortOrder }
  jobStore.fetchJobs(params)
  lastUpdated.value = dayjs().format('MM-DD HH:mm')
}

// 当数据变化时重新 observe 卡片
watch([jobs, viewMode], () => { if (!loading.value) observeCards() })

const loadStats = async () => {
  try {
    const { getJobList } = await import('@/api/job')
    // 获取总数用于 hero
    const res = await getJobList({ page: 1, pageSize: 1, status: 1 })
    const data = (res as any)?.data || res
    const t = data?.total || 0
    heroStats[0].value = t
    if (t > 0) { heroStats[3].value = '21-44K'; animateCounter(t, 'jobs', 1500); animateCounter(8, 'depts', 1000) }

    // 各部门数量
    for (const chip of deptChips) {
      try {
        const dr = await getJobList({ page: 1, pageSize: 1, dept: chip.name, status: 1 })
        const dd = (dr as any)?.data || dr
        chip.count = dd?.total || 0
      } catch { chip.count = 0 }
    }
  } catch {}
}

const resetSearch = () => {
  searchParams.keyword = ''; searchParams.dept = ''; searchParams.location = ''
  searchParams.salaryMin = undefined; searchParams.salaryMax = undefined
  searchParams.page = 1; sortBy.value = 'newest'
  fetchJobs()
}

const salaryLabel = (max: number) => {
  if (max >= 70) return '高薪'
  if (max >= 40) return '优厚'
  if (max >= 20) return '良好'
  return ''
}

const goToDetail = (id: number) => { router.push(`/jobs/${id}`) }
const goToSubmit = (jobId: number) => { router.push(`/resume/submit/${jobId}`) }
const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD')
const isNewJob = (date: string) => dayjs().diff(dayjs(date), 'day') <= 3
</script>

<style scoped lang="scss">
// ====== 页面容器 ======
.job-list-page {
  max-width: 1260px; margin: 0 auto; padding: 0 20px 40px; position: relative;
}

// ====== Hero 横幅 ======
.hero-banner {
  position: relative; overflow: hidden;
  background: linear-gradient(135deg, #09090B 0%, #13131E 35%, #1A1A2E 65%, #0D0D12 100%);
  border-radius: 20px; margin: 20px 0 24px; padding: 48px 56px;
  .hero-content {
    position: relative; z-index: 2;
    display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 24px;
  }
  .hero-text {
    h1 {
      font-size: 36px; font-weight: 800; color: #fff; margin: 0 0 12px;
      // gradient-text now uses global design tokens
    }
    p { color: rgba(255,255,255,.7); font-size: 16px; margin: 0; }
    .update-badge {
      display: inline-block; margin-left: 12px; padding: 2px 10px;
      background: rgba(255,255,255,.15); border-radius: 12px; font-size: 12px; color: rgba(255,255,255,.8);
    }
  }
  .hero-stats { display: flex; gap: 32px; flex-shrink: 0; }
  .hero-stat {
    text-align: center;
      .hs-num { font-size: 32px; font-weight: 700; color: var(--color-primary); }
    .hs-label { font-size: 13px; color: var(--color-text-secondary); margin-top: 4px; }
  }
  .hero-decor {
    .decor-circle { position: absolute; border-radius: 50%; opacity: .08;
      &.c1 { width: 300px; height: 300px; background: var(--color-primary); top: -80px; right: -60px; }
      &.c2 { width: 200px; height: 200px; background: var(--color-success); bottom: -40px; left: 10%; }
      &.c3 { width: 150px; height: 150px; background: var(--color-accent); top: 20px; left: 40%; }
    }
  }
}

// ====== 搜索栏 ======
.search-bar {
  background: rgba(19, 19, 22, 0.85);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid var(--color-border);
  box-shadow: var(--shadow-lg);
  border-radius: 16px; padding: 20px 24px 16px; margin-bottom: 16px;
  position: sticky; top: 12px; z-index: 10;

  .search-row {
    display: flex; gap: 10px; align-items: center; flex-wrap: wrap;
    .search-input { width: 240px; }
    .salary-range { display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--color-text-secondary);
      .range-sep { margin: 0 2px; }
    }
  }

  .hot-tags {
    display: flex; align-items: center; gap: 8px; margin-top: 12px; flex-wrap: wrap;
    .hot-label { font-size: 12px; color: var(--color-text-muted); flex-shrink: 0; }
    .hot-tag { cursor: pointer; transition: all .2s;
      &:hover { transform: translateY(-1px); color: var(--color-primary-hover); border-color: var(--color-primary-hover); }
    }
  }
}

// ====== 快速分类 ======
.category-bar {
  display: flex; gap: 10px; margin-bottom: 20px; overflow-x: auto; padding: 4px 0;
  &::-webkit-scrollbar { height: 0; }
  .category-chip {
    display: flex; align-items: center; gap: 8px; padding: 10px 18px;
    border-radius: 12px; background: var(--color-surface); border: 2px solid var(--color-border);
    cursor: pointer; transition: all .25s; white-space: nowrap;

    .chip-icon {
      display: flex; align-items: center; justify-content: center;
      width: 32px; height: 32px;
      filter: drop-shadow(0 0 6px rgba(76, 201, 240, 0.3));
      animation: iconBreathe 3s ease-in-out infinite;
      transition: filter 0.3s;
    }

    .chip-name { font-size: 14px; font-weight: 500; color: var(--color-text); }
    .chip-count { font-size: 12px; color: var(--color-text-muted); background: var(--color-bg-alt); padding: 2px 8px; border-radius: 10px; }

    &:hover {
      border-color: var(--chip-color, #22C5DE);
      transform: translateY(-2px);
      box-shadow: 0 4px 16px rgba(76, 201, 240, 0.15);

      .chip-icon {
        filter: drop-shadow(0 0 12px rgba(76, 201, 240, 0.5));
      }
    }

    &.active {
      background: var(--chip-color, #22C5DE); border-color: var(--chip-color, #22C5DE);
      .chip-name, .chip-count { color: #fff !important; }
      .chip-icon { filter: drop-shadow(0 0 10px rgba(255, 255, 255, 0.5)); }
    }
  }

  @keyframes iconBreathe {
    0%, 100% { filter: drop-shadow(0 0 4px rgba(76, 201, 240, 0.2)); }
    50%      { filter: drop-shadow(0 0 10px rgba(76, 201, 240, 0.45)); }
  }
}

// ====== 结果工具栏 ======
.results-toolbar {
  display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;
  .results-info { font-size: 14px; color: var(--color-text-secondary);
    b { color: var(--color-text); }
  }
  .view-toggle { display: flex; gap: 6px; }
}

// ====== 空状态 ======
.empty-state { padding: 60px 0; }

// ====== 骨架屏 ======
.job-card-skeleton {
  background: var(--color-surface); border-radius: 14px; border: 1px solid var(--color-border); overflow: hidden;
  .sk-inner { padding: 22px; }
  .sk-row { display: flex; gap: 16px; align-items: center; }
  .sk-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 16px; padding-top: 14px; border-top: 1px solid var(--color-border); }
}

// ====== 网格卡片 ======
.job-cards {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(370px, 1fr)); gap: 18px;
  @media (max-width: 768px) { grid-template-columns: 1fr; }
}

.job-card {
  cursor: pointer; transition: transform .3s ease, box-shadow .3s ease, border-color .3s ease; border-radius: 14px; overflow: hidden;
  position: relative; border: 1px solid var(--color-border);
  animation: cardEnter .45s ease-out both; animation-delay: var(--card-delay, 0s);
  :deep(.el-card__body) { padding: 0; }
  .card-accent { position: absolute; top: 0; left: 0; right: 0; height: 3px; background: var(--gradient-primary); opacity: 0; transition: opacity .3s; }
  &:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 30px rgba(99, 102, 241, 0.12), 0 0 0 1px rgba(99, 102, 241, 0.2);
    border-color: var(--color-border-glow);
    .card-accent { opacity: 1; }
  }
  .card-body { padding: 20px 22px; }
}

// 卡片右上角区域
.card-corner {
  position: absolute; top: 16px; right: 16px; display: flex; gap: 8px; align-items: center; z-index: 2;
}

.title-row { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px; gap: 10px; padding-right: 80px; min-height: 24px;
  h3 { font-size: 17px; font-weight: 600; color: var(--color-text); line-height: 1.4; margin: 0; flex: 1; }
  .hot-tag { flex-shrink: 0; }
}

// 薪资区域
.salary-bar-card {
  background: rgba(99, 102, 241, 0.05); border-radius: 10px; padding: 12px 16px; border: 1px solid var(--color-border);
  display: flex; align-items: center; gap: 16px; margin-bottom: 10px;
  .salary-num {
    font-size: 22px; font-weight: 700; font-family: var(--font-mono); font-variant-numeric: tabular-nums;
    background: linear-gradient(135deg, #22C5DE, #6366f1); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;
    filter: drop-shadow(0 0 4px rgba(76, 201, 240, 0.3));
    flex-shrink: 0;
    &.muted { color: var(--color-text-muted); -webkit-text-fill-color: var(--color-text-muted); filter: none; background: none; }
  }
  .salary-unit { font-size: 12px; color: var(--color-text-muted); margin-left: -10px; flex-shrink: 0; }
  .salary-track { flex: 1; height: 6px; background: var(--color-bg-alt); border-radius: 3px; overflow: hidden;
    .salary-fill { height: 100%; border-radius: 3px; background: linear-gradient(90deg, #22C5DE, #6366f1); transition: width 1s cubic-bezier(.4,0,.2,1); box-shadow: 0 0 6px rgba(76, 201, 240, 0.3); }
  }
}

.meta-row { display: flex; gap: 16px; margin-bottom: 8px;
  .meta-item { display: flex; align-items: center; gap: 4px; font-size: 13px; color: var(--color-text-secondary); }
}

// 技能胶囊
.skill-row { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
.skill-pill {
  display: inline-block; padding: 2px 10px; border-radius: 20px; font-size: 12px; font-weight: 500;
  color: var(--color-text-secondary); background: var(--color-bg-alt); border: 1px solid var(--color-border);
  transition: all .2s; cursor: default;
  &:hover {
    color: var(--color-primary-hover); border-color: var(--color-primary);
    box-shadow: 0 0 8px rgba(99, 102, 241, 0.2); background: rgba(99, 102, 241, 0.08);
  }
}

.req-text {
  margin-bottom: 12px;
  :deep(.el-text) { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
}

// 底栏
.card-footer {
  display: flex; justify-content: space-between; align-items: center;
  padding-top: 10px; border-top: 1px solid var(--color-border);
  .view-count { font-size: 12px; color: var(--color-text-muted); display: flex; align-items: center; gap: 4px; }
  .footer-btns { display: flex; gap: 8px; }
}

// 底部按钮样式
.btn-outline-sm {
  border: 1px solid var(--color-border); color: var(--color-text-secondary); background: transparent;
  transition: all .2s;
  &:hover { border-color: var(--color-border-glow); color: var(--color-primary-hover); }
}

.btn-apply {
  background: var(--gradient-primary); border: none; color: #fff;
  animation: applyPulse 2s ease-in-out infinite; box-shadow: var(--glow-primary);
  transition: all .2s;
  &:hover { box-shadow: var(--glow-primary-lg); transform: translateY(-1px); }
}

@keyframes applyPulse {
  0%, 100% { box-shadow: 0 0 8px rgba(99, 102, 241, 0.3); }
  50%      { box-shadow: 0 0 20px rgba(99, 102, 241, 0.5); }
}

// ====== 列表视图 ======
.job-list-view { display: flex; flex-direction: column; gap: 10px; }
.list-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px; background: var(--color-surface); border-radius: 12px; border: 1px solid var(--color-border);
  cursor: pointer; transition: all .25s;
  animation: cardEnter .35s ease-out both; animation-delay: var(--item-delay, 0s);
  &:hover { border-color: var(--color-border-glow); box-shadow: var(--shadow-md); transform: translateX(4px); }
  .list-left { display: flex; gap: 16px; align-items: center; flex: 1; }
  .list-icon { width: 44px; height: 44px; border-radius: 10px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
  .list-main { min-width: 0; }
  .list-title-row { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
  .list-title { font-size: 16px; font-weight: 600; color: var(--color-text); }
  .list-meta { display: flex; gap: 16px; font-size: 13px; color: var(--color-text-secondary);
    span { display: flex; align-items: center; gap: 4px; }
  }
  .list-right { display: flex; align-items: center; gap: 16px; flex-shrink: 0; }
  .list-salary { text-align: right;
    .ls-num { font-size: 20px; font-weight: 700; color: var(--color-accent); }
    .ls-unit { font-size: 12px; color: var(--color-text-muted); }
  }
  .list-actions { display: flex; gap: 8px; }
}

// ====== 分页 ======
.pagination { margin-top: 28px; display: flex; justify-content: center; }

@keyframes cardEnter { from { opacity: 0; transform: translateY(24px); } to { opacity: 1; transform: translateY(0); } }

// ====== 滚动入场动画 ======
.reveal-card {
  opacity: 0; transform: translateY(30px); transition: opacity .5s ease, transform .5s ease;
  &.visible { opacity: 1; transform: translateY(0); }
}


// ====== Hero 统计数字跳动 ======
.hs-num {
  transition: all .3s ease;
}

// ====== 响应式 ======
@media (max-width: 768px) {
  .hero-banner { padding: 32px 24px; border-radius: 14px;
    .hero-stats { gap: 16px; }
    .hero-text h1 { font-size: 24px; }
    .hero-stat .hs-num { font-size: 24px; }
  }
  .search-bar {
    position: static;
    .search-row { flex-direction: column; align-items: stretch;
      .search-input, .el-select, .salary-range { width: 100% !important; }
    }
  }
  .category-bar { flex-wrap: nowrap; overflow-x: auto; }
  .job-cards { grid-template-columns: 1fr; }
  .list-item { flex-direction: column; align-items: flex-start; gap: 12px;
    .list-right { width: 100%; justify-content: space-between; }
  }
}
</style>
