<template>
  <div class="common-interviewer-setting">
    <el-card shadow="never">
      <template #header>
        <div class="card-header">
          <span>常用面试官设置</span>
          <el-button type="primary" :loading="saving" @click="handleSave">
            <el-icon><Check /></el-icon>保存设置
          </el-button>
        </div>
      </template>

      <el-alert
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 20px"
      >
        设置常用面试官后，在「安排面试」弹窗中会显示快捷选择标签，方便快速分配面试官。
      </el-alert>

      <el-form label-width="100px">
        <el-form-item label="选择面试官">
          <el-select
            v-model="selectedIds"
            multiple
            placeholder="请选择常用面试官（可多选）"
            style="width: 100%"
            filterable
            clearable
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

      <!-- 已选预览 -->
      <div v-if="selectedIds.length" class="preview-section">
        <div class="preview-title">已选常用面试官预览：</div>
        <div class="preview-tags">
          <el-tag
            v-for="id in selectedIds"
            :key="id"
            size="large"
            type="primary"
            closable
            @close="removeInterviewer(id)"
          >
            {{ getInterviewerName(id) }}
          </el-tag>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Check } from '@element-plus/icons-vue'
import { getInterviewerList } from '@/api/interview'
import { getCommonInterviewers, saveCommonInterviewers } from '@/api/sys-config'

const interviewers = ref<any[]>([])
const selectedIds = ref<number[]>([])
const saving = ref(false)

// 加载面试官列表和已保存的常用配置
const loadData = async () => {
  try {
    const [interviewerRes, configRes] = await Promise.all([
      getInterviewerList(),
      getCommonInterviewers()
    ])
    interviewers.value = interviewerRes || []
    selectedIds.value = configRes || []
  } catch (error) {
    console.error('加载数据失败', error)
    ElMessage.error('加载数据失败')
  }
}

const getInterviewerName = (id: number) => {
  const item = interviewers.value.find((i: any) => i.userId === id)
  return item ? `${item.realName} (${item.roleName})` : `ID:${id}`
}

const removeInterviewer = (id: number) => {
  selectedIds.value = selectedIds.value.filter((item) => item !== id)
}

const handleSave = async () => {
  saving.value = true
  try {
    await saveCommonInterviewers(selectedIds.value)
    ElMessage.success('保存成功')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="scss">
.common-interviewer-setting {
  max-width: 800px;
  margin: 0 auto;

  .card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-weight: 600;
  }

  .preview-section {
    margin-top: 20px;
    padding: 16px;
    background: var(--color-bg);
    border-radius: 8px;

    .preview-title {
      font-size: 14px;
      color: var(--color-text-secondary);
      margin-bottom: 12px;
    }

    .preview-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  }
}
</style>
