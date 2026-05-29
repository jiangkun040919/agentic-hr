<template>
  <div class="job-list-page">
    <!-- Hero -->
    <div class="hero">
      <div class="hero-decor" />
      <h1 class="hero-title">发现你的<span class="gradient-text">理想岗位</span></h1>
      <p class="hero-sub">AI 驱动的智能招聘平台 &middot; <span class="hero-count">{{ total }}</span> 个岗位开放中</p>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <div class="search-capsule">
        <el-icon class="search-icon"><Search /></el-icon>
        <input v-model="searchKeyword" placeholder="搜索岗位、技能..." class="search-input" @keyup.enter="handleSearch" />
        <select v-model="searchDept" class="search-select" @change="handleSearch">
          <option value="">全部部门</option>
          <option v-for="d in deptOptions" :key="d" :value="d">{{ d }}</option>
        </select>
        <select v-model="searchCity" class="search-select search-select--sm" @change="handleSearch">
          <option value="">全部城市</option>
          <option v-for="c in cityOptions" :key="c" :value="c">{{ c }}</option>
        </select>
        <button class="search-btn" @click="handleSearch">搜索</button>
      </div>
    </div>

    <!-- 部门快捷入口 -->
    <div class="dept-chips">
      <VChip
        v-for="d in deptChips"
        :key="d.name"
        :emoji="d.emoji"
        :count="d.count"
        :color="d.color"
        :active="searchDept === d.name"
        @click="searchDept = searchDept === d.name ? '' : d.name; handleSearch()"
      >
        {{ d.name }}
      </VChip>
    </div>

    <!-- 加载骨架 -->
    <div v-if="loading" class="job-grid">
      <div v-for="n in 6" :key="n" class="skeleton-card">
        <VSkeleton variant="card" />
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else-if="jobs.length === 0" class="empty-state">
      <VEmpty title="暂无匹配岗位" description="试试调整搜索条件，或者浏览其他部门的岗位" emoji="🔍" />
    </div>

    <!-- 岗位卡片网格 -->
    <div v-else class="job-grid">
      <div
        v-for="job in jobs"
        :key="job.jobId"
        class="job-card"
        @click="$router.push(`/jobs/${job.jobId}`)"
      >
        <div class="card-accent" :style="{ background: deptGradients[job.dept] || 'var(--gradient-primary)' }" />
        <div class="card-content">
          <div class="card-header">
            <h3 class="card-title">{{ job.title }}</h3>
            <div class="card-match" v-if="job.matchRate">
              <div class="match-ring" :style="{ '--pct': job.matchRate }">
                <span class="match-num">{{ job.matchRate }}</span>
                <span class="match-unit">%</span>
              </div>
            </div>
          </div>

          <div class="card-tags">
            <VTag :color="deptTagColors[job.dept] || 'coral'" size="sm">{{ job.dept }}</VTag>
            <VTag color="gray" size="sm">{{ job.location }}</VTag>
            <VTag v-if="job.headCount" color="sunny" size="sm">招{{ job.headCount }}人</VTag>
            <span class="tag-time">{{ timeAgo(job.createdAt) }}</span>
          </div>

          <div class="card-salary" v-if="job.salaryMin && job.salaryMax">
            <span class="salary-num">{{ job.salaryMin }}-{{ job.salaryMax }}K</span>
            <span class="salary-unit">/月</span>
            <span v-if="job.salaryMin >= 30" class="salary-hot">高薪</span>
          </div>
          <div class="card-salary" v-else>
            <span class="salary-num salary-num--muted">面议</span>
          </div>

          <div class="card-jd" v-if="job.jd">{{ truncate(job.jd, 100) }}</div>

          <div class="card-skills" v-if="job.matched?.length">
            <VTag v-for="s in job.matched.slice(0, 4)" :key="s" color="mint" size="sm">{{ s }}</VTag>
          </div>
          <div class="card-skills" v-else-if="job.skills?.length">
            <VTag v-for="s in job.skills.slice(0, 4)" :key="s" color="sky" size="sm">{{ s }}</VTag>
          </div>
        </div>

        <div class="card-footer">
          <VBtn variant="ghost" color="gray" size="sm" @click.stop="$router.push(`/jobs/${job.jobId}`)">查看详情</VBtn>
          <VBtn v-if="isCandidate" variant="filled" color="coral" size="sm" @click.stop="$router.push(`/resume/submit/${job.jobId}`)">投递</VBtn>
        </div>
      </div>
    </div>

    <!-- 分页 -->
    <div class="pagination" v-if="total > pageSize">
      <button class="page-btn" :disabled="page <= 1" @click="page--; handleSearch()">&lsaquo;</button>
      <button v-for="p in totalPages" :key="p" class="page-btn" :class="{ active: p === page }" @click="page = p; handleSearch()">{{ p }}</button>
      <button class="page-btn" :disabled="page >= totalPages" @click="page++; handleSearch()">&rsaquo;</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Search } from '@element-plus/icons-vue'
import { request } from '@/utils/request'
import { useUserStore } from '@/stores/user'
import VChip from '@/components/ui/VChip.vue'
import VTag from '@/components/ui/VTag.vue'
import VBtn from '@/components/ui/VBtn.vue'
import VEmpty from '@/components/ui/VEmpty.vue'
import VSkeleton from '@/components/ui/VSkeleton.vue'

const userStore = useUserStore()
const isCandidate = ref(userStore.isCandidate)

const loading = ref(false)
const jobs = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(12)

const searchKeyword = ref('')
const searchDept = ref('')
const searchCity = ref('')

const deptOptions = ['技术部', 'AI部', '前端部', '数据部', '产品部', '架构部', '运维部', '安全部', '设计部']
const cityOptions = ['北京', '上海', '广州', '深圳', '杭州', '成都', '武汉', '南京', '西安', '苏州']

const deptGradients: Record<string, string> = {
  '技术部': 'linear-gradient(135deg, var(--color-primary), #E85555)',
  'AI部': 'linear-gradient(135deg, #8B9A6E, #9333EA)',
  '前端部': 'linear-gradient(135deg, #8A9BA8, #6B7B8D)',
  '数据部': 'linear-gradient(135deg, #7A8B5E, #6B8B4E)',
  '产品部': 'linear-gradient(135deg, var(--color-primary), #B08040)',
  '架构部': 'linear-gradient(135deg, #C08070, #C08070)',
  '运维部': 'linear-gradient(135deg, #8A9BA8, #7A8B5E)',
  '安全部': 'linear-gradient(135deg, var(--color-primary), #8B9A6E)',
  '设计部': 'linear-gradient(135deg, #C08070, var(--color-primary))',
}

const deptTagColors: Record<string, string> = {
  '技术部': 'coral', 'AI部': 'purple', '前端部': 'sky', '数据部': 'mint',
  '产品部': 'sunny', '架构部': 'coral', '运维部': 'sky', '安全部': 'coral', '设计部': 'sunny',
}

const deptChips = ref([
  { name: '技术部', emoji: '💻', count: 0, color: 'coral' as const },
  { name: 'AI部', emoji: '🤖', count: 0, color: 'purple' as const },
  { name: '前端部', emoji: '🎨', count: 0, color: 'sky' as const },
  { name: '数据部', emoji: '📊', count: 0, color: 'mint' as const },
  { name: '产品部', emoji: '📱', count: 0, color: 'sunny' as const },
  { name: '架构部', emoji: '🏗️', count: 0, color: 'coral' as const },
])

const totalPages = computed(() => Math.ceil(total.value / pageSize.value))

const handleSearch = async () => {
  loading.value = true
  try {
    const params: any = { page: page.value, pageSize: pageSize.value }
    if (searchKeyword.value) params.keyword = searchKeyword.value
    if (searchDept.value) params.dept = searchDept.value
    if (searchCity.value) params.location = searchCity.value
    const res: any = await request.get('/job/list', { params })
    jobs.value = res?.items || res?.data?.items || []
    total.value = res?.total || res?.data?.total || 0
  } finally { loading.value = false }
}

const timeAgo = (dateStr: string) => {
  if (!dateStr) return ''
  const diff = Date.now() - new Date(dateStr).getTime()
  const days = Math.floor(diff / 86400000)
  if (days < 1) return '今天'
  if (days < 2) return '昨天'
  if (days < 7) return `${days}天前`
  return `${Math.floor(days / 7)}周前`
}

const truncate = (text: string, max: number) => text.length > max ? text.slice(0, max) + '...' : text

const fetchDeptStats = async () => {
  try {
    const res: any = await request.get('/job/dept-stats')
    const stats = res?.data || res || []
    const map: Record<string, number> = {}
    stats.forEach((s: any) => { map[s.dept] = s.count })
    deptChips.value.forEach(d => { d.count = map[d.name] || 0 })
  } catch {}
}

onMounted(() => { handleSearch(); fetchDeptStats() })
</script>

<style scoped lang="scss">
.job-list-page { max-width: 1100px; margin: 0 auto; padding: 0 16px 40px; }

// Hero
.hero {
  text-align: center; padding: 48px 0 36px; position: relative;
}
.hero-decor {
  position: absolute; top: 0; left: 50%; transform: translateX(-50%);
  width: 300px; height: 300px; border-radius: 50%;
  background: radial-gradient(circle, rgba(196,169,106,0.08) 0%, transparent 70%);
  pointer-events: none;
}
.hero-title { font-size: 32px; font-weight: 800; color: var(--color-text); margin: 0 0 10px; position: relative; }
.hero-sub { font-size: 15px; color: var(--color-text-secondary); margin: 0; position: relative; }
.hero-count { font-weight: 800; color: var(--color-primary); font-size: 18px; }

// 搜索胶囊
.search-bar { margin-bottom: 20px; }
.search-capsule {
  display: flex; align-items: center; gap: 0;
  background: var(--color-surface); border: 1.5px solid var(--color-border);
  border-radius: var(--radius-full); padding: 6px 6px 6px 18px;
  max-width: 700px; margin: 0 auto;
  transition: border-color 0.2s, box-shadow 0.2s;
  &:focus-within { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.1); }
}
.search-icon { color: var(--color-text-muted); font-size: 18px; flex-shrink: 0; }
.search-input {
  flex: 1; border: none; outline: none; background: transparent;
  font-size: 15px; color: var(--color-text); font-family: var(--font-sans);
  padding: 8px 12px; min-width: 0;
  &::placeholder { color: var(--color-text-muted); }
}
.search-select {
  border: none; outline: none; background: var(--color-bg-alt);
  color: var(--color-text-secondary); font-size: 13px;
  padding: 6px 10px; border-radius: var(--radius-full); cursor: pointer;
  font-family: var(--font-sans);
  &--sm { max-width: 90px; }
}
.search-btn {
  padding: 8px 24px; border-radius: var(--radius-full); border: none;
  background: var(--gradient-primary); color: #fff;
  font-size: 14px; font-weight: 600; cursor: pointer;
  font-family: var(--font-sans);
  transition: all 0.2s var(--ease-bounce);
  &:hover { box-shadow: 0 4px 16px rgba(196,169,106,0.3); transform: scale(1.02); }
}

// 部门 chip
.dept-chips {
  display: flex; gap: 8px; justify-content: center; flex-wrap: wrap; margin-bottom: 28px;
}

// 岗位网格
.job-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 16px;
  @media (max-width: 768px) { grid-template-columns: 1fr; }
}

.skeleton-card { background: var(--color-surface); border-radius: var(--radius-lg); overflow: hidden; }

.job-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-lg); overflow: hidden; cursor: pointer;
  transition: all 0.25s var(--ease-bounce);
  display: flex; flex-direction: column;
  &:hover { transform: translateY(-4px); box-shadow: 0 8px 24px rgba(0,0,0,0.08), 0 0 0 1px var(--color-border-glow); border-color: var(--color-border-glow); }
}

.card-accent { height: 4px; }

.card-content { padding: 20px 20px 14px; flex: 1; }

.card-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px; }
.card-title { font-size: 16px; font-weight: 700; margin: 0; line-height: 1.4; }

.match-ring {
  width: 48px; height: 48px; border-radius: 50%; position: relative;
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  background: conic-gradient(#7A8B5E calc(var(--pct) * 1%), var(--color-bg-alt) 0);
  &::before { content: ''; position: absolute; inset: 4px; border-radius: 50%; background: var(--color-surface); }
}
.match-num { font-size: 16px; font-weight: 800; color: #7A8B5E; position: relative; z-index: 1; line-height: 1; }
.match-unit { font-size: 9px; color: var(--color-text-muted); position: relative; z-index: 1; }

.card-tags { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; margin-bottom: 10px; }
.tag-time { font-size: 11px; color: var(--color-text-muted); }

.card-salary { display: flex; align-items: baseline; gap: 4px; margin: 8px 0; }
.salary-num { font-size: 22px; font-weight: 800; color: var(--color-primary); &--muted { color: var(--color-text-muted); font-size: 16px; } }
.salary-unit { font-size: 12px; color: var(--color-text-muted); }
.salary-hot {
  font-size: 10px; padding: 2px 8px; border-radius: var(--radius-full);
  background: var(--gradient-warm); color: #fff; font-weight: 700;
}

.card-jd { font-size: 13px; color: var(--color-text-secondary); line-height: 1.6; margin: 8px 0; }

.card-skills { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 10px; }

.card-footer {
  display: flex; gap: 8px; justify-content: flex-end;
  padding: 12px 20px; border-top: 1px solid var(--color-border);
  background: var(--color-bg);
}

// 分页
.pagination { display: flex; justify-content: center; gap: 6px; margin-top: 28px; }
.page-btn {
  width: 36px; height: 36px; border-radius: 50%; border: 1.5px solid var(--color-border);
  background: var(--color-surface); color: var(--color-text-secondary);
  font-size: 14px; font-weight: 600; cursor: pointer;
  display: flex; align-items: center; justify-content: center;
  transition: all 0.2s var(--ease-bounce); font-family: var(--font-sans);
  &:hover:not(:disabled) { border-color: var(--color-primary); color: var(--color-primary); }
  &.active { background: var(--gradient-primary); color: #fff; border-color: transparent; box-shadow: 0 2px 8px rgba(196,169,106,0.25); }
  &:disabled { opacity: 0.4; cursor: not-allowed; }
}
</style>
