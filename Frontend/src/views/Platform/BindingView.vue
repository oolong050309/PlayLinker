<template>
  <div class="app-container">
    <!-- 头部区域 -->
    <header class="header">
      <div class="header-content">
        <div class="header-title">
          <h1>平台绑定</h1>
          <p>连接你的游戏账号以同步游戏库</p>
        </div>
        <div class="header-status">
          <div class="status-badge">
            <CheckCircle class="icon icon-success" size="16" />
            <span><span class="text-success font-bold">{{ stats.connectedCount }}</span> 已连接</span>
          </div>
        </div>
      </div>
    </header>

    <!-- 主要内容 -->
    <main class="main-content">
      <!-- 统计概览 -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon bg-indigo">
            <Link class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-label">已连接平台</div>
            <div class="stat-value">{{ stats.connectedCount }} / 7</div>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon bg-emerald">
            <Gamepad2 class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-label">已同步游戏总数</div>
            <div class="stat-value">{{ stats.totalGames }}</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-amber">
            <Trophy class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-label">已同步成就数</div>
            <div class="stat-value">{{ stats.totalAchievements }}</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-rose">
            <Clock class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-label">最后同步时间</div>
            <div class="stat-value">{{ stats.lastSync }}</div>
          </div>
        </div>
      </div>

      <!-- 已连接平台 -->
      <div class="section" v-if="connectedPlatforms.length > 0">
        <h2 class="section-title">
          <CheckCircle class="icon icon-success" size="20" />
          已连接平台
        </h2>
        <div class="platform-grid">
          <div 
            v-for="binding in connectedPlatforms" 
            :key="binding.bindingId"
            class="platform-card connected"
          >
            <div class="platform-header" :class="getPlatformConfig(binding.platformId)?.gradient || ''">
              <div class="platform-info">
                <div class="platform-logo">
                  <img :src="getPlatformLogo(binding.platformId)" :alt="binding.platformName">
                </div>
                <div class="platform-details">
                  <h3>{{ binding.platformName }}</h3>
                  <p>@{{ binding.platformUserId }}</p>
                </div>
              </div>
              <span class="badge success">已连接</span>
            </div>
            <div class="platform-actions">
              <span class="last-synced">绑定时间：{{ formatTime(binding.bindingTime) }}</span>
              <div class="action-buttons">
                <button 
                  class="btn btn-secondary"
                  @click="handleSync(binding.platformId)"
                  :disabled="loading"
                >
                  <RefreshCw class="icon" size="12" /> 同步
                </button>
                <button 
                  class="btn btn-danger"
                  @click="handleUnbind(binding)"
                  :disabled="loading"
                >
                  断开连接
                </button>
              </div>
            </div>
          </div>
            </div>
          </div>

      <!-- 可用平台 -->
      <div class="section" v-if="availablePlatforms.length > 0">
        <h2 class="section-title">
          <PlusCircle class="icon icon-muted" size="20" />
          可用平台
        </h2>
        <div class="platform-grid">
          <div 
            v-for="platform in availablePlatforms" 
            :key="platform.id"
            class="platform-card"
          >
            <div class="platform-header" :class="platform.gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img :src="getPlatformLogo(platform.id)" :alt="platform.name">
                </div>
                <div class="platform-details">
                  <h3>{{ platform.name }}</h3>
                  <p>未连接</p>
                </div>
              </div>
              <p class="platform-desc">{{ getPlatformDescription(platform.id) }}</p>
            </div>
            <div class="platform-actions">
              <button 
                class="btn btn-primary w-full"
                @click="openBindModal(platform)"
                :disabled="loading"
              >
                <Link class="icon" size="16" /> 连接账号
                </button>
            </div>
          </div>
        </div>
      </div>

      <!-- 同步设置 -->
      <div class="section">
        <div class="settings-card">
        <h2 class="section-title">
            <Settings class="icon" size="20" />
            同步设置
        </h2>
          <div class="settings-list">
            <div class="setting-item">
              <div class="setting-info">
                <h3>自动同步游戏库</h3>
                <p>每小时自动同步你的游戏库</p>
                </div>
              <div 
                class="toggle-switch" 
                :class="{ active: syncSettings.autoSync }"
                @click="toggleSwitch('autoSync')"
              >
                <span class="toggle-thumb"></span>
                </div>
              </div>
            
            <div class="setting-item">
              <div class="setting-info">
                <h3>同步成就</h3>
                <p>从已连接平台导入成就和奖杯</p>
            </div>
              <div 
                class="toggle-switch" 
                :class="{ active: syncSettings.achievements }"
                @click="toggleSwitch('achievements')"
              >
                <span class="toggle-thumb"></span>
            </div>
          </div>

            <div class="setting-item">
              <div class="setting-info">
                <h3>同步游玩时长</h3>
                <p>追踪并汇总所有平台的游玩时长</p>
                </div>
              <div 
                class="toggle-switch" 
                :class="{ active: syncSettings.playtime }"
                @click="toggleSwitch('playtime')"
              >
                <span class="toggle-thumb"></span>
                </div>
              </div>
            
            <div class="setting-item">
              <div class="setting-info">
                <h3>同步通知</h3>
                <p>同步完成或失败时收到通知</p>
            </div>
              <div 
                class="toggle-switch" 
                :class="{ active: syncSettings.notify }"
                @click="toggleSwitch('notify')"
              >
                <span class="toggle-thumb"></span>
            </div>
            </div>
          </div>

          <div class="settings-actions">
            <button 
              class="btn btn-primary"
              @click="handleSyncAll"
              :disabled="loading || connectedPlatforms.length === 0"
            >
              <RefreshCw class="icon" size="16" /> 立即同步全部
            </button>
            <button class="btn btn-tertiary">
              查看同步历史
            </button>
                </div>
                </div>
              </div>
    </main>

    <!-- 绑定模态框 -->
    <div v-if="showBindModal" class="modal-overlay" @click="closeBindModal">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3 class="modal-title">绑定{{ selectedPlatform?.name }}</h3>
          <button class="modal-close" @click="closeBindModal">
            <X size="20" />
              </button>
            </div>
        <div class="modal-body">
          <div v-if="selectedPlatform?.id === 1" class="form-group">
            <label>Steam ID *</label>
            <input 
              v-model="bindForm.steamId" 
              type="text" 
              class="form-input"
              placeholder="请输入Steam ID"
            />
            <p class="form-hint">可在Steam个人资料页面URL中找到</p>
          </div>
          <div v-if="selectedPlatform?.id === 1" class="form-group">
            <label>Steam API Key *</label>
            <input 
              v-model="bindForm.apiKey" 
              type="password" 
              class="form-input"
              placeholder="请输入Steam API Key"
            />
            <p class="form-hint">在 <a href="https://steamcommunity.com/dev/apikey" target="_blank">Steam API Key页面</a> 申请</p>
          </div>

          <div v-if="selectedPlatform?.id === 7" class="form-group">
            <label>Xbox用户ID *</label>
            <input 
              v-model="bindForm.xboxUserId" 
              type="text" 
              class="form-input"
              placeholder="请输入Xbox用户ID"
            />
                </div>
          <div v-if="selectedPlatform?.id === 7" class="form-group">
            <label>访问令牌（可选）</label>
            <input 
              v-model="bindForm.accessToken" 
              type="text" 
              class="form-input"
              placeholder="请输入访问令牌"
            />
                </div>
          <div v-if="selectedPlatform?.id === 7" class="form-group">
            <label>刷新令牌（可选）</label>
            <input 
              v-model="bindForm.refreshToken" 
              type="text" 
              class="form-input"
              placeholder="请输入刷新令牌"
            />
              </div>

          <div v-if="selectedPlatform?.id === 6" class="form-group">
            <label>PSN在线ID *</label>
            <input 
              v-model="bindForm.psnOnlineId" 
              type="text" 
              class="form-input"
              placeholder="请输入PSN在线ID"
            />
            </div>
          <div v-if="selectedPlatform?.id === 6" class="form-group">
            <label>访问令牌（可选）</label>
            <input 
              v-model="bindForm.accessToken" 
              type="text" 
              class="form-input"
              placeholder="请输入访问令牌"
            />
            </div>
          <div v-if="selectedPlatform?.id === 6" class="form-group">
            <label>刷新令牌（可选）</label>
            <input 
              v-model="bindForm.refreshToken" 
              type="text" 
              class="form-input"
              placeholder="请输入刷新令牌"
            />
      </div>

          <div v-if="selectedPlatform?.id === 5" class="form-group">
            <label>GOG用户ID *</label>
            <input 
              v-model="bindForm.gogUserId" 
              type="text" 
              class="form-input"
              placeholder="请输入GOG用户ID"
            />
              </div>
          <div v-if="selectedPlatform?.id === 5" class="form-group">
            <label>访问令牌（可选）</label>
            <input 
              v-model="bindForm.accessToken" 
              type="text" 
              class="form-input"
              placeholder="请输入访问令牌"
            />
            </div>
          <div v-if="selectedPlatform?.id === 5" class="form-group">
            <label>刷新令牌（可选）</label>
            <input 
              v-model="bindForm.refreshToken" 
              type="text" 
              class="form-input"
              placeholder="请输入刷新令牌"
            />
              </div>
            </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeBindModal">取消</button>
          <button 
            class="btn btn-primary" 
            @click="handleBind"
            :disabled="loading"
          >
            {{ loading ? '绑定中...' : '确认绑定' }}
              </button>
            </div>
            </div>
          </div>
          
    <!-- 加载遮罩 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-spinner">加载中...</div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { CheckCircle, Link, Gamepad2, Trophy, Clock, PlusCircle, Settings, RefreshCw, X } from 'lucide-vue-next'
import { platformsApi } from '@/api/platforms'
import { libraryApi, steamApi, xboxApi, psnApi, gogApi } from '@/api/index'

// 平台配置
const platformConfig = {
  1: { name: 'Steam', id: 1, icon: 'steam', gradient: 'steam-gradient', requires: ['steamId', 'apiKey'] },
  2: { name: 'Epic Games', id: 2, icon: 'epic', gradient: 'epic-gradient', requires: [] },
  5: { name: 'GOG Galaxy', id: 5, icon: 'gog', gradient: 'gog-gradient', requires: ['gogUserId'] },
  6: { name: 'PlayStation', id: 6, icon: 'playstation', gradient: 'playstation-gradient', requires: ['psnOnlineId'] },
  7: { name: 'Xbox', id: 7, icon: 'xbox', gradient: 'xbox-gradient', requires: ['xboxUserId'] },
  8: { name: 'Nintendo Switch', id: 8, icon: 'nintendo', gradient: 'nintendo-gradient', requires: [] }
}

// 响应式数据
const loading = ref(false)
const bindings = ref([])
const stats = ref({
  connectedCount: 0,
  totalGames: 0,
  totalAchievements: 0,
  lastSync: '从未同步'
})

const syncSettings = ref({
  autoSync: true,
  achievements: true,
  playtime: true,
  notify: false
})

// 获取当前登录用户ID（从 sessionStorage 的 user 中解析）
const getCurrentUserId = () => {
  try {
    const userStr = sessionStorage.getItem('user')
    if (!userStr) return null
    const user = JSON.parse(userStr)
    return user.userId || user.id || null
  } catch (e) {
    console.warn('解析用户ID失败:', e)
    return null
  }
}

// 绑定表单
const showBindModal = ref(false)
const selectedPlatform = ref(null)
const bindForm = ref({
  platformId: null,
  steamId: '',
  apiKey: '',
  xboxUserId: '',
  psnOnlineId: '',
  gogUserId: '',
  accessToken: '',
  refreshToken: ''
})

// 计算属性
const connectedPlatforms = computed(() => {
  return bindings.value.filter(b => b.bindingStatus !== false)
})

const availablePlatforms = computed(() => {
  const connectedIds = connectedPlatforms.value.map(b => b.platformId)
  return Object.values(platformConfig).filter(p => !connectedIds.includes(p.id))
})

// 加载绑定列表
const loadBindings = async () => {
  loading.value = true
  try {
    const response = await platformsApi.getBindings()
    if (response.success && response.data) {
      // 后端返回的数据结构：{ bindings: [...], totalCount: ... }
      bindings.value = (response.data.bindings || []).map(binding => ({
        ...binding,
        bindingStatus: true, // 后端只返回已绑定的记录
        platformId: getPlatformIdByName(binding.platformName)
      }))
      updateStats()
    }
  } catch (error) {
    console.error('加载绑定列表失败:', error)
    alert('加载绑定列表失败: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

// 根据平台名称获取平台ID
const getPlatformIdByName = (platformName) => {
  const nameMap = {
    'Steam': 1,
    'Epic Games': 2,
    'GOG': 5,
    'GOG Galaxy': 5,
    'PSN': 6,
    'PlayStation': 6,
    'Xbox': 7,
    'Nintendo Switch': 8
  }
  return nameMap[platformName] || null
}

// 从后端刷新统计数据（游戏库 + 成就）
const refreshStats = async () => {
  try {
    const res = await libraryApi.getOverview()
    if (res.success && res.data) {
      const o = res.data
      // 兼容大小写字段
      const totalGamesOwned = o.totalGamesOwned ?? o.TotalGamesOwned ?? 0
      const totalAchievements = o.totalAchievements ?? o.TotalAchievements ?? 0

      stats.value.connectedCount = connectedPlatforms.value.length
      stats.value.totalGames = totalGamesOwned
      stats.value.totalAchievements = totalAchievements

      const platformStats = o.platformStats || o.PlatformStats || []
      const firstPlatform = platformStats[0]
      const lastSyncTime = firstPlatform?.lastSyncTime || firstPlatform?.LastSyncTime
      stats.value.lastSync = lastSyncTime || '刚刚'
    } else {
      // 后端返回空数据时，重置为0
      stats.value.connectedCount = connectedPlatforms.value.length
      stats.value.totalGames = 0
      stats.value.totalAchievements = 0
      stats.value.lastSync = '从未同步'
    }
  } catch (error) {
    console.error('刷新游戏库统计失败:', error)
  }
}

// 更新统计数据：现在直接调用后端刷新
const updateStats = () => {
  stats.value.connectedCount = connectedPlatforms.value.length
  refreshStats()
}

// 打开绑定模态框
const openBindModal = (platform) => {
  selectedPlatform.value = platform
  bindForm.value = {
    platformId: platform.id,
    steamId: '',
    apiKey: '',
    xboxUserId: '',
    psnOnlineId: '',
    gogUserId: '',
    accessToken: '',
    refreshToken: ''
  }
  showBindModal.value = true
}

// 关闭绑定模态框
const closeBindModal = () => {
  showBindModal.value = false
  selectedPlatform.value = null
}

// 绑定平台
const handleBind = async () => {
  if (!selectedPlatform.value) return

  const platform = selectedPlatform.value
  const requiredFields = platform.requires || []
  
  // 验证必填字段
  for (const field of requiredFields) {
    if (!bindForm.value[field] || bindForm.value[field].trim() === '') {
      alert(`请填写${getFieldLabel(field)}`)
      return
    }
  }

  loading.value = true
  try {
    const bindData = {
      platformId: platform.id
    }

    // 根据平台添加相应字段
    if (platform.id === 1) { // Steam
      bindData.steamId = bindForm.value.steamId
      bindData.apiKey = bindForm.value.apiKey
    } else if (platform.id === 7) { // Xbox
      bindData.xboxUserId = bindForm.value.xboxUserId
      if (bindForm.value.accessToken) bindData.accessToken = bindForm.value.accessToken
      if (bindForm.value.refreshToken) bindData.refreshToken = bindForm.value.refreshToken
    } else if (platform.id === 6) { // PSN
      bindData.psnOnlineId = bindForm.value.psnOnlineId
      if (bindForm.value.accessToken) bindData.accessToken = bindForm.value.accessToken
      if (bindForm.value.refreshToken) bindData.refreshToken = bindForm.value.refreshToken
    } else if (platform.id === 5) { // GOG
      bindData.gogUserId = bindForm.value.gogUserId
      if (bindForm.value.accessToken) bindData.accessToken = bindForm.value.accessToken
      if (bindForm.value.refreshToken) bindData.refreshToken = bindForm.value.refreshToken
    }

    const response = await platformsApi.bindPlatform(bindData)
    if (response.success) {
      alert(`${platform.name}绑定成功！`)

      // 绑定成功后，根据平台调用对应的导入接口
      const userId = getCurrentUserId()
      if (userId) {
        switch (platform.id) {
          case 1: // Steam
            try {
              await steamApi.importData({
                userId,
                steamId: bindForm.value.steamId,
                importGames: true,
                importAchievements: true,
                importFriends: false
              })
              await new Promise(resolve => setTimeout(resolve, 500))
            } catch (e) {
              console.error('Steam 数据导入失败:', e)
            }
            break
          
          case 7: // Xbox
            try {
              const xboxUserId = bindForm.value.xboxUserId || response.data?.platformUserId
              if (xboxUserId) {
                await xboxApi.importData({
                  userId,
                  xboxUserId,
                  importGames: true,
                  importAchievements: true
                })
                await new Promise(resolve => setTimeout(resolve, 500))
              }
            } catch (e) {
              console.error('Xbox 数据导入失败:', e)
            }
            break
          
          case 6: // PSN
            try {
              const psnOnlineId = bindForm.value.psnOnlineId || response.data?.platformUserId
              if (psnOnlineId) {
                await psnApi.importData({
                  userId,
                  psnOnlineId,
                  importGames: true,
                  importTrophies: true
                })
                await new Promise(resolve => setTimeout(resolve, 500))
              }
            } catch (e) {
              console.error('PSN 数据导入失败:', e)
            }
            break
          
          case 5: // GOG
            try {
              const gogUserId = bindForm.value.gogUserId || response.data?.platformUserId
              if (gogUserId) {
                await gogApi.importData({
                  userId,
                  gogUserId,
                  importGames: true
                })
                await new Promise(resolve => setTimeout(resolve, 500))
              }
            } catch (e) {
              console.error('GOG 数据导入失败:', e)
            }
            break
        }
      }

      closeBindModal()
      await loadBindings()
      // 刷新统计数据，确保显示最新的成就数量
      await refreshStats()
    }
  } catch (error) {
    console.error('绑定平台失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('绑定失败: ' + errorMessage)
  } finally {
    loading.value = false
  }
}

// 获取字段标签
const getFieldLabel = (field) => {
  const labels = {
    steamId: 'Steam ID',
    apiKey: 'Steam API Key',
    xboxUserId: 'Xbox用户ID',
    psnOnlineId: 'PSN在线ID',
    gogUserId: 'GOG用户ID',
    accessToken: '访问令牌',
    refreshToken: '刷新令牌'
  }
  return labels[field] || field
}

// 解绑平台
const handleUnbind = async (binding) => {
  if (!confirm(`确定要解绑${binding.platformName}吗？`)) {
    return
  }

  loading.value = true
  try {
    const response = await platformsApi.unbindPlatform(binding.bindingId)
    if (response.success) {
      alert(`${binding.platformName}解绑成功！`)
      await loadBindings()
    }
  } catch (error) {
    console.error('解绑平台失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('解绑失败: ' + errorMessage)
  } finally {
    loading.value = false
  }
}

// 同步平台
const handleSync = async (platformId) => {
  loading.value = true
  try {
    const userId = getCurrentUserId()
    const binding = connectedPlatforms.value.find(b => b.platformId === platformId)
    
    if (!userId || !binding?.platformUserId) {
      throw new Error('用户ID或平台用户ID缺失')
    }

    // 根据平台ID调用对应的导入接口
    switch (platformId) {
      case 1: // Steam
        try {
          await steamApi.importData({
            userId,
            steamId: binding.platformUserId,
            importGames: true,
            importAchievements: true,
            importFriends: false
          })
          await new Promise(resolve => setTimeout(resolve, 500))
        } catch (e) {
          console.error('Steam 数据导入失败:', e)
          throw e
        }
        break
      
      case 7: // Xbox
        try {
          await xboxApi.importData({
            userId,
            xboxUserId: binding.platformUserId,
            importGames: true,
            importAchievements: true
          })
          await new Promise(resolve => setTimeout(resolve, 500))
        } catch (e) {
          console.error('Xbox 数据导入失败:', e)
          throw e
        }
        break
      
      case 6: // PSN
        try {
          await psnApi.importData({
            userId,
            psnOnlineId: binding.platformUserId,
            importGames: true,
            importTrophies: true
          })
          await new Promise(resolve => setTimeout(resolve, 500))
        } catch (e) {
          console.error('PSN 数据导入失败:', e)
          throw e
        }
        break
      
      case 5: // GOG
        try {
          await gogApi.importData({
            userId,
            gogUserId: binding.platformUserId,
            importGames: true
          })
          await new Promise(resolve => setTimeout(resolve, 500))
        } catch (e) {
          console.error('GOG 数据导入失败:', e)
          throw e
        }
        break
      
      default:
        // 对于其他平台，暂时只调用占位符接口
        await platformsApi.syncPlatform(platformId)
    }

    alert('同步成功！')
    await loadBindings()
    // 刷新统计数据，确保显示最新的成就数量
    await refreshStats()
  } catch (error) {
    console.error('同步平台失败:', error)
    alert('同步失败: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

// 切换开关
const toggleSwitch = (type) => {
  syncSettings.value[type] = !syncSettings.value[type]
}

// 获取平台配置
const getPlatformConfig = (platformId) => {
  return platformConfig[platformId] || { name: 'Unknown', gradient: '' }
}

// 格式化时间
const formatTime = (dateString) => {
  if (!dateString) return '未知'
  const date = new Date(dateString)
  const now = new Date()
  const diff = Math.floor((now - date) / 1000 / 60) // 分钟差
  
  if (diff < 1) return '刚刚'
  if (diff < 60) return `${diff}分钟前`
  if (diff < 1440) return `${Math.floor(diff / 60)}小时前`
  return `${Math.floor(diff / 1440)}天前`
}

// 获取平台Logo
const getPlatformLogo = (platformId) => {
  const logos = {
    1: 'https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/1024px-Steam_icon_logo.svg.png',
    2: 'https://upload.wikimedia.org/wikipedia/commons/a/a7/Epic_Games_logo.png',
    5: 'https://w7.pngwing.com/pngs/403/46/png-transparent-gog-galaxy-alt-macos-bigsur-icon-thumbnail.png',
    6: 'https://images.seeklogo.com/logo-png/49/1/playstation-logo-png_seeklogo-494440.png',
    7: 'https://upload.wikimedia.org/wikipedia/commons/thumb/f/f9/Xbox_one_logo.svg/2048px-Xbox_one_logo.svg.png',
    8: 'https://images.seeklogo.com/logo-png/31/1/nintendo-switch-logo-png_seeklogo-315901.png'
  }
  return logos[platformId] || ''
}

// 获取平台描述
const getPlatformDescription = (platformId) => {
  const descriptions = {
    1: '同步你的Steam游戏库和成就',
    2: '同步Epic Games商店的游戏',
    5: '无DRM保护的游戏和经典游戏作品',
    6: '同步你的PlayStation奖杯和游戏库',
    7: '同步Xbox游戏和成就',
    8: '连接你的任天堂账号以同步Switch游戏'
  }
  return descriptions[platformId] || '连接平台以同步游戏库'
}

// 同步全部平台
const handleSyncAll = async () => {
  if (connectedPlatforms.value.length === 0) {
    alert('没有已连接的平台')
    return
  }
  
  loading.value = true
  try {
    const userId = getCurrentUserId()
    
    for (const binding of connectedPlatforms.value) {
      if (!userId || !binding.platformUserId) continue

      // 根据平台ID调用对应的导入接口
      switch (binding.platformId) {
        case 1: // Steam
          try {
            await steamApi.importData({
              userId,
              steamId: binding.platformUserId,
              importGames: true,
              importAchievements: true,
              importFriends: false
            })
            await new Promise(resolve => setTimeout(resolve, 500))
          } catch (e) {
            console.error('Steam 数据导入失败:', e)
          }
          break
        
        case 7: // Xbox
          try {
            await xboxApi.importData({
              userId,
              xboxUserId: binding.platformUserId,
              importGames: true,
              importAchievements: true
            })
            await new Promise(resolve => setTimeout(resolve, 500))
          } catch (e) {
            console.error('Xbox 数据导入失败:', e)
          }
          break
        
        case 6: // PSN
          try {
            await psnApi.importData({
              userId,
              psnOnlineId: binding.platformUserId,
              importGames: true,
              importTrophies: true
            })
            await new Promise(resolve => setTimeout(resolve, 500))
          } catch (e) {
            console.error('PSN 数据导入失败:', e)
          }
          break
        
        case 5: // GOG
          try {
            await gogApi.importData({
              userId,
              gogUserId: binding.platformUserId,
              importGames: true
            })
            await new Promise(resolve => setTimeout(resolve, 500))
          } catch (e) {
            console.error('GOG 数据导入失败:', e)
          }
          break
        
        default:
          // 对于其他平台，暂时只调用占位符接口
          try {
            await platformsApi.syncPlatform(binding.platformId)
          } catch (e) {
            console.error(`平台 ${binding.platformId} 同步失败:`, e)
          }
      }
    }
    alert('全部平台同步成功！')
    await loadBindings()
    // 刷新统计数据，确保显示最新的成就数量
    await refreshStats()
  } catch (error) {
    console.error('同步全部平台失败:', error)
    alert('同步失败: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadBindings()
  refreshStats()
})
</script>

<style scoped>
/* 基础样式 */
.app-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #000000 0%, #000000 50%, #0f0f0f 100%);
  color: #ffffff;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部样式 */
.header {
  position: sticky;
  top: 0;
  z-index: 50;
  background: rgba(10, 10, 10, 0.8);
  backdrop-filter: blur(24px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.header-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-title h1 {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0 0 0.25rem 0;
}

.header-title p {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin: 0;
}

.status-badge {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1rem;
  font-size: 0.75rem;
}

/* 主要内容 */
.main-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem;
}

/* 统计网格 */
.stats-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

@media (min-width: 768px) {
  .stats-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
}

.stat-icon {
  width: 3rem;
  height: 3rem;
  border-radius: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.bg-indigo {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.bg-emerald {
  background: rgba(16, 185, 129, 0.2);
  color: #6ee7b7;
}

.bg-amber {
  background: rgba(245, 158, 11, 0.2);
  color: #fcd34d;
}

.bg-rose {
  background: rgba(244, 63, 94, 0.2);
  color: #fda4af;
}

.stat-info {
  flex: 1;
}

.stat-label {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin-bottom: 0.25rem;
}

.stat-value {
  font-size: 1.25rem;
  font-weight: 700;
}

/* 通用样式 */
.section {
  margin-bottom: 2rem;
}

.section-title {
  font-size: 1.125rem;
  font-weight: 700;
  margin: 0 0 1rem 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

/* 平台网格 */
.platform-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.5rem;
}

@media (min-width: 768px) {
  .platform-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .platform-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

.platform-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
  overflow: hidden;
  transition: all 0.3s ease;
}

.platform-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
}

.platform-card.connected {
  border-color: rgba(34, 197, 94, 0.3);
}

.platform-header {
  padding: 1.5rem;
}

.platform-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.platform-info > div:first-child {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.platform-logo {
  width: 3rem;
  height: 3rem;
  background: #ffffff;
  border-radius: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.platform-logo img {
  width: 2rem;
  height: 2rem;
  object-fit: contain;
}

.platform-details h3 {
  font-size: 1rem;
  font-weight: 700;
  margin: 0 0 0.25rem 0;
}

.platform-details p {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.6);
  margin: 0;
}

.badge {
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.7rem;
  font-weight: 700;
}

.badge.success {
  background: rgba(34, 197, 94, 0.2);
  color: #4ade80;
  border: 1px solid rgba(34, 197, 94, 0.3);
}

.platform-stats {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.platform-desc {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
  margin: 0;
}

.platform-actions {
  padding: 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.last-synced {
  font-size: 0.7rem;
  color: #71717a;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

/* 渐变样式 */
.steam-gradient {
  background: linear-gradient(135deg, #1b2838 0%, #2a475e 100%);
}

.epic-gradient {
  background: linear-gradient(135deg, #2a2a2a 0%, #1a1a1a 100%);
}

.xbox-gradient {
  background: linear-gradient(135deg, #107c10 0%, #0e6b0e 100%);
}

.playstation-gradient {
  background: linear-gradient(135deg, #003791 0%, #00246d 100%);
}

.nintendo-gradient {
  background: linear-gradient(135deg, #e60012 0%, #c4000f 100%);
}

.gog-gradient {
  background: linear-gradient(135deg, #86328a 0%, #5c2260 100%);
}

.battlenet-gradient {
  background: linear-gradient(135deg, #00aeff 0%, #0078d4 100%);
}

/* 按钮样式 */
.btn {
  padding: 0.5rem 0.75rem;
  border-radius: 0.75rem;
  font-size: 0.75rem;
  font-weight: 500;
  border: none;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #4f46e5;
  color: #ffffff;
}

.btn-primary:hover {
  background: #4338ca;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.05);
  color: #ffffff;
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-tertiary {
  background: rgba(255, 255, 255, 0.05);
  color: #ffffff;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-tertiary:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-danger {
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
}

.btn-danger:hover {
  background: rgba(239, 68, 68, 0.2);
}

.w-full {
  width: 100%;
}

/* 设置卡片 */
.settings-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
  padding: 1.5rem;
}

.settings-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 1rem;
}

.setting-info h3 {
  font-size: 0.875rem;
  font-weight: 500;
  margin: 0 0 0.25rem 0;
}

.setting-info p {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin: 0;
}

/* 开关样式 */
.toggle-switch {
  width: 3rem;
  height: 1.75rem;
  border-radius: 9999px;
  background: #3f3f46;
  position: relative;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toggle-switch.active {
  background: #4f46e5;
}

.toggle-thumb {
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 50%;
  background: #ffffff;
  position: absolute;
  top: 0.25rem;
  left: 0.25rem;
  transition: all 0.2s ease;
}

.toggle-switch.active .toggle-thumb {
  left: calc(100% - 1.25rem - 0.25rem);
}

.settings-actions {
  display: flex;
  gap: 1rem;
  margin-top: 1.5rem;
}

/* 图标样式 */
.icon {
  width: 1.25rem;
  height: 1.25rem;
}

.icon-success {
  color: #4ade80;
}

.icon-muted {
  color: #a1a1aa;
}

.text-success {
  color: #4ade80;
}

.font-bold {
  font-weight: 700;
}

/* 模态框样式 */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: rgba(20, 20, 23, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 1.25rem;
  padding: 2rem;
  max-width: 500px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}

.modal-title {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0;
}

.modal-close {
  background: transparent;
  border: none;
  color: #a1a1aa;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 0.5rem;
  transition: all 0.2s;
}

.modal-close:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #ffffff;
}

.modal-body {
  margin-bottom: 1.5rem;
}

.form-group {
  margin-bottom: 1.25rem;
}

.form-group label {
  display: block;
  font-size: 0.875rem;
  font-weight: 500;
  margin-bottom: 0.5rem;
  color: #ffffff;
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 0.75rem;
  color: #ffffff;
  font-size: 0.875rem;
  transition: all 0.2s;
}

.form-input:focus {
  outline: none;
  border-color: #4f46e5;
  background: rgba(255, 255, 255, 0.08);
}

.form-hint {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin-top: 0.25rem;
  margin-bottom: 0;
}

.form-hint a {
  color: #4f46e5;
  text-decoration: none;
}

.form-hint a:hover {
  text-decoration: underline;
}

.modal-footer {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
}

/* 加载遮罩 */
.loading-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.loading-spinner {
  background: rgba(20, 20, 23, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 1rem;
  padding: 2rem;
  color: #ffffff;
  font-size: 1rem;
}
</style>