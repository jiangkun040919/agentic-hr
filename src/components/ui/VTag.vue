<template>
  <span
    class="v-tag"
    :class="[`v-tag--${color}`, `v-tag--${size}`, { 'v-tag--round': round }]"
  >
    <slot />
  </span>
</template>

<script setup lang="ts">
defineProps({
  color: { type: String as () => 'coral' | 'mint' | 'purple' | 'sunny' | 'sky' | 'gray', default: 'gray' },
  size: { type: String as () => 'sm' | 'md' | 'lg', default: 'md' },
  round: { type: Boolean, default: true },
})
</script>

<style scoped lang="scss">
.v-tag {
  display: inline-flex; align-items: center; gap: 4px;
  font-weight: 600; letter-spacing: 0.02em; white-space: nowrap;
  transition: all 0.2s var(--ease-out);

  &--sm { padding: 2px 8px; font-size: 11px; }
  &--md { padding: 3px 12px; font-size: 12px; }
  &--lg { padding: 5px 16px; font-size: 13px; }
  &--round { border-radius: var(--radius-sm); }
  &:not(.v-tag--round) { border-radius: 4px; }

  &--coral  { background: rgba(196,169,106,0.10); color: var(--color-primary); }
  &--mint   { background: rgba(122,139,94,0.10); color: #7A8B5E; }
  &--purple { background: rgba(139,154,110,0.10); color: #8B9A6E; }
  &--sunny  { background: rgba(196,148,90,0.10); color: #C4945A; }
  &--sky    { background: rgba(138,155,168,0.10); color: #8A9BA8; }
  &--gray   { background: var(--color-surface-hover); color: var(--color-text-secondary); }
}
</style>
