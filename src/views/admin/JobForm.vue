<template>
  <div class="job-form-container">
    <el-card>
      <template #header>
        <div style="display: flex; align-items: center; justify-content: space-between;">
          <span>{{ isEdit ? '编辑岗位' : '发布新岗位' }}</span>
          <el-button type="warning" plain @click="showAIDialog = true" :disabled="isEdit">
            <el-icon><MagicStick /></el-icon> AI 智能生成 JD
          </el-button>
        </div>
      </template>
      
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="岗位名称" prop="title">
          <el-input v-model="form.title" placeholder="如：高级前端工程师" />
        </el-form-item>
        
        <el-form-item label="所属部门" prop="dept">
          <el-select v-model="form.dept" placeholder="请选择部门">
            <el-option label="技术部" value="技术部" />
            <el-option label="产品部" value="产品部" />
            <el-option label="运营部" value="运营部" />
            <el-option label="市场部" value="市场部" />
            <el-option label="财务部" value="财务部" />
            <el-option label="人力资源部" value="人力资源部" />
          </el-select>
        </el-form-item>
        
        <el-form-item label="工作地点" prop="location">
          <el-input v-model="form.location" placeholder="如：北京" />
        </el-form-item>
        
        <el-form-item label="薪资范围">
          <el-input-number v-model="form.salaryMin" :min="1" :max="100" placeholder="最低" /> K
          ~
          <el-input-number v-model="form.salaryMax" :min="1" :max="100" placeholder="最高" /> K
        </el-form-item>
        
        <el-form-item label="招聘人数">
          <el-input-number v-model="form.headCount" :min="1" :max="100" />
        </el-form-item>
        
        <el-form-item label="任职要求" prop="requirements">
          <el-input v-model="form.requirements" type="textarea" :rows="6" placeholder="请输入任职要求" />
        </el-form-item>

        <el-form-item label="岗位描述">
          <el-input v-model="form.JD" type="textarea" :rows="4" placeholder="岗位职责、工作内容、团队介绍等" />
        </el-form-item>

        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">开放</el-radio>
            <el-radio :value="0">关闭</el-radio>
          </el-radio-group>
        </el-form-item>
        
        <el-form-item>
          <el-button type="primary" :loading="submitting" @click="handleSubmit">{{ isEdit ? '保存' : '发布' }}</el-button>
          <el-button @click="$router.back()">取消</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- AI 生成 JD 弹窗 -->
    <el-dialog v-model="showAIDialog" title="AI 智能生成岗位 JD（3个版本）" width="780px" :close-on-click-modal="false" destroy-on-close>
      <div class="ai-dialog-body">
        <p style="color: var(--color-text-secondary); font-size: 14px; margin-bottom: 12px;">
          用一句话描述你想要的候选人，例如：
          <el-tag size="small" style="cursor:pointer;margin:4px 4px 0 0;" @click="aiBrief='3年经验Python后端，熟悉FastAPI和PostgreSQL，有微服务经验'">3年经验Python后端</el-tag>
          <el-tag size="small" style="cursor:pointer;margin:4px 4px 0 0;" @click="aiBrief='高级前端工程师，精通React和TypeScript，有大型项目经验'">高级前端工程师</el-tag>
          <el-tag size="small" style="cursor:pointer;margin:4px 4px 0 0;" @click="aiBrief='产品经理，B端SaaS经验，3年以上，会数据分析'">产品经理</el-tag>
        </p>
        <el-input v-model="aiBrief" type="textarea" :rows="3" placeholder="例如：我需要一个3年经验的Python后端工程师，熟悉FastAPI和PostgreSQL..." />
        <el-button type="warning" :loading="aiGenerating" @click="handleAIGenerate" style="margin-top:10px;width:100%">
          <el-icon><MagicStick /></el-icon> AI 生成 3 个版本
        </el-button>

        <!-- 生成结果 - 3个版本 -->
        <div v-if="aiResult" style="margin-top:16px">
          <el-alert :title="`${aiResult.title} · ${aiResult.dept} · ${aiResult.location} · ${formatSalary(aiResult.salaryMin)}-${formatSalary(aiResult.salaryMax)}`" type="success" :closable="false" style="margin-bottom:12px" />
          <el-tabs v-model="selectedVersion">
            <el-tab-pane v-for="(v, i) in aiResult.versions" :key="i" :label="v.version" :name="i">
              <div class="version-card">
                <div class="vc-section">
                  <h4>📋 岗位职责</h4>
                  <ul><li v-for="(r, j) in v.responsibilities" :key="j">{{ r }}</li></ul>
                </div>
                <div class="vc-section">
                  <h4>✅ 任职要求</h4>
                  <ul><li v-for="(r, j) in v.requirements" :key="j">{{ r }}</li></ul>
                </div>
                <div class="vc-highlight">
                  <span>✨ {{ v.highlights }}</span>
                </div>
              </div>
            </el-tab-pane>
          </el-tabs>
          <el-button type="primary" @click="applyJDVersion" style="margin-top:12px;width:100%">
            应用「{{ aiResult.versions[selectedVersion]?.version }}」到表单
          </el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="showAIDialog = false">关闭</el-button>
      </template>
    </el-dialog>

  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { ElMessage, FormInstance, FormRules } from 'element-plus'
import { MagicStick } from '@element-plus/icons-vue'
import { generateJD } from '@/api/job'
import { formatSalary } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const isEdit = computed(() => !!route.params.id)

const form = reactive({
  title: '',
  dept: '',
  location: '',
  salaryMin: undefined as number | undefined,
  salaryMax: undefined as number | undefined,
  headCount: 1,
  JD: '',
  requirements: '',
  expiredAt: '',
  status: 1,
})

const rules: FormRules = {
  title: [{ required: true, message: '请输入岗位名称', trigger: 'blur' }],
  dept: [{ required: true, message: '请选择部门', trigger: 'change' }],
  location: [{ required: true, message: '请输入工作地点', trigger: 'blur' }],
  requirements: [{ required: true, message: '请输入任职要求', trigger: 'blur' }],
}

onMounted(async () => {
  if (isEdit.value) {
    const id = Number(route.params.id)
    await jobStore.fetchJobDetail(id)
    const job = jobStore.currentJob
    if (job) {
      Object.assign(form, {
        title: job.title, dept: job.dept, location: job.location,
        salaryMin: job.salaryMin, salaryMax: job.salaryMax,
        headCount: job.headCount, JD: job.JD, requirements: job.requirements,
        expiredAt: job.expiredAt, status: job.status,
      })
    }
  }
})

// ── AI 生成 JD ───────────────────────────────────────
const showAIDialog = ref(false)
const aiBrief = ref('')
const aiGenerating = ref(false)
const aiResult = ref<any>(null)
const selectedVersion = ref(0)

const handleAIGenerate = async () => {
  if (!aiBrief.value.trim()) { ElMessage.warning('请先输入岗位需求描述'); return }
  aiGenerating.value = true
  aiResult.value = null
  try {
    const res = await generateJD(aiBrief.value.trim())
    // 拦截器已解包：res = { title, dept, location, salaryMin, salaryMax, headCount, versions, verification }
    if (res && res.versions?.length > 0) {
      aiResult.value = res
      selectedVersion.value = 0
      ElMessage.success(`已生成 ${res.versions.length} 个版本，请选择后应用`)
    } else {
      ElMessage.error('JD 生成失败，请重试')
    }
  } catch (e: any) { ElMessage.error(e.message || '生成失败') }
  finally { aiGenerating.value = false }
}

const applyJDVersion = () => {
  if (!aiResult.value) return
  const v = aiResult.value.versions[selectedVersion.value]
  if (!v) return
  form.title = aiResult.value.title || form.title
  form.dept = aiResult.value.dept || form.dept
  form.location = aiResult.value.location || form.location
  if (aiResult.value.salaryMin) form.salaryMin = aiResult.value.salaryMin
  if (aiResult.value.salaryMax) form.salaryMax = aiResult.value.salaryMax
  if (aiResult.value.headCount) form.headCount = aiResult.value.headCount
  form.JD = v.responsibilities.join('\n')
  form.requirements = v.requirements.join('\n')
  showAIDialog.value = false
  ElMessage.success(`已应用「${v.version}」`)
}


const handleSubmit = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    submitting.value = true
    try {
      const payload = {
        title: form.title,
        dept: form.dept,
        location: form.location,
        JD: form.JD || '',
        requirements: form.requirements || '',
        salaryMin: form.salaryMin || null,
        salaryMax: form.salaryMax || null,
        headCount: form.headCount,
        expiredAt: form.expiredAt || null,
        status: form.status as any,
      }
      if (isEdit.value) {
        await jobStore.update(Number(route.params.id), payload)
        ElMessage.success('保存成功')
      } else {
        await jobStore.create(payload)
        ElMessage.success('发布成功')
      }
      router.push('/admin/jobs')
    } catch (error: any) {
      ElMessage.error(error.message || '操作失败')
    } finally {
      submitting.value = false
    }
  })
}
</script>

<style scoped lang="scss">
.job-form-container {
  max-width: 800px;
  margin: 0 auto;
}

.ai-dialog-body {
  .version-card {
    .vc-section {
      margin-bottom: 14px;
      h4 { font-size: 14px; color: var(--color-primary); margin: 0 0 8px; }
      ul { margin: 0; padding-left: 20px;
        li { font-size: 13px; color: var(--color-text-secondary); line-height: 2; }
      }
    }
    .vc-highlight {
      padding: 10px 14px;
      background: linear-gradient(135deg, var(--color-accent-bg), rgba(6, 182, 212, 0.04));
      border-radius: var(--radius-md);
      border: 1px solid var(--color-accent-light);
      span { font-size: 13px; color: var(--color-accent); font-weight: 500; }
    }
  }
}
</style>