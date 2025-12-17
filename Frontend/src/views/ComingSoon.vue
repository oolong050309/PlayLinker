<template>
  <div class="coming-soon">
    <div class="coming-soon-content">
      <div class="icon">🚧</div>
      <h1 class="title">页面开发中</h1>
      <p class="message">{{ pageName }} 正在开发中，敬请期待...</p>
      <button @click="goBack" class="btn btn-primary">
        返回上一页
      </button>
    </div>
  </div>
</template>

<script setup>
import { useRouter, useRoute } from 'vue-router'
import { computed } from 'vue'

const router = useRouter()
const route = useRoute()

const pageName = computed(() => route.meta.title || '此页面')

const goBack = () => {
  router.back()
}
</script>

<style scoped>
.coming-soon {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background-color: var(--bg-primary);
  padding: 20px;
}

.coming-soon-content {
  text-align: center;
  max-width: 500px;
}

.icon {
  font-size: 80px;
  margin-bottom: 24px;
}

.title {
  font-size: 32px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.message {
  font-size: 16px;
  color: var(--text-secondary);
  margin-bottom: 32px;
}
</style>
