<template>
  <Teleport to="body">
    <Transition name="v-dialog-fade">
      <div v-if="modelValue" class="v-dialog-overlay" @click.self="close">
        <Transition name="v-dialog-bounce">
          <div v-if="modelValue" class="v-dialog" :style="{ maxWidth }">
            <div class="v-dialog__header">
              <span class="v-dialog__title">{{ title }}</span>
              <button class="v-dialog__close" @click="close">&times;</button>
            </div>
            <div class="v-dialog__body">
              <slot />
            </div>
            <div v-if="$slots.footer" class="v-dialog__footer">
              <slot name="footer" />
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
defineProps({
  modelValue: Boolean,
  title: { type: String, default: '' },
  maxWidth: { type: String, default: '480px' },
})
const emit = defineEmits(['update:modelValue'])
const close = () => emit('update:modelValue', false)
</script>

<style scoped lang="scss">
.v-dialog-overlay {
  position: fixed; inset: 0; z-index: 2000;
  background: rgba(28, 25, 21, 0.6);
  backdrop-filter: blur(4px);
  display: flex; align-items: center; justify-content: center;
  padding: 24px;
}

.v-dialog {
  background: var(--color-surface-elevated);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  width: 100%; max-height: 80vh;
  display: flex; flex-direction: column; overflow: hidden;

  &__header {
    display: flex; align-items: center; justify-content: space-between;
    padding: 20px 24px; border-bottom: 1px solid var(--color-border);
  }

  &__title { font-size: 18px; font-weight: 700; color: var(--color-text); }

  &__close {
    width: 32px; height: 32px; border-radius: 8px;
    border: none; background: var(--color-surface-hover);
    color: var(--color-text-muted); font-size: 20px;
    cursor: pointer; display: flex; align-items: center; justify-content: center;
    transition: all 0.15s;
    &:hover { background: rgba(184,96,90,0.10); color: #B8605A; }
  }

  &__body { padding: 24px; overflow-y: auto; flex: 1; }
  &__footer { padding: 16px 24px; border-top: 1px solid var(--color-border); display: flex; justify-content: flex-end; gap: 10px; }
}

// Transitions
.v-dialog-fade-enter-active, .v-dialog-fade-leave-active { transition: opacity 0.2s ease; }
.v-dialog-fade-enter-from, .v-dialog-fade-leave-to { opacity: 0; }

.v-dialog-bounce-enter-active { transition: all 0.3s var(--ease-bounce); }
.v-dialog-bounce-leave-active { transition: all 0.2s ease; }
.v-dialog-bounce-enter-from { opacity: 0; transform: scale(0.92) translateY(10px); }
.v-dialog-bounce-leave-to { opacity: 0; transform: scale(0.95); }
</style>
