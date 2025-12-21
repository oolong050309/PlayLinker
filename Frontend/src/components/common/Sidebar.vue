<template>
  <aside 
    :class="[
      'sidebar', 
      { 'sidebar-collapsed': isCollapsed }, 
      { 'mobile-open': mobileOpen }
    ]"
  >
    <div class="sidebar-glow"></div>

    <div class="sidebar-header">
      <div class="logo-area">
        <div class="logo-icon">
          <Gamepad2 class="icon" />
        </div>
        <div class="logo-text" :class="{ 'fade-out': isCollapsed }">
          <h1>PlayLinker</h1>
        </div>
      </div>
      
      <button 
        class="toggle-btn" 
        @click="toggleCollapse"
        :aria-label="isCollapsed ? '展开' : '折叠'"
      >
        <ChevronLeft v-if="!isCollapsed" class="icon" />
        <ChevronRight v-else class="icon" />
      </button>
    </div>

    <div class="user-card-wrapper">
      <div class="user-card" :class="{ 'compact': isCollapsed }">
        <div class="avatar-ring">
          <img 
            :src="displayUserAvatar" 
            alt="User" 
            class="user-avatar"
            @error="handleAvatarError"
          >
          <div class="status-dot"></div>
        </div>
        <div class="user-meta" :class="{ 'fade-out': isCollapsed }">
          <span class="username">{{ displayUserName }}</span>
          <span class="user-lvl">Lv. {{ currentUser?.level || 1 }}</span>
        </div>
      </div>
    </div>

    <nav class="sidebar-nav custom-scrollbar">
      <ul class="nav-menu">
        <li v-for="item in mainMenuItems" :key="item.path" class="nav-item">
          <router-link 
            :to="item.path" 
            class="nav-link"
            :class="{ 'active': isActive(item.path) }"
            @click="handleNavClick"
          >
            <div class="nav-icon-wrapper">
              <component :is="item.icon" class="icon" />
            </div>
            <span class="nav-text" :class="{ 'fade-out': isCollapsed }">{{ item.label }}</span>
            <div class="active-indicator" v-if="isActive(item.path)"></div>
          </router-link>
        </li>
        
        <li class="nav-divider"></li>
        
        <li v-for="item in extraMenuItems" :key="item.path" class="nav-item">
          <router-link 
            :to="item.path" 
            class="nav-link"
            :class="{ 'active': isActive(item.path) }"
            @click="handleNavClick"
          >
            <div class="nav-icon-wrapper">
              <component :is="item.icon" class="icon" />
            </div>
            <span class="nav-text" :class="{ 'fade-out': isCollapsed }">{{ item.label }}</span>
            <div class="active-indicator" v-if="isActive(item.path)"></div>
          </router-link>
        </li>
      </ul>
    </nav>

    <div class="sidebar-footer">
      <button 
        class="logout-btn"
        @click="handleLogout"
        :disabled="isLoggingOut"
      >
        <LogOut class="icon" />
        <span class="nav-text" :class="{ 'fade-out': isCollapsed }">
          {{ isLoggingOut ? '退出中...' : '退出登录' }}
        </span>
      </button>
    </div>
  </aside>
</template>

<script setup>
import { ref, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Gamepad2, Compass, List, Trophy, Library, BarChart2, Newspaper,
  ChevronLeft, ChevronRight, Settings, Bell, Link, LogOut, Shield
} from 'lucide-vue-next'
import { authApi } from '@/api/auth'

const props = defineProps({
  collapsed: { type: Boolean, default: false },
  mobileOpen: { type: Boolean, default: false },
  userAvatar: { type: String, default: '' },
  userName: { type: String, default: '' }
})

const emit = defineEmits(['update:collapsed', 'close-mobile'])
const route = useRoute()
const router = useRouter()
const isCollapsed = ref(props.collapsed)
const defaultAvatar = 'https://picsum.photos/200/200?random=1'
const isLoggingOut = ref(false)

// 从 sessionStorage 读取用户信息
const currentUser = computed(() => {
  try {
    const userStr = sessionStorage.getItem('user')
    if (userStr) {
      return JSON.parse(userStr)
    }
  } catch (e) {
    console.warn('解析用户信息失败:', e)
  }
  return null
})

// 计算显示的用户名和头像
const displayUserName = computed(() => {
  return props.userName || currentUser.value?.username || currentUser.value?.email || 'Guest Player'
})

const displayUserAvatar = computed(() => {
  // 优先使用 props，然后是 avatarUrl，最后是 avatar，都没有则使用默认头像
  const avatar = props.userAvatar || currentUser.value?.avatarUrl || currentUser.value?.avatar
  if (avatar && typeof avatar === 'string' && avatar.trim() !== '') {
    const trimmedAvatar = avatar.trim()
    if (trimmedAvatar.startsWith('http://') || trimmedAvatar.startsWith('https://') || trimmedAvatar.startsWith('data:')) {
      return trimmedAvatar
    }
  }
  return defaultAvatar
})

const handleAvatarError = (e) => { e.target.src = defaultAvatar }

const mainMenuItems = [
  { path: '/app/discover', label: '探索', icon: Compass },
  { path: '/app/list', label: '游戏库', icon: List },
  { path: '/app/ranking', label: '排行榜', icon: BarChart2 },
  { path: '/app/library', label: '我的收藏', icon: Library },
  { path: '/app/achievements', label: '成就墙', icon: Trophy },
  { path: '/app/news', label: '资讯', icon: Newspaper }
]

const extraMenuItems = [
  { path: '/app/notifications', label: '消息中心', icon: Bell },
  { path: '/app/binding', label: '账号绑定', icon: Link },
  { path: '/app/parental', label: '家长监管', icon: Shield },
  { path: '/app/settings', label: '设置', icon: Settings }
]

watch(() => props.collapsed, (newVal) => { isCollapsed.value = newVal }, { immediate: true })

const toggleCollapse = () => {
  isCollapsed.value = !isCollapsed.value
  emit('update:collapsed', isCollapsed.value)
}

const isActive = (path) => {
  if (path === '/app/discover') return route.path === '/app/discover' || route.path === '/app'
  return route.path.startsWith(path)
}

const handleNavClick = () => {
  if (window.innerWidth <= 768 && props.mobileOpen) emit('close-mobile')
}

const handleLogout = async () => {
  // 防止重复点击
  if (isLoggingOut.value) return
  
  // 确认对话框
  if (!confirm('确定要退出登录吗？')) {
    return
  }

  isLoggingOut.value = true

  try {
    // 调用后端退出登录 API（可选，用于服务端清理 token）
    const refreshToken = sessionStorage.getItem('refreshToken')
    if (refreshToken) {
      try {
        await authApi.logout({ allDevices: false })
      } catch (err) {
        // 即使 API 调用失败，也继续执行本地清理
        console.warn('退出登录 API 调用失败，继续执行本地清理:', err)
      }
    }

    // 清除本地存储的认证信息
    sessionStorage.removeItem('token')
    sessionStorage.removeItem('refreshToken')
    sessionStorage.removeItem('user')

    // 关闭移动端侧边栏
    if (window.innerWidth <= 768 && props.mobileOpen) {
      emit('close-mobile')
    }

    // 跳转到登录页
    router.push({
      path: '/login',
      query: { 
        redirect: route.fullPath // 保存当前路径，方便登录后返回
      }
    })
  } catch (error) {
    console.error('退出登录失败:', error)
    // 即使出错也清除本地存储并跳转
    sessionStorage.removeItem('token')
    sessionStorage.removeItem('refreshToken')
    sessionStorage.removeItem('user')
    router.push('/login')
  } finally {
    isLoggingOut.value = false
  }
}
</script>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Rajdhani:wght@500;600;700&family=Inter:wght@400;500;600&display=swap');

:root {
  /* 核心色板 */
  --c-bg-base: #0f0f13;
  --c-bg-glass: rgba(20, 20, 23, 0.75);
  --c-border: rgba(255, 255, 255, 0.08);
  --c-border-hover: rgba(255, 255, 255, 0.15);
  
  /* 品牌色 - 赛博紫 */
  --c-primary: #8b5cf6; 
  --c-primary-glow: rgba(139, 92, 246, 0.5);
  --c-accent: #38bdf8; /* 天蓝色点缀 */
  
  /* 文字 */
  --c-text-main: #f8fafc;
  --c-text-muted: #94a3b8;
  
  /* 尺寸 */
  --w-expanded: 260px;
  --w-collapsed: 72px;
  --anim-speed: 0.4s;
  --anim-ease: cubic-bezier(0.25, 1, 0.5, 1); /* 丝滑缓动 */
}

/* 侧边栏容器 */
.sidebar {
  position: fixed;
  left: 0;
  top: 0;
  height: 100vh;
  width: var(--w-expanded);
  background: var(--c-bg-glass);
  backdrop-filter: blur(20px) saturate(180%); /* 增强毛玻璃 */
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-right: 1px solid var(--c-border);
  display: flex;
  flex-direction: column;
  transition: width var(--anim-speed) var(--anim-ease);
  z-index: 50;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
  box-shadow: 4px 0 30px rgba(0, 0, 0, 0.3);
}

/* 背景光晕装饰 */
.sidebar-glow {
  position: absolute;
  top: -20%;
  left: -20%;
  width: 140%;
  height: 50%;
  background: radial-gradient(circle at 50% 50%, rgba(139, 92, 246, 0.08), transparent 70%);
  pointer-events: none;
  z-index: -1;
}

/* 折叠状态 */
.sidebar-collapsed {
  width: var(--w-collapsed);
}

/* 头部 Header */
.sidebar-header {
  height: 80px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  flex-shrink: 0;
  position: relative;
}

.logo-area {
  display: flex;
  align-items: center;
  gap: 12px;
  overflow: hidden;
}

.logo-icon {
  width: 40px;
  height: 40px;
  background: linear-gradient(135deg, var(--c-primary), #6366f1);
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 12px var(--c-primary-glow);
  flex-shrink: 0;
}

.logo-icon .icon {
  color: white;
  width: 24px;
}

.logo-text h1 {
  font-family: 'Rajdhani', sans-serif; /* 游戏科技感字体 */
  font-size: 22px;
  font-weight: 700;
  color: var(--c-text-main);
  margin: 0;
  white-space: nowrap;
  letter-spacing: 0.5px;
}

/* 切换按钮 */
.toggle-btn {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  border: 1px solid var(--c-border);
  background: rgba(255, 255, 255, 0.03);
  color: var(--c-text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}

.toggle-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: var(--c-text-main);
  border-color: var(--c-border-hover);
}

/* 修复：折叠状态下调整切换按钮位置，而不是隐藏它 */
.sidebar-collapsed .toggle-btn {
  /* 关键修复：恢复显示和点击交互 */
  opacity: 1; 
  pointer-events: auto;
  
  /* 重新定位：居中显示在 Header 底部 */
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  bottom: 12px; /* 距离 Header 底部留出空间 */
  
  /* 样式调整：变成小圆钮 */
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--c-bg-base);
  border: 1px solid var(--c-border);
  z-index: 20;
}

.sidebar-collapsed .toggle-btn:hover {
  background: var(--c-primary);
  border-color: var(--c-primary);
  color: white;
}

/* 配合修复：折叠时的 Header 布局微调 */
.sidebar-collapsed .sidebar-header {
  justify-content: flex-start; /* 改为从上到下排列 */
  padding-top: 20px; /* 给 Logo 留出顶部空间 */
  flex-direction: column; /* 垂直排列 */
  height: 100px; /* 稍微增加高度以容纳 Logo 和 按钮 */
}

/* 配合修复：折叠时的 Logo 区域 */
.sidebar-collapsed .logo-area {
  gap: 0;
  margin-bottom: 0;
}

/* 用户卡片 */
.user-card-wrapper {
  padding: 0 16px 20px 16px;
  flex-shrink: 0;
}

.user-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--c-border);
  border-radius: 16px;
  padding: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
  transition: all 0.3s ease;
  overflow: hidden;
}

.user-card.compact {
  padding: 8px;
  justify-content: center;
  background: transparent;
  border-color: transparent;
}

.avatar-ring {
  position: relative;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
}

.user-avatar {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid rgba(255,255,255,0.1);
}

.status-dot {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 10px;
  height: 10px;
  background: #22c55e;
  border: 2px solid #18181b;
  border-radius: 50%;
}

.user-meta {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.username {
  font-size: 14px;
  font-weight: 600;
  color: var(--c-text-main);
  white-space: nowrap;
}

.user-lvl {
  font-size: 12px;
  color: var(--c-accent);
  font-family: 'Rajdhani', sans-serif;
  font-weight: 700;
}

/* 导航区域 */
.sidebar-nav {
  flex: 1;
  padding: 10px 12px;
  overflow-y: auto;
  overflow-x: hidden;
}

.nav-item {
  margin-bottom: 4px;
}

.nav-link {
  position: relative;
  display: flex;
  align-items: center;
  height: 48px;
  padding: 0 12px;
  border-radius: 12px;
  color: var(--c-text-muted);
  text-decoration: none;
  transition: all 0.2s ease;
  overflow: hidden;
}

.nav-link:hover {
  background: rgba(255, 255, 255, 0.04);
  color: var(--c-text-main);
}

.nav-link.active {
  background: rgba(139, 92, 246, 0.1); /* 极淡的紫色背景 */
  color: white;
}

.nav-icon-wrapper {
  width: 24px;
  height: 24px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  transition: margin 0.3s;
}

.sidebar-collapsed .nav-icon-wrapper {
  margin-right: 0; /* 折叠时图标居中 */
  width: 100%;
}

.nav-text {
  font-size: 14px;
  font-weight: 500;
  white-space: nowrap;
  transition: opacity 0.2s, transform 0.2s;
}

/* 激活状态的光条 */
.active-indicator {
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 4px;
  height: 20px;
  background: var(--c-primary);
  border-radius: 4px 0 0 4px;
  box-shadow: -2px 0 10px var(--c-primary);
}

.sidebar-collapsed .active-indicator {
  height: 6px;
  width: 6px;
  right: 6px;
  border-radius: 50%;
}

/* 分隔线 */
.nav-divider {
  height: 1px;
  background: linear-gradient(90deg, transparent, var(--c-border), transparent);
  margin: 16px 0;
}

/* 底部 Footer */
.sidebar-footer {
  padding: 20px;
  border-top: 1px solid var(--c-border);
}

.logout-btn {
  width: 100%;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 10px;
  color: #f87171;
  cursor: pointer;
  transition: all 0.2s;
  gap: 10px;
}

.logout-btn:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.1);
  border-color: #ef4444;
}

.logout-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.sidebar-collapsed .logout-btn {
  border: none;
  padding: 0;
}

/* 通用动画控制 */
.fade-out {
  opacity: 0;
  width: 0;
  pointer-events: none;
  transform: translateX(-10px);
}

/* 自定义滚动条 */
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 4px;
}
.custom-scrollbar:hover::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.2);
}

/* 移动端适配 */
@media (max-width: 768px) {
  .sidebar {
    transform: translateX(-100%);
    width: 260px; /* 移动端保持展开宽度 */
  }
  .sidebar.mobile-open {
    transform: translateX(0);
  }
  .sidebar-collapsed {
    width: 260px; /* 移动端禁止折叠逻辑 */
  }
  .toggle-btn {
    display: none; /* 移动端隐藏折叠按钮 */
  }
}
</style>