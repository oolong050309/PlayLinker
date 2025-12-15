<template>
  <div class="loading-spinner" :class="{ 'loading-fullscreen': fullscreen }">
    <div class="spinner-container">
      <div class="spinner" :style="spinnerStyle"></div>
      <p v-if="text" class="loading-text">{{ text }}</p>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  text: {
    type: String,
    default: '加载中...'
  },
  size: {
    type: String,
    default: 'medium',
    validator: (value) => ['small', 'medium', 'large'].includes(value)
  },
  color: {
    type: String,
    default: '#6366f1'
  },
  fullscreen: {
    type: Boolean,
    default: false
  }
})

const spinnerStyle = computed(() => {
  const sizes = {
    small: '24px',
    medium: '40px',
    large: '60px'
  }
  
  return {
    width: sizes[props.size],
    height: sizes[props.size],
    borderTopColor: props.color
  }
})
</script>

<style scoped>
.loading-spinner {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
}

.loading-fullscreen {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 9999;
}

.spinner-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
}

.spinner {
  border: 4px solid rgba(99, 102, 241, 0.1);
  border-top-color: #6366f1;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.loading-text {
  color: var(--text-secondary, #a1a1aa);
  font-size: 14px;
  margin: 0;
}

.loading-fullscreen .loading-text {
  color: #ffffff;
}
</style>
