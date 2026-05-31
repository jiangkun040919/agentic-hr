<template>
  <div class="job-list-page">
    <!-- Hero -->
    <div class="hero">
      <div class="hero-left">
        <div class="hero-decor" />
        <h1 class="hero-title">发现你的<span class="gradient-text">理想岗位</span></h1>
        <p class="hero-sub">AI 驱动的智能招聘平台 &middot; <span class="hero-count">{{ total }}</span> 个岗位开放中</p>
        <div class="hero-badge">● 数据更新于 {{ updateTime }}</div>
      </div>
      <div class="hero-right">
        <div class="hero-stat">
          <div class="hs-num">{{ displayStats.jobs ?? heroStats[0].value }}</div>
          <div class="hs-label">{{ heroStats[0].label }}</div>
        </div>
        <div class="hero-stat">
          <div class="hs-num">{{ displayStats.depts ?? heroStats[1].value }}</div>
          <div class="hs-label">{{ heroStats[1].label }}</div>
        </div>
        <div class="hero-stat">
          <div class="hs-num">{{ displayStats.cities ?? heroStats[2].value }}</div>
          <div class="hs-label">{{ heroStats[2].label }}</div>
        </div>
        <div class="hero-stat">
          <div class="hs-num">{{ heroStats[3].value }}</div>
          <div class="hs-label">{{ heroStats[3].label }}</div>
        </div>
      </div>
    </div>

    <!-- 搜索 & 热门卡片 -->
    <div class="search-card">
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

      <!-- 热门搜索 -->
      <div class="hot-search">
        <span class="hot-label">🔥 热门搜索</span>
        <button
          v-for="kw in hotKeywords"
          :key="kw"
          class="hot-tag"
          @click="searchKeyword = kw; handleSearch()"
        >{{ kw }}</button>
      </div>
    </div>

    <!-- 部门快捷入口 -->
    <div ref="deptChipsRef" class="dept-chips" @mousedown="onDeptChipsMouseDown">
      <VChip
        v-for="d in deptChips"
        :key="d.name"
        :emoji="d.emoji"
        :count="d.count"
        :color="d.color || 'gray'"
        :custom-color="d.customColor"
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
            <div class="card-clay-icon" :style="clayIconStyle(job.dept)">
              <span class="card-clay-emoji">{{ deptEmoji[job.dept] || '💼' }}</span>
            </div>
            <div class="card-header-text">
              <h3 class="card-title">{{ job.title }}</h3>
            </div>
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
import { ref, reactive, computed, onMounted } from 'vue'
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
const updateTime = ref(new Date().toLocaleDateString('zh-CN'))

// Hero 统计数据
const heroStats = reactive([
  { label: '总岗位', value: 0, key: 'jobs' },
  { label: '技术部门', value: 0, key: 'depts' },
  { label: '热门城市', value: 0, key: 'cities' },
  { label: '薪资范围', value: '21-44K', key: 'salary' },
])
const displayStats = reactive({ jobs: 0, depts: 0, cities: 0 })

const hotKeywords = ['Java', 'Python', '前端', 'AI算法', '数据分析', '产品经理']
const jobs = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(12)

const searchKeyword = ref('')
const searchDept = ref('')
const searchCity = ref('')

const deptOptions = ref<string[]>([])
const cityOptions = ['北京', '上海', '广州', '深圳', '杭州', '成都', '武汉', '南京', '西安', '苏州']

// 部门渐变 & 标签色 & emoji（动态从 API 填充）
const deptGradients = reactive<Record<string, string>>({})
const deptTagColors = reactive<Record<string, string>>({})
const deptEmoji = reactive<Record<string, string>>({})

const TAG_COLORS = ['coral','mint','sky','sunny','purple','gray'] as const

// 部门 emoji 配色库 (已知部门直接用，未知部门自动生成)
const deptPresets: Record<string, { emoji: string; color: string }> = {
  '技术部': { emoji: '💻', color: '#C4A96A' },
  'AI部': { emoji: '🤖', color: '#8B9A6E' },
  '前端部': { emoji: '🎨', color: '#8A9BA8' },
  '数据部': { emoji: '📊', color: '#7A8B5E' },
  '产品部': { emoji: '📱', color: '#C4945A' },
  '架构部': { emoji: '🏗️', color: '#B8A878' },
  '运维部': { emoji: '⚙️', color: '#A09888' },
  '安全部': { emoji: '🛡️', color: '#B8605A' },
  '质量部': { emoji: '✅', color: '#6B8B4E' },
  '移动部': { emoji: '📲', color: '#B8A878' },
  '云平台部': { emoji: '☁️', color: '#9AABB8' },
  '数据平台部': { emoji: '🗄️', color: '#5A7040' },
  '设计部': { emoji: '✨', color: '#C08070' },
}

const FALLBACK_EMOJIS = ['📌', '🔖', '🏷️', '📋', '📎', '💼', '🎯', '🚀', '⭐', '💡', '🔔', '📢', '🏢']
const PRESET_COLORS = ['#C4A96A','#8B9A6E','#8A9BA8','#7A8B5E','#C4945A','#B8A878','#A09888','#B8605A','#6B8B4E','#9AABB8','#5A7040','#C08070']

// 动态部门 chips（从 API 填充）
const deptChips = ref<Array<{ name: string; emoji: string; count: number; color: any; customColor: string }>>([])

let _colorIdx = 0
function getAutoColor(name: string): string {
  // 用名称 hash 选颜色
  let hash = 0
  for (let i = 0; i < name.length; i++) hash = ((hash << 5) - hash) + name.charCodeAt(i)
  return PRESET_COLORS[Math.abs(hash) % PRESET_COLORS.length]
}

// 黏土图标颜色辅助
function clayIconStyle(dept: string) {
  const color = deptGradients[dept] ? extractGradientMid(deptGradients[dept]) : '#C4A96A'
  const light = lightenColor(color, 35)
  const shadow = darkenColor(color, 35)
  return { '--clay-color': color, '--clay-light': light, '--clay-shadow': shadow }
}

function extractGradientMid(grad: string): string {
  const m = grad.match(/#[0-9a-fA-F]{6}/g)
  return m?.[1] || m?.[0] || '#C4A96A'
}
function lightenColor(hex: string, p: number): string {
  const n = parseInt(hex.slice(1), 16)
  const r = Math.min(255, (n >> 16) + Math.round(255 * p / 100))
  const g = Math.min(255, ((n >> 8) & 0xFF) + Math.round(255 * p / 100))
  const b = Math.min(255, (n & 0xFF) + Math.round(255 * p / 100))
  return '#' + ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')
}
function darkenColor(hex: string, p: number): string {
  const n = parseInt(hex.slice(1), 16)
  const r = Math.max(0, (n >> 16) - Math.round(255 * p / 100))
  const g = Math.max(0, ((n >> 8) & 0xFF) - Math.round(255 * p / 100))
  const b = Math.max(0, (n & 0xFF) - Math.round(255 * p / 100))
  return '#' + ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')
}

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

const animateCounter = (target: number, key: string, duration: number = 1500) => {
  const start = performance.now()
  const from = 0
  const step = (now: number) => {
    const progress = Math.min((now - start) / duration, 1)
    const eased = 1 - Math.pow(1 - progress, 3)
    displayStats[key] = Math.round(from + (target - from) * eased)
    if (progress < 1) requestAnimationFrame(step)
  }
  requestAnimationFrame(step)
}

const fetchDeptStats = async () => {
  try {
    const stats: any[] = await request.get('/job/dept-stats') || []
    // 从 API 数据动态构建所有部门信息
    const chips: typeof deptChips.value = []
    const options: string[] = []
    
    stats.forEach((s: any, idx: number) => {
      const name = s.dept
      if (!name) return
      
      const preset = deptPresets[name]
      const color = preset?.color || getAutoColor(name)
      const emoji = preset?.emoji || FALLBACK_EMOJIS[idx % FALLBACK_EMOJIS.length]
      const tagColor = TAG_COLORS[idx % TAG_COLORS.length]
      
      // 填充映射
      deptEmoji[name] = emoji
      deptGradients[name] = `linear-gradient(135deg, ${color}, ${darkenColor(color, 20)})`
      deptTagColors[name] = tagColor
      
      chips.push({ name, emoji, count: s.count, color: '' as any, customColor: color })
      options.push(name)
    })
    
    deptChips.value = chips
    deptOptions.value = options
  } catch {}
}

onMounted(async () => {
  await handleSearch()
  await fetchDeptStats()
  // Hero 统计数据动画
  heroStats[0].value = total.value
  if (total.value > 0) animateCounter(total.value, 'jobs', 1500)
  const activeDepts = deptChips.value.filter(d => d.count > 0).length
  heroStats[1].value = activeDepts || deptChips.value.length
  if (heroStats[1].value > 0) animateCounter(heroStats[1].value, 'depts', 1000)
  heroStats[2].value = cityOptions.length
  animateCounter(cityOptions.length, 'cities', 1000)
})

// 部门 chips 鼠标拖拽横向滚动
const deptChipsRef = ref<HTMLElement | null>(null)
let isDragging = false, dragStartX = 0, dragScrollLeft = 0

function onDeptChipsMouseDown(e: MouseEvent) {
  const el = deptChipsRef.value
  if (!el) return
  isDragging = true
  dragStartX = e.pageX - el.offsetLeft
  dragScrollLeft = el.scrollLeft
  el.style.cursor = 'grabbing'
  el.style.userSelect = 'none'
  document.addEventListener('mousemove', onDeptChipsMouseMove)
  document.addEventListener('mouseup', onDeptChipsMouseUp)
}

function onDeptChipsMouseMove(e: MouseEvent) {
  const el = deptChipsRef.value
  if (!isDragging || !el) return
  e.preventDefault()
  const x = e.pageX - el.offsetLeft
  el.scrollLeft = dragScrollLeft - (x - dragStartX) * 1.5
}

function onDeptChipsMouseUp() {
  const el = deptChipsRef.value
  isDragging = false
  if (el) {
    el.style.cursor = 'grab'
    el.style.userSelect = ''
  }
  document.removeEventListener('mousemove', onDeptChipsMouseMove)
  document.removeEventListener('mouseup', onDeptChipsMouseUp)
}
</script>

<style scoped lang="scss">
.job-list-page { max-width: 1100px; margin: 0 auto; padding: 0 16px 40px; }

// Hero
.hero {
  display: flex; justify-content: space-between; align-items: center;
  padding: 36px 32px; position: relative;
  background: linear-gradient(135deg, #0f0f23 0%, #1a1a3e 50%, #16162e 100%);
  border-radius: var(--radius-xl);
  margin-bottom: 24px;
  overflow: hidden;
}
.hero::before {
  content: '';
  position: absolute; top: -50%; right: -10%;
  width: 400px; height: 400px; border-radius: 50%;
  background: radial-gradient(circle, rgba(196,169,106,0.08) 0%, transparent 70%);
  pointer-events: none;
}
.hero-left { position: relative; z-index: 1; }
.hero-decor {
  position: absolute; top: -60px; left: -40px;
  width: 200px; height: 200px; border-radius: 50%;
  background: radial-gradient(circle, rgba(196,169,106,0.06) 0%, transparent 70%);
  pointer-events: none;
}
.hero-title { font-size: 28px; font-weight: 800; color: #fff; margin: 0 0 10px; position: relative; }
.hero-sub { font-size: 14px; color: rgba(255,255,255,0.6); margin: 0 0 8px; position: relative; }
.hero-count { font-weight: 800; color: #C4A96A; font-size: 18px; }
.hero-badge { font-size: 11px; color: rgba(255,255,255,0.35); position: relative; }

// Hero 右侧统计
.hero-right {
  display: flex; gap: 28px; position: relative; z-index: 1;
}
.hero-stat { text-align: center; }
.hs-num {
  font-size: 26px; font-weight: 800; color: #8A9BA8;
  font-variant-numeric: tabular-nums; font-family: var(--font-mono);
  line-height: 1.2;
}
.hs-label { font-size: 12px; color: rgba(255,255,255,0.45); margin-top: 4px; }

// 搜索卡片 - 深蓝背景
.search-card {
  background: linear-gradient(135deg, #0f0f23 0%, #1a1a3e 50%, #16162e 100%);
  border-radius: var(--radius-xl);
  padding: 28px 32px 20px;
  margin-bottom: 24px;
  position: relative;
  overflow: hidden;
  &::before {
    content: '';
    position: absolute; top: -30%; left: -5%;
    width: 300px; height: 300px; border-radius: 50%;
    background: radial-gradient(circle, rgba(138,155,168,0.06) 0%, transparent 70%);
    pointer-events: none;
  }
}

// 搜索胶囊
.search-bar { margin-bottom: 16px; position: relative; z-index: 1; }
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
  appearance: none; -webkit-appearance: none;
  border: 1.5px solid var(--color-border); outline: none;
  background: var(--color-bg);
  color: var(--color-text-secondary); font-size: 13px;
  padding: 6px 28px 6px 12px; border-radius: var(--radius-full);
  cursor: pointer; font-family: var(--font-sans);
  font-weight: 500;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%23999' stroke-width='2'%3E%3Cpath d='M6 9l6 6 6-6'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 10px center;
  transition: all 0.2s var(--ease-out);
  &:hover { border-color: var(--color-primary); color: var(--color-primary); }
  &--sm { max-width: 90px; padding: 6px 24px 6px 10px; }
}
.search-btn {
  padding: 8px 24px; border-radius: var(--radius-full); border: none;
  background: var(--gradient-primary); color: #fff;
  font-size: 14px; font-weight: 600; cursor: pointer;
  font-family: var(--font-sans);
  transition: all 0.2s var(--ease-bounce);
  &:hover { box-shadow: 0 4px 16px rgba(196,169,106,0.3); transform: scale(1.02); }
}

// 热门搜索
.hot-search {
  display: flex; align-items: center; gap: 8px;
  justify-content: center; flex-wrap: wrap;
  position: relative; z-index: 1;
}
.hot-label { font-size: 12px; color: rgba(255,255,255,0.5); }
.hot-tag {
  padding: 4px 12px; border-radius: var(--radius-full);
  border: 1px solid rgba(255,255,255,0.12);
  background: rgba(255,255,255,0.06);
  color: rgba(255,255,255,0.65);
  font-size: 12px; cursor: pointer;
  transition: all 0.2s var(--ease-out);
  &:hover { border-color: rgba(196,169,106,0.5); color: #C4A96A; background: rgba(196,169,106,0.1); }
}

    // 部门 chip — 单排横向滚动
  .dept-chips {
    display: flex !important; gap: 10px; flex-wrap: nowrap; overflow-x: auto;
    margin-bottom: 28px; padding: 4px 0 12px 0;
    scroll-behavior: smooth;
    cursor: grab;
    -webkit-overflow-scrolling: touch;
    &::-webkit-scrollbar { height: 0; display: none; }
    scrollbar-width: none;
    mask-image: linear-gradient(to right, transparent 0%, black 3%, black 97%, transparent 100%);
    -webkit-mask-image: linear-gradient(to right, transparent 0%, black 3%, black 97%, transparent 100%);
    > * { flex-shrink: 0 !important; }
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

.card-header { display: flex; align-items: flex-start; gap: 12px; margin-bottom: 10px; }
.card-header-text { flex: 1; min-width: 0; }
.card-title { font-size: 16px; font-weight: 700; margin: 0; line-height: 1.4; }

// 岗位卡片黏土图标
.card-clay-icon {
  width: 40px; height: 40px; flex-shrink: 0;
  border-radius: 12px;
  display: flex; align-items: center; justify-content: center;
  background: linear-gradient(145deg, var(--clay-light), var(--clay-color));
  box-shadow:
    3px 3px 8px color-mix(in srgb, var(--clay-shadow) 35%, transparent),
    -1px -1px 3px rgba(255,255,255,0.2),
    inset 2px 2px 3px rgba(255,255,255,0.3),
    inset -2px -2px 4px rgba(0,0,0,0.08);
}
.card-clay-emoji {
  font-size: 20px; line-height: 1;
  text-shadow:
    0 2px 1px rgba(0,0,0,0.2),
    0 -1px 1px rgba(255,255,255,0.5);
  filter:
    drop-shadow(0 2px 2px rgba(0,0,0,0.18))
    brightness(1.08)
    contrast(1.08)
    saturate(1.1);
}

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
