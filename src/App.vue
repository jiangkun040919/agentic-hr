<script setup lang="ts">
import { RouterView } from 'vue-router'
import { useTheme } from '@/utils/theme'

const { theme } = useTheme()
</script>

<template>
  <RouterView :key="theme" />
</template>

<style>
#app {
  width: 100%;
  height: 100%;
  background: var(--color-bg);
  color: var(--color-text);
}

/* 全局路由过渡动画 */
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition: opacity 0.25s ease, transform 0.25s ease;
}
.fade-slide-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
.fade-slide-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
</style>
