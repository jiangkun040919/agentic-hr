<template>
  <div class="resume-submit-container">
    <el-card v-loading="loading">
      <template #header>
        <div class="card-header">
          <el-icon :size="20" color="var(--color-primary)"><Document /></el-icon>
          <span>投递简历</span>
        </div>
      </template>

      <!-- ═══ 步骤指示 ═══ -->
      <el-steps :active="currentStep" align-center class="submit-steps">
        <el-step title="上传简历" description="PDF 或 Word 格式" />
        <el-step title="填写信息" description="完善个人资料" />
        <el-step title="确认投递" description="提交申请" />
      </el-steps>

      <!-- ═══ 步骤 1：上传简历 ═══ -->
      <div v-if="currentStep === 0" class="step-content">
        <div class="upload-area" :class="{ 'has-file': resumeFile }">
          <el-icon v-if="!resumeFile" :size="48" color="var(--color-border)"><UploadFilled /></el-icon>
          <el-icon v-else :size="48" color="var(--color-success)"><CircleCheckFilled /></el-icon>
          <div class="upload-title" v-if="!resumeFile">请上传您的简历文件</div>
          <div class="upload-title success" v-else>简历已就绪</div>
          <div class="upload-desc" v-if="!resumeFile">支持 PDF、Word（.doc/.docx）格式，大小不超过 10MB</div>
          <div class="upload-desc" v-else>
            {{ resumeFile?.name }} ({{ formatFileSize(resumeFile?.size || 0) }})
          </div>
          <div class="upload-actions">
            <el-upload
              ref="uploadRef"
              :auto-upload="false"
              :limit="1"
              accept=".pdf,.doc,.docx"
              :show-file-list="false"
              :before-upload="beforeUpload"
              :on-change="handleFileChange"
              drag
            >
              <el-button type="primary" size="large">
                <el-icon><Upload /></el-icon>{{ resumeFile ? '重新选择' : '选择简历文件' }}
              </el-button>
            </el-upload>
          </div>
          <el-alert
            v-if="resumeFile"
            type="success"
            :closable="false"
            show-icon
            title="文件已选择，请点击「下一步」继续"
            style="margin-top:16px;max-width:400px"
          />
        </div>
        <div class="step-actions">
          <el-button type="primary" size="large" :disabled="!resumeFile" @click="currentStep = 1">
            下一步 <el-icon><ArrowRight /></el-icon>
          </el-button>
        </div>
      </div>

      <!-- ═══ 步骤 2：填写信息 ═══ -->
      <div v-if="currentStep === 1" class="step-content">
        <el-form ref="formRef" :model="form" :rules="rules" label-width="100px" class="submit-form">
          <el-form-item label="投递岗位">
            <el-tag type="primary" size="large">{{ job?.title }}</el-tag>
          </el-form-item>

          <el-form-item label="姓名" prop="candidateName">
            <el-input v-model="form.candidateName" placeholder="请输入真实姓名" size="large" :prefix-icon="User" />
          </el-form-item>

          <el-form-item label="手机号" prop="phone">
            <el-input v-model="form.phone" placeholder="请输入手机号" size="large" :prefix-icon="Phone" />
          </el-form-item>

          <el-form-item label="邮箱" prop="email">
            <el-input v-model="form.email" placeholder="请输入邮箱（选填）" size="large" :prefix-icon="Message" />
          </el-form-item>

          <el-form-item label="学历" prop="education">
            <el-select v-model="form.education" placeholder="请选择最高学历" size="large" style="width:100%">
              <el-option label="大专" value="大专" />
              <el-option label="本科" value="本科" />
              <el-option label="硕士" value="硕士" />
              <el-option label="博士" value="博士" />
            </el-select>
          </el-form-item>

          <el-form-item label="工作年限" prop="workYears">
            <el-input-number v-model="form.workYears" :min="0" :max="30" size="large" controls-position="right" />
            <span class="input-hint">年</span>
          </el-form-item>
        </el-form>
        <div class="step-actions">
          <el-button size="large" @click="currentStep = 0">
            <el-icon><ArrowLeft /></el-icon>上一步
          </el-button>
          <el-button type="primary" size="large" @click="goToConfirm">
            下一步 <el-icon><ArrowRight /></el-icon>
          </el-button>
        </div>
      </div>

      <!-- ═══ 步骤 3：确认投递 ═══ -->
      <div v-if="currentStep === 2" class="step-content">
        <el-descriptions :column="1" border class="confirm-desc">
          <el-descriptions-item label="投递岗位">{{ job?.title }}</el-descriptions-item>
          <el-descriptions-item label="简历文件">{{ resumeFile?.name }}</el-descriptions-item>
          <el-descriptions-item label="姓名">{{ form.candidateName }}</el-descriptions-item>
          <el-descriptions-item label="手机号">{{ form.phone }}</el-descriptions-item>
          <el-descriptions-item label="邮箱">{{ form.email || '未填写' }}</el-descriptions-item>
          <el-descriptions-item label="学历">{{ form.education }}</el-descriptions-item>
          <el-descriptions-item label="工作年限">{{ form.workYears }}年</el-descriptions-item>
        </el-descriptions>
        <div class="step-actions">
          <el-button size="large" @click="currentStep = 1">
            <el-icon><ArrowLeft /></el-icon>上一步
          </el-button>
          <el-button type="primary" size="large" :loading="submitting" @click="handleSubmit">
            <el-icon><Check /></el-icon>确认投递
          </el-button>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { useResumeStore } from '@/stores/resume'
import { uploadResumeFile } from '@/api/delivery'
import { ElMessage, FormInstance, FormRules } from 'element-plus'
import type { UploadInstance, UploadFile } from 'element-plus'
import { Document, Upload, UploadFilled, CircleCheckFilled, ArrowLeft, ArrowRight, User, Phone, Message, Check } from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()
const resumeStore = useResumeStore()

const formRef = ref<FormInstance>()
const uploadRef = ref<UploadInstance>()
const loading = ref(false)
const submitting = ref(false)
const currentStep = ref(0)
const resumeFile = ref<UploadFile | null>(null)

const job = computed(() => jobStore.currentJob)
const jobId = computed(() => Number(route.params.jobId))

const form = reactive({
  candidateName: '',
  phone: '',
  email: '',
  education: '',
  workYears: 0,
})

const rules: FormRules = {
  candidateName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  phone: [
    { required: true, message: '请输入手机号', trigger: 'blur' },
    { pattern: /^1[3-9]\d{9}$/, message: '手机号格式错误', trigger: 'blur' },
  ],
  education: [{ required: true, message: '请选择学历', trigger: 'change' }],
}

onMounted(() => {
  jobStore.fetchJobDetail(jobId.value)
})

const formatFileSize = (bytes: number) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}

const beforeUpload = (rawFile: File) => {
  const ext = rawFile.name.split('.').pop()?.toLowerCase()
  if (ext !== 'pdf' && ext !== 'doc' && ext !== 'docx') {
    ElMessage.error('仅支持 PDF、Word（.doc/.docx）格式')
    return false
  }
  if (rawFile.size / 1024 / 1024 > 10) {
    ElMessage.error('文件大小不能超过 10MB')
    return false
  }
  return true
}

const handleFileChange = (uploadFile: UploadFile) => {
  resumeFile.value = uploadFile
}

const goToConfirm = async () => {
  if (!formRef.value) return
  await formRef.value.validate((valid) => {
    if (!valid) return
    currentStep.value = 2
  })
}

const readFileAsBase64 = (rawFile: File): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve((reader.result as string).split(',')[1] || reader.result as string)
    reader.onerror = () => reject(new Error('文件读取失败'))
    reader.readAsDataURL(rawFile)
  })
}

const handleSubmit = async () => {
  if (!resumeFile.value?.raw) {
    ElMessage.warning('请先上传简历文件')
    currentStep.value = 0
    return
  }

  submitting.value = true
  try {
    // 先提交投递申请
    const result = await resumeStore.submit({
      jobId: jobId.value,
      candidateName: form.candidateName,
      phone: form.phone,
      email: form.email,
      education: form.education,
      workYears: form.workYears,
      resumeUrl: resumeFile.value.name,
    })

    // 立即上传简历文件并提取文本（这是AI分析的数据源）
    const base64 = await readFileAsBase64(resumeFile.value.raw)
    try {
      await uploadResumeFile(result.deliveryId, base64, resumeFile.value.name)
      ElMessage.success('简历文件已上传，AI 即将分析')
    } catch (e: any) {
      ElMessage.warning('简历文件上传失败，但投递已提交。可在简历管理中重新上传。')
    }

    ElMessage.success('投递成功！')
    router.push('/my/deliveries')
  } catch (error: any) {
    ElMessage.error(error.message || '投递失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="scss">
.resume-submit-container {
  max-width: 680px;
  margin: 0 auto;
  padding: var(--space-5);
}

.card-header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-lg);
  font-weight: var(--weight-semibold);
  color: var(--color-primary);
}

.submit-steps {
  margin: var(--space-6) 0 var(--space-8);
}

.step-content {
  min-height: 300px;
}

// ═══ 上传区域 ═══
.upload-area {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: var(--space-10) var(--space-6);
  border: 2px dashed var(--color-border);
  border-radius: var(--radius-xl);
  background: var(--color-bg);
  transition: all 0.3s;

  &.has-file {
    border-color: var(--color-success);
    background: var(--color-success-bg);
  }

  .upload-title {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    color: var(--color-text);
    margin: var(--space-3) 0 var(--space-2);

    &.success { color: var(--color-success); }
  }

  .upload-desc {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    margin-bottom: var(--space-5);
    text-align: center;
  }

  .upload-actions {
    margin-top: var(--space-2);
  }
}

.step-actions {
  display: flex;
  justify-content: center;
  gap: var(--space-3);
  margin-top: var(--space-8);
}

.submit-form {
  max-width: 480px;
  margin: 0 auto;

  .input-hint {
    margin-left: var(--space-2);
    font-size: var(--text-sm);
    color: var(--color-text-muted);
  }
}

.confirm-desc {
  max-width: 480px;
  margin: 0 auto;
}
</style>
