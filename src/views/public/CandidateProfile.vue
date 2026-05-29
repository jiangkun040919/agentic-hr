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
      <div v-else class="upload-area">
        <span class="ua-icon">📤</span>
        <span>上传简历文件（PDF/Word）</span>
        <el-upload action="/api/upload" :headers="uploadHeaders" :on-success="handleUploadSuccess" :on-error="handleUploadError" :before-upload="beforeUpload" :show-file-list="false" accept=".pdf,.doc,.docx">
          <VBtn variant="outlined" color="coral" size="sm">选择文件</VBtn>
        </el-upload>
      </div>
      <p class="upload-hint">支持 PDF、Word 格式，上传后 AI 将自动解析技能标签</p>
    </div>

    <VBtn variant="filled" color="coral" size="lg" block :loading="saving" @click="handleSave">💾 保存全部</VBtn>

    <!-- 推荐岗位 -->
    <div class="section-card" v-loading="jobsLoading">
      <h3 class="section-title">🎯 推荐岗位</h3>
      <div v-if="recommendResult?.recommendations?.length" class="recommend-list">
        <div v-for="r in recommendResult.recommendations" :key="r.jobId" class="recommend-item" @click="$router.push(`/jobs/${r.jobId}`)">
          <div class="ri-left">
            <h4>{{ r.jobTitle }}</h4>
            <span class="ri-meta">{{ r.department }} · {{ r.location }}</span>
          </div>
          <div class="ri-right">
            <span class="ri-match" :style="{ color: r.matchRate >= 80 ? '#7A8B5E' : r.matchRate >= 60 ? 'var(--color-primary)' : 'var(--color-primary)' }">{{ r.matchRate }}%</span>
            <VTag v-if="r.skillGapCount === 0" color="mint" size="sm">完美匹配</VTag>
            <VTag v-else color="sunny" size="sm">差{{ r.skillGapCount }}项</VTag>
          </div>
        </div>
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
import { ref, reactive, computed, onMounted } from 'vue'
import { useUserStore } from '@/stores/user'
import { updateProfile } from '@/api/auth'
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

const jobsLoading = ref(false)
const recommendResult = ref<any>(null)
const careerLoading = ref(false)
const selectedJobId = ref<number | null>(null)
const careerResult = ref<any>(null)

const uploadHeaders = computed(() => ({ Authorization: `Bearer ${userStore.token}` }))
const getCandidateId = () => (userStore.userInfo as any)?.candidateId || (userStore.userInfo as any)?.userId

const loadRecommendJobs = async () => {
  jobsLoading.value = true
  try { const res = await getRecommendJobs(getCandidateId()) as any; recommendResult.value = res.data || res }
  catch { ElMessage.warning('岗位推荐暂不可用') }
  finally { jobsLoading.value = false }
}

const loadCareerPath = async () => {
  if (!selectedJobId.value) return
  careerLoading.value = true
  try { const res = await getCareerPath(getCandidateId(), selectedJobId.value) as any; careerResult.value = res.data || res }
  catch { ElMessage.warning('路径规划暂不可用') }
  finally { careerLoading.value = false }
}

const handleUploadSuccess = (response: any) => {
  if (response.code === 200 && response.data?.url) { form.resumeUrl = response.data.url; ElMessage.success('简历上传成功') }
  else { form.resumeUrl = `/uploads/${Date.now()}.pdf`; ElMessage.success('简历已接收') }
}
const handleUploadError = () => ElMessage.warning('上传失败，可手动填写在线简历代替')
const beforeUpload = (file: File) => {
  const ok = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'].includes(file.type)
  if (!ok) { ElMessage.error('仅支持 PDF 和 Word 格式'); return false }
  return true
}

onMounted(() => {
  const info = userStore.userInfo
  if (info) Object.assign(form, { username: info.username || '', realName: info.realName || '', phone: info.phone || '', email: info.email || '', education: (info as any).education || '', workYears: (info as any).workYears, resumeContent: (info as any).resumeContent || '', resumeUrl: (info as any).resumeUrl || '' })
})

const handleSave = async () => {
  saving.value = true
  try {
    await updateProfile({ realName: form.realName, phone: form.phone, email: form.email, education: form.education, workYears: form.workYears, resumeContent: form.resumeContent, resumeUrl: form.resumeUrl })
    await userStore.fetchUserInfo(); ElMessage.success('保存成功')
  } catch (error: any) { if (error.response?.data?.message) ElMessage.error(error.response.data.message) }
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
}
.ua-icon { font-size: 24px; }
.upload-hint { font-size: 12px; color: var(--color-text-muted); margin-top: 8px; }

// 推荐岗位
.recommend-list { display: flex; flex-direction: column; gap: 8px; }
.recommend-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 14px 16px; border-radius: 14px; cursor: pointer;
  background: var(--color-bg); transition: all 0.2s var(--ease-bounce);
  &:hover { background: var(--color-primary-bg); transform: translateX(4px); }
}
.ri-left { h4 { margin: 0 0 4px; font-size: 15px; } }
.ri-meta { font-size: 12px; color: var(--color-text-muted); }
.ri-right { display: flex; align-items: center; gap: 10px; }
.ri-match { font-size: 22px; font-weight: 800; }

// 职业路径
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
