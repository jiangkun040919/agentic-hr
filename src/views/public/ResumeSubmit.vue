<template>
  <div class="resume-submit-container">
    <el-card v-loading="loading" class="submit-card">
      <template #header>
        <div class="card-header">
          <el-icon :size="20" color="var(--color-primary)"><Document /></el-icon>
          <span>投递简历</span>
        </div>
      </template>

      <div class="submit-body">
        <!-- ═══ 投递岗位 ═══ -->
        <div class="job-info">
          <span class="job-label">投递岗位</span>
          <el-tag type="primary" size="large" effect="dark">{{ job?.title }}</el-tag>
        </div>

        <!-- ═══ 上传简历 ═══ -->
        <div class="section-title">
          <span class="required-mark">*</span> 上传简历
        </div>
        <div class="upload-area" :class="{ 'has-file': resumeFile, 'has-error': uploadError }">
          <el-icon v-if="!resumeFile" :size="48" color="var(--color-border)"><UploadFilled /></el-icon>
          <el-icon v-else :size="48" color="var(--color-success)"><CircleCheckFilled /></el-icon>
          <div class="upload-title" v-if="!resumeFile">点击或拖拽上传简历</div>
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
          <div v-if="uploadError" class="upload-error-msg">{{ uploadError }}</div>
        </div>

        <!-- ═══ 个人信息 ═══ -->
        <div class="section-title">
          <span class="required-mark">*</span> 个人信息
        </div>
        <el-form ref="formRef" :model="form" :rules="rules" label-width="80px" class="submit-form" @submit.prevent>
          <el-form-item label="姓名" prop="candidateName">
            <el-input v-model="form.candidateName" placeholder="请输入真实姓名" size="large" :prefix-icon="User" />
          </el-form-item>

          <el-form-item label="手机号" prop="phone">
            <el-input v-model="form.phone" placeholder="请输入手机号" size="large" :prefix-icon="Phone" />
          </el-form-item>

          <el-form-item label="邮箱" prop="email">
            <el-input v-model="form.email" placeholder="请输入邮箱" size="large" :prefix-icon="Message" />
          </el-form-item>

          <el-form-item label="学历" prop="education">
            <el-select v-model="form.education" placeholder="请选择最高学历" size="large" style="width:100%">
              <el-option label="高中" value="高中" />
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

        <!-- ═══ 提交 ═══ -->
        <div class="submit-section">
          <el-button
            type="primary"
            size="large"
            :loading="submitting"
            :disabled="!canSubmit"
            class="submit-btn"
            @click="handleSubmit"
          >
            <el-icon><Check /></el-icon>确认投递
          </el-button>
          <div v-if="!resumeFile" class="submit-hint">请先上传简历文件</div>
          <div v-else-if="!canSubmit" class="submit-hint">请填写所有必填项后提交</div>
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
import { Document, Upload, UploadFilled, CircleCheckFilled, User, Phone, Message, Check } from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()
const resumeStore = useResumeStore()

const formRef = ref<FormInstance>()
const uploadRef = ref<UploadInstance>()
const loading = ref(false)
const submitting = ref(false)
const resumeFile = ref<UploadFile | null>(null)
const uploadError = ref('')

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
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式错误', trigger: 'blur' },
  ],
  education: [{ required: true, message: '请选择学历', trigger: 'change' }],
}

// 是否可以提交：简历已上传 + 表单必填项全部通过验证
const canSubmit = computed(() => {
  return !!resumeFile.value
})

onMounted(async () => {
  await jobStore.fetchJobDetail(jobId.value)
})

const formatFileSize = (bytes: number) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}

const beforeUpload = (rawFile: File) => {
  uploadError.value = ''
  const ext = rawFile.name.split('.').pop()?.toLowerCase()
  if (ext !== 'pdf' && ext !== 'doc' && ext !== 'docx') {
    uploadError.value = '仅支持 PDF、Word（.doc/.docx）格式'
    ElMessage.error('仅支持 PDF、Word（.doc/.docx）格式')
    return false
  }
  if (rawFile.size / 1024 / 1024 > 10) {
    uploadError.value = '文件大小不能超过 10MB'
    ElMessage.error('文件大小不能超过 10MB')
    return false
  }
  return true
}

const handleFileChange = (uploadFile: UploadFile) => {
  resumeFile.value = uploadFile
  uploadError.value = ''
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
    return
  }

  if (!formRef.value) return

  // 验证表单
  try {
    await formRef.value.validate()
  } catch {
    ElMessage.warning('请填写所有必填项')
    return
  }

  submitting.value = true
  try {
    const result = await resumeStore.submit({
      jobId: jobId.value,
      candidateName: form.candidateName,
      phone: form.phone,
      email: form.email,
      education: form.education,
      workYears: form.workYears,
      resumeUrl: resumeFile.value.name,
    })

    // 上传简历文件
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

.submit-body {
  padding: var(--space-2) 0;
}

// ═══ 岗位信息 ═══
.job-info {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-6);
  padding: var(--space-3) var(--space-4);
  background: var(--color-primary-bg);
  border-radius: var(--radius-md);

  .job-label {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
  }
}

// ═══ 分区标题 ═══
.section-title {
  font-size: var(--text-base);
  font-weight: var(--weight-semibold);
  color: var(--color-text);
  margin-bottom: var(--space-3);
  margin-top: var(--space-6);

  &:first-of-type {
    margin-top: 0;
  }

  .required-mark {
    color: var(--color-danger);
    margin-right: 2px;
  }
}

// ═══ 上传区域 ═══
.upload-area {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: var(--space-8) var(--space-6);
  border: 2px dashed var(--color-border);
  border-radius: var(--radius-xl);
  background: var(--color-bg);
  transition: all 0.3s;
  margin-bottom: var(--space-2);

  &.has-file {
    border-color: var(--color-success);
    background: var(--color-success-bg);
  }

  &.has-error {
    border-color: var(--color-danger);
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
    margin-bottom: var(--space-4);
    text-align: center;
  }

  .upload-actions {
    margin-top: var(--space-1);
  }

  .upload-error-msg {
    margin-top: var(--space-3);
    color: var(--color-danger);
    font-size: var(--text-sm);
  }
}

// ═══ 表单 ═══
.submit-form {
  max-width: 480px;

  .input-hint {
    margin-left: var(--space-2);
    font-size: var(--text-sm);
    color: var(--color-text-muted);
  }
}

// ═══ 提交按钮 ═══
.submit-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-top: var(--space-8);
  padding-top: var(--space-6);
  border-top: 1px solid var(--color-border);

  .submit-btn {
    width: 240px;
    height: 48px;
    font-size: var(--text-md);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.1em;
  }

  .submit-hint {
    margin-top: var(--space-2);
    font-size: var(--text-sm);
    color: var(--color-text-muted);
  }
}
</style>
