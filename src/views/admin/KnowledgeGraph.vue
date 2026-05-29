<template>
  <div class="kb-container">
    <!-- ═══ 左栏：技能树 ═══ -->
    <aside class="kb-sidebar">
      <div class="kb-sidebar-header">
        <el-input
          v-model="searchQuery"
          placeholder="搜索技能..."
          clearable
          :prefix-icon="Search"
          size="large"
          @input="onSearch"
        />
      </div>

      <!-- 排序切换 -->
      <div class="kb-sort-bar">
        <el-radio-group v-model="sortMode" size="small" @change="loadSkills">
          <el-radio-button value="count">热度</el-radio-button>
          <el-radio-button value="name">字母</el-radio-button>
        </el-radio-group>
      </div>

      <!-- 分类树 -->
      <nav class="kb-tree" v-loading="loading">
        <div v-for="cat in categories" :key="cat.category" class="kb-tree-category">
          <div
            class="kb-cat-header"
            :class="{ collapsed: !cat.expanded }"
            @click="cat.expanded = !cat.expanded"
          >
            <el-icon class="kb-cat-arrow"><ArrowRight v-if="!cat.expanded" /><ArrowDown v-else /></el-icon>
            <span class="kb-cat-name">{{ cat.category }}</span>
            <span class="kb-cat-count">{{ cat.skills?.length || 0 }}</span>
          </div>
          <div v-show="cat.expanded" class="kb-cat-items">
            <div
              v-for="skill in cat.skills"
              :key="skill.name"
              class="kb-tree-item"
              :class="{ active: activeSkill === skill.name }"
              @click="selectSkill(skill.name)"
            >
              <span class="kb-item-name">{{ skill.name }}</span>
              <span class="kb-item-badge">{{ skill.jobCount }}</span>
            </div>
            <div v-if="!cat.skills?.length" class="kb-cat-empty">空</div>
          </div>
        </div>
      </nav>

      <!-- 热度排行 -->
      <div class="kb-hot-section">
        <div class="kb-hot-title">🔥 热度 Top10</div>
        <div
          v-for="(skill, i) in hotSkills"
          :key="skill.name"
          class="kb-hot-item"
          @click="selectSkill(skill.name)"
        >
          <span class="kb-hot-rank">{{ i + 1 }}</span>
          <span class="kb-hot-name">{{ skill.name }}</span>
          <div class="kb-hot-bar">
            <div class="kb-hot-fill" :style="{ width: hotBarWidth(skill) }" />
          </div>
        </div>
      </div>
    </aside>

    <!-- ═══ 中栏：技能卡片 ═══ -->
    <main class="kb-main" v-loading="detailLoading">
      <template v-if="activeSkill && skillDetail">
        <div class="kb-card">
          <!-- 技能头部 -->
          <div class="kb-card-header">
            <div class="kb-card-title-row">
              <h2 class="kb-card-title"># {{ skillDetail.name }}</h2>
              <el-tag :type="catTagType(skillDetail.category)" size="small" effect="plain">
                {{ skillDetail.category }}
              </el-tag>
            </div>
            <div class="kb-card-stats">
              <div class="kb-stat">
                <span class="kb-stat-num">{{ skillDetail.jobCount }}</span>
                <span class="kb-stat-label">关联岗位</span>
              </div>
              <div class="kb-stat">
                <span class="kb-stat-num">{{ skillDetail.relatedSkills?.length || 0 }}</span>
                <span class="kb-stat-label">相关技能</span>
              </div>
              <div class="kb-stat" v-if="trendLabel">
                <span class="kb-stat-num">{{ trendLabel }}</span>
                <span class="kb-stat-label">趋势</span>
              </div>
            </div>
          </div>

          <!-- Markdown 笔记区 -->
          <div class="kb-card-body">
            <div class="kb-content-header">
              <span>📝 笔记</span>
              <div class="kb-content-actions">
                <el-button size="small" text @click="editing = !editing">
                  {{ editing ? '预览' : '编辑' }}
                </el-button>
                <el-button size="small" text type="primary" :loading="aiWriting" @click="aiGenerate">
                  ✨ AI 生成
                </el-button>
              </div>
            </div>
            <div v-if="editing" class="kb-editor">
              <el-input
                v-model="noteContent"
                type="textarea"
                :rows="10"
                placeholder="写点笔记...支持 Markdown"
              />
              <el-button size="small" type="primary" style="margin-top:8px" @click="saveContent">
                保存
              </el-button>
            </div>
            <div v-else class="kb-markdown" v-html="renderedContent" />
          </div>
        </div>
      </template>
      <div v-else class="kb-empty-state">
        <el-empty description="选择左侧技能查看详情" :image-size="80" />
      </div>
    </main>

    <!-- ═══ 右栏：关系面板 ═══ -->
    <aside class="kb-relations" v-if="skillDetail">
      <!-- 关联岗位 -->
      <div class="kb-rel-section">
        <div class="kb-rel-header">关联岗位 ({{ skillDetail.jobCount }})</div>
        <div class="kb-job-list">
          <div
            v-for="job in skillDetail.jobs"
            :key="job.jobId"
            class="kb-job-item"
            @click="$router.push(`/admin/resumes?jobId=${job.jobId}`)"
          >
            <div class="kb-job-title">{{ job.title }}</div>
            <div class="kb-job-meta">
              <span>{{ job.location }}</span>
              <span v-if="job.salaryMin">{{ job.salaryMin }}-{{ job.salaryMax }}K</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 相关技能 -->
      <div class="kb-rel-section">
        <div class="kb-rel-header">相关技能 ({{ skillDetail.relatedSkills?.length || 0 }})</div>
        <div class="kb-related-list">
          <div
            v-for="rs in skillDetail.relatedSkills"
            :key="rs.name"
            class="kb-related-item"
            @click="selectSkill(rs.name)"
          >
            <span class="kb-related-name">{{ rs.name }}</span>
            <div class="kb-related-bar">
              <div
                class="kb-related-fill"
                :style="{ width: relatedBarWidth(rs) }"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- 局部图谱 -->
      <div class="kb-rel-section kb-graph-section">
        <div class="kb-rel-header">🕸️ 技能关系图</div>
        <GraphCanvas
          v-if="graphData"
          :nodes="graphData.nodes"
          :edges="graphData.edges"
          :height="280"
          @node-click="onGraphNodeClick"
        />
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { Search, ArrowRight, ArrowDown } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { request } from '@/utils/request'
import GraphCanvas from '@/components/graph/GraphCanvas.vue'
import { marked } from 'marked'

// ── 状态 ──
const loading = ref(false)
const detailLoading = ref(false)
const searchQuery = ref('')
const sortMode = ref<'count' | 'name'>('count')
const activeSkill = ref<string | null>(null)
const skillDetail = ref<any>(null)
const noteContent = ref('')
const editing = ref(false)
const aiWriting = ref(false)
const graphData = ref<any>(null)
const trendLabel = ref('')

interface CatGroup {
  category: string
  count: number
  skills: any[]
  expanded: boolean
}

const categories = ref<CatGroup[]>([])
const hotSkills = ref<any[]>([])

// ── 加载技能列表 ──
const loadSkills = async () => {
  loading.value = true
  try {
    const params: any = { sort: sortMode.value }
    if (searchQuery.value) params.search = searchQuery.value
    const qs = new URLSearchParams(params).toString()
    const res: any = await request.get(`/kb/skills?${qs}`)
    const allSkills = res?.data || res || []

    // 按分类分组
    const catMap: Record<string, any[]> = {}
    const catCounts: Record<string, number> = {}
    for (const s of allSkills) {
      const cat = s.category || '其他'
      if (!catMap[cat]) catMap[cat] = []
      catMap[cat].push(s)
      catCounts[cat] = (catCounts[cat] || 0) + 1
    }

    categories.value = Object.entries(catMap).map(([cat, skills]) => ({
      category: cat,
      count: skills.length,
      skills,
      expanded: true,
    }))

    hotSkills.value = [...allSkills].sort((a: any, b: any) => b.jobCount - a.jobCount).slice(0, 10)
  } finally {
    loading.value = false
  }
}

// ── 选择技能 ──
const selectSkill = async (name: string) => {
  activeSkill.value = name
  detailLoading.value = true
  editing.value = false
  try {
    const res: any = await request.get(`/kb/skills/detail?name=${encodeURIComponent(name)}`)
    skillDetail.value = res?.data || res

    // 加载笔记内容
    const noteRes: any = await request.get(`/kb/skills/content?name=${encodeURIComponent(name)}`)
    noteContent.value = noteRes?.data?.content || ''

    // 图谱数据
    graphData.value = skillDetail.value?.graph

    // 趋势
    const trend = skillDetail.value?.trend || []
    if (trend.length >= 2) {
      const last = trend[trend.length - 1].count
      const prev = trend[trend.length - 2].count
      trendLabel.value = last > prev ? '📈 上升' : last < prev ? '📉 下降' : '➡️ 平稳'
    }
  } finally {
    detailLoading.value = false
  }
}

// ── 搜索 ──
let searchTimer: any = null
const onSearch = () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(loadSkills, 300)
}

// ── Markdown 渲染 ──
const renderedContent = computed(() => {
  if (!noteContent.value) return '<p style="color:#999">暂无笔记，点「编辑」或「AI 生成」添加</p>'
  try {
    return marked(noteContent.value) as string
  } catch { return noteContent.value }
})

// ── 保存笔记 ──
const saveContent = async () => {
  try {
    await request.put(`/kb/skills/content?name=${encodeURIComponent(activeSkill.value!)}`, {
      content: noteContent.value,
    })
    ElMessage.success('已保存')
    editing.value = false
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ── AI 生成 ──
const aiGenerate = async () => {
  aiWriting.value = true
  try {
    const res: any = await request.post('/kb/ai/generate', {
      prompt: `你是资深技术专家。请为技能"${skillDetail.value.name}"写一份学习路径笔记（Markdown格式）：
1. 技能概述（2-3句话）
2. 前置知识
3. 学习路径（分初中高三级）
4. 推荐资源（书籍/文档/项目）
5. 面试重点
简洁专业，不超过500字。`,
    })
    const text = res?.data || res?.result || res
    noteContent.value = typeof text === 'string' ? text : JSON.stringify(text)
    ElMessage.success('AI 生成完成，可编辑后保存')
  } catch (e: any) {
    ElMessage.error('AI 生成失败: ' + (e.message || '未知错误'))
  } finally {
    aiWriting.value = false
  }
}

// ── 辅助 ──
const catTagType = (cat: string): any => {
  const map: Record<string, string> = { '后端': 'success', '前端': '', '数据': 'warning', 'AI/ML': 'danger', 'DevOps': 'info' }
  return map[cat] || ''
}

const hotBarWidth = (skill: any) => {
  const max = hotSkills.value[0]?.jobCount || 1
  return `${(skill.jobCount / max) * 100}%`
}

const relatedBarWidth = (rs: any) => {
  const max = skillDetail.value?.relatedSkills?.[0]?.weight || 1
  return `${(rs.weight / max) * 100}%`
}

const onGraphNodeClick = (node: any) => {
  if (node?.id && node.id !== activeSkill.value) {
    selectSkill(node.id)
  }
}

onMounted(loadSkills)
</script>

<style scoped lang="scss">
// ═══ 整体布局 ═══
.kb-container {
  display: flex;
  height: calc(100vh - 120px);
  gap: 0;
  background: var(--color-bg);
}

// ═══ 左栏 ═══
.kb-sidebar {
  width: 280px;
  min-width: 280px;
  border-right: 1px solid var(--color-border);
  background: var(--color-surface);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.kb-sidebar-header {
  padding: 12px;
  border-bottom: 1px solid var(--color-border-light);
}

.kb-sort-bar {
  padding: 8px 12px;
  border-bottom: 1px solid var(--color-border-light);
}

.kb-tree {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.kb-tree-category {
  margin-bottom: 2px;
}

.kb-cat-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-secondary);
  transition: background 0.15s;

  &:hover { background: var(--color-bg-alt); }

  .kb-cat-arrow { font-size: 12px; }
  .kb-cat-name { flex: 1; }
  .kb-cat-count {
    font-size: 11px;
    color: var(--color-text-muted);
    background: var(--color-bg-alt);
    padding: 1px 6px;
    border-radius: 8px;
  }
}

.kb-tree-item {
  display: flex;
  align-items: center;
  padding: 5px 12px 5px 28px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.15s;

  &:hover { background: var(--color-primary-bg); }
  &.active {
    background: var(--color-primary-bg);
    color: var(--color-primary);
    font-weight: 600;
  }

  .kb-item-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .kb-item-badge {
    font-size: 10px;
    color: var(--color-text-muted);
    background: var(--color-bg);
    padding: 0 5px;
    border-radius: 6px;
    min-width: 18px;
    text-align: center;
  }
}

.kb-cat-empty {
  padding: 4px 28px;
  font-size: 12px;
  color: var(--color-text-muted);
}

// 热度区
.kb-hot-section {
  border-top: 1px solid var(--color-border);
  padding: 10px 12px;
  max-height: 220px;
  overflow-y: auto;
}

.kb-hot-title { font-size: 12px; font-weight: 600; color: var(--color-text-secondary); margin-bottom: 6px; }

.kb-hot-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 2px 0;
  cursor: pointer;
  font-size: 12px;

  &:hover { color: var(--color-primary); }
}

.kb-hot-rank { width: 16px; text-align: center; color: var(--color-text-muted); font-weight: 600; }
.kb-hot-name { width: 70px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.kb-hot-bar {
  flex: 1;
  height: 4px;
  background: var(--color-bg-alt);
  border-radius: 2px;
  overflow: hidden;
}
.kb-hot-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--color-primary), var(--color-primary-light));
  border-radius: 2px;
  transition: width 0.3s;
}

// ═══ 中栏 ═══
.kb-main {
  flex: 1;
  overflow-y: auto;
  padding: 20px 24px;
  min-width: 0;
}

.kb-card {
  max-width: 720px;
}

.kb-card-header {
  margin-bottom: 20px;
}

.kb-card-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}

.kb-card-title {
  font-size: 24px;
  font-weight: 700;
  margin: 0;
  color: var(--color-text);
}

.kb-card-stats {
  display: flex;
  gap: 24px;
}

.kb-stat {
  text-align: center;
}
.kb-stat-num { display: block; font-size: 20px; font-weight: 700; color: var(--color-primary); }
.kb-stat-label { font-size: 12px; color: var(--color-text-muted); }

.kb-card-body {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 16px 20px;
}

.kb-content-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  font-size: 14px;
  font-weight: 600;
}

.kb-markdown {
  font-size: 14px;
  line-height: 1.8;
  color: var(--color-text);

  :deep(h2) { font-size: 18px; margin: 16px 0 8px; }
  :deep(h3) { font-size: 15px; margin: 12px 0 6px; }
  :deep(ul) { padding-left: 20px; }
  :deep(li) { margin: 2px 0; }
  :deep(code) { background: var(--color-bg-alt); padding: 2px 6px; border-radius: 4px; font-size: 13px; }
}

.kb-editor {
  :deep(.el-textarea__inner) {
    font-family: 'Fira Code', monospace;
    font-size: 13px;
    line-height: 1.6;
  }
}

.kb-empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

// ═══ 右栏 ═══
.kb-relations {
  width: 320px;
  min-width: 320px;
  border-left: 1px solid var(--color-border);
  background: var(--color-surface);
  overflow-y: auto;
  padding: 12px;
}

.kb-rel-section {
  margin-bottom: 16px;
}

.kb-rel-header {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--color-border-light);
}

.kb-job-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.kb-job-item {
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--color-bg);
  cursor: pointer;
  transition: all 0.15s;

  &:hover {
    background: var(--color-primary-bg);
    transform: translateX(2px);
  }
}

.kb-job-title {
  font-size: 13px;
  font-weight: 500;
  margin-bottom: 2px;
}

.kb-job-meta {
  font-size: 11px;
  color: var(--color-text-muted);
  display: flex;
  gap: 8px;
}

.kb-related-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.kb-related-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 3px 6px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;

  &:hover { background: var(--color-bg-alt); }
}

.kb-related-name {
  width: 100px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.kb-related-bar {
  flex: 1;
  height: 4px;
  background: var(--color-bg-alt);
  border-radius: 2px;
  overflow: hidden;
}
.kb-related-fill {
  height: 100%;
  background: var(--color-primary);
  border-radius: 2px;
  transition: width 0.3s;
}

.kb-graph-section {
  flex: 1;
  min-height: 300px;
}
</style>
