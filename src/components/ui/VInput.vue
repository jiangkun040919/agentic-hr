<template>
  <div class="v-input" :class="[`v-input--${size}`, { 'v-input--focus': focused, 'v-input--error': error }]">
    <span v-if="$slots.prefix" class="v-input__prefix"><slot name="prefix" /></span>
    <input
      ref="inputRef"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      class="v-input__inner"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @focus="focused = true"
      @blur="focused = false"
    />
    <span v-if="$slots.suffix" class="v-input__suffix"><slot name="suffix" /></span>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps({
  modelValue: { type: [String, Number], default: '' },
  type: { type: String, default: 'text' },
  placeholder: String,
  disabled: Boolean,
  error: Boolean,
  size: { type: String as () => 'sm' | 'md' | 'lg', default: 'md' },
})
defineEmits(['update:modelValue'])

const focused = ref(false)
const inputRef = ref<HTMLInputElement>()
defineExpose({ inputRef })
</script>

<style scoped lang="scss">
.v-input {
  display: flex; align-items: center; gap: 8px;
  background: var(--color-surface);
  border: 1.5px solid var(--color-border);
  border-radius: var(--radius-md);
  transition: all 0.2s var(--ease-out);
  padding: 0 14px;

  &:hover { border-color: var(--color-border-glow); }
  &--focus { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.12); }
  &--error { border-color: var(--color-danger); box-shadow: 0 0 0 3px rgba(196,169,106,0.12); }

  &--sm { height: 34px; font-size: 13px; }
  &--md { height: 40px; font-size: 14px; }
  &--lg { height: 48px; font-size: 16px; padding: 0 18px; }

  &__inner {
    flex: 1; border: none; outline: none; background: transparent;
    color: var(--color-text); font-family: var(--font-sans);
    &::placeholder { color: var(--color-text-muted); }
    &:disabled { cursor: not-allowed; opacity: 0.6; }
  }

  &__prefix, &__suffix { color: var(--color-text-muted); display: flex; flex-shrink: 0; }
}
</style>
