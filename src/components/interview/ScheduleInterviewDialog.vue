<template>
  <el-dialog
    v-model="visible"
    :title="mode === 'edit' ? '修改面试信息' : '安排面试'"
    width="680px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
      <!-- 模块1：候选人基础信息（只读） -->
      <el-divider content-position="left">
        <el-icon><User /></el-icon> 候选人基础信息
      </el-divider>
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="应聘岗位">
            <el-input :model-value="delivery?.jobTitle" disabled />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="候选人姓名">
            <el-input :model-value="delivery?.candidateName" disabled />
          </el-form-item>
        </el-col>
      </el-row>
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="手机号">
            <el-input :model-value="delivery?.phone" disabled />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="学历/工作年限">
            <el-input :model-value="`${delivery?.education || '-'} / ${delivery?.workYears ? delivery.workYears + '年' : '-'}`" disabled />
          </el-form-item>
        </el-col>
      </el-row>

      <!-- 模块2：面试基础信息配置 -->
      <el-divider content-position="left">
        <el-icon><Calendar /></el-icon> 面试基础信息
      </el-divider>
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="面试轮次" prop="round">
            <el-select v-model="form.round" placeholder="请选择面试轮次" style="width: 100%">
              <el-option label="HR 初面" value="HR初面" />
              <el-option label="技术初试" value="技术初试" />
              <el-option label="技术复试" value="技术复试" />
              <el-option label="终面" value="终面" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="面试形式" prop="interviewType">
            <el-select v-model="form.interviewType" placeholder="请选择面试形式" style="width: 100%">
              <el-option label="线上面试" value="线上面试" />
              <el-option label="现场面试" value="现场面试" />
              <el-option label="电话面试" value="电话面试" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="面试时间" prop="scheduleTime">
        <el-date-picker
          v-model="form.scheduleTime"
          type="datetime"
          placeholder="选择面试时间"
          format="YYYY-MM-DD HH:mm"
          value-format="YYYY-MM-DD HH:mm:ss"
          :disabled-date="disabledDate"
          style="width: 100%"
        />
      </el-form-item>

      <el-form-item label="面试时长">
        <el-radio-group v-model="form.duration" class="duration-group">
          <el-radio-button value="30">30分钟</el-radio-button>
          <el-radio-button value="45">45分钟</el-radio-button>
          <el-radio-button value="60">60分钟</el-radio-button>
          <el-radio-button value="90">90分钟</el-radio-button>
        </el-radio-group>
      </el-form-item>

      <!-- 模块3：面试官分配 -->
      <el-divider content-position="left">
        <el-icon><UserFilled /></el-icon> 面试官分配
      </el-divider>
      <el-form-item label="面试官" prop="interviewerIds">
        <div class="interviewer-select-row">
          <el-select
            v-model="form.interviewerIds"
            multiple
            placeholder="请选择面试官"
            style="flex: 1"
            filterable
          >
            <el-option
              v-for="item in interviewers"
              :key="item.userId"
              :label="`${item.realName} (${item.roleName})`"
              :value="item.userId"
            />
          </el-select>
          <el-button type="primary" plain @click="openInterviewerManage">
            <el-icon><Setting /></el-icon>管理
          </el-button>
        </div>
        <div v-if="commonInterviewers.length" class="common-tips">
          常用面试官：
          <el-tag
            v-for="id in commonInterviewers"
            :key="id"
            size="small"
            class="common-tag"
            @click="addCommonInterviewer(id)"
          >
            {{ getInterviewerName(id) }}
          </el-tag>
          <el-button link type="primary" size="small" @click="openCommonSetting" style="margin-left: 8px;">
            <el-icon><Edit /></el-icon>编辑常用
          </el-button>
        </div>
      </el-form-item>

      <!-- 模块4：面试通知设置 -->
      <el-divider content-position="left">
        <el-icon><Message /></el-icon> 面试通知设置
      </el-divider>
      <el-form-item label="通知渠道">
        <el-checkbox-group v-model="form.notifyChannels">
          <el-checkbox label="站内" disabled>站内消息通知</el-checkbox>
        </el-checkbox-group>
        <div class="notify-tip">
          <el-icon style="color: var(--el-color-success); margin-right: 4px;"><CircleCheck /></el-icon>
          面试通知将通过站内消息实时推送给候选人，候选人可在招聘页面查看。
        </div>
      </el-form-item>

      <el-form-item label="通知内容">
        <el-input
          v-model="form.notifyContent"
          type="textarea"
          :rows="4"
          placeholder="系统将自动生成通知内容，您也可以手动修改"
        />
      </el-form-item>

      <!-- 模块5：HR内部备注 -->
      <el-divider content-position="left">
        <el-icon><Document /></el-icon> HR内部备注
      </el-divider>
      <el-form-item label="内部备注">
        <el-input
          v-model="form.hrRemark"
          type="textarea"
          :rows="2"
          placeholder="仅后台内部可见，候选人无法查看"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">{{ mode === 'edit' ? '保存修改' : '确认安排' }}</el-button>
    </template>
  </el-dialog>

  <!-- 面试官管理弹窗 -->
  <el-dialog
    v-model="interviewerDialogVisible"
    :title="interviewerDialogMode === 'create' ? '新增面试官' : '编辑面试官'"
    width="500px"
    :close-on-click-modal="false"
    append-to-body
  >
    <el-form label-width="90px">
      <el-form-item label="用户名">
        <el-input v-model="interviewerForm.username" placeholder="请输入用户名" :disabled="interviewerDialogMode === 'edit'" />
      </el-form-item>
      <el-form-item label="姓名">
        <el-input v-model="interviewerForm.realName" placeholder="请输入姓名" />
      </el-form-item>
      <el-form-item label="手机号">
        <el-input v-model="interviewerForm.phone" placeholder="请输入手机号" />
      </el-form-item>
      <el-form-item label="邮箱">
        <el-input v-model="interviewerForm.email" placeholder="请输入邮箱" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="interviewerForm.password" type="password" :placeholder="interviewerDialogMode === 'edit' ? '不修改请留空' : '默认123456'" show-password />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="interviewerDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="interviewerSubmitting" @click="handleSaveInterviewer">保存</el-button>
    </template>
  </el-dialog>

  <!-- 面试官列表管理弹窗 -->
  <el-dialog
    v-model="interviewerListDialogVisible"
    title="面试官管理"
    width="650px"
    :close-on-click-modal="false"
    append-to-body
  >
    <div style="margin-bottom: 12px;">
      <el-button type="primary" @click="openCreateInterviewer">
        <el-icon><Plus /></el-icon>新增面试官
      </el-button>
    </div>
    <el-table :data="interviewers" stripe size="small" max-height="300">
      <el-table-column prop="realName" label="姓名" width="100" />
      <el-table-column prop="username" label="用户名" width="120" />
      <el-table-column prop="phone" label="手机号" width="120" />
      <el-table-column prop="email" label="邮箱" />
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" link @click="openEditInterviewer(row)">编辑</el-button>
          <el-button size="small" type="danger" link @click="handleDeleteInterviewer(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
  </el-dialog>

  <!-- 常用面试官设置弹窗 -->
  <el-dialog
    v-model="commonDialogVisible"
    title="常用面试官设置"
    width="500px"
    :close-on-click-modal="false"
    append-to-body
  >
    <el-alert type="info" :closable="false" show-icon style="margin-bottom: 16px;">
      设置常用面试官后，下方会显示快捷选择标签，方便快速分配。
    </el-alert>
    <el-form label-width="100px">
      <el-form-item label="选择面试官">
        <el-select
          v-model="commonSelectedIds"
          multiple
          placeholder="请选择常用面试官"
          style="width: 100%"
          filterable
        >
          <el-option
            v-for="item in interviewers"
            :key="item.userId"
            :label="`${item.realName} (${item.roleName})`"
            :value="item.userId"
          />
        </el-select>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="commonDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="commonSaving" @click="handleSaveCommon">保存</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { User, Calendar, UserFilled, Message, Document, CircleCheck, Setting, Edit, Plus } from '@element-plus/icons-vue'
import dayjs from 'dayjs'
import { scheduleInterview, getInterviewerList, updateInterview } from '@/api/interview'
import { createInterviewer, updateInterviewer, deleteInterviewer } from '@/api/user'
import { getCommonInterviewers, saveCommonInterviewers } from '@/api/sys-config'
import type { FormInstance, FormRules } from 'element-plus'

interface Props {
  modelValue: boolean
  delivery: any
  mode?: 'create' | 'edit'
  interview?: any
  interviewId?: number
}

interface Emits {
  (e: 'update:modelValue', val: boolean): void
  (e: 'success'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  interview: null,
  interviewId: undefined
})
const emit = defineEmits<Emits>()

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const formRef = ref<FormInstance>()
const submitting = ref(false)
const interviewers = ref<any[]>([])
const commonInterviewers = ref<number[]>([])

// 面试官管理弹窗
const interviewerDialogVisible = ref(false)
const interviewerListDialogVisible = ref(false)
const interviewerForm = reactive({
  userId: 0,
  username: '',
  realName: '',
  phone: '',
  email: '',
  password: ''
})
const interviewerDialogMode = ref<'create' | 'edit'>('create')
const interviewerSubmitting = ref(false)

// 常用面试官设置弹窗
const commonDialogVisible = ref(false)
const commonSelectedIds = ref<number[]>([])
const commonSaving = ref(false)

const form = reactive({
  round: 'HR初面',
  interviewType: '线上面试',
  scheduleTime: '',
  duration: '45',
  interviewerIds: [] as number[],
  notifyChannels: ['站内'] as string[],
  notifyContent: '',
  hrRemark: ''
})

const rules: FormRules = {
  round: [{ required: true, message: '请选择面试轮次', trigger: 'change' }],
  interviewType: [{ required: true, message: '请选择面试形式', trigger: 'change' }],
  scheduleTime: [{ required: true, message: '请选择面试时间', trigger: 'change' }],
  interviewerIds: [
    { required: true, message: '请选择面试官', trigger: 'change' },
    { type: 'array', min: 1, message: '至少选择一位面试官', trigger: 'change' }
  ]
}

// 加载面试官列表和常用面试官配置
const loadInterviewers = async () => {
  try {
    const [response, commonRes] = await Promise.all([
      getInterviewerList(),
      getCommonInterviewers().catch(() => [])
    ])
    interviewers.value = response || []
    // 优先使用后台配置的常用面试官，如果没有配置则取前3个
    const configured = commonRes || []
    if (configured.length > 0) {
      commonInterviewers.value = configured
    } else {
      commonInterviewers.value = interviewers.value.slice(0, 3).map((i: any) => i.userId)
    }
  } catch (error) {
    console.error('加载面试官列表失败', error)
    interviewers.value = []
    commonInterviewers.value = []
  }
}

// 生成通知内容
const generateNotifyContent = () => {
  const { candidateName, jobTitle, phone } = props.delivery || {}
  const time = dayjs(form.scheduleTime).format('YYYY-MM-DD HH:mm')
  const type = form.interviewType
  const hrPhone = 'HR联系电话'

  let content = `【XX 招聘】您好 ${candidateName}，恭喜您获得面试邀请！
应聘岗位：${jobTitle}
面试时间：${time}
面试形式：${type}
面试时长：${form.duration}分钟
`

  if (type === '线上面试') {
    content += `面试链接：会议链接将在面试前发送至您的${phone ? '手机' : '邮箱'}
`
  } else if (type === '现场面试') {
    content += `公司地址：XX市XX区XX路XX大厦
`
  }

  content += `如有疑问可联系 HR：${hrPhone}`

  return content
}

// 禁用过去的时间
const disabledDate = (date: Date) => {
  return date.getTime() < Date.now() - 8.64e7 // 禁用今天之前
}

// 添加常用面试官
const addCommonInterviewer = (id: number) => {
  if (!form.interviewerIds.includes(id)) {
    form.interviewerIds.push(id)
  }
}

const getInterviewerName = (id: number) => {
  const interviewer = interviewers.value.find((i: any) => i.userId === id)
  return interviewer?.realName || ''
}

// 打开面试官管理弹窗（列表）
const openInterviewerManage = () => {
  interviewerListDialogVisible.value = true
}

// 打开新增面试官弹窗
const openCreateInterviewer = () => {
  interviewerDialogMode.value = 'create'
  interviewerForm.userId = 0
  interviewerForm.username = ''
  interviewerForm.realName = ''
  interviewerForm.phone = ''
  interviewerForm.email = ''
  interviewerForm.password = ''
  interviewerDialogVisible.value = true
}

// 打开编辑面试官
const openEditInterviewer = (row: any) => {
  interviewerDialogMode.value = 'edit'
  interviewerForm.userId = row.userId
  interviewerForm.username = row.username || ''
  interviewerForm.realName = row.realName || ''
  interviewerForm.phone = row.phone || ''
  interviewerForm.email = row.email || ''
  interviewerForm.password = ''
  interviewerDialogVisible.value = true
}

// 保存面试官
const handleSaveInterviewer = async () => {
  if (!interviewerForm.username.trim()) {
    ElMessage.warning('请输入用户名')
    return
  }
  interviewerSubmitting.value = true
  try {
    if (interviewerDialogMode.value === 'create') {
      await createInterviewer({
        username: interviewerForm.username,
        realName: interviewerForm.realName,
        password: interviewerForm.password || '123456',
        phone: interviewerForm.phone,
        email: interviewerForm.email
      })
      ElMessage.success('创建成功')
    } else {
      await updateInterviewer(interviewerForm.userId, {
        realName: interviewerForm.realName,
        phone: interviewerForm.phone,
        email: interviewerForm.email,
        password: interviewerForm.password || undefined
      })
      ElMessage.success('更新成功')
    }
    interviewerDialogVisible.value = false
    await loadInterviewers()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '操作失败')
  } finally {
    interviewerSubmitting.value = false
  }
}

// 删除面试官
const handleDeleteInterviewer = async (row: any) => {
  try {
    await deleteInterviewer(row.userId)
    ElMessage.success('删除成功')
    await loadInterviewers()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '删除失败')
  }
}

// 打开常用面试官设置
const openCommonSetting = () => {
  commonSelectedIds.value = [...commonInterviewers.value]
  commonDialogVisible.value = true
}

// 保存常用面试官
const handleSaveCommon = async () => {
  commonSaving.value = true
  try {
    await saveCommonInterviewers(commonSelectedIds.value)
    commonInterviewers.value = [...commonSelectedIds.value]
    ElMessage.success('保存成功')
    commonDialogVisible.value = false
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '保存失败')
  } finally {
    commonSaving.value = false
  }
}

// 监听通知渠道变化，自动更新内容
watch(
  () => [form.scheduleTime, form.interviewType, form.round],
  () => {
    if (form.scheduleTime) {
      form.notifyContent = generateNotifyContent()
    }
  }
)

// 监听弹窗打开
watch(visible, (val) => {
  if (val) {
    loadInterviewers()
    if (props.mode === 'edit' && props.interview) {
      loadInterviewData()
    } else {
      form.notifyContent = generateNotifyContent()
    }
  }
})

const loadInterviewData = () => {
  if (props.interview) {
    form.round = props.interview.round || 'HR初面'
    form.interviewType = props.interview.interviewType || '线上面试'
    form.scheduleTime = props.interview.scheduleTime
    form.duration = String(props.interview.duration || 45)
    form.interviewerIds = props.interview.interviewerIds || [props.interview.interviewerId]
    form.hrRemark = props.interview.remark || ''
    form.notifyChannels = ['站内']
  }
}

// 关闭弹窗
const handleClose = () => {
  formRef.value?.resetFields()
  form.round = 'HR初面'
  form.interviewType = '线上面试'
  form.duration = '45'
  form.interviewerIds = []
  form.notifyChannels = ['站内']
  form.hrRemark = ''
  visible.value = false
}

// 提交安排
const handleSubmit = async () => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
  } catch {
    ElMessage.warning('请完善必填信息')
    return
  }

  if (props.mode === 'edit') {
    await handleUpdate()
  } else {
    await handleCreate()
  }
}

const handleUpdate = async () => {
  submitting.value = true
  try {
    const interviewId = props.interviewId || props.interview?.interviewId
    if (!interviewId) {
      ElMessage.error('面试ID无效，请刷新页面后重试')
      return
    }

    const data = {
      interviewerId: form.interviewerIds[0],
      interviewerIds: form.interviewerIds,
      scheduleTime: form.scheduleTime,
      location: form.interviewType === '现场面试' ? '公司总部' : (form.interviewType === '线上面试' ? '线上会议' : '电话面试'),
      round: form.round,
      interviewType: form.interviewType,
      duration: parseInt(form.duration),
      remark: form.hrRemark
    }

    await updateInterview(interviewId, data)
    ElMessage.success('面试信息修改成功')
    emit('success')
    handleClose()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '修改失败，请重试')
  } finally {
    submitting.value = false
  }
}

const handleCreate = async () => {
  submitting.value = true

  try {
    const deliveryId = props.delivery?.deliveryId ?? props.delivery?.DeliveryId
    if (!deliveryId) {
      ElMessage.error('投递ID无效，请刷新页面后重试')
      return
    }

    const locationText = form.interviewType === '现场面试' ? '公司总部' : (form.interviewType === '线上面试' ? '线上会议' : '电话面试')
    console.log('Form values - deliveryId:', deliveryId, 'interviewerIds:', form.interviewerIds, 'interviewerId:', form.interviewerIds[0], 'scheduleTime:', form.scheduleTime, 'location:', locationText)
    
    const data = {
      deliveryId: deliveryId,
      interviewerId: form.interviewerIds[0],
      interviewerIds: form.interviewerIds,
      scheduleTime: dayjs(form.scheduleTime).toISOString(),
      location: locationText,
      round: form.round,
      interviewType: form.interviewType,
      duration: parseInt(form.duration),
      remark: form.hrRemark,
      notifyChannels: form.notifyChannels,
      notifyContent: form.notifyContent
    }

    console.log('Sending schedule interview request:', JSON.stringify(data, null, 2))
    const response = await scheduleInterview(data)
    console.log('Schedule interview response:', response)
    ElMessage.success('面试安排成功')
    emit('success')
    handleClose()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '安排失败，请重试')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="scss">
.quick-time-btns {
  margin-top: 8px;
  display: flex;
  gap: 8px;
}

.duration-group {
  display: flex;
}

.interviewer-select-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.common-tips {
  margin-top: 8px;
  font-size: 12px;
  color: var(--color-text-secondary);

  .common-tag {
    margin-right: 4px;
    cursor: pointer;
  }
}

.notify-tip {
  margin-top: 6px;
  font-size: 12px;
  color: var(--color-text-secondary);
  display: flex;
  align-items: center;
  line-height: 1.5;
}
</style>
