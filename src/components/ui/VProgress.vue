<template>
  <div class="v-progress" :class="[`v-progress--${size}`]">
    <div class="v-progress__track">
      <div
        class="v-progress__fill"
        :class="[`v-progress__fill--${color}`]"
        :style="{ width: `${percentage}%` }"
      />
    </div>
    <span v-if="showLabel" class="v-progress__label">{{ percentage }}%</span>
  </div>
</template>

<script setup lang="ts">
defineProps({
  percentage: { type: Number, default: 0 },
  color: { type: String as () => 'coral' | 'mint' | 'purple' | 'sunny' | 'sky', default: 'coral' },
  size: { type: String as () => 'sm' | 'md' | 'lg', default: 'md' },
  showLabel: Boolean,
})
</script>

<style scoped lang="scss">
.v-progress {
  display: flex; align-items: center; gap: 10px; width: 100%;

  &__track {
    flex: 1; border-radius: var(--radius-full); overflow: hidden;
    background: var(--color-bg-alt);
  }

  &--sm &__track { height: 4px; }
  &--md &__track { height: 6px; }
  &--lg &__track { height: 10px; }

  &__fill {
    height: 100%; border-radius: var(--radius-full);
    transition: width 0.6s var(--ease-out);
    position: relative;

    &::after {
      content: ''; position: absolute; top: 0; left: 0; right: 0; height: 50%;
      background: linear-gradient(180deg, rgba(255,255,255,0.25), transparent);
      border-radius: inherit;
    }

    &--coral  { background: linear-gradient(90deg, var(--color-primary), #A08050); }
    &--mint   { background: linear-gradient(90deg, #7A8B5E, #6B7B4E); }
    &--purple { background: linear-gradient(90deg, #8B9A6E, #7A8B5E); }
    &--sunny  { background: linear-gradient(90deg, #C4945A, #B08040); }
    &--sky    { background: linear-gradient(90deg, #8A9BA8, #6B7B8D); }
  }

  &__label {
    font-size: 12px; font-weight: 700; color: var(--color-text-secondary);
    min-width: 32px; text-align: right;
  }
}
</style>
