<template>
  <div class="register-page">
    <StarfieldBackground />
    <div class="bg-orb orb-1" /><div class="bg-orb orb-2" /><div class="bg-orb orb-3" />

    <div class="register-container">
      <div class="register-card glass-panel glow-border">
        <div class="brand-panel">
          <div class="brand-glow" />
          <div class="brand-content">
            <div class="brand-icon">
              <div class="brand-hex"><el-icon :size="36"><UserFilled /></el-icon></div>
            </div>
            <h1 class="brand-title neon-text">加入我们</h1>
            <p class="brand-desc">开启智能招聘之旅<br/>让AI为你的职业生涯导航</p>
          </div>
        </div>

        <div class="form-panel">
          <div class="form-header">
            <h2 class="form-title">创建账号</h2>
            <p class="form-subtitle">填写信息完成注册</p>
          </div>
          <el-form ref="formRef" :model="form" :rules="rules" class="register-form" @submit.prevent="handleRegister">
            <div class="role-selector">
              <div class="role-card" :class="{ active: form.role === 'hr' }" @click="form.role = 'hr'">
                <el-icon :size="20"><Briefcase /></el-icon>
                <span>HR / 招聘方</span>
              </div>
              <div class="role-card" :class="{ active: form.role === 'candidate' }" @click="form.role = 'candidate'">
                <el-icon :size="20"><User /></el-icon>
                <span>求职者</span>
              </div>
            </div>
            <el-row :gutter="12">
              <el-col :span="12">
                <el-form-item prop="username">
                  <el-input v-model="form.username" placeholder="用户名" :prefix-icon="User" class="dark-input" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item prop="realName">
                  <el-input v-model="form.realName" placeholder="真实姓名" :prefix-icon="UserFilled" class="dark-input" />
                </el-form-item>
              </el-col>
            </el-row>
            <el-form-item prop="password">
              <el-input v-model="form.password" type="password" placeholder="密码（至少6位）" :prefix-icon="Lock" show-password class="dark-input" />
            </el-form-item>
            <el-form-item prop="confirmPassword">
              <el-input v-model="form.confirmPassword" type="password" placeholder="确认密码" :prefix-icon="Lock" show-password class="dark-input" />
            </el-form-item>
            <el-row :gutter="12">
              <el-col :span="12">
                <el-form-item prop="phone">
                  <el-input v-model="form.phone" placeholder="手机号" :prefix-icon="Phone" class="dark-input" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item prop="email">
                  <el-input v-model="form.email" placeholder="邮箱" :prefix-icon="Message" class="dark-input" />
                </el-form-item>
              </el-col>
            </el-row>
            <el-form-item>
              <el-button type="primary" size="large" class="submit-btn" :loading="loading" @click="handleRegister">
                <span v-if="!loading">注 册</span>
              </el-button>
            </el-form-item>
          </el-form>
          <div class="form-footer">
            <span class="footer-text">已有账号？</span>
            <router-link to="/login" class="footer-link gradient-text">立即登录</router-link>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { register as registerApi } from '@/api/auth'
import { User, UserFilled, Lock, Phone, Message, Briefcase } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import StarfieldBackground from '@/components/StarfieldBackground.vue'

const router = useRouter()
const formRef = ref<FormInstance>()
const loading = ref(false)
const form = reactive({
  username: '', realName: '', password: '', confirmPassword: '',
  phone: '', email: '', role: 'candidate' as 'hr' | 'candidate',
})

const validateConfirmPassword = (_rule: any, value: string, cb: any) => {
  if (value !== form.password) cb(new Error('两次密码不一致'))
  else cb()
}

const rules: FormRules = {
  username: [{ required: true, min: 3, message: '用户名至少3位', trigger: 'blur' }],
  realName: [{ required: true, message: '请输入真实姓名', trigger: 'blur' }],
  password: [{ required: true, min: 6, message: '密码至少6位', trigger: 'blur' }],
  confirmPassword: [{ required: true, validator: validateConfirmPassword, trigger: 'blur' }],
}

const handleRegister = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    loading.value = true
    try {
      await registerApi({ ...form, confirmPassword: undefined } as any)
      ElMessage.success('注册成功，请登录')
      router.push('/login')
    } catch {
      ElMessage.error('注册失败，请稍后重试')
    } finally {
      loading.value = false
    }
  })
}

</script>

<style scoped lang="scss">
.register-page {
  min-height: 100vh; display: flex; align-items: center; justify-content: center;
  background: var(--color-bg); position: relative; overflow: hidden;
}
.bg-orb { position: fixed; border-radius: 50%; pointer-events: none; z-index: 0; filter: blur(80px); opacity: 0.12; }
.orb-1 { width: 350px; height: 350px; background: radial-gradient(circle, #A855F7 0%, transparent 70%); top: -60px; right: -60px; animation: floatDrift 9s ease-in-out infinite; }
.orb-2 { width: 400px; height: 400px; background: radial-gradient(circle, #6366F1 0%, transparent 70%); bottom: -100px; left: -100px; animation: floatDrift 11s ease-in-out infinite reverse; }
.orb-3 { width: 200px; height: 200px; background: radial-gradient(circle, #06B6D4 0%, transparent 70%); top: 40%; left: 55%; animation: floatDrift 13s ease-in-out infinite 2s; }

.register-container { z-index: 1; width: 100%; max-width: 920px; padding: var(--space-5); }
.register-card { display: flex; border-radius: var(--radius-xl); overflow: hidden; min-height: 580px; }

.brand-panel {
  width: 40%; background: linear-gradient(180deg, rgba(42, 37, 32, 0.95) 0%, rgba(13, 13, 18, 0.98) 100%);
  display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden;
  .brand-glow { position: absolute; width: 180px; height: 180px; border-radius: 50%; background: radial-gradient(circle, rgba(139, 154, 110, 0.25) 0%, transparent 70%); top: 50%; left: 50%; transform: translate(-50%, -50%); animation: neonPulse 3s ease-in-out infinite; }
  .brand-content { position: relative; z-index: 1; text-align: center; padding: var(--space-8); }
  .brand-icon { margin-bottom: var(--space-6); }
  .brand-hex { width: 72px; height: 72px; margin: 0 auto; background: var(--gradient-primary); clip-path: polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%); display: flex; align-items: center; justify-content: center; color: #fff; box-shadow: var(--glow-primary-lg); }
  .brand-title { font-size: 28px; font-weight: var(--weight-bold); margin-bottom: var(--space-3); }
  .brand-desc { font-size: var(--text-sm); color: var(--color-text-muted); line-height: 1.8; }
}

.form-panel {
  flex: 1; padding: var(--space-8); display: flex; flex-direction: column; justify-content: center; background: var(--color-surface);
  .form-header { margin-bottom: var(--space-6); .form-title { font-size: 22px; font-weight: var(--weight-bold); color: var(--color-text); margin-bottom: var(--space-1); } .form-subtitle { font-size: var(--text-sm); color: var(--color-text-muted); } }
  .register-form {
    .role-selector { display: flex; gap: var(--space-3); margin-bottom: var(--space-5); }
    .role-card {
      flex: 1; display: flex; align-items: center; gap: var(--space-2); padding: var(--space-3) var(--space-4);
      border-radius: var(--radius-md); border: 1px solid var(--color-border); cursor: pointer;
      font-size: var(--text-sm); color: var(--color-text-secondary);
      transition: all var(--duration-fast) var(--ease-out);
      &:hover { border-color: var(--color-border-glow); }
      &.active { border-color: var(--color-border-glow); background: var(--color-primary-bg); color: var(--color-primary); font-weight: var(--weight-medium); box-shadow: var(--glow-primary); }
    }
    .dark-input :deep(.el-input__wrapper) { background: var(--color-bg-alt); border: 1px solid var(--color-border); border-radius: var(--radius-md); transition: all var(--duration-fast) var(--ease-out); box-shadow: none; &:hover { border-color: var(--color-border-glow); } &.is-focus { border-color: var(--color-border-glow); box-shadow: var(--glow-primary); } }
    .dark-input :deep(.el-input__inner) { color: var(--color-text); font-size: var(--text-sm); &::placeholder { color: var(--color-text-muted); } }
    .submit-btn { width: 100%; height: 44px; font-size: var(--text-md); font-weight: var(--weight-semibold); letter-spacing: 0.15em; background: var(--gradient-primary); border: none; border-radius: var(--radius-md); box-shadow: var(--glow-primary); transition: all var(--duration-fast) var(--ease-out); &:hover { box-shadow: var(--glow-primary-lg); transform: translateY(-1px); } }
  }
  .form-footer { text-align: center; margin-top: var(--space-4); .footer-text { color: var(--color-text-muted); font-size: var(--text-sm); } .footer-link { font-size: var(--text-sm); font-weight: var(--weight-medium); margin-left: var(--space-1); } }
}

@media (max-width: 768px) {
  .register-card { flex-direction: column; }
  .brand-panel { width: 100%; padding: var(--space-5); .brand-hex { width: 56px; height: 56px; } .brand-title { font-size: 20px; } }
  .form-panel { padding: var(--space-5); }
}
</style>
