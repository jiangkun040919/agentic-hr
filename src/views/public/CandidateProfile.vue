<template>
  <div class="candidate-profile-container">
    <h2 class="page-title">个人中心</h2>
    <el-card v-loading="loading">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="用户名">
          <el-input v-model="form.username" disabled />
        </el-form-item>
        <el-form-item label="真实姓名" prop="realName">
          <el-input v-model="form.realName" />
        </el-form-item>
        <el-form-item label="手机号" prop="phone">
          <el-input v-model="form.phone" />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSave">保存</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useUserStore } from '@/stores/user'
import { updateProfile } from '@/api/auth'
import { ElMessage, FormInstance, FormRules } from 'element-plus'

const userStore = useUserStore()
const loading = computed(() => !userStore.userInfo)
const formRef = ref<FormInstance>()

const form = reactive({
  username: '',
  realName: '',
  phone: '',
  email: '',
})

const rules: FormRules = {
  realName: [{ required: true, message: '请输入真实姓名', trigger: 'blur' }],
  phone: [{ pattern: /^1[3-9]\d{9}$/, message: '手机号格式错误', trigger: 'blur' }],
}

onMounted(() => {
  const info = userStore.userInfo
  if (info) {
    form.username = info.username
    form.realName = info.realName || ''
    form.phone = info.phone || ''
    form.email = info.email || ''
  }
})

const handleSave = async () => {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
    await updateProfile({
      realName: form.realName,
      phone: form.phone,
      email: form.email,
    })
    ElMessage.success('保存成功')
  } catch (error: any) {
    if (error.response?.data?.message) {
      ElMessage.error(error.response.data.message)
    }
  }
}
</script>

<style scoped lang="scss">
.candidate-profile-container {
  max-width: 600px;
  margin: 0 auto;
  padding: 20px;
}

.page-title {
  margin-bottom: 20px;
  color: var(--color-primary);
}
</style>