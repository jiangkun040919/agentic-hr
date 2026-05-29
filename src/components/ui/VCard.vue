<template>
  <div
    class="v-card"
    :class="[
      { 'v-card--hover': hover, 'v-card--glass': glass, 'v-card--accent': accent }
    ]"
    :style="accentColor ? `--accent-color: ${accentColor}` : undefined"
  >
    <div v-if="$slots.header" class="v-card__header">
      <slot name="header" />
    </div>
    <div class="v-card__body">
      <slot />
    </div>
    <div v-if="$slots.footer" class="v-card__footer">
      <slot name="footer" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps({
  hover: { type: Boolean, default: true },
  glass: Boolean,
  accent: Boolean,
  accentColor: String,
})
</script>

<style scoped lang="scss">
.v-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-card);
  transition: all 0.2s var(--ease-bounce);
  overflow: hidden;

  &--hover:hover {
    transform: translateY(-3px);
    box-shadow: 0 8px 24px rgba(0,0,0,0.08), 0 0 0 1px var(--color-border-glow);
    border-color: var(--color-border-glow);
  }

  &--glass {
    background: rgba(255,255,255,0.06);
    backdrop-filter: blur(16px);
    -webkit-backdrop-filter: blur(16px);
  }

  &--accent {
    border-top: 3px solid var(--accent-color, var(--color-primary));
  }

  &__header {
    padding: 16px 20px;
    border-bottom: 1px solid var(--color-border);
    font-weight: 600;
    font-size: 15px;
  }

  &__body {
    padding: 20px;
  }

  &__footer {
    padding: 12px 20px;
    border-top: 1px solid var(--color-border);
  }
}
</style>
