<template>
  <div class="v-empty">
    <div class="v-empty__clay">
      <span class="v-empty__clay-inner">{{ emoji }}</span>
    </div>
    <div class="v-empty__title">{{ title }}</div>
    <div v-if="description" class="v-empty__desc">{{ description }}</div>
    <slot />
  </div>
</template>

<script setup lang="ts">
defineProps({
  title: { type: String, default: '暂无数据' },
  description: String,
  emoji: { type: String, default: '📭' },
})
</script>

<style scoped lang="scss">
.v-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  padding: 48px 24px; text-align: center;

  // 3D 黏土图标容器
  &__clay {
    width: 72px; height: 72px;
    border-radius: 20px;
    display: flex; align-items: center; justify-content: center;
    margin-bottom: 16px;
    background: linear-gradient(145deg, #f5efe5, #e8dcc8);
    box-shadow:
      6px 6px 14px rgba(0,0,0,0.08),
      -2px -2px 8px rgba(255,255,255,0.8),
      inset 3px 3px 6px rgba(255,255,255,0.5),
      inset -3px -3px 6px rgba(0,0,0,0.04);
    animation: clay-float 3s ease-in-out infinite;
  }
  &__clay-inner {
    font-size: 36px; line-height: 1;
    text-shadow:
      0 3px 2px rgba(0,0,0,0.15),
      0 -1px 1px rgba(255,255,255,0.6);
    filter:
      drop-shadow(0 3px 3px rgba(0,0,0,0.12))
      brightness(1.06)
      contrast(1.06)
      saturate(1.08);
  }

  &__title { font-size: 16px; font-weight: 600; color: var(--color-text-secondary); margin-bottom: 8px; }
  &__desc { font-size: 13px; color: var(--color-text-muted); max-width: 280px; line-height: 1.6; }
}

@keyframes clay-float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-6px); }
}
</style>
