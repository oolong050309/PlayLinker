# PlayLinker 前端开发分工文档

## 项目信息

**项目名称**: PlayLinker - 统一游戏管理平台前端  
**技术栈**: Vue 3 + Vite + Pinia + Axios + TailwindCSS  
**开发周期**: 2周  
**最后更新**: 2024-12-15

---

## 开发团队分工

### 👤 开发者A - 用户认证与监管模块

**负责后端API**: 用户认证、用户管理、通知中心、家长监管  
**负责前端页面**:

#### 页面列表
1. **登录注册页面**
   - `login.html` → `src/views/Auth/LoginView.vue`
   - `register.html` → `src/views/Auth/RegisterView.vue`

2. **通知中心页面**
   - `app-notifications.html` → `src/views/Notification/NotificationView.vue`

3. **家长控制页面**
   - `app-parental.html` → `src/views/Parental/ParentalView.vue`

#### API接口文件
- `src/api/auth.js` - 用户认证相关接口
- `src/api/notification.js` - 通知中心相关接口
- `src/api/parental.js` - 家长监管相关接口

#### 路由配置
```javascript
// src/router/modules/auth.js
// src/router/modules/notification.js
// src/router/modules/parental.js
```

---

### 🎮 开发者B - 游戏数据与库管理模块

**负责后端API**: 游戏数据、游戏元数据、游戏库管理、成就系统、平台集成(Steam/Xbox/PSN/GOG)  
**负责前端页面**:

#### 页面列表
1. **游戏库页面**
   - `app-library.html` → `src/views/Game/LibraryView.vue`

2. **游戏详情页面**（用户已拥有）
   - `app-detail.html` → `src/views/Game/GameDetailView.vue`

3. **商店游戏详情页面**（含新闻）
   - `app-store.html` → `src/views/Game/StoreDetailView.vue`

#### API接口文件
- `src/api/game.js` - 游戏数据相关接口
- `src/api/library.js` - 游戏库管理相关接口
- `src/api/achievement.js` - 成就系统相关接口
- `src/api/news.js` - 游戏新闻相关接口
- `src/api/steam.js` - Steam集成接口
- `src/api/xbox.js` - Xbox集成接口
- `src/api/psn.js` - PSN集成接口
- `src/api/gog.js` - GOG集成接口

#### 路由配置
```javascript
// src/router/modules/game.js
// src/router/modules/library.js
```

---

### 💾 开发者C - 本地文件与数据分析模块

**负责后端API**: 本地文件管理、存档管理、Mod管理、报表系统、数据分析  
**负责前端页面**:

#### 页面列表
1. **Mod与存档管理页面**（整合）
   - `app-mods.html` → `src/views/LocalManage/ModsView.vue`
   - `app-game-mods-detail.html` → `src/views/LocalManage/ModDetailView.vue`

2. **数据分析页面**（含报表）
   - `app-analytics.html` → `src/views/Analytics/AnalyticsView.vue`

3. **设置页面**
   - `app-settings.html` → `src/views/Settings/SettingsView.vue`

#### API接口文件
- `src/api/localGame.js` - 本地游戏管理相关接口
- `src/api/save.js` - 存档管理相关接口
- `src/api/cloud.js` - 云存档相关接口
- `src/api/mod.js` - Mod管理相关接口
- `src/api/report.js` - 报表系统相关接口
- `src/api/analytics.js` - 数据分析相关接口

#### 路由配置
```javascript
// src/router/modules/localManage.js
// src/router/modules/analytics.js
```

---

### 🎯 开发者D - 推荐与价格监控模块

**负责后端API**: 用户偏好、推荐系统、价格监控、愿望单、折扣提醒、平台绑定  
**负责前端页面**:

#### 页面列表
1. **游戏发现页面**（含排行榜）
   - `app-discover.html` → `src/views/Game/DiscoverView.vue`

2. **平台绑定页面**
   - `app-binding.html` → `src/views/Platform/BindingView.vue`

3. **价格监控页面**
   - `app-price-monitor.html` → `src/views/Price/PriceMonitorView.vue`

#### API接口文件
- `src/api/preference.js` - 用户偏好相关接口
- `src/api/recommendation.js` - 推荐系统相关接口
- `src/api/platform.js` - 平台绑定相关接口
- `src/api/price.js` - 价格监控相关接口
- `src/api/wishlist.js` - 愿望单相关接口

#### 路由配置
```javascript
// src/router/modules/platform.js
// src/router/modules/price.js
// src/router/modules/recommendation.js
```

---

## 前端项目结构

```
Frontend/
├── public/                      # 静态资源
│   └── favicon.ico
├── src/
│   ├── api/                     # API接口封装
│   │   ├── index.js            # Axios实例配置
│   │   ├── auth.js             # 开发者A
│   │   ├── platform.js         # 开发者A
│   │   ├── notification.js     # 开发者A
│   │   ├── parental.js         # 开发者A
│   │   ├── game.js             # 开发者B
│   │   ├── library.js          # 开发者B
│   │   ├── achievement.js      # 开发者B
│   │   ├── news.js             # 开发者B
│   │   ├── steam.js            # 开发者B
│   │   ├── xbox.js             # 开发者B
│   │   ├── psn.js              # 开发者B
│   │   ├── gog.js              # 开发者B
│   │   ├── localGame.js        # 开发者C
│   │   ├── save.js             # 开发者C
│   │   ├── cloud.js            # 开发者C
│   │   ├── mod.js              # 开发者C
│   │   ├── report.js           # 开发者C
│   │   ├── analytics.js        # 开发者C
│   │   ├── preference.js       # 开发者D
│   │   ├── recommendation.js   # 开发者D
│   │   ├── price.js            # 开发者D
│   │   └── wishlist.js         # 开发者D
│   │
│   ├── assets/                  # 资源文件
│   │   ├── images/
│   │   └── styles/
│   │       └── main.css
│   │
│   ├── components/              # 公共组件
│   │   ├── common/             # 通用组件
│   │   │   ├── AppHeader.vue
│   │   │   ├── AppSidebar.vue
│   │   │   ├── AppFooter.vue
│   │   │   ├── LoadingSpinner.vue
│   │   │   └── ErrorMessage.vue
│   │   ├── game/               # 游戏相关组件
│   │   │   ├── GameCard.vue
│   │   │   ├── GameList.vue
│   │   │   └── GameFilter.vue
│   │   └── chart/              # 图表组件
│   │       ├── LineChart.vue
│   │       ├── PieChart.vue
│   │       └── BarChart.vue
│   │
│   ├── composables/             # 组合式函数
│   │   ├── useAuth.js
│   │   ├── useGame.js
│   │   └── useNotification.js
│   │
│   ├── router/                  # 路由配置
│   │   ├── index.js            # 主路由文件
│   │   └── modules/            # 路由模块
│   │       ├── auth.js         # 开发者A
│   │       ├── platform.js     # 开发者A
│   │       ├── notification.js # 开发者A
│   │       ├── parental.js     # 开发者A
│   │       ├── game.js         # 开发者B
│   │       ├── library.js      # 开发者B
│   │       ├── localManage.js  # 开发者C
│   │       ├── analytics.js    # 开发者C
│   │       ├── price.js        # 开发者D
│   │       └── recommendation.js # 开发者D
│   │
│   ├── stores/                  # Pinia状态管理
│   │   ├── auth.js             # 开发者A
│   │   ├── user.js             # 开发者A
│   │   ├── game.js             # 开发者B
│   │   ├── library.js          # 开发者B
│   │   ├── analytics.js        # 开发者C
│   │   └── price.js            # 开发者D
│   │
│   ├── utils/                   # 工具函数
│   │   ├── request.js          # HTTP请求封装
│   │   ├── storage.js          # 本地存储封装
│   │   ├── format.js           # 格式化工具
│   │   └── validate.js         # 表单验证
│   │
│   ├── views/                   # 页面组件
│   │   ├── Auth/               # 开发者A
│   │   │   ├── LoginView.vue
│   │   │   └── RegisterView.vue
│   │   ├── Platform/           # 开发者A
│   │   │   └── BindingView.vue
│   │   ├── Notification/       # 开发者A
│   │   │   └── NotificationView.vue
│   │   ├── Parental/           # 开发者A
│   │   │   └── ParentalView.vue
│   │   ├── Settings/           # 开发者C
│   │   │   └── SettingsView.vue
│   │   ├── Game/               # 开发者B
│   │   │   ├── DiscoverView.vue
│   │   │   ├── LibraryView.vue
│   │   │   ├── GameDetailView.vue
│   │   │   └── StoreDetailView.vue
│   │   ├── LocalManage/        # 开发者C
│   │   │   ├── ModsView.vue
│   │   │   └── ModDetailView.vue
│   │   ├── Analytics/          # 开发者C
│   │   │   └── AnalyticsView.vue
│   │   └── Price/              # 开发者D
│   │       └── PriceMonitorView.vue
│   │
│   ├── App.vue                  # 根组件
│   └── main.js                  # 入口文件
│
├── .env.development             # 开发环境变量
├── .env.production              # 生产环境变量
├── .gitignore
├── index.html
├── package.json
├── vite.config.js
└── README.md
```

---

## 文件命名规范

### 1. 组件命名
- **页面组件**: 使用 `PascalCase` + `View` 后缀
  - 示例: `LoginView.vue`, `GameDetailView.vue`
- **公共组件**: 使用 `PascalCase`
  - 示例: `GameCard.vue`, `LoadingSpinner.vue`

### 2. API文件命名
- 使用 `camelCase`
- 按功能模块划分
- 示例: `auth.js`, `game.js`, `localGame.js`

### 3. 路由模块命名
- 使用 `camelCase`
- 与API文件对应
- 示例: `auth.js`, `game.js`, `analytics.js`

### 4. Store命名
- 使用 `camelCase`
- 按数据类型划分
- 示例: `auth.js`, `user.js`, `game.js`

---

## 代码规范

### 1. Vue组件结构
```vue
<template>
  <!-- 模板内容 -->
</template>

<script setup>
// 1. 导入依赖
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

// 2. 定义响应式数据
const loading = ref(false)
const data = ref([])

// 3. 定义计算属性
const filteredData = computed(() => {
  // ...
})

// 4. 定义方法
const fetchData = async () => {
  // ...
}

// 5. 生命周期钩子
onMounted(() => {
  fetchData()
})
</script>

<style scoped>
/* 组件样式 */
</style>
```

### 2. API接口封装
```javascript
// src/api/game.js
import request from './index'

/**
 * 获取游戏列表
 * @param {Object} params - 查询参数
 * @returns {Promise}
 */
export const getGameList = (params) => {
  return request({
    url: '/games',
    method: 'get',
    params
  })
}

/**
 * 获取游戏详情
 * @param {number} id - 游戏ID
 * @returns {Promise}
 */
export const getGameDetail = (id) => {
  return request({
    url: `/games/${id}`,
    method: 'get'
  })
}
```

### 3. 路由配置
```javascript
// src/router/modules/game.js
export default [
  {
    path: '/discover',
    name: 'Discover',
    component: () => import('@/views/Game/DiscoverView.vue'),
    meta: {
      title: '游戏发现',
      requiresAuth: true
    }
  },
  {
    path: '/library',
    name: 'Library',
    component: () => import('@/views/Game/LibraryView.vue'),
    meta: {
      title: '我的游戏库',
      requiresAuth: true
    }
  }
]
```

### 4. Pinia Store
```javascript
// src/stores/auth.js
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { login, logout } from '@/api/auth'

export const useAuthStore = defineStore('auth', () => {
  // State
  const token = ref(localStorage.getItem('token') || '')
  const user = ref(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value)

  // Actions
  const loginUser = async (credentials) => {
    const res = await login(credentials)
    token.value = res.data.token
    user.value = res.data.user
    localStorage.setItem('token', token.value)
  }

  const logoutUser = () => {
    token.value = ''
    user.value = null
    localStorage.removeItem('token')
  }

  return {
    token,
    user,
    isAuthenticated,
    loginUser,
    logoutUser
  }
})
```

---

## 开发流程

### 1. 环境准备
```bash
# 安装依赖
cd Frontend
npm install

# 启动开发服务器
npm run dev

# 访问地址
http://localhost:3000
```

### 2. 开发步骤
1. **创建API接口文件** (`src/api/xxx.js`)
2. **创建路由配置** (`src/router/modules/xxx.js`)
3. **创建Store** (`src/stores/xxx.js`) (如需要)
4. **创建页面组件** (`src/views/xxx/XxxView.vue`)
5. **测试功能**
6. **提交代码**

### 3. Git提交规范
```bash
# 功能开发
git commit -m "feat(模块名): 添加xxx功能"

# Bug修复
git commit -m "fix(模块名): 修复xxx问题"

# 样式调整
git commit -m "style(模块名): 调整xxx样式"

# 文档更新
git commit -m "docs: 更新xxx文档"
```

---

## API基础配置

### Axios实例配置
```javascript
// src/api/index.js
import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import { ElMessage } from 'element-plus'

const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1',
  timeout: 10000
})

// 请求拦截器
request.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// 响应拦截器
request.interceptors.response.use(
  (response) => {
    const res = response.data
    if (!res.success) {
      ElMessage.error(res.message || '请求失败')
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    return res
  },
  (error) => {
    ElMessage.error(error.message || '网络错误')
    return Promise.reject(error)
  }
)

export default request
```

---

## 环境变量配置

### .env.development
```env
VITE_API_BASE_URL=http://localhost:5000/api/v1
VITE_APP_TITLE=PlayLinker - 开发环境
```

### .env.production
```env
VITE_API_BASE_URL=https://api.playlinker.com/api/v1
VITE_APP_TITLE=PlayLinker
```

---

## 注意事项

### 1. 认证处理
- 所有需要认证的页面在路由meta中设置 `requiresAuth: true`
- 在路由守卫中统一处理认证逻辑
- Token存储在localStorage中，过期后自动跳转登录页

### 2. 错误处理
- 统一在Axios拦截器中处理错误
- 使用Toast/Message组件显示错误信息
- 关键操作需要二次确认

### 3. 加载状态
- 数据加载时显示Loading组件
- 空数据显示Empty状态
- 错误状态显示Error组件

### 4. 性能优化
- 路由懒加载
- 图片懒加载
- 列表虚拟滚动（大数据量）
- 防抖节流处理

### 5. 代码复用
- 提取公共组件到 `components/common`
- 提取公共逻辑到 `composables`
- 提取工具函数到 `utils`

---

## 联系方式

**项目负责人**: [项目负责人姓名]  
**技术支持**: [技术支持联系方式]  
**问题反馈**: 请在项目Issues中提交

---

**最后更新**: 2024-12-15  
**文档版本**: v1.0.0
