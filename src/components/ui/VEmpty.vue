<template>
  <div class="v-empty">
    <div class="v-empty__icon">{{ emoji }}</div>
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

  &__icon { font-size: 48px; margin-bottom: 16px; animation: float 3s ease-in-out infinite; }
  &__title { font-size: 16px; font-weight: 600; color: var(--color-text-secondary); margin-bottom: 8px; }
  &__desc { font-size: 13px; color: var(--color-text-muted); max-width: 280px; line-height: 1.6; }
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-8px); }
}
</style>
