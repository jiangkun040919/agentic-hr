<template>
  <div class="job-detail-layout">
    <div class="job-detail-main">
      <button class="back-btn" @click="$router.back()"><el-icon><ArrowLeft /></el-icon> 返回</button>

      <div v-if="job" class="detail-card">
        <div class="detail-banner" :style="{ background: deptGradients[job.dept] || 'var(--gradient-primary)' }">
          <h1 class="detail-title">{{ job.title }}</h1>
          <div class="detail-tags">
            <span class="dt-tag">{{ job.dept }}</span>
            <span class="dt-tag">{{ job.location }}</span>
            <span class="dt-tag" v-if="job.salaryMin && job.salaryMax">{{ formatSalary(job.salaryMin) }}-{{ formatSalary(job.salaryMax) }}</span>
            <span class="dt-tag" v-if="job.headCount">招{{ job.headCount }}人</span>
          </div>
        </div>

        <div class="detail-body">
          <div class="section">
            <h3 class="section-title"><span class="section-icon" style="color:#C4A96A">📋</span> 职位描述</h3>
            <div class="section-content">{{ job.JD || '暂无描述' }}</div>
          </div>
          <div class="section">
            <h3 class="section-title"><span class="section-icon" style="color:#8B9A6E">🎯</span> 任职要求</h3>
            <div class="section-content">{{ job.requirements || '暂无要求' }}</div>
          </div>
          <div class="section-row">
            <div class="section">
              <h3 class="section-title"><span class="section-icon" style="color:#7A8B5E">💰</span> 薪资范围</h3>
              <p class="section-text">{{ job.salaryMin && job.salaryMax ? `${formatSalary(job.salaryMin)}-${formatSalary(job.salaryMax)}` : '面议' }}</p>
            </div>
            <div class="section">
              <h3 class="section-title"><span class="section-icon" style="color:#C4A96A">📅</span> 发布时间</h3>
              <p class="section-text">{{ formatDate(job.createdAt) }}</p>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="!loading" class="empty-state">
        <VEmpty title="岗位不存在" emoji="😢" />
      </div>
    </div>

    <div class="job-detail-sidebar">
      <div class="side-card">
        <div class="side-card-title">公司信息</div>
        <div class="company-info">
          <div class="company-row"><span class="ci-icon">🏢</span><span>AI智能招聘科技</span></div>
          <div class="company-row"><span class="ci-icon">📍</span><span>深圳南山区科技园</span></div>
          <div class="company-row"><span class="ci-icon">👥</span><span>500-1000人</span></div>
          <div class="company-row"><span class="ci-icon">💼</span><span>企业服务 / SaaS</span></div>
        </div>
      </div>

      <!-- 即时匹配度 -->
      <div v-if="isLoggedIn && isCandidate" class="side-card match-card" v-loading="matchLoading">
        <div class="side-card-title">即时匹配度</div>
        <div v-if="transparentMatch" class="match-result">
          <div class="match-ring-big" :style="{ '--pct': transparentMatch.overallScore }">
            <span class="ring-score" :style="{ color: tmScoreColor }">{{ transparentMatch.overallScore }}%</span>
          </div>
          <VTag :color="transparentMatch.overallScore >= 80 ? 'mint' : transparentMatch.overallScore >= 60 ? 'sunny' : 'coral'" size="md">
            {{ transparentMatch.recommendation }}
          </VTag>
          <div class="match-bars">
            <div class="mb"><span>技能</span><VProgress :percentage="transparentMatch.skillScore" color="coral" size="sm" :showLabel="true" /></div>
            <div class="mb"><span>经验</span><VProgress :percentage="transparentMatch.experienceScore" color="purple" size="sm" :showLabel="true" /></div>
            <div class="mb"><span>学历</span><VProgress :percentage="transparentMatch.educationScore" color="mint" size="sm" :showLabel="true" /></div>
          </div>
          <div v-if="transparentMatch.matchedSkills?.length" class="skill-section">
            <span class="ss-label">已匹配</span>
            <div class="ss-tags"><VTag v-for="s in transparentMatch.matchedSkills.slice(0,5)" :key="s" color="mint" size="sm">{{ s }}</VTag></div>
          </div>
          <div v-if="transparentMatch.missingSkills?.length" class="skill-section">
            <span class="ss-label">待补足</span>
            <div class="ss-tags"><VTag v-for="s in transparentMatch.missingSkills.slice(0,3)" :key="s" color="sunny" size="sm">{{ s }}</VTag></div>
          </div>
        </div>
        <VBtn v-else variant="filled" color="coral" size="sm" block @click="loadTransparentMatch" :loading="matchLoading">查看匹配度</VBtn>
      </div>
    </div>

    <!-- 底部投递栏 -->
    <div class="sticky-apply" v-if="isLoggedIn && isCandidate">
      <VBtn variant="filled" color="coral" size="lg" @click="handleDeliver">📤 立即投递</VBtn>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { useUserStore } from '@/stores/user'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import dayjs from 'dayjs'
import { getTransparentMatch } from '@/api/graph'
import { formatSalary } from '@/utils/format'
import VBtn from '@/components/ui/VBtn.vue'
import VTag from '@/components/ui/VTag.vue'
import VProgress from '@/components/ui/VProgress.vue'
import VEmpty from '@/components/ui/VEmpty.vue'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()
const userStore = useUserStore()

const job = computed(() => jobStore.currentJob)
const loading = computed(() => jobStore.loading)
const isLoggedIn = computed(() => userStore.isLoggedIn)
const isCandidate = computed(() => userStore.isCandidate)

const deptGradients: Record<string, string> = {
  '技术部': 'linear-gradient(135deg, #C4A96A, #B8605A)', 'AI部': 'linear-gradient(135deg, #8B9A6E, #8B9A6E)',
  '前端部': 'linear-gradient(135deg, #8A9BA8, #6B7B8D)', '数据部': 'linear-gradient(135deg, #7A8B5E, #6B8B4E)',
  '产品部': 'linear-gradient(135deg, #C4A96A, #B08040)', '架构部': 'linear-gradient(135deg, #C08070, #C08070)',
  '运维部': 'linear-gradient(135deg, #8A9BA8, #7A8B5E)', '安全部': 'linear-gradient(135deg, #C4A96A, #8B9A6E)',
  '设计部': 'linear-gradient(135deg, #C08070, #C4A96A)',
}

const matchLoading = ref(false)
const transparentMatch = ref<any>(null)
const tmScoreColor = computed(() =>
  (transparentMatch.value?.overallScore || 0) >= 80 ? '#7A8B5E' :
  (transparentMatch.value?.overallScore || 0) >= 60 ? '#C4A96A' : '#C4A96A'
)


const loadTransparentMatch = async () => {
  if (!job.value || matchLoading.value) return
  matchLoading.value = true
  try {
    const candidateId = userStore.userInfo?.userId
    if (!candidateId) { matchLoading.value = false; return }
    const res = await getTransparentMatch(candidateId!, job.value.jobId) as any
    transparentMatch.value = res.data || res
  } catch { ElMessage.warning('匹配分析暂不可用') }
  finally { matchLoading.value = false }
}

onMounted(() => {
  const id = Number(route.params.id)
  jobStore.fetchJobDetail(id)
})

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD')

const handleDeliver = () => {
  if (!userStore.isLoggedIn) { ElMessage.warning('请先登录'); router.push('/login?redirect=' + route.fullPath); return }
  router.push(`/resume/submit/${job.value?.jobId}`)
}
</script>

<style scoped lang="scss">
.job-detail-layout { max-width: 1200px; margin: 0 auto; padding: 20px; display: flex; gap: 24px; align-items: flex-start; padding-bottom: 80px; }
.job-detail-main { flex: 1; min-width: 0; }
.job-detail-sidebar { width: 280px; flex-shrink: 0; position: sticky; top: 80px; display: flex; flex-direction: column; gap: 16px; }

.back-btn {
  display: inline-flex; align-items: center; gap: 6px; padding: 8px 16px;
  border: none; background: var(--color-surface); border-radius: var(--radius-full);
  color: var(--color-text-secondary); font-size: 14px; cursor: pointer;
  font-family: var(--font-sans); margin-bottom: 16px;
  transition: all 0.2s var(--ease-bounce);
  &:hover { background: var(--color-primary-bg); color: var(--color-primary); transform: translateX(-2px); }
}

// 主卡片
.detail-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-xl); overflow: hidden;
  box-shadow: var(--shadow-card);
}

.detail-banner {
  padding: 32px 28px; color: #fff; position: relative; overflow: hidden;
  &::after { content: ''; position: absolute; top: -50%; right: -20%; width: 200px; height: 200px; border-radius: 50%; background: rgba(255,255,255,0.08); }
}
.detail-title { font-size: 26px; font-weight: 800; margin: 0 0 14px; position: relative; }
.detail-tags { display: flex; gap: 8px; flex-wrap: wrap; position: relative; }
.dt-tag {
  padding: 4px 14px; border-radius: var(--radius-full);
  background: rgba(255,255,255,0.2); color: #fff;
  font-size: 13px; font-weight: 500; backdrop-filter: blur(4px);
}

.detail-body { padding: 28px; }

.section { margin-bottom: 24px; }
.section-row { display: flex; gap: 24px; }
.section-title {
  font-size: 16px; font-weight: 700; color: var(--color-text);
  margin: 0 0 12px; display: flex; align-items: center; gap: 8px;
}
.section-icon { font-size: 18px; }
.section-content {
  white-space: pre-wrap; color: var(--color-text-secondary); line-height: 1.8;
  background: var(--color-bg); padding: 16px 20px; border-radius: 14px;
  border-left: 3px solid var(--color-primary);
}
.section-text { color: var(--color-text-secondary); line-height: 1.8; font-size: 15px; }


// 侧边卡片
.side-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-xl); padding: 20px;
  transition: all 0.2s var(--ease-bounce);
  &:hover { transform: translateY(-2px); box-shadow: var(--shadow-md); }
}
.side-card-title { font-size: 15px; font-weight: 700; color: var(--color-text); margin-bottom: 14px; }

.company-info { display: flex; flex-direction: column; gap: 10px; }
.company-row { display: flex; align-items: center; gap: 10px; font-size: 14px; color: var(--color-text-secondary); }
.ci-icon { font-size: 16px; }

// 匹配度
.match-card { border-top: 3px solid var(--color-primary); }
.match-result { display: flex; flex-direction: column; align-items: center; gap: 12px; }

.match-ring-big {
  width: 80px; height: 80px; border-radius: 50%; position: relative;
  background: conic-gradient(var(--color-primary) calc(var(--pct) * 1%), var(--color-bg-alt) 0);
  display: flex; align-items: center; justify-content: center;
  &::before { content: ''; position: absolute; inset: 6px; border-radius: 50%; background: var(--color-surface); }
}
.ring-score { font-size: 22px; font-weight: 800; position: relative; z-index: 1; }

.match-bars { width: 100%; }
.mb { display: flex; align-items: center; gap: 10px; margin: 8px 0; > span { font-size: 12px; color: var(--color-text-secondary); min-width: 30px; } }

.skill-section { width: 100%; }
.ss-label { font-size: 12px; color: var(--color-text-secondary); margin-bottom: 6px; display: block; }
.ss-tags { display: flex; flex-wrap: wrap; gap: 4px; }

// 底部投递栏
.sticky-apply {
  position: fixed; bottom: 0; left: 0; right: 0;
  background: var(--color-surface); padding: 14px 0; text-align: center;
  border-top: 1px solid var(--color-border);
  box-shadow: 0 -4px 24px rgba(0,0,0,0.1); z-index: 100;
}

// 响应式
@media (max-width: 768px) {
  .job-detail-layout { flex-direction: column; padding: 12px; gap: 16px; }
  .job-detail-sidebar { width: 100%; position: static; order: 2; }
  .detail-banner { padding: 24px 20px; }
  .detail-title { font-size: 20px; }
  .section-row { flex-direction: column; gap: 16px; }
  .sticky-apply .v-btn { min-width: 140px; }
}
</style>
