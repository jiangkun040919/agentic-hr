<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    width="700px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 查看详情模式 -->
    <template v-if="mode === 'view'">
      <el-descriptions :column="2" border>
        <el-descriptions-item label="候选人">{{ interview?.candidateName }}</el-descriptions-item>
        <el-descriptions-item label="应聘岗位">{{ interview?.jobTitle }}</el-descriptions-item>
        <el-descriptions-item label="面试官">{{ interview?.interviewerName }}</el-descriptions-item>
        <el-descriptions-item label="面试轮次">{{ interview?.round }}</el-descriptions-item>
        <el-descriptions-item label="面试形式">{{ interview?.interviewType }}</el-descriptions-item>
        <el-descriptions-item label="面试时长">{{ interview?.duration }}分钟</el-descriptions-item>
        <el-descriptions-item label="面试时间" :span="2">{{ formatDateTime(interview?.scheduleTime) }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusType(interview?.status)">{{ getStatusText(interview?.status) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="创建时间">{{ formatDateTime(interview?.createdAt) }}</el-descriptions-item>
      </el-descriptions>

      <!-- 已取消原因 -->
      <el-alert v-if="interview?.status === 4" type="warning" :closable="false" style="margin-top: 16px">
        取消原因：{{ interview?.cancelReason || '未填写' }}
      </el-alert>

      <!-- 面试结果 -->
      <el-card v-if="interview?.status === 1" style="margin-top: 16px">
        <template #header>面试结果</template>
        <el-descriptions :column="1" border>
          <el-descriptions-item label="面试结果">{{ interview?.result || '-' }}</el-descriptions-item>
          <el-descriptions-item label="面试评价">{{ interview?.record || '-' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>
    </template>

    <!-- 改期模式 -->
    <template v-else-if="mode === 'reschedule'">
      <el-form ref="formRef" :model="form" label-width="100px">
        <el-form-item label="面试时间" prop="scheduleTime">
          <el-date-picker
            v-model="form.scheduleTime"
            type="datetime"
            placeholder="选择新面试时间"
            format="YYYY-MM-DD HH:mm"
            value-format="YYYY-MM-DD HH:mm:ss"
            :disabled-date="disabledDate"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="面试形式">
          <el-select v-model="form.interviewType" style="width: 100%">
            <el-option label="线上面试" value="线上面试" />
            <el-option label="现场面试" value="现场面试" />
            <el-option label="电话面试" value="电话面试" />
          </el-select>
        </el-form-item>
        <el-form-item label="面试官">
          <el-select v-model="form.interviewerId" style="width: 100%" filterable>
            <el-option
              v-for="item in interviewers"
              :key="item.userId"
              :label="`${item.realName} (${item.roleName})`"
              :value="item.userId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="改期原因">
          <el-input v-model="form.rescheduleReason" type="textarea" :rows="2" placeholder="请输入改期原因" />
        </el-form-item>
      </el-form>
    </template>

    <!-- 取消模式 -->
    <template v-else-if="mode === 'cancel'">
      <el-form ref="formRef" :model="form" label-width="100px">
        <el-form-item label="取消原因" prop="cancelReason">
          <el-input
            v-model="form.cancelReason"
            type="textarea"
            :rows="3"
            placeholder="请输入取消原因（将通知候选人）"
          />
        </el-form-item>
        <el-alert type="warning" :closable="false">
          取消后将自动发送短信和邮件通知候选人，并更新简历状态为"已取消面试"。
        </el-alert>
      </el-form>
    </template>

    <!-- 结果回填模式 -->
    <template v-else-if="mode === 'result'">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="面试结果" prop="result">
          <el-radio-group v-model="form.result">
            <el-radio value="通过">通过</el-radio>
            <el-radio value="不通过">不通过</el-radio>
            <el-radio value="待定">待定</el-radio>
            <el-radio value="进入下一轮复试">进入下一轮复试</el-radio>
          </el-radio-group>
        </el-form-item>

        <el-form-item label="综合评分">
          <el-slider v-model="form.score" :min="0" :max="100" show-input />
        </el-form-item>

        <el-form-item label="分项评分">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="专业能力">
                <el-slider v-model="form.scores.professional" :min="0" :max="100" show-input size="small" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="项目经验">
                <el-slider v-model="form.scores.experience" :min="0" :max="100" show-input size="small" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="沟通表达">
                <el-slider v-model="form.scores.communication" :min="0" :max="100" show-input size="small" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="综合素质">
                <el-slider v-model="form.scores.quality" :min="0" :max="100" show-input size="small" />
              </el-form-item>
            </el-col>
          </el-row>
        </el-form-item>

        <el-form-item label="面试评价" prop="record">
          <el-input
            v-model="form.record"
            type="textarea"
            :rows="4"
            placeholder="请填写面试评价和备注"
          />
        </el-form-item>
      </el-form>
    </template>

    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button v-if="mode === 'view' && interview?.status === 0" type="warning" @click="mode = 'reschedule'">改期</el-button>
      <el-button v-if="mode === 'view' && interview?.status === 0" type="danger" @click="mode = 'cancel'">取消面试</el-button>
      <el-button v-if="mode === 'view' && interview?.status === 0" type="success" @click="mode = 'result'">记录结果</el-button>
      <el-button v-if="mode !== 'view'" type="primary" :loading="submitting" @click="handleSubmit">确认</el-button>
      <el-button v-if="mode !== 'view'" text @click="mode = 'view'">返回</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import dayjs from 'dayjs'
import type { FormInstance, FormRules } from 'element-plus'
import { updateInterview, cancelInterview, recordInterviewResult, getInterviewerList } from '@/api/interview'

interface Props {
  modelValue: boolean
  interview: any
  mode?: 'view' | 'reschedule' | 'cancel' | 'result'
}

interface Emits {
  (e: 'update:modelValue', val: boolean): void
  (e: 'success'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'view'
})

const emit = defineEmits<Emits>()

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const dialogTitle = computed(() => {
  switch (props.mode) {
    case 'view': return '面试详情'
    case 'reschedule': return '面试改期'
    case 'cancel': return '取消面试'
    case 'result': return '记录面试结果'
    default: return '面试详情'
  }
})

const mode = ref(props.mode)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const interviewers = ref<any[]>([])

const form = reactive({
  scheduleTime: '',
  interviewType: '',
  interviewerId: 0,
  rescheduleReason: '',
  cancelReason: '',
  result: '',
  score: 80,
  scores: {
    professional: 80,
    experience: 80,
    communication: 80,
    quality: 80
  },
  record: ''
})

const rules: FormRules = {
  cancelReason: [{ required: true, message: '请输入取消原因', trigger: 'blur' }],
  result: [{ required: true, message: '请选择面试结果', trigger: 'change' }]
}

// 加载面试官列表
const loadInterviewers = async () => {
  try {
    const response = await getInterviewerList()
    interviewers.value = response || []
  } catch (error) {
    console.error('加载面试官列表失败', error)
  }
}

// 格式化时间
const formatDateTime = (date: string) => {
  return date ? dayjs(date).format('YYYY-MM-DD HH:mm') : '-'
}

// 禁用过去时间
const disabledDate = (date: Date) => {
  return date.getTime() < Date.now() - 8.64e7
}

// 状态类型
const getStatusType = (status: number): 'primary' | 'success' | 'warning' | 'info' | 'danger' | undefined => {
  const types: Record<number, 'primary' | 'success' | 'warning' | 'info' | 'danger'> = {
    0: 'primary',
    1: 'warning',
    2: 'info',
    3: 'success',
    4: 'danger',
    5: 'info'
  }
  return types[status] || 'info'
}

// 状态文本
const getStatusText = (status: number) => {
  const texts = ['', '待面试', '已面试', '通过', '未通过', '已取消']
  return texts[status] || '未知'
}

// 监听模式变化
watch(mode, (val) => {
  if (val === 'reschedule') {
    form.scheduleTime = props.interview?.scheduleTime || ''
    form.interviewType = props.interview?.interviewType || '线上面试'
    form.interviewerId = props.interview?.interviewerId || 0
    loadInterviewers()
  }
})

// 关闭弹窗
const handleClose = () => {
  formRef.value?.resetFields()
  Object.assign(form, {
    scheduleTime: '',
    interviewType: '',
    interviewerId: 0,
    rescheduleReason: '',
    cancelReason: '',
    result: '',
    score: 80,
    scores: { professional: 80, experience: 80, communication: 80, quality: 80 },
    record: ''
  })
  visible.value = false
}

// 提交处理
const handleSubmit = async () => {
  if (mode.value === 'reschedule') {
    // 改期
    try {
      await formRef.value?.validate()
    } catch {
      return
    }

    submitting.value = true
    try {
      await updateInterview(props.interview.interviewId, {
        scheduleTime: form.scheduleTime,
        interviewType: form.interviewType,
        interviewerId: form.interviewerId,
        remark: form.rescheduleReason
      })
      ElMessage.success('面试改期成功，已发送改期通知')
      emit('success')
      handleClose()
    } catch (error: any) {
      ElMessage.error(error.response?.data?.message || '改期失败')
    } finally {
      submitting.value = false
    }
  } else if (mode.value === 'cancel') {
    // 取消
    try {
      await formRef.value?.validate()
    } catch {
      return
    }

    submitting.value = true
    try {
      await cancelInterview(props.interview.interviewId, form.cancelReason)
      ElMessage.success('面试已取消，已发送取消通知')
      emit('success')
      handleClose()
    } catch (error: any) {
      ElMessage.error(error.response?.data?.message || '取消失败')
    } finally {
      submitting.value = false
    }
  } else if (mode.value === 'result') {
    // 结果回填
    try {
      await formRef.value?.validate()
    } catch {
      return
    }

    submitting.value = true
    try {
      await recordInterviewResult(props.interview.interviewId, {
        result: form.result,
        record: form.record,
        score: form.score,
        scores: form.scores
      })
      ElMessage.success('面试结果已记录')
      emit('success')
      handleClose()
    } catch (error: any) {
      ElMessage.error(error.response?.data?.message || '记录失败')
    } finally {
      submitting.value = false
    }
  }
}

// 监听 interview 变化
watch(() => props.interview, (val) => {
  if (val && mode.value === 'view') {
    // 自动切换到结果tab
  }
}, { immediate: true })
</script>

<style scoped lang="scss">
.el-radio-group {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}
</style>
