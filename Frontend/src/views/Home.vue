<template>
  <div class="app-layout">
    <Sidebar 
      :collapsed="sidebarCollapsed" 
      :mobile-open="mobileMenuOpen"
      @update:collapsed="sidebarCollapsed = $event"
      @close-mobile="mobileMenuOpen = false"
    />
    
    <div class="main-content" :class="{ 'sidebar-collapsed': sidebarCollapsed }">
      <!-- 移动端菜单按钮 -->
      <button 
        v-if="isMobile" 
        class="mobile-menu-btn"
        @click="mobileMenuOpen = true"
        aria-label="打开菜单"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M3 12h18M3 6h18M3 18h18"/>
        </svg>
      </button>

      <div class="content-wrapper">
      <router-view />
      </div>
    </div>

    <!-- 移动端遮罩层 -->
    <div 
      v-if="mobileMenuOpen" 
      class="mobile-overlay"
      @click="mobileMenuOpen = false"
    ></div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import Sidebar from '@/components/common/Sidebar.vue'

const sidebarCollapsed = ref(false)
const mobileMenuOpen = ref(false)
const isMobile = ref(false)

const checkMobile = () => {
  isMobile.value = window.innerWidth <= 768
  if (!isMobile.value) {
    mobileMenuOpen.value = false
  }
}

onMounted(() => {
  checkMobile()
  window.addEventListener('resize', checkMobile)
})

onUnmounted(() => {
  window.removeEventListener('resize', checkMobile)
})
</script>

<style scoped>
.app-layout {
  display: flex;
  min-height: 100vh;
  background-color: var(--bg-primary);
}

.main-content {
  margin-left: 260px;
  flex: 1;
  transition: margin-left 0.3s ease;
  min-height: 100vh;
  position: relative;
}

.main-content.sidebar-collapsed {
  margin-left: 80px;
}

.mobile-menu-btn {
  position: fixed;
  top: var(--spacing-md);
  left: var(--spacing-md);
  z-index: 998;
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  padding: var(--spacing-sm);
  cursor: pointer;
  display: none;
  align-items: center;
  justify-content: center;
  transition: all 0.3s ease;
  box-shadow: var(--shadow-md);
}

.mobile-menu-btn:hover {
  background: var(--bg-secondary);
  border-color: var(--border-color-strong);
}

.content-wrapper {
  padding: var(--spacing-lg);
  max-width: 1400px;
  margin: 0 auto;
}

.mobile-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 999;
  display: none;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .main-content {
    margin-left: 0;
  }

  .main-content.sidebar-collapsed {
    margin-left: 0;
}

  .mobile-menu-btn {
    display: flex;
}

  .mobile-overlay {
    display: block;
  }

  .content-wrapper {
    padding: var(--spacing-md);
    padding-top: calc(var(--spacing-md) + 50px);
  }
}
</style>

