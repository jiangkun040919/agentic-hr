<template>
  <div class="login-page">
    <StarfieldBackground />
    <div class="bg-orb orb-1" /><div class="bg-orb orb-2" /><div class="bg-orb orb-3" />

    <div class="login-container">
      <div class="login-card glass-panel glow-border">
        <div class="brand-panel">
          <div class="brand-glow" />
          <div class="brand-content">
            <div class="brand-icon">
              <div class="brand-hex"><el-icon :size="36"><MagicStick /></el-icon></div>
            </div>
            <h1 class="brand-title neon-text">AI 智能招聘</h1>
            <p class="brand-desc">企业级智能招聘管理平台<br/>驱动人才决策，释放AI力量</p>
            <div class="brand-features">
              <div class="feature-item"><span class="feature-dot" /><span>AI 简历智能解析</span></div>
              <div class="feature-item"><span class="feature-dot" /><span>多维度人才匹配</span></div>
              <div class="feature-item"><span class="feature-dot" /><span>智能面试评估</span></div>
            </div>
          </div>
        </div>

        <div class="form-panel">
          <div class="form-header">
            <h2 class="form-title">欢迎回来</h2>
            <p class="form-subtitle">登录您的账号以继续</p>
          </div>
          <el-form ref="formRef" :model="form" :rules="rules" class="login-form" @submit.prevent="handleLogin">
            <el-form-item prop="username">
              <el-input v-model="form.username" placeholder="请输入用户名" :prefix-icon="User" size="large" class="dark-input" />
            </el-form-item>
            <el-form-item prop="password">
              <el-input v-model="form.password" type="password" placeholder="请输入密码" :prefix-icon="Lock" size="large" show-password class="dark-input" @keyup.enter="handleLogin" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" size="large" class="submit-btn" :loading="loading" @click="handleLogin">
                <span v-if="!loading">登 录</span>
              </el-button>
            </el-form-item>
          </el-form>
          <div class="form-footer">
            <span class="footer-text">还没有账号？</span>
            <router-link to="/register" class="footer-link gradient-text">立即注册</router-link>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { User, Lock, InfoFilled, MagicStick } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import StarfieldBackground from '@/components/StarfieldBackground.vue'

const router = useRouter()
const userStore = useUserStore()
const formRef = ref<FormInstance>()
const loading = ref(false)
const form = reactive({ username: 'hr_admin', password: '123456' })
const rules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, min: 6, message: '密码至少6位', trigger: 'blur' }],
}

const handleLogin = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    loading.value = true
    try {
      await userStore.login({ username: form.username, password: form.password })
      ElMessage.success('登录成功')
      if (userStore.isHR) router.push('/admin/dashboard')
      else router.push('/')
    } catch {
      ElMessage.error('登录失败，请检查用户名和密码')
    } finally {
      loading.value = false
    }
  })
}

</script>

<style scoped lang="scss">
.login-page {
  min-height: 100vh; display: flex; align-items: center; justify-content: center;
  background: var(--color-bg); position: relative; overflow: hidden;
}
.bg-orb { position: fixed; border-radius: 50%; pointer-events: none; z-index: 0; filter: blur(80px); opacity: 0.15; }
.orb-1 { width: 400px; height: 400px; background: radial-gradient(circle, #6366F1 0%, transparent 70%); top: -100px; left: -100px; animation: floatDrift 8s ease-in-out infinite; }
.orb-2 { width: 350px; height: 350px; background: radial-gradient(circle, #A855F7 0%, transparent 70%); bottom: -80px; right: -80px; animation: floatDrift 10s ease-in-out infinite reverse; }
.orb-3 { width: 250px; height: 250px; background: radial-gradient(circle, #06B6D4 0%, transparent 70%); top: 50%; left: 60%; animation: floatDrift 12s ease-in-out infinite 2s; }

.login-container { z-index: 1; width: 100%; max-width: 900px; padding: var(--space-5); }
.login-card { display: flex; border-radius: var(--radius-xl); overflow: hidden; min-height: 520px; }

.brand-panel {
  width: 44%; background: linear-gradient(180deg, rgba(19, 19, 22, 0.95) 0%, rgba(13, 13, 18, 0.98) 100%);
  display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden;
  .brand-glow { position: absolute; width: 200px; height: 200px; border-radius: 50%; background: radial-gradient(circle, rgba(99, 102, 241, 0.3) 0%, transparent 70%); top: 50%; left: 50%; transform: translate(-50%, -50%); animation: neonPulse 3s ease-in-out infinite; }
  .brand-content { position: relative; z-index: 1; text-align: center; padding: var(--space-8); }
  .brand-icon { margin-bottom: var(--space-6); }
  .brand-hex { width: 72px; height: 72px; margin: 0 auto; background: var(--gradient-primary); clip-path: polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%); display: flex; align-items: center; justify-content: center; color: #fff; box-shadow: var(--glow-primary-lg); }
  .brand-title { font-size: 28px; font-weight: var(--weight-bold); margin-bottom: var(--space-3); }
  .brand-desc { font-size: var(--text-sm); color: var(--color-text-muted); line-height: 1.8; margin-bottom: var(--space-8); }
  .brand-features { text-align: left; display: inline-flex; flex-direction: column; gap: var(--space-3); }
  .feature-item { display: flex; align-items: center; gap: var(--space-2); font-size: var(--text-sm); color: var(--color-text-secondary); .feature-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--gradient-primary); box-shadow: 0 0 6px rgba(99, 102, 241, 0.4); } }
}

.form-panel {
  flex: 1; padding: var(--space-10); display: flex; flex-direction: column; justify-content: center; background: var(--color-surface);
  .form-header { margin-bottom: var(--space-8); .form-title { font-size: 24px; font-weight: var(--weight-bold); color: var(--color-text); margin-bottom: var(--space-1); } .form-subtitle { font-size: var(--text-sm); color: var(--color-text-muted); } }
  .login-form {
    .dark-input :deep(.el-input__wrapper) { background: var(--color-bg-alt); border: 1px solid var(--color-border); border-radius: var(--radius-md); transition: all var(--duration-fast) var(--ease-out); box-shadow: none; &:hover { border-color: var(--color-border-glow); } &.is-focus { border-color: var(--color-border-glow); box-shadow: var(--glow-primary); } }
    .dark-input :deep(.el-input__inner) { color: var(--color-text); &::placeholder { color: var(--color-text-muted); } }
    .submit-btn { width: 100%; height: 44px; font-size: var(--text-md); font-weight: var(--weight-semibold); letter-spacing: 0.15em; background: var(--gradient-primary); border: none; border-radius: var(--radius-md); box-shadow: var(--glow-primary); transition: all var(--duration-fast) var(--ease-out); &:hover { box-shadow: var(--glow-primary-lg); transform: translateY(-1px); } }
  }
  .form-footer { text-align: center; margin-top: var(--space-6); .footer-text { color: var(--color-text-muted); font-size: var(--text-sm); } .footer-link { font-size: var(--text-sm); font-weight: var(--weight-medium); margin-left: var(--space-1); } }
}

@media (max-width: 768px) {
  .login-card { flex-direction: column; }
  .brand-panel { width: 100%; padding: var(--space-6); .brand-title { font-size: 22px; } }
  .form-panel { padding: var(--space-6); }
}
</style>
