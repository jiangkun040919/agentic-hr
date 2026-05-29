<template>
  <div class="v-select" :class="[`v-select--${size}`, { 'v-select--open': open, 'v-select--focus': focused }]">
    <div class="v-select__trigger" @click="toggle" tabindex="0" @focus="focused = true" @blur="focused = false; open = false">
      <span class="v-select__value" :class="{ 'v-select__value--placeholder': !modelValue }">
        {{ selectedLabel || placeholder }}
      </span>
      <span class="v-select__arrow" :class="{ 'v-select__arrow--up': open }">&#9662;</span>
    </div>
    <Transition name="v-select-dropdown">
      <div v-if="open" class="v-select__dropdown">
        <div
          v-for="opt in options"
          :key="opt.value"
          class="v-select__option"
          :class="{ 'v-select__option--selected': modelValue === opt.value }"
          @mousedown.prevent="select(opt)"
        >
          {{ opt.label }}
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps({
  modelValue: { type: [String, Number], default: '' },
  options: { type: Array as () => { label: string; value: string | number }[], default: () => [] },
  placeholder: { type: String, default: '请选择' },
  size: { type: String as () => 'sm' | 'md' | 'lg', default: 'md' },
})
const emit = defineEmits(['update:modelValue'])

const open = ref(false)
const focused = ref(false)
const toggle = () => { open.value = !open.value }
const select = (opt: { label: string; value: string | number }) => {
  emit('update:modelValue', opt.value)
  open.value = false
}

const selectedLabel = computed(() => props.options.find(o => o.value === props.modelValue)?.label)
</script>

<style scoped lang="scss">
.v-select {
  position: relative;

  &__trigger {
    display: flex; align-items: center; justify-content: space-between;
    background: var(--color-surface); border: 1.5px solid var(--color-border);
    border-radius: var(--radius-md); cursor: pointer;
    transition: all 0.2s var(--ease-out); outline: none;
    &:hover { border-color: var(--color-border-glow); }
  }

  &--focus &__trigger,
  &--open &__trigger { border-color: var(--color-primary); box-shadow: 0 0 0 3px rgba(196,169,106,0.12); }

  &--sm &__trigger { height: 34px; padding: 0 10px; font-size: 13px; }
  &--md &__trigger { height: 40px; padding: 0 14px; font-size: 14px; }
  &--lg &__trigger { height: 48px; padding: 0 18px; font-size: 16px; }

  &__value { flex: 1; color: var(--color-text); &--placeholder { color: var(--color-text-muted); } }
  &__arrow { font-size: 10px; color: var(--color-text-muted); transition: transform 0.2s; &--up { transform: rotate(180deg); } }

  &__dropdown {
    position: absolute; top: calc(100% + 4px); left: 0; right: 0;
    background: var(--color-surface-elevated); border: 1px solid var(--color-border);
    border-radius: var(--radius-md); box-shadow: 0 8px 24px rgba(0,0,0,0.2);
    z-index: 100; padding: 4px; max-height: 240px; overflow-y: auto;
  }

  &__option {
    padding: 8px 12px; border-radius: 8px; cursor: pointer;
    font-size: 14px; color: var(--color-text-secondary);
    transition: all 0.15s;
    &:hover { background: var(--color-primary-bg); color: var(--color-primary); }
    &--selected { color: var(--color-primary); font-weight: 600; background: var(--color-primary-bg); }
  }
}

.v-select-dropdown-enter-active { transition: all 0.2s var(--ease-bounce); }
.v-select-dropdown-leave-active { transition: all 0.15s ease; }
.v-select-dropdown-enter-from, .v-select-dropdown-leave-to { opacity: 0; transform: translateY(-4px) scale(0.98); }
</style>
