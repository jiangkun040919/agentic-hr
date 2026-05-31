<template>
  <button
    class="v-chip"
    :class="[
      customColor ? '' : `v-chip--${color}`,
      { 'v-chip--active': active, 'v-chip--sm': size === 'sm', 'v-chip--custom': !!customColor }
    ]"
    :style="customColor ? `background:${customColor};border-color:${customColor};color:#fff;` : ''"
    @click="$emit('click', $event)"
  >
    <span v-if="emoji" class="v-chip__emoji">{{ emoji }}</span>
    <span class="v-chip__label"><slot /></span>
    <span v-if="count !== undefined" class="v-chip__count">{{ count }}</span>
  </button>
</template>

<script setup lang="ts">
defineProps({
  color: { type: String as () => 'coral' | 'mint' | 'purple' | 'sunny' | 'sky' | 'gray', default: 'gray' },
  customColor: String,
  active: Boolean,
  emoji: String,
  count: [Number, String],
  size: { type: String as () => 'sm' | 'md', default: 'md' },
})
defineEmits(['click'])
</script>

<style scoped lang="scss">
.v-chip {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 6px 16px; border-radius: var(--radius-full);
  border: 1.5px solid var(--color-border);
  background: var(--color-surface);
  cursor: pointer; font-family: var(--font-sans);
  font-size: 13px; font-weight: 500;
  color: var(--color-text-secondary);
  transition: all 0.2s var(--ease-out);

  &:hover { border-color: var(--color-border-glow); }
  &:active { transform: scale(0.97); }

  &--sm { padding: 4px 12px; font-size: 12px; }

  &__emoji { font-size: 15px; }
  &__count {
    font-size: 11px; font-weight: 700;
    background: var(--color-bg-alt); color: var(--color-text-muted);
    padding: 1px 6px; border-radius: 8px;
  }

  // Active states (柔光深空调)
  &--active {
    border-color: transparent; color: #fff; font-weight: 600;
    .v-chip__count { background: rgba(255,255,255,0.2); color: #fff; }
  }
  // Custom color variant
  &--custom {
    font-weight: 500;
    .v-chip__count { background: rgba(255,255,255,0.25); color: #fff; }
    &.v-chip--active {
      filter: brightness(1.15);
      box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    }
  }
  &--active.v-chip--coral  { background: linear-gradient(135deg, var(--color-primary), #A08050); box-shadow: var(--shadow-3d-sm); color: #1C1915; .v-chip__count { background: rgba(0,0,0,0.15); color: #1C1915; } }
  &--active.v-chip--mint   { background: linear-gradient(135deg, #7A8B5E, #6B7B4E); box-shadow: var(--shadow-3d-sm); }
  &--active.v-chip--purple { background: linear-gradient(135deg, #8B9A6E, #7A8B5E); box-shadow: var(--shadow-3d-sm); }
  &--active.v-chip--sunny  { background: linear-gradient(135deg, #C4945A, #B08040); box-shadow: var(--shadow-3d-sm); color: #1C1915; .v-chip__count { background: rgba(0,0,0,0.15); color: #1C1915; } }
  &--active.v-chip--sky    { background: linear-gradient(135deg, #8A9BA8, #6B7B8D); box-shadow: var(--shadow-3d-sm); }
  &--active.v-chip--gray   { background: var(--color-text); color: var(--color-bg); }
}
</style>
