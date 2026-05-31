<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const userStore = useUserStore()

// 页面刷新时验证 token 是否仍然有效
onMounted(async () => {
  if (userStore.isLoggedIn) {
    try {
      await userStore.fetchUserInfo()
    } catch {
      // token 失效，fetchUserInfo 内部不会清 token，
      // 但后续 API 调用会触发 401 → 请求拦截器会清 token 并跳转登录
    }
  }
})
</script>

<template>
  <RouterView v-slot="{ Component, route }">
    <Transition name="page" mode="out-in">
      <component :is="Component" :key="route.path" />
    </Transition>
  </RouterView>
</template>

<style>
#app {
  width: 100%;
  height: 100%;
  background: var(--color-bg);
  color: var(--color-text);
}

.page-enter-active,
.page-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.page-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
.page-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
