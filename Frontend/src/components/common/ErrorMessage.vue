<template>
  <div class="error-message" :class="errorClass">
    <div class="error-icon">
      <i v-if="type === 'error'">⚠️</i>
      <i v-else-if="type === 'warning'">⚡</i>
      <i v-else-if="type === 'info'">ℹ️</i>
    </div>
    <div class="error-content">
      <h3 v-if="title" class="error-title">{{ title }}</h3>
      <p class="error-text">{{ message }}</p>
      <div v-if="$slots.default" class="error-extra">
        <slot></slot>
      </div>
      <div v-if="retry || closable" class="error-actions">
        <button v-if="retry" @click="handleRetry" class="btn-retry">
          重试
        </button>
        <button v-if="closable" @click="handleClose" class="btn-close">
          关闭
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  title: {
    type: String,
    default: ''
  },
  message: {
    type: String,
    required: true
  },
  type: {
    type: String,
    default: 'error',
    validator: (value) => ['error', 'warning', 'info'].includes(value)
  },
  retry: {
    type: Boolean,
    default: false
  },
  closable: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['retry', 'close'])

const errorClass = computed(() => {
  return `error-${props.type}`
})

const handleRetry = () => {
  emit('retry')
}

const handleClose = () => {
  emit('close')
}
</script>

<style scoped>
.error-message {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 20px;
  border-radius: 8px;
  margin: 20px 0;
  border: 1px solid;
}

.error-error {
  background-color: rgba(239, 68, 68, 0.1);
  border-color: rgba(239, 68, 68, 0.3);
}

.error-warning {
  background-color: rgba(245, 158, 11, 0.1);
  border-color: rgba(245, 158, 11, 0.3);
}

.error-info {
  background-color: rgba(59, 130, 246, 0.1);
  border-color: rgba(59, 130, 246, 0.3);
}

.error-icon {
  font-size: 32px;
  flex-shrink: 0;
}

.error-content {
  flex: 1;
  min-width: 0;
}

.error-title {
  margin: 0 0 8px 0;
  font-size: 16px;
  font-weight: 600;
}

.error-error .error-title {
  color: var(--error-color, #ef4444);
}

.error-warning .error-title {
  color: var(--warning-color, #f59e0b);
}

.error-info .error-title {
  color: var(--info-color, #3b82f6);
}

.error-text {
  margin: 0;
  color: var(--text-secondary, #a1a1aa);
  font-size: 14px;
  line-height: 1.6;
}

.error-extra {
  margin-top: 12px;
  font-size: 14px;
  color: var(--text-tertiary, #71717a);
}

.error-actions {
  display: flex;
  gap: 12px;
  margin-top: 16px;
}

.btn-retry,
.btn-close {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-retry {
  background-color: var(--primary-color, #6366f1);
  color: white;
}

.btn-retry:hover {
  background-color: var(--primary-hover, #4f46e5);
}

.btn-close {
  background-color: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary, #a1a1aa);
  border: 1px solid var(--border-color, rgba(255, 255, 255, 0.1));
}

.btn-close:hover {
  background-color: rgba(255, 255, 255, 0.15);
}
</style>
