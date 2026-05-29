<template>
  <button
    class="v-btn"
    :class="[
      `v-btn--${variant}`,
      `v-btn--${color}`,
      `v-btn--${size}`,
      { 'v-btn--block': block, 'v-btn--loading': loading, 'v-btn--icon': icon }
    ]"
    :disabled="disabled || loading"
    @click="$emit('click', $event)"
  >
    <span v-if="loading" class="v-btn__spinner" />
    <slot v-else />
  </button>
</template>

<script setup lang="ts">
defineProps({
  variant: { type: String as () => 'filled' | 'outlined' | 'soft' | 'ghost', default: 'filled' },
  color: { type: String as () => 'coral' | 'mint' | 'purple' | 'sunny' | 'sky' | 'gray', default: 'coral' },
  size: { type: String as () => 'sm' | 'md' | 'lg', default: 'md' },
  block: Boolean,
  loading: Boolean,
  disabled: Boolean,
  icon: Boolean,
})
defineEmits(['click'])
</script>

<style scoped lang="scss">
.v-btn {
  display: inline-flex; align-items: center; justify-content: center; gap: 6px;
  border: none; cursor: pointer; font-family: var(--font-sans);
  font-weight: 600; letter-spacing: 0.01em;
  border-radius: var(--radius-md);
  transition: all 0.2s var(--ease-out);

  &:active { transform: scale(0.97); }
  &:disabled { opacity: 0.4; cursor: not-allowed; transform: none; box-shadow: none; }

  // Sizes
  &--sm { padding: 6px 14px; font-size: 12px; height: 28px; }
  &--md { padding: 10px 20px; font-size: 13px; height: 34px; }
  &--lg { padding: 12px 24px; font-size: 14px; height: 40px; }
  &--block { width: 100%; }
  &--icon { padding: 0; width: 34px; &.v-btn--sm { width: 28px; } &.v-btn--lg { width: 40px; } }

  // ── Filled (竹金微凸阴影) ──
  &--filled.v-btn--coral  { background: linear-gradient(135deg, var(--color-primary), #A08050); color: #1C1915; box-shadow: var(--shadow-3d-sm); &:hover { box-shadow: var(--shadow-3d-md); transform: translateY(-1px); } }
  &--filled.v-btn--mint   { background: linear-gradient(135deg, #7A8B5E, #6B7B4E); color: #fff; box-shadow: var(--shadow-3d-sm); &:hover { box-shadow: var(--shadow-3d-md); transform: translateY(-1px); } }
  &--filled.v-btn--purple { background: linear-gradient(135deg, #8B9A6E, #7A8B5E); color: #fff; box-shadow: var(--shadow-3d-sm); &:hover { box-shadow: var(--shadow-3d-md); transform: translateY(-1px); } }
  &--filled.v-btn--sunny  { background: linear-gradient(135deg, #C4945A, #B08040); color: #1C1915; box-shadow: var(--shadow-3d-sm); &:hover { box-shadow: var(--shadow-3d-md); transform: translateY(-1px); } }
  &--filled.v-btn--sky    { background: linear-gradient(135deg, #8A9BA8, #6B7B8D); color: #fff; box-shadow: var(--shadow-3d-sm); &:hover { box-shadow: var(--shadow-3d-md); transform: translateY(-1px); } }
  &--filled.v-btn--gray   { background: var(--color-surface); color: var(--color-text); border: 1px solid var(--color-border); &:hover { border-color: var(--color-border-glow); box-shadow: var(--shadow-3d-sm); } }

  // ── Outlined ──
  &--outlined { background: transparent; border: 1.5px solid; border-radius: var(--radius-md); }
  &--outlined.v-btn--coral  { border-color: var(--color-primary); color: var(--color-primary); &:hover { background: rgba(196,169,106,0.08); } }
  &--outlined.v-btn--mint   { border-color: #7A8B5E; color: #7A8B5E; &:hover { background: rgba(122,139,94,0.08); } }
  &--outlined.v-btn--purple { border-color: #8B9A6E; color: #8B9A6E; &:hover { background: rgba(139,154,110,0.08); } }
  &--outlined.v-btn--sunny  { border-color: #C4945A; color: #C4945A; &:hover { background: rgba(196,148,90,0.08); } }
  &--outlined.v-btn--sky    { border-color: #8A9BA8; color: #8A9BA8; &:hover { background: rgba(138,155,168,0.08); } }
  &--outlined.v-btn--gray   { border-color: var(--color-border); color: var(--color-text-secondary); &:hover { background: var(--color-surface-hover); } }

  // ── Soft ──
  &--soft { border: none; border-radius: var(--radius-sm); }
  &--soft.v-btn--coral  { background: rgba(196,169,106,0.10); color: var(--color-primary); &:hover { background: rgba(196,169,106,0.18); } }
  &--soft.v-btn--mint   { background: rgba(122,139,94,0.10); color: #7A8B5E; &:hover { background: rgba(122,139,94,0.18); } }
  &--soft.v-btn--purple { background: rgba(139,154,110,0.10); color: #8B9A6E; &:hover { background: rgba(139,154,110,0.18); } }
  &--soft.v-btn--sunny  { background: rgba(196,148,90,0.10); color: #C4945A; &:hover { background: rgba(196,148,90,0.18); } }
  &--soft.v-btn--sky    { background: rgba(138,155,168,0.10); color: #8A9BA8; &:hover { background: rgba(138,155,168,0.18); } }
  &--soft.v-btn--gray   { background: var(--color-surface-hover); color: var(--color-text-secondary); &:hover { background: var(--color-border); } }

  // ── Ghost ──
  &--ghost { background: transparent; border: none; }
  &--ghost.v-btn--coral  { color: var(--color-primary); &:hover { background: rgba(196,169,106,0.08); } }
  &--ghost.v-btn--mint   { color: #7A8B5E; &:hover { background: rgba(122,139,94,0.08); } }
  &--ghost.v-btn--purple { color: #8B9A6E; &:hover { background: rgba(139,154,110,0.08); } }
  &--ghost.v-btn--sunny  { color: #C4945A; &:hover { background: rgba(196,148,90,0.08); } }
  &--ghost.v-btn--sky    { color: #8A9BA8; &:hover { background: rgba(138,155,168,0.08); } }
  &--ghost.v-btn--gray   { color: var(--color-text-secondary); &:hover { background: var(--color-surface-hover); } }

  // Loading spinner
  &__spinner {
    width: 16px; height: 16px; border: 2px solid transparent;
    border-top-color: currentColor; border-radius: 50%;
    animation: spin 0.6s linear infinite;
  }
}

@keyframes spin { to { transform: rotate(360deg); } }
</style>
