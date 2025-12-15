# PlayLinker 前端公共文件配置文档

## 📋 必须预先创建的公共文件

在各开发者开始开发前，需要先创建以下公共文件，确保所有人使用统一的基础配置。

---

## 1. API基础配置

### `src/api/index.js` - Axios实例配置 ✅ 已存在

**负责人**: 项目负责人  
**状态**: ✅ 已创建  
**说明**: 已包含基础的axios配置、请求/响应拦截器

**需要补充**:
- 错误处理优化（添加Toast提示）
- 环境变量配置（baseURL从环境变量读取）

**建议修改**:
```javascript
import axios from 'axios'
import { ElMessage } from 'element-plus' // 如果使用Element Plus

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1',
  timeout: 10000
})

// 请求拦截器
api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
api.interceptors.response.use(
  response => {
    const res = response.data
    // 统一处理响应格式
    if (!res.success) {
      ElMessage.error(res.message || '请求失败')
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    return res
  },
  error => {
    console.error('API Error:', error)
    if (error.response) {
      switch (error.response.status) {
        case 401:
          ElMessage.error('未授权，请重新登录')
          // 跳转到登录页
          window.location.href = '/login'
          break
        case 403:
          ElMessage.error('没有权限访问')
          break
        case 404:
          ElMessage.error('请求的资源不存在')
          break
        case 500:
          ElMessage.error('服务器错误')
          break
        default:
          ElMessage.error(error.response.data?.message || '请求失败')
      }
    } else {
      ElMessage.error('网络错误，请检查网络连接')
    }
    return Promise.reject(error)
  }
)

export default api
```

---

## 2. 全局样式文件

### `src/style.css` - 全局样式 ✅ 已存在

**负责人**: 项目负责人  
**状态**: ✅ 已创建  
**说明**: 已包含基础样式，但需要补充

**需要补充的内容**:

```css
/* ========== CSS变量定义 ========== */
:root {
  /* 主题色 */
  --primary-color: #6366f1;
  --primary-hover: #4f46e5;
  --secondary-color: #a1a1aa;
  
  /* 背景色 */
  --bg-primary: #09090b;
  --bg-secondary: #18181b;
  --bg-surface: rgba(24, 24, 27, 0.6);
  
  /* 文字色 */
  --text-primary: #ffffff;
  --text-secondary: #a1a1aa;
  --text-tertiary: #71717a;
  
  /* 边框色 */
  --border-color: rgba(255, 255, 255, 0.1);
  
  /* 状态色 */
  --success-color: #10b981;
  --warning-color: #f59e0b;
  --error-color: #ef4444;
  --info-color: #3b82f6;
  
  /* 间距 */
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  
  /* 圆角 */
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-xl: 16px;
  
  /* 阴影 */
  --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
  --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.1);
  --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.1);
}

/* ========== 工具类 ========== */
.text-center { text-align: center; }
.text-left { text-align: left; }
.text-right { text-align: right; }

.flex { display: flex; }
.flex-col { flex-direction: column; }
.items-center { align-items: center; }
.justify-center { justify-content: center; }
.justify-between { justify-content: space-between; }

.mt-1 { margin-top: var(--spacing-xs); }
.mt-2 { margin-top: var(--spacing-sm); }
.mt-4 { margin-top: var(--spacing-md); }
.mt-6 { margin-top: var(--spacing-lg); }

.mb-1 { margin-bottom: var(--spacing-xs); }
.mb-2 { margin-bottom: var(--spacing-sm); }
.mb-4 { margin-bottom: var(--spacing-md); }
.mb-6 { margin-bottom: var(--spacing-lg); }

.p-2 { padding: var(--spacing-sm); }
.p-4 { padding: var(--spacing-md); }
.p-6 { padding: var(--spacing-lg); }

/* ========== 毛玻璃效果 ========== */
.glass-panel {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color);
}
```

---

## 3. 路由配置

### `src/router/index.js` - 主路由文件 ⚠️ 需要创建

**负责人**: 项目负责人  
**状态**: ⚠️ 待创建  

```javascript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

// 导入路由模块
import authRoutes from './modules/auth'
import platformRoutes from './modules/platform'
import notificationRoutes from './modules/notification'
import parentalRoutes from './modules/parental'
import gameRoutes from './modules/game'
import libraryRoutes from './modules/library'
import localManageRoutes from './modules/localManage'
import analyticsRoutes from './modules/analytics'
import priceRoutes from './modules/price'
import recommendationRoutes from './modules/recommendation'

const routes = [
  {
    path: '/',
    redirect: '/discover'
  },
  ...authRoutes,
  ...platformRoutes,
  ...notificationRoutes,
  ...parentalRoutes,
  ...gameRoutes,
  ...libraryRoutes,
  ...localManageRoutes,
  ...analyticsRoutes,
  ...priceRoutes,
  ...recommendationRoutes,
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/Error/NotFoundView.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 全局路由守卫
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()
  
  // 检查是否需要认证
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next('/login')
  } else if (to.path === '/login' && authStore.isAuthenticated) {
    next('/discover')
  } else {
    next()
  }
})

export default router
```

---

## 4. 状态管理

### `src/stores/auth.js` - 认证状态 ⚠️ 需要创建

**负责人**: 开发者A  
**状态**: ⚠️ 待创建  

```javascript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useAuthStore = defineStore('auth', () => {
  // State
  const token = ref(localStorage.getItem('token') || '')
  const user = ref(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value)

  // Actions
  const setToken = (newToken) => {
    token.value = newToken
    localStorage.setItem('token', newToken)
  }

  const setUser = (userData) => {
    user.value = userData
  }

  const logout = () => {
    token.value = ''
    user.value = null
    localStorage.removeItem('token')
  }

  return {
    token,
    user,
    isAuthenticated,
    setToken,
    setUser,
    logout
  }
})
```

---

## 5. 工具函数

### `src/utils/request.js` - HTTP请求封装 ⚠️ 需要创建

**负责人**: 项目负责人  
**状态**: ⚠️ 待创建  

```javascript
import api from '@/api'

/**
 * 通用GET请求
 */
export const get = (url, params = {}) => {
  return api.get(url, { params })
}

/**
 * 通用POST请求
 */
export const post = (url, data = {}) => {
  return api.post(url, data)
}

/**
 * 通用PUT请求
 */
export const put = (url, data = {}) => {
  return api.put(url, data)
}

/**
 * 通用DELETE请求
 */
export const del = (url) => {
  return api.delete(url)
}
```

### `src/utils/storage.js` - 本地存储封装 ⚠️ 需要创建

```javascript
/**
 * 存储数据
 */
export const setItem = (key, value) => {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch (error) {
    console.error('存储失败:', error)
  }
}

/**
 * 获取数据
 */
export const getItem = (key) => {
  try {
    const value = localStorage.getItem(key)
    return value ? JSON.parse(value) : null
  } catch (error) {
    console.error('读取失败:', error)
    return null
  }
}

/**
 * 删除数据
 */
export const removeItem = (key) => {
  localStorage.removeItem(key)
}

/**
 * 清空所有数据
 */
export const clear = () => {
  localStorage.clear()
}
```

### `src/utils/format.js` - 格式化工具 ⚠️ 需要创建

```javascript
/**
 * 格式化日期
 */
export const formatDate = (date, format = 'YYYY-MM-DD HH:mm:ss') => {
  if (!date) return ''
  const d = new Date(date)
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const hour = String(d.getHours()).padStart(2, '0')
  const minute = String(d.getMinutes()).padStart(2, '0')
  const second = String(d.getSeconds()).padStart(2, '0')
  
  return format
    .replace('YYYY', year)
    .replace('MM', month)
    .replace('DD', day)
    .replace('HH', hour)
    .replace('mm', minute)
    .replace('ss', second)
}

/**
 * 格式化价格
 */
export const formatPrice = (price) => {
  return `$${Number(price).toFixed(2)}`
}

/**
 * 格式化文件大小
 */
export const formatFileSize = (bytes) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i]
}

/**
 * 格式化游戏时长
 */
export const formatPlaytime = (hours) => {
  if (hours < 1) return `${Math.round(hours * 60)}分钟`
  return `${Math.round(hours)}小时`
}
```

---

## 6. 公共组件

### `src/components/common/LoadingSpinner.vue` ⚠️ 需要创建

```vue
<template>
  <div class="loading-spinner">
    <div class="spinner"></div>
    <p v-if="text">{{ text }}</p>
  </div>
</template>

<script setup>
defineProps({
  text: {
    type: String,
    default: '加载中...'
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

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid rgba(99, 102, 241, 0.1);
  border-top-color: #6366f1;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

p {
  margin-top: 16px;
  color: var(--text-secondary);
  font-size: 14px;
}
</style>
```

### `src/components/common/ErrorMessage.vue` ⚠️ 需要创建

```vue
<template>
  <div class="error-message">
    <i class="icon-error">⚠️</i>
    <div class="error-content">
      <h3>{{ title }}</h3>
      <p>{{ message }}</p>
      <button v-if="retry" @click="$emit('retry')" class="btn-retry">
        重试
      </button>
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: {
    type: String,
    default: '出错了'
  },
  message: {
    type: String,
    required: true
  },
  retry: {
    type: Boolean,
    default: false
  }
})

defineEmits(['retry'])
</script>

<style scoped>
.error-message {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  background-color: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 8px;
  margin: 20px 0;
}

.icon-error {
  font-size: 32px;
}

.error-content h3 {
  color: var(--error-color);
  margin-bottom: 8px;
}

.error-content p {
  color: var(--text-secondary);
  font-size: 14px;
}

.btn-retry {
  margin-top: 12px;
  padding: 8px 16px;
  background-color: var(--primary-color);
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}
</style>
```

---

## 7. 环境变量配置

### `.env.development` ⚠️ 需要创建

```env
# 开发环境配置
VITE_API_BASE_URL=http://localhost:5000/api/v1
VITE_APP_TITLE=PlayLinker - 开发环境
VITE_APP_ENV=development
```

### `.env.production` ⚠️ 需要创建

```env
# 生产环境配置
VITE_API_BASE_URL=https://api.playlinker.com/api/v1
VITE_APP_TITLE=PlayLinker
VITE_APP_ENV=production
```

---

## 8. 配置文件

### `vite.config.js` - Vite配置 ⚠️ 需要检查

```javascript
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
```

---

## 📝 创建优先级

### 🔴 高优先级（必须立即创建）
1. ✅ `src/api/index.js` - 已存在，需要优化
2. ⚠️ `src/router/index.js` - 主路由配置
3. ⚠️ `src/stores/auth.js` - 认证状态管理
4. ⚠️ `.env.development` - 开发环境变量
5. ⚠️ `.env.production` - 生产环境变量

### 🟡 中优先级（开发前创建）
6. ⚠️ `src/utils/request.js` - HTTP请求工具
7. ⚠️ `src/utils/storage.js` - 本地存储工具
8. ⚠️ `src/utils/format.js` - 格式化工具
9. ✅ `src/style.css` - 已存在，需要补充CSS变量

### 🟢 低优先级（开发中创建）
10. ⚠️ `src/components/common/LoadingSpinner.vue` - 加载组件
11. ⚠️ `src/components/common/ErrorMessage.vue` - 错误提示组件

---

## 🎯 行动计划

### 第一步：项目负责人创建基础配置（1-2小时）
- [ ] 优化 `src/api/index.js`
- [ ] 创建 `src/router/index.js`
- [ ] 创建环境变量文件
- [ ] 创建工具函数文件
- [ ] 补充全局样式

### 第二步：开发者A创建认证相关（30分钟）
- [ ] 创建 `src/stores/auth.js`
- [ ] 创建 `src/router/modules/auth.js`

### 第三步：各开发者创建自己的路由模块（各15分钟）
- [ ] 开发者A: notification.js, parental.js
- [ ] 开发者B: game.js, library.js
- [ ] 开发者C: localManage.js, analytics.js
- [ ] 开发者D: platform.js, price.js, recommendation.js

### 第四步：创建公共组件（按需）
- [ ] LoadingSpinner.vue
- [ ] ErrorMessage.vue

---

## 📌 注意事项

1. **所有开发者必须等待基础配置完成后再开始开发**
2. **不要修改公共文件的核心逻辑，如需修改请先讨论**
3. **CSS变量统一使用，不要硬编码颜色值**
4. **API调用统一使用 `src/api/index.js` 中的实例**
5. **路由配置统一在 `src/router/modules/` 中管理**

---

**最后更新**: 2024-12-15  
**文档版本**: v1.0.0
