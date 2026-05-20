<template>
  <div class="job-detail-layout">
    <div class="job-detail-main">
      <el-button @click="$router.back()" class="back-btn" text>
        <el-icon><ArrowLeft /></el-icon>返回
      </el-button>

      <el-card v-loading="loading" v-if="job">
        <div class="job-header">
          <div class="title-section">
            <h1>{{ job.title }}</h1>
            <div class="tags">
              <el-tag>{{ job.dept }}</el-tag>
              <el-tag type="success">{{ job.location }}</el-tag>
              <el-tag v-if="job.salaryMin && job.salaryMax" type="warning">
                {{ job.salaryMin }}-{{ job.salaryMax }}K
              </el-tag>
              <el-tag v-if="job.headCount" type="info">招{{ job.headCount }}人</el-tag>
            </div>
          </div>
        </div>

        <el-divider />

        <div class="content-section">
          <h3><el-icon><Document /></el-icon> 职位描述</h3>
          <div class="JD-content">{{ job.JD || '暂无描述' }}</div>

          <h3><el-icon><List /></el-icon> 任职要求</h3>
          <div class="requirements-content">{{ job.requirements || '暂无要求' }}</div>

          <h3><el-icon><Money /></el-icon> 薪资范围</h3>
          <p>{{ job.salaryMin && job.salaryMax ? `${job.salaryMin}-${job.salaryMax}K` : '面议' }}</p>

          <h3><el-icon><Clock /></el-icon> 发布时间</h3>
          <p>{{ formatDate(job.createdAt) }}</p>
        </div>
      </el-card>
      <el-empty v-else-if="!loading" description="岗位不存在" />
    </div>

    <div class="job-detail-sidebar">
      <el-card>
        <template #header>公司信息</template>
        <p>🏢 AI智能招聘科技</p>
        <p>📍 深圳南山区科技园</p>
        <p>👥 规模：500-1000人</p>
        <p>💼 行业：企业服务 / SaaS</p>
      </el-card>
    </div>

    <div class="sticky-apply" v-if="isLoggedIn && isCandidate">
      <el-button type="primary" size="large" @click="handleDeliver">
        <el-icon><Upload /></el-icon> 立即投递
      </el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { useUserStore } from '@/stores/user'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Document, List, Money, Clock, Upload } from '@element-plus/icons-vue'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()
const jobStore = useJobStore()
const userStore = useUserStore()

const job = computed(() => jobStore.currentJob)
const loading = computed(() => jobStore.loading)
const isLoggedIn = computed(() => userStore.isLoggedIn)
const isCandidate = computed(() => userStore.isCandidate)

onMounted(() => {
  const id = Number(route.params.id)
  jobStore.fetchJobDetail(id)
})

const formatDate = (date: string) => dayjs(date).format('YYYY-MM-DD')

const handleDeliver = () => {
  if (!userStore.isLoggedIn) {
    ElMessage.warning('请先登录')
    router.push('/login?redirect=' + route.fullPath)
    return
  }
  router.push(`/resume/submit/${job.value?.jobId}`)
}
</script>

<style scoped lang="scss">
.job-detail-layout {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
  display: flex;
  gap: 24px;
  align-items: flex-start;
  padding-bottom: 80px;
}

.job-detail-main {
  flex: 1;
  min-width: 0;
}

.job-detail-sidebar {
  width: 280px;
  flex-shrink: 0;
  position: sticky;
  top: 80px;

  p {
    color: var(--color-text-secondary);
    font-size: 14px;
    line-height: 2;
  }
}

.back-btn {
  margin-bottom: 16px;
}

.job-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  .title-section {
    h1 { font-size: 24px; color: var(--color-primary); margin: 0 0 12px; }
    .tags { display: flex; gap: 8px; }
  }
}

.content-section {
  h3 {
    color: var(--color-primary); margin: 24px 0 12px; font-size: 16px;
    display: flex; align-items: center; gap: 6px;
  }
  p { color: var(--color-text-secondary); line-height: 1.8; }
  .JD-content, .requirements-content {
    white-space: pre-wrap; color: var(--color-text-secondary); line-height: 1.8;
    background: var(--color-bg); padding: 16px; border-radius: 8px;
  }
}

.sticky-apply {
  position: fixed; bottom: 0; left: 0; right: 0;
  background: var(--color-surface); padding: 12px 0; text-align: center;
  border-top: 1px solid var(--color-border);
  box-shadow: 0 -2px 20px rgba(0,0,0,0.5); z-index: 100;
  .el-button { min-width: 200px; height: 44px; font-size: 16px; }
}
</style>
