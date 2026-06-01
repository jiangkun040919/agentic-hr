<template>
  <div class="profile-page">
    <div class="page-header">
      <h1 class="page-title">个人中心</h1>
      <p class="page-sub">管理你的个人资料和职业发展</p>
    </div>

    <!-- 头像区 -->
    <div class="profile-banner">
      <div class="avatar-circle">{{ form.realName?.charAt(0) || form.username?.charAt(0) || 'U' }}</div>
      <div class="banner-info">
        <h2>{{ form.realName || form.username }}</h2>
        <p>{{ form.education || '未填写学历' }} · {{ form.workYears ?? 0 }}年经验</p>
      </div>
    </div>

    <!-- 基本信息 -->
    <div class="section-card" v-loading="loading">
      <h3 class="section-title">📝 基本信息</h3>
      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">用户名</label>
          <input :value="form.username" class="form-input" disabled />
        </div>
        <div class="form-group">
          <label class="form-label">真实姓名</label>
          <input v-model="form.realName" class="form-input" placeholder="请输入真实姓名" />
        </div>
        <div class="form-group">
          <label class="form-label">手机号</label>
          <input v-model="form.phone" class="form-input" placeholder="请输入手机号" />
        </div>
        <div class="form-group">
          <label class="form-label">邮箱</label>
          <input v-model="form.email" class="form-input" placeholder="请输入邮箱" />
        </div>
        <div class="form-group">
          <label class="form-label">学历</label>
          <select v-model="form.education" class="form-select">
            <option value="">请选择学历</option>
            <option v-for="e in ['大专','本科','硕士','博士','其他']" :key="e" :value="e">{{ e }}</option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">工作年限</label>
          <input v-model.number="form.workYears" type="number" min="0" max="30" class="form-input" placeholder="年" />
        </div>
      </div>
    </div>

    <!-- 在线简历 -->
    <div class="section-card">
      <h3 class="section-title">📄 在线简历</h3>
      <p class="section-desc">填写技能、工作经历、项目经验，系统会根据此内容匹配岗位</p>
      <textarea v-model="form.resumeContent" class="form-textarea" rows="8" placeholder="例如：&#10;技能：Java, Spring Boot, MySQL, Redis, Docker&#10;工作经历：3年Java后端开发经验&#10;项目经验：参与过亿级用户平台的微服务改造" />
    </div>

    <!-- 附件简历 -->
    <div class="section-card">
      <h3 class="section-title">📎 附件简历</h3>
      <div v-if="form.resumeUrl" class="resume-file">
        <span class="rf-icon">📄</span>
        <span class="rf-name">{{ form.resumeUrl.split('/').pop() || '简历文件' }}</span>
        <VBtn variant="ghost" color="coral" size="sm" @click="form.resumeUrl = ''">删除</VBtn>
      </div>
      <div v-else class="upload-area" :class="{ uploading: uploadLoading }">
        <span class="ua-icon">📤</span>
        <span v-if="!uploadLoading">上传简历文件（PDF/Word）</span>
        <span v-else>上传中...</span>
        <VBtn variant="outlined" color="coral" size="sm" :disabled="uploadLoading" @click="triggerFileInput">选择文件</VBtn>
        <input ref="fileInputRef" type="file" accept=".pdf,.doc,.docx" @change="handleFileSelect" hidden />
      </div>
      <p class="upload-hint">支持 PDF、Word 格式，上传后 AI 将自动解析技能标签</p>
    </div>

    <VBtn variant="filled" color="coral" size="lg" block :loading="saving" @click="handleSave">💾 保存全部</VBtn>

    <!-- 推荐岗位 -->
    <div class="section-card recommend-section" v-loading="jobsLoading">
      <div class="recommend-header">
        <h3 class="section-title">🎯 为你推荐</h3>
        <span v-if="recommendResult?.recommendations?.length" class="recommend-count">{{ recommendResult.recommendations.length }} 个岗位</span>
      </div>

      <!-- 横向卡片滑动区 -->
      <div
        v-if="recommendResult?.recommendations?.length"
        class="recommend-track-wrapper"
        ref="trackWrapper"
      >
        <div class="recommend-track" ref="trackRef" @scroll="onTrackScroll">
          <div
            v-for="(r, i) in recommendResult.recommendations"
            :key="r.jobId"
            class="recommend-card"
            :class="{ 'is-active': activeCardIndex === i }"
            @click="$router.push(`/jobs/${r.jobId}`)"
          >
            <!-- 匹配率环形 -->
            <div class="rc-ring-wrap">
              <svg class="rc-ring" viewBox="0 0 80 80">
                <circle cx="40" cy="40" r="34" fill="none" stroke="var(--color-border-light)" stroke-width="5" />
                <circle
                  cx="40" cy="40" r="34" fill="none"
                  :stroke="r.matchRate >= 80 ? '#7A8B5E' : r.matchRate >= 60 ? '#5B8BA0' : '#A08060'"
                  stroke-width="5"
                  stroke-linecap="round"
                  :stroke-dasharray="`${r.matchRate * 2.136} 213.6`"
                  transform="rotate(-90 40 40)"
                  class="rc-ring-fill"
                />
                <text x="40" y="36" text-anchor="middle" class="rc-ring-num">{{ Math.round(r.matchRate) }}</text>
                <text x="40" y="52" text-anchor="middle" class="rc-ring-label">匹配度</text>
              </svg>
            </div>

            <!-- 信息区 -->
            <div class="rc-info">
              <h4 class="rc-title">{{ r.jobTitle }}</h4>
              <div class="rc-meta">
                <span>{{ r.department }}</span>
                <span class="rc-dot">·</span>
                <span>{{ r.location }}</span>
              </div>
              <div v-if="r.salaryRange" class="rc-salary">💰 {{ r.salaryRange }}</div>

              <!-- AI 推荐理由 -->
              <div v-if="r.aiReason" class="rc-reason">
                <span class="rc-reason-icon">💡</span>
                <span>{{ r.aiReason }}</span>
              </div>

              <!-- 技能标签 -->
              <div class="rc-skills">
                <span v-for="s in r.matchedSkills?.slice(0, 4)" :key="'m'+s" class="rc-tag rc-tag-match">✅ {{ s }}</span>
                <span v-for="s in r.missingSkills?.slice(0, 3)" :key="'g'+s" class="rc-tag rc-tag-gap">⚠ {{ s }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 左右导航箭头 -->
        <button v-if="canScrollLeft" class="rec-nav rec-nav-left" @click.stop="scrollCards(-1)">‹</button>
        <button v-if="canScrollRight" class="rec-nav rec-nav-right" @click.stop="scrollCards(1)">›</button>
      </div>

      <!-- 指示点 -->
      <div v-if="recommendResult?.recommendations?.length > 1" class="rec-dots">
        <span v-for="(r, i) in recommendResult.recommendations" :key="'d'+r.jobId"
          class="rec-dot" :class="{ active: activeCardIndex === i }"
          @click="scrollToCard(i)"
        />
      </div>

      <VEmpty v-else-if="recommendResult && !recommendResult.recommendations?.length" title="暂无推荐" description="请先完善在线简历" emoji="📭" />
      <VBtn v-else variant="filled" color="purple" size="sm" @click="loadRecommendJobs" :loading="jobsLoading">查看推荐岗位</VBtn>
    </div>

    <!-- 职业发展路径 -->
    <div class="section-card" v-loading="careerLoading">
      <h3 class="section-title">🚀 职业发展路径</h3>
      <div v-if="!careerResult">
        <p class="section-desc">选择一个目标岗位，我们为你规划最佳学习路径</p>
        <div class="career-select">
          <select v-model="selectedJobId" class="form-select" style="max-width:240px">
            <option :value="null" disabled>选择目标岗位</option>
            <option v-for="r in recommendResult?.recommendations" :key="r.jobId" :value="r.jobId">{{ r.jobTitle }}</option>
          </select>
          <VBtn variant="filled" color="coral" size="sm" @click="loadCareerPath" :loading="careerLoading" :disabled="!selectedJobId">规划路径</VBtn>
        </div>
      </div>
      <div v-else class="career-result">
        <div class="career-summary">
          <div class="cs-match">
            <span class="cs-current">{{ careerResult.currentMatchRate }}%</span>
            <span class="cs-arrow">→</span>
            <span class="cs-projected">{{ careerResult.projectedMatchRate }}%</span>
          </div>
          <span class="cs-label">当前 → 学成后匹配度</span>
          <span class="cs-weeks">预计学习 <b>{{ careerResult.learningWeeksTotal }}</b> 周</span>
        </div>

        <div v-if="careerResult.aiAdvice" class="career-advice">{{ careerResult.aiAdvice }}</div>

        <div class="career-steps">
          <h4>学习路径 ({{ careerResult.steps?.length || 0 }} 步)</h4>
          <div v-for="(s, i) in careerResult.steps" :key="s.skill" class="step-item">
            <div class="step-num" :class="{ high: s.priority === '高' }">{{ i + 1 }}</div>
            <div class="step-body">
              <div class="step-header">
                {{ s.skill }}
                <VTag v-if="s.priority === '高'" color="coral" size="sm">优先</VTag>
                <VTag color="gray" size="sm">约{{ s.estimatedWeeks }}周</VTag>
              </div>
              <div class="step-desc">{{ s.learningSuggestion }}</div>
              <div v-if="s.prerequisites?.length" class="step-prereq">
                前置技能：<VTag v-for="p in s.prerequisites" :key="p" color="sky" size="sm">{{ p }}</VTag>
              </div>
            </div>
          </div>
        </div>

        <VBtn variant="ghost" color="gray" size="sm" @click="careerResult = null" style="margin-top:12px">重新选择</VBtn>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useUserStore } from '@/stores/user'
import { updateProfile } from '@/api/auth'
import { request } from '@/utils/request'
import { getRecommendJobs, getCareerPath } from '@/api/graph'
import { ElMessage } from 'element-plus'
import VBtn from '@/components/ui/VBtn.vue'
import VTag from '@/components/ui/VTag.vue'
import VEmpty from '@/components/ui/VEmpty.vue'

const userStore = useUserStore()
const loading = computed(() => !userStore.userInfo)
const saving = ref(false)

const form = reactive({
  username: '', realName: '', phone: '', email: '',
  education: '', workYears: undefined as number | undefined,
  resumeContent: '', resumeUrl: '',
})

// 追踪表单是否有未保存的修改
const formDirty = ref(false)
watch(
  () => [form.realName, form.phone, form.email, form.education, form.workYears, form.resumeContent, form.resumeUrl],
  () => { formDirty.value = true },
  { deep: false }
)

const jobsLoading = ref(false)
const recommendResult = ref<any>(null)
const trackRef = ref<HTMLElement | null>(null)
const trackWrapper = ref<HTMLElement | null>(null)
const activeCardIndex = ref(0)
const canScrollLeft = ref(false)
const canScrollRight = ref(false)
const careerLoading = ref(false)
const selectedJobId = ref<number | null>(null)
const careerResult = ref<any>(null)

const uploadLoading = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)
const triggerFileInput = () => { fileInputRef.value?.click() }
const getCandidateId = () => (userStore.userInfo as any)?.candidateId || (userStore.userInfo as any)?.userId

const onTrackScroll = () => {
  const track = trackRef.value
  if (!track) return
  const cardW = track.querySelector('.recommend-card')?.clientWidth || 300
  const idx = Math.round(track.scrollLeft / (cardW + 16))
  activeCardIndex.value = Math.max(0, idx)
  canScrollLeft.value = track.scrollLeft > 10
  canScrollRight.value = track.scrollLeft < track.scrollWidth - track.clientWidth - 10
}

const scrollCards = (dir: number) => {
  const track = trackRef.value
  if (!track) return
  const cardW = (track.querySelector('.recommend-card')?.clientWidth || 300) + 16
  track.scrollBy({ left: cardW * dir, behavior: 'smooth' })
}

const scrollToCard = (i: number) => {
  const track = trackRef.value
  if (!track) return
  const cardW = (track.querySelector('.recommend-card')?.clientWidth || 300) + 16
  track.scrollTo({ left: cardW * i, behavior: 'smooth' })
}

const loadRecommendJobs = async () => {
  jobsLoading.value = true
  try {
    const res = await getRecommendJobs(getCandidateId()) as any
    // request 拦截器已解包 data 字段
    recommendResult.value = res
  } catch { ElMessage.warning('岗位推荐暂不可用') }
  finally {
    jobsLoading.value = false
    // 加载完成后检查滚动状态
    setTimeout(() => {
      onTrackScroll()
    }, 300)
  }
}

const loadCareerPath = async () => {
  if (!selectedJobId.value) return
  careerLoading.value = true
  try { const res = await getCareerPath(getCandidateId(), selectedJobId.value) as any; careerResult.value = res.data || res }
  catch { ElMessage.warning('路径规划暂不可用') }
  finally { careerLoading.value = false }
}

const handleFileSelect = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  // Validate type
  const validTypes = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document']
  if (!validTypes.includes(file.type)) {
    ElMessage.error('仅支持 PDF 和 Word 格式')
    input.value = ''
    return
  }

  // Validate size (10MB)
  if (file.size > 15 * 1024 * 1024) {
    ElMessage.error('文件大小不能超过 15MB')
    input.value = ''
    return
  }

  // Read as base64
  uploadLoading.value = true
  console.log('[上传] 开始读取文件:', file.name, file.size, 'bytes')
  try {
    const base64 = await readFileAsBase64(file)
    console.log('[上传] base64 编码完成, 长度:', base64.length)
    console.log('[上传] 发送请求...')
    const res = await request.post<any>('/auth/upload-resume', { fileBase64: base64, fileName: file.name })
    console.log('[上传] 响应:', res)
    if (res.url) {
      form.resumeUrl = res.url
      // 同步提取的文本到在线简历
      if (res.resumeContent) {
        form.resumeContent = res.resumeContent
      }

      // 立即保存到后端，防止刷新丢失
      try {
        console.log('[上传] 开始保存到后端...', { resumeUrl: res.url, resumeContent: res.resumeContent })
        const updateRes = await updateProfile({
          resumeUrl: res.url,
          resumeContent: res.resumeContent || undefined,
        })
        console.log('[上传] 保存到后端成功:', updateRes)
        // 保存后立即刷新用户信息，确保 store 和表单同步
        console.log('[上传] 刷新用户信息...')
        await userStore.fetchUserInfo()
        console.log('[上传] 刷新后 userInfo:', userStore.userInfo)
        syncUserInfoToForm(true)
        console.log('[上传] 表单同步后:', { resumeUrl: form.resumeUrl, resumeContent: form.resumeContent?.substring(0, 50) })
        ElMessage.success('简历上传成功，已自动保存')
      } catch (err: any) {
        console.error('自动保存失败:', err)
        ElMessage.warning('简历已上传，但保存失败: ' + (err?.message || '保存失败，请手动点击保存'))
      }
    } else {
      ElMessage.error((res as any).message || '上传失败')
    }
  } catch (err: any) {
    console.error('[上传] 失败:', err)
    ElMessage.error('上传失败: ' + (err.message || '网络错误'))
  } finally {
    uploadLoading.value = false
    input.value = ''
  }
}

const readFileAsBase64 = (file: File): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve((reader.result as string).split(',')[1] || reader.result as string)
    reader.onerror = () => reject(new Error('文件读取失败'))
    reader.readAsDataURL(file)
  })
}

// 从 store 同步用户信息到表单（不覆盖已编辑的值）
function syncUserInfoToForm(force = false) {
  if (!force && formDirty.value) return
  const info = userStore.userInfo
  if (!info) return
  form.username = info.username || ''
  form.realName = info.realName || ''
  form.phone = info.phone || ''
  form.email = info.email || ''
  form.education = (info as any).education || ''
  form.workYears = (info as any).workYears ?? 0
  form.resumeContent = (info as any).resumeContent || ''
  form.resumeUrl = (info as any).resumeUrl || ''
  if (force) formDirty.value = false
}

// 页面加载时拉取最新数据
onMounted(async () => {
  if (userStore.isLoggedIn) {
    try {
      await userStore.fetchUserInfo()
      syncUserInfoToForm()
    } catch (err: any) {
      console.error('加载用户信息失败:', err)
      ElMessage.warning('加载个人资料失败，请刷新重试')
    }
  }
})

const handleSave = async () => {
  saving.value = true
  try {
    await updateProfile({
      realName: form.realName, phone: form.phone, email: form.email,
      education: form.education, workYears: form.workYears,
      resumeContent: form.resumeContent, resumeUrl: form.resumeUrl
    })
    await userStore.fetchUserInfo()
    syncUserInfoToForm(true)
    ElMessage.success('保存成功')
  } catch (error: any) {
    const msg = error?.message || '保存失败'
    ElMessage.error(msg)
  }
  finally { saving.value = false }
}
</script>

<style scoped lang="scss">
.profile-page { max-width: 720px; margin: 0 auto; padding: 20px; }

.page-header { margin-bottom: 24px; }
.page-title { font-size: 28px; font-weight: 800; color: var(--color-text); margin: 0 0 4px; }
.page-sub { font-size: 14px; color: var(--color-text-muted); margin: 0; }

// 头像横幅
.profile-banner {
  display: flex; align-items: center; gap: 20px; padding: 28px;
  background: var(--gradient-primary); border-radius: var(--radius-xl);
  margin-bottom: 20px; color: #fff; position: relative; overflow: hidden;
  &::after { content: ''; position: absolute; top: -30%; right: -10%; width: 180px; height: 180px; border-radius: 50%; background: rgba(255,255,255,0.08); }
}
.avatar-circle {
  width: 64px; height: 64px; border-radius: 50%; background: rgba(255,255,255,0.2);
  display: flex; align-items: center; justify-content: center;
  font-size: 28px; font-weight: 800; color: #fff;
  backdrop-filter: blur(8px); position: relative; z-index: 1;
}
.banner-info { position: relative; z-index: 1; h2 { margin: 0 0 4px; font-size: 22px; } p { margin: 0; font-size: 14px; opacity: 0.85; } }

// 区块卡片
.section-card {
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: var(--radius-xl); padding: 24px; margin-bottom: 16px;
  transition: all 0.2s var(--ease-bounce);
  &:hover { transform: translateY(-1px); box-shadow: var(--shadow-md); }
}
.section-title { font-size: 18px; font-weight: 700; color: var(--color-text); margin: 0 0 16px; }
.section-desc { font-size: 13px; color: var(--color-text-muted); margin: 0 0 14px; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
.form-group { display: flex; flex-direction: column; gap: 5px; }
.form-label { font-size: 12px; font-weight: 600; color: var(--color-text-secondary); }
.form-input {
  height: 40px; padding: 0 14px; border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md); background: var(--color-bg);
  font-size: 14px; color: var(--color-text); font-family: var(--font-sans); outline: none;
  transition: border-color 0.2s;
  &:focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.1); }
  &:disabled { opacity: 0.5; cursor: not-allowed; }
}
.form-select {
  height: 40px; padding: 0 14px; border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md); background: var(--color-bg);
  font-size: 14px; color: var(--color-text); font-family: var(--font-sans); outline: none; cursor: pointer;
}
.form-textarea {
  width: 100%; padding: 14px; border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md); background: var(--color-bg);
  font-size: 14px; color: var(--color-text); font-family: var(--font-sans);
  outline: none; resize: vertical; line-height: 1.6;
  transition: border-color 0.2s;
  &:focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.1); }
}

.resume-file { display: flex; align-items: center; gap: 10px; padding: 10px 0; }
.rf-icon { font-size: 20px; }
.rf-name { flex: 1; font-size: 14px; }

.upload-area {
  display: flex; align-items: center; gap: 12px; padding: 16px;
  border: 2px dashed var(--color-border); border-radius: 14px;
  font-size: 14px; color: var(--color-text-secondary);
  &.uploading { opacity: 0.6; pointer-events: none; }
}
.ua-icon { font-size: 24px; }
.upload-hint { font-size: 12px; color: var(--color-text-muted); margin-top: 8px; }

// 推荐岗位
// 推荐岗位 — 横向卡片滑动
.recommend-section { overflow: visible; }
.recommend-header { display: flex; align-items: baseline; gap: 10px; margin-bottom: 12px; }
.recommend-count { font-size: 13px; color: var(--color-text-muted); }

.recommend-track-wrapper { position: relative; margin: 0 -4px; }
.recommend-track {
  display: flex; gap: 16px; overflow-x: auto; scroll-snap-type: x mandatory;
  padding: 4px 4px 8px; scroll-behavior: smooth;
  scrollbar-width: none;
  &::-webkit-scrollbar { display: none; }
}

.recommend-card {
  flex: 0 0 280px; scroll-snap-align: start;
  background: var(--color-surface); border: 1px solid var(--color-border);
  border-radius: 20px; padding: 20px; cursor: pointer;
  box-shadow: 0 2px 8px rgba(0,0,0,.04), 0 1px 2px rgba(0,0,0,.03), inset 0 1px 0 rgba(255,255,255,.6);
  transition: all 0.25s;
  &:hover { border-color: var(--color-primary-light); box-shadow: 0 4px 16px rgba(0,0,0,.08), inset 0 1px 0 rgba(255,255,255,.8); transform: translateY(-2px); }
  &.is-active { border-color: var(--color-primary); }
}

.rc-ring-wrap { text-align: center; margin-bottom: 14px; }
.rc-ring { width: 80px; height: 80px; display: inline-block; }
.rc-ring-fill { transition: stroke-dasharray 0.8s ease; }
.rc-ring-num { font-size: 20px; font-weight: 800; fill: var(--color-text); }
.rc-ring-label { font-size: 10px; fill: var(--color-text-muted); }

.rc-info { text-align: center; }
.rc-title { margin: 0 0 4px; font-size: 16px; font-weight: 700; color: var(--color-text); }
.rc-meta { font-size: 12px; color: var(--color-text-muted); margin-bottom: 6px; }
.rc-dot { margin: 0 4px; }
.rc-salary { font-size: 13px; color: var(--color-accent-coral); font-weight: 600; margin-bottom: 10px; }
.rc-reason {
  display: flex; align-items: flex-start; gap: 6px; padding: 10px 12px;
  background: var(--color-bg); border-radius: 12px;
  margin-bottom: 12px; font-size: 13px; color: var(--color-text-secondary); line-height: 1.5;
  text-align: left;
}
.rc-reason-icon { flex-shrink: 0; font-size: 16px; }
.rc-skills { display: flex; flex-wrap: wrap; gap: 6px; justify-content: center; }
.rc-tag {
  padding: 3px 10px; border-radius: 10px; font-size: 11px; font-weight: 600;
}
.rc-tag-match {
  background: #EFF5EC; color: #6B8B5E;
}
.rc-tag-gap {
  background: #FFF3EB; color: #B08040;
}

.rec-nav {
  position: absolute; top: 50%; transform: translateY(-50%);
  width: 36px; height: 36px; border-radius: 50%;
  background: var(--color-surface); border: 1px solid var(--color-border);
  color: var(--color-text); font-size: 22px; line-height: 1;
  display: flex; align-items: center; justify-content: center;
  cursor: pointer; z-index: 2; box-shadow: 0 2px 8px rgba(0,0,0,.1);
  transition: all 0.2s;
  &:hover { background: var(--color-bg); box-shadow: 0 4px 12px rgba(0,0,0,.15); }
}
.rec-nav-left { left: -14px; }
.rec-nav-right { right: -14px; }

.rec-dots { display: flex; justify-content: center; gap: 8px; margin-top: 12px; }
.rec-dot {
  width: 8px; height: 8px; border-radius: 50%;
  background: var(--color-border); cursor: pointer; transition: all 0.3s;
  &.active { background: var(--color-primary); width: 24px; border-radius: 4px; }
}

.career-select { display: flex; gap: 10px; }
.career-result { margin-top: 12px; }

.career-summary {
  display: flex; flex-direction: column; align-items: center; gap: 6px;
  padding: 20px; background: var(--color-bg); border-radius: 14px; margin-bottom: 16px;
}
.cs-match { display: flex; align-items: center; gap: 12px; }
.cs-current { font-size: 28px; font-weight: 800; color: var(--color-text-muted); }
.cs-arrow { font-size: 24px; color: var(--color-primary); }
.cs-projected { font-size: 28px; font-weight: 800; color: #7A8B5E; }
.cs-label { font-size: 12px; color: var(--color-text-muted); }
.cs-weeks { font-size: 14px; color: var(--color-text-secondary); b { color: var(--color-primary); } }

.career-advice {
  font-size: 14px; color: var(--color-text-secondary); line-height: 1.7;
  padding: 14px 18px; background: rgba(139,154,110,0.04); border-radius: 14px;
  border-left: 3px solid var(--color-secondary); margin-bottom: 16px;
}

.career-steps { h4 { font-size: 15px; margin: 0 0 12px; } }
.step-item {
  display: flex; gap: 12px; padding: 14px; margin-bottom: 8px;
  background: var(--color-bg); border-radius: 14px;
}
.step-num {
  width: 30px; height: 30px; border-radius: 50%; background: var(--color-border);
  display: flex; align-items: center; justify-content: center;
  font-size: 14px; font-weight: 700; flex-shrink: 0;
  &.high { background: var(--gradient-primary); color: #fff; }
}
.step-body { flex: 1; }
.step-header { font-weight: 600; display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.step-desc { font-size: 13px; color: var(--color-text-secondary); margin-top: 4px; }
.step-prereq { font-size: 12px; color: var(--color-text-muted); margin-top: 4px; display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
</style>
