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
    <span v-if="emoji" class="v-chip__clay" :style="clayStyle">
      <span class="v-chip__clay-inner">{{ emoji }}</span>
    </span>
    <span class="v-chip__label"><slot /></span>
    <span v-if="count !== undefined" class="v-chip__count">{{ count }}</span>
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps({
  color: { type: String as () => 'coral' | 'mint' | 'purple' | 'sunny' | 'sky' | 'gray', default: 'gray' },
  customColor: String,
  active: Boolean,
  emoji: String,
  count: [Number, String],
  size: { type: String as () => 'sm' | 'md', default: 'md' },
})

defineEmits(['click'])

const clayStyle = computed(() => {
  const c = props.customColor || '#999'
  return {
    '--clay-color': c,
    '--clay-light': lighten(c, 30),
    '--clay-shadow': darken(c, 40),
  }
})

// Simple color manipulation for CSS custom properties
function lighten(hex: string, percent: number): string {
  const num = parseInt(hex.replace('#', ''), 16)
  const r = Math.min(255, (num >> 16) + Math.round(255 * percent / 100))
  const g = Math.min(255, ((num >> 8) & 0x00FF) + Math.round(255 * percent / 100))
  const b = Math.min(255, (num & 0x0000FF) + Math.round(255 * percent / 100))
  return `#${(r << 16 | g << 8 | b).toString(16).padStart(6, '0')}`
}

function darken(hex: string, percent: number): string {
  const num = parseInt(hex.replace('#', ''), 16)
  const r = Math.max(0, (num >> 16) - Math.round(255 * percent / 100))
  const g = Math.max(0, ((num >> 8) & 0x00FF) - Math.round(255 * percent / 100))
  const b = Math.max(0, (num & 0x0000FF) - Math.round(255 * percent / 100))
  return `#${(r << 16 | g << 8 | b).toString(16).padStart(6, '0')}`
}
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

  // 3D 黏土图标容器
  &__clay {
    width: 30px; height: 30px; flex-shrink: 0;
    border-radius: 10px;
    display: flex; align-items: center; justify-content: center;
    background: linear-gradient(145deg, var(--clay-light), var(--clay-color));
    box-shadow:
      3px 3px 8px color-mix(in srgb, var(--clay-shadow) 40%, transparent),
      -1px -1px 3px rgba(255,255,255,0.25),
      inset 2px 2px 3px rgba(255,255,255,0.35),
      inset -2px -2px 4px rgba(0,0,0,0.1);
    transition: all 0.25s var(--ease-bounce);
  }
  &__clay-inner {
    font-size: 16px;
    line-height: 1;
    text-shadow:
      0 2px 1px rgba(0,0,0,0.2),
      0 -1px 1px rgba(255,255,255,0.5);
    filter:
      drop-shadow(0 2px 2px rgba(0,0,0,0.18))
      brightness(1.08)
      contrast(1.08)
      saturate(1.1);
    transition: transform 0.25s var(--ease-bounce), filter 0.25s var(--ease-bounce);
  }

  &:hover &__clay {
    transform: translateY(-1px);
    box-shadow:
      4px 4px 12px color-mix(in srgb, var(--clay-shadow) 45%, transparent),
      -2px -2px 4px rgba(255,255,255,0.3),
      inset 2px 2px 3px rgba(255,255,255,0.4),
      inset -2px -2px 5px rgba(0,0,0,0.12);
  }
  &:hover &__clay-inner {
    transform: scale(1.1);
  }

  &--sm &__clay {
    width: 24px; height: 24px; border-radius: 8px;
  }
  &--sm &__clay-inner { font-size: 13px; }

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
