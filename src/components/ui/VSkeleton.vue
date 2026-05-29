<template>
  <div class="v-skeleton" :class="[`v-skeleton--${variant}`]" :style="{ width, height }">
    <div class="v-skeleton__shimmer" />
  </div>
</template>

<script setup lang="ts">
defineProps({
  variant: { type: String as () => 'text' | 'card' | 'circle' | 'rect', default: 'text' },
  width: String,
  height: String,
})
</script>

<style scoped lang="scss">
.v-skeleton {
  background: var(--color-surface-hover);
  border-radius: 8px; position: relative; overflow: hidden;

  &--text { height: 16px; border-radius: 6px; width: 100%; }
  &--card { height: 180px; border-radius: var(--radius-lg); }
  &--circle { border-radius: 50%; }
  &--rect { border-radius: 8px; }

  &__shimmer {
    position: absolute; inset: 0;
    background: linear-gradient(90deg, transparent 25%, rgba(255,255,255,0.08) 50%, transparent 75%);
    background-size: 200% 100%;
    animation: shimmer 1.5s ease-in-out infinite;
  }
}

@keyframes shimmer {
  0% { background-position: -200% 0; }
  100% { background-position: 200% 0; }
}
</style>
