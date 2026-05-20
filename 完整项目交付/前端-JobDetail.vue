<template>
  <div class="job-detail">
    <el-card v-if="job" class="job-card">
      <template #header>
        <div class="job-header">
          <h1>{{ job.title }}</h1>
          <div class="job-meta">
            <el-tag>{{ job.dept }}</el-tag>
            <el-tag type="success">{{ job.location }}</el-tag>
            <el-tag type="warning">{{ job.salaryMin && job.salaryMax ? `${job.salaryMin}-${job.salaryMax}K` : '薪资面议' }}</el-tag>
          </div>
        </div>
      </template>
      
      <div class="job-content">
        <h3>岗位职责</h3>
        <div class="jd-content">{{ job.jd }}</div>
        
        <h3>任职要求</h3>
        <div class="requirements-content">{{ job.requirements }}</div>
        
        <h3>薪资范围</h3>
        <p>{{ job.salaryMin && job.salaryMax ? `${job.salaryMin}-${job.salaryMax}K` : '面议' }}</p>
        
        <h3>招聘人数</h3>
        <p>{{ job.headCount || '若干' }}人</p>
        
        <h3>发布时间</h3>
        <p>{{ formatDate(job.createdAt) }}</p>
      </div>
      
      <div class="job-actions">
        <el-button type="primary" size="large" @click="handleApply">立即投递</el-button>
        <el-button size="large" @click="$router.back()">返回列表</el-button>
      </div>
    </el-card>

    <el-empty v-else-if="!loading" description="岗位不存在" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useJobStore } from '@/stores/job'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()

const job = computed(() => jobStore.currentJob)
const loading = computed(() => jobStore.loading)

onMounted(async () => {
  const jobId = parseInt(route.params.id as string)
  if (jobId) {
    await jobStore.fetchJobDetail(jobId)
  }
})

const handleApply = () => {
  const token = localStorage.getItem('token')
  if (!token) {
    ElMessage.warning('请先登录')
    router.push('/login')
    return
  }
  router.push(`/resume/submit/${job.value?.jobId}`)
}

const formatDate = (date: string) => {
  return new Date(date).toLocaleDateString('zh-CN')
}
</script>

<style scoped lang="scss">
.job-detail {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}

.job-card {
  .job-header {
    h1 {
      margin: 0 0 16px 0;
      color: #1F4E78;
    }
    .job-meta {
      display: flex;
      gap: 8px;
    }
  }
  
  .job-content {
    h3 {
      color: #1F4E78;
      margin: 24px 0 12px 0;
      padding-bottom: 8px;
      border-bottom: 1px solid #e4e7ed;
    }
    
    .jd-content, .requirements-content {
      line-height: 1.8;
      color: #606266;
      white-space: pre-line;
    }
  }
  
  .job-actions {
    margin-top: 32px;
    display: flex;
    gap: 16px;
    justify-content: center;
  }
}
</style>
