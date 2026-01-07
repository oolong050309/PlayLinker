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
            <div class="stat-value">{{ stats.connectedCount }} / 5</div>
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
              <div class="time-info">
                <span class="last-synced">同步时间：{{ formatTime(binding.bindingTime) }}</span>
                <span class="last-synced" v-if="binding.lastSyncTime">最后同步：{{ formatTime(binding.lastSyncTime) }}</span>
              </div>
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

          <!-- Xbox认证流程 -->
          <div v-if="selectedPlatform?.id === 7">
            <div class="form-group">
              <div class="alert alert-info">
                <p><strong>Xbox绑定</strong></p>
                <p>无需填写用户ID，认证成功后会自动获取并完成绑定。</p>
              </div>
            </div>
            <div class="form-group">
              <label>
                <input 
                  type="checkbox" 
                  v-model="bindForm.openBrowser"
                  style="margin-right: 0.5rem;"
                />
                打开浏览器进行OAuth2登录
              </label>
              
            </div>
            <div v-if="xboxAuthStep === 'authUrl'" class="form-group">
              <div class="alert alert-info">
                <p><strong>请在浏览器中完成登录</strong></p>
                <p v-if="xboxAuthUrl" style="word-break: break-all; margin: 0.5rem 0;">
                  <a :href="xboxAuthUrl" target="_blank" style="color: #4f46e5;">{{ xboxAuthUrl }}</a>
                </p>
                <p style="margin-top: 0.5rem;">登录完成后，系统会自动完成认证</p>
              </div>
            </div>
          </div>

          <!-- PSN认证流程 -->
          <div v-if="selectedPlatform?.id === 6">
            <div class="form-group">
              <div class="alert alert-info">
                <p><strong>PlayStation 绑定</strong></p>
                <p>无需填写在线ID，认证成功后会自动获取并完成绑定。</p>
              </div>
            </div>
            <div class="form-group">
              <label>NPSSO *</label>
              <input 
                v-model="bindForm.npsso" 
                type="text" 
                class="form-input"
                placeholder="请输入64位NPSSO字符串"
              />
              <p class="form-hint">
                如何获取NPSSO：
                <br>1. 在浏览器中登录 PlayStation 账户
                <br>2. 访问: <a href="https://ca.account.sony.com/api/v1/ssocookie" target="_blank" style="color: #4f46e5;">https://ca.account.sony.com/api/v1/ssocookie</a>
                <br>3. 复制返回的 npsso 值(64个字符的字符串)
              </p>
            </div>
          </div>

          <!-- GOG认证流程（两步） -->
          <div v-if="selectedPlatform?.id === 5">
            <!-- 步骤1: 获取认证URL -->
            <div v-if="gogAuthStep === 'step1'">
              <div class="form-group">
                <div class="alert alert-info">
                  <p><strong>步骤1: 获取认证URL</strong></p>
                  <p v-if="!gogAuthUrl">点击下方按钮获取认证URL，然后在浏览器中打开并完成登录</p>
                  <p v-else>已获取认证URL，请按照下方说明完成登录</p>
                </div>
              </div>
              <div v-if="gogAuthUrl" class="form-group">
                <label>认证URL</label>
                <textarea
                  :value="gogAuthUrl"
                  class="form-input"
                  readonly
                  rows="3"
                  style="resize: vertical;"
                  @focus="$event.target.select()"
                ></textarea>
                <div style="display: flex; gap: 0.5rem; margin-top: 0.5rem;">
                  <button class="btn btn-secondary" @click="copyToClipboard(gogAuthUrl)">复制认证URL</button>
                  <button class="btn btn-primary" @click="openUrl(gogAuthUrl)">在新标签页打开</button>
                </div>
                <p class="form-hint">
                  <strong>操作步骤：</strong><br>
                  1) 点击“复制认证URL”或“在新标签页打开”<br>
                  2) 在浏览器中完成GOG登录<br>
                  3) 登录成功后会跳转到类似这样的URL：<code>https://embed.gog.com/on_login_success?origin=client&amp;code=xxxxx</code><br>
                  4) 复制浏览器地址栏的<strong>完整URL</strong>，然后点击下方“进入下一步”按钮粘贴
                </p>
              </div>
            </div>
            <!-- 步骤2: 提供重定向URL -->
            <div v-if="gogAuthStep === 'step2'">
              <div class="form-group">
                <div class="alert alert-success">
                  <p><strong>步骤2: 提供重定向URL完成认证</strong></p>
                  <p>请复制浏览器地址栏的完整URL并粘贴到下方</p>
                </div>
              </div>
              <div class="form-group">
                <label>登录成功后的跳转URL*</label>
                <textarea
                  v-model="bindForm.redirectUrl"
                  class="form-input"
                  rows="3"
                  style="resize: vertical;"
                  placeholder="把浏览器地址栏里的完整URL粘贴到这里，例如：https://embed.gog.com/on_login_success?origin=client&code=xxxxx"
                ></textarea>
                <p class="form-hint">
                  只需要复制浏览器地址栏<strong>完整URL</strong>，不需要你手动提取 code。
                </p>
              </div>
            </div>
            <!-- 刷新令牌提示 -->
            <div v-if="gogAuthStep === 'refresh'">
              <div class="form-group">
                <div class="alert alert-info">
                  <p><strong>刷新令牌</strong></p>
                  <p>如果已有有效令牌，会自动刷新，无需提供redirectUrl</p>
                </div>
              </div>
            </div>
          </div>

          <!-- Epic Games认证流程 -->
          <div v-if="selectedPlatform?.id === 2">
            <div class="form-group">
              <div class="alert alert-info">
                <p><strong>Epic Games 绑定</strong></p>
                <p>Epic Games需要通过Legendary CLI进行认证。</p>
              </div>
            </div>
            <div class="form-group">
              <div class="alert alert-warning">
                <p><strong>方式: 使用授权码</strong></p>
                <p>1. 访问以下链接并登录Epic账户：</p>
                <p style="word-break: break-all; margin: 0.5rem 0;">
                  <a href="https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code" target="_blank" style="color: #4f46e5;">
                    https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code
                  </a>
                </p>
                <p>2. 登录后，从URL中复制 <code>code</code> 参数的值</p>
                <p>3. 将授权码粘贴到下方输入框</p>
              </div>
            </div>
            <div class="form-group">
              <label>授权码</label>
              <input 
                v-model="bindForm.epicCode" 
                type="text" 
                class="form-input"
              />
            </div>
          </div>
            </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeBindModal">取消</button>
          <button 
            v-if="selectedPlatform?.id === 5 && gogAuthStep === 'step1' && !gogAuthUrl"
            class="btn btn-primary" 
            @click="handleBind"
            :disabled="loading"
          >
            {{ loading ? '获取认证URL中...' : '获取认证URL' }}
          </button>
          <button 
            v-if="selectedPlatform?.id === 5 && gogAuthStep === 'step1' && gogAuthUrl"
            class="btn btn-primary" 
            @click="gogAuthStep = 'step2'"
          >
            进入下一步（已获取URL）
          </button>
          <button 
            v-else-if="selectedPlatform?.id === 5 && gogAuthStep === 'step2'"
            class="btn btn-primary" 
            @click="handleBind"
            :disabled="loading"
          >
            {{ loading ? '认证中...' : '完成认证' }}
          </button>
          <button 
            v-else-if="selectedPlatform?.id === 5 && gogAuthStep === 'refresh'"
            class="btn btn-primary" 
            @click="handleBind"
            :disabled="loading"
          >
            {{ loading ? '刷新令牌中...' : '刷新令牌' }}
          </button>
          <button 
            v-else
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
import { CheckCircle, Link, Gamepad2, Trophy, Clock, PlusCircle, RefreshCw, X } from 'lucide-vue-next'
import { platformsApi } from '@/api/platforms'
import { libraryApi, steamApi, xboxApi, psnApi, gogApi, epicApi } from '@/api/index'

// 平台配置
const platformConfig = {
  1: { name: 'Steam', id: 1, icon: 'steam', gradient: 'steam-gradient', requires: ['steamId', 'apiKey'] },
  2: { name: 'Epic Games', id: 2, icon: 'epic', gradient: 'epic-gradient', requires: [] },
  5: { name: 'GOG Galaxy', id: 5, icon: 'gog', gradient: 'gog-gradient', requires: ['gogUserId'] },
  6: { name: 'PlayStation', id: 6, icon: 'playstation', gradient: 'playstation-gradient', requires: [] },
  7: { name: 'Xbox', id: 7, icon: 'xbox', gradient: 'xbox-gradient', requires: [] },
  // 8: { name: 'Nintendo Switch', id: 8, icon: 'nintendo', gradient: 'nintendo-gradient', requires: [] } // 暂时隐藏
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
  xboxUserId: '', // 已废弃：Xbox绑定不需要手动输入
  psnOnlineId: '', // 已废弃：PSN绑定不需要手动输入
  npsso: '',
  gogUserId: '',
  redirectUrl: '',
  accessToken: '',
  epicCode: '', // Epic Games授权码
  refreshToken: '',
  openBrowser: true // Xbox认证选项
})

// GOG认证步骤状态
const gogAuthStep = ref('step1') // 'step1' | 'step2' | 'refresh'
const gogAuthUrl = ref('')

// Xbox认证状态
const xboxAuthStep = ref('') // '' | 'authUrl'
const xboxAuthUrl = ref('')

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

      // 获取所有平台中最近的同步时间
      const platformStats = o.platformStats || o.PlatformStats || []
      let latestSyncTime = null
      if (platformStats.length > 0) {
        // 找到最近的同步时间
        const syncTimes = platformStats
          .map(p => p.lastSyncTime || p.LastSyncTime)
          .filter(t => t != null)
          .map(t => new Date(t.endsWith('Z') ? t : t + 'Z'))
        
        if (syncTimes.length > 0) {
          latestSyncTime = new Date(Math.max(...syncTimes.map(d => d.getTime())))
        }
      }
      
      // 如果没有找到同步时间，使用当前时间或显示"从未同步"
      if (latestSyncTime) {
        stats.value.lastSync = formatTime(latestSyncTime.toISOString())
      } else {
        stats.value.lastSync = '从未同步'
      }
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
const openBindModal = async (platform) => {
  selectedPlatform.value = platform
  bindForm.value = {
    platformId: platform.id,
    steamId: '',
    apiKey: '',
    xboxUserId: '', // 已废弃：Xbox绑定不需要手动输入
    psnOnlineId: '', // 已废弃：PSN绑定不需要手动输入
    npsso: '',
    gogUserId: '',
    redirectUrl: '',
    accessToken: '',
    refreshToken: '',
    openBrowser: true
  }
  
  // 重置认证状态
  gogAuthStep.value = 'step1'
  gogAuthUrl.value = ''
  xboxAuthStep.value = ''
  xboxAuthUrl.value = ''
  
  // 如果是GOG、PSN或Xbox，先检查令牌状态
  if (platform.id === 5) { // GOG
    try {
      const statusRes = await gogApi.checkTokenStatus()
      if (statusRes.success && statusRes.data?.success) {
        gogAuthStep.value = 'refresh'
      }
    } catch (error) {
      console.log('检查GOG令牌状态失败，将进行首次认证')
    }
  } else if (platform.id === 6) { // PSN
    try {
      const statusRes = await psnApi.checkTokenStatus()
      if (statusRes.success && statusRes.data?.success) {
        // 令牌有效，可以直接绑定
      }
    } catch (error) {
      console.log('检查PSN令牌状态失败，将进行首次认证')
    }
  } else if (platform.id === 7) { // Xbox
    try {
      const statusRes = await xboxApi.checkTokenStatus()
      if (statusRes.success && statusRes.data?.success) {
        // 令牌有效，可以直接绑定
      }
    } catch (error) {
      console.log('检查Xbox令牌状态失败，将进行首次认证')
    }
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
  
  loading.value = true
  try {
    // GOG认证流程（两步）
    if (platform.id === 5) {
      if (gogAuthStep.value === 'step1') {
        // 步骤1: 获取认证URL
        try {
          const authRes = await gogApi.authenticate({
            forceReauth: false
          })
          
          if (authRes.success && authRes.data) {
            if (authRes.data.needsBrowserAuth && authRes.data.authUrl) {
              // 获取到认证URL，保持在步骤1显示URL，不立即跳转到步骤2
              gogAuthUrl.value = authRes.data.authUrl
              // 不立即跳转到step2，让用户先看到URL并完成登录
              // gogAuthStep.value = 'step2' // 注释掉，让用户手动点击"进入下一步"
            } else if (authRes.data.success) {
              // 已有有效令牌，直接刷新
              alert('GOG认证成功！正在同步数据...')
              await handleGogPostAuth(authRes)
            } else {
              alert(authRes.data.message || '获取认证URL失败')
            }
          }
        } catch (error) {
          console.error('GOG认证失败:', error)
          alert('GOG认证失败: ' + (error.message || '未知错误'))
        } finally {
          loading.value = false
        }
        return
      } else if (gogAuthStep.value === 'step2') {
        // 步骤2: 提供重定向URL完成认证
        if (!bindForm.value.redirectUrl || bindForm.value.redirectUrl.trim() === '') {
          alert('请提供重定向URL')
          loading.value = false
          return
        }
        
        try {
          const authRes = await gogApi.authenticate({
            redirectUrl: bindForm.value.redirectUrl,
            forceReauth: false
          })
          
          if (authRes.success && authRes.data?.success) {
            alert('GOG认证成功！正在同步数据...')
            await handleGogPostAuth(authRes)
          } else {
            alert(authRes.data?.message || 'GOG认证失败')
          }
        } catch (error) {
          console.error('GOG认证失败:', error)
          alert('GOG认证失败: ' + (error.message || '未知错误'))
        } finally {
          loading.value = false
        }
        return
      } else if (gogAuthStep.value === 'refresh') {
        // 刷新令牌
        try {
          const authRes = await gogApi.authenticate({
            forceReauth: false
          })
          
          if (authRes.success && authRes.data?.success) {
            alert('GOG令牌刷新成功！正在同步数据...')
            await handleGogPostAuth(authRes)
          } else {
            // 刷新失败，回到步骤1
            gogAuthStep.value = 'step1'
            alert('令牌刷新失败，请重新进行认证')
          }
        } catch (error) {
          console.error('GOG令牌刷新失败:', error)
          gogAuthStep.value = 'step1'
          alert('令牌刷新失败，请重新进行认证')
        } finally {
          loading.value = false
        }
        return
      }
    }
    
    // PSN认证流程
    if (platform.id === 6) {
      if (!bindForm.value.npsso || bindForm.value.npsso.trim() === '') {
        alert('请填写NPSSO')
        loading.value = false
        return
      }
      
      try {
        const authRes = await psnApi.authenticate({
          npsso: bindForm.value.npsso,
          forceReauth: false
        })
        
        if (authRes.success && authRes.data?.success) {
          alert('PSN认证成功！正在同步数据...')
          await handlePsnPostAuth(authRes)
        } else {
          alert(authRes.data?.message || 'PSN认证失败')
        }
      } catch (error) {
        console.error('PSN认证失败:', error)
        alert('PSN认证失败: ' + (error.message || '未知错误'))
      } finally {
        loading.value = false
      }
      return
    }
    
    // Xbox认证流程
    if (platform.id === 7) {
      try {
        const authRes = await xboxApi.authenticate({
          openBrowser: bindForm.value.openBrowser,
          forceReauth: false
        })
        
        if (authRes.success && authRes.data) {
          if (authRes.data.needsBrowserAuth && authRes.data.authUrl) {
            xboxAuthUrl.value = authRes.data.authUrl
            xboxAuthStep.value = 'authUrl'
            if (bindForm.value.openBrowser) {
              openUrl(authRes.data.authUrl)
            }
            alert('请在浏览器中完成登录，登录完成后系统会自动完成认证')
            // 轮询检查认证状态
            checkXboxAuthStatus()
          } else if (authRes.data.success) {
            alert('Xbox认证成功！正在同步数据...')
            await handleXboxPostAuth(authRes)
          } else {
            alert(authRes.data.message || 'Xbox认证失败')
          }
        }
      } catch (error) {
        console.error('Xbox认证失败:', error)
        alert('Xbox认证失败: ' + (error.message || '未知错误'))
      } finally {
        loading.value = false
      }
      return
    }
    
    // Epic Games认证流程
    if (platform.id === 2) {
      if (!bindForm.value.epicCode || bindForm.value.epicCode.trim() === '') {
        // 如果没有提供授权码，尝试检查令牌状态
        try {
          const statusRes = await epicApi.checkTokenStatus()
          if (statusRes.success && statusRes.data?.success) {
            alert('Epic Games已登录，正在同步数据...')
            await handleEpicPostAuth(statusRes)
          } else {
            alert(statusRes.data?.message || '请提供Epic Games授权码或先通过命令行登录')
          }
        } catch (error) {
          console.error('Epic Games令牌状态检查失败:', error)
          alert('Epic Games令牌状态检查失败: ' + (error.message || '未知错误'))
        } finally {
          loading.value = false
        }
        return
      }
      
      try {
        const authRes = await epicApi.authenticate({
          code: bindForm.value.epicCode,
          forceReauth: false
        })
        
        if (authRes.success && authRes.data) {
          if (authRes.data.success) {
            alert('Epic Games认证成功！正在同步数据...')
            await handleEpicPostAuth(authRes)
          } else {
            alert(authRes.data.message || 'Epic Games认证失败')
          }
        }
      } catch (error) {
        console.error('Epic Games认证失败:', error)
        alert('Epic Games认证失败: ' + (error.message || '未知错误'))
      } finally {
        loading.value = false
      }
      return
    }
    
    // Steam等其他平台的原有逻辑
    const requiredFields = platform.requires || []
    
    // 验证必填字段
    for (const field of requiredFields) {
      if (!bindForm.value[field] || bindForm.value[field].trim() === '') {
        alert(`请填写${getFieldLabel(field)}`)
        loading.value = false
        return
      }
    }

    const bindData = {
      platformId: platform.id
    }

    // 根据平台添加相应字段
    if (platform.id === 1) { // Steam
      bindData.steamId = bindForm.value.steamId
      bindData.apiKey = bindForm.value.apiKey
    }

    const response = await platformsApi.bindPlatform(bindData)
    if (response.success) {
      alert(`${platform.name}绑定成功！正在同步数据...`)

      // 绑定成功后，根据平台调用对应的导入接口
      try {
        if (platform.id === 1) { // Steam
          try {
            await steamApi.importData({
              steamId: bindForm.value.steamId,
              importGames: true,
              importAchievements: true,
              importFriends: false
            })
            await new Promise(resolve => setTimeout(resolve, 1000))
          } catch (e) {
            console.error('Steam 数据导入失败:', e)
            alert('Steam 数据同步失败，请稍后手动同步')
          }
        }
      } catch (error) {
        console.error('数据同步过程出错:', error)
      }

      closeBindModal()
      await loadBindings()
      await new Promise(resolve => setTimeout(resolve, 2000))
      await refreshStats()
      alert('数据同步完成！')
    }
  } catch (error) {
    console.error('绑定平台失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('绑定失败: ' + errorMessage)
  } finally {
    loading.value = false
  }
}

// GOG认证后的处理
const handleGogPostAuth = async (authResponse) => {
  try {
    // 从认证响应中获取gogUserId
    const gogUserId = authResponse?.data?.userId || bindForm.value.gogUserId
    
    if (!gogUserId) {
      alert('GOG认证成功，但无法获取用户ID，请手动输入GOG用户ID')
      return
    }

    // 先绑定平台（如果已绑定，409错误是正常的，继续执行同步）
    const bindData = {
      platformId: 5,
      gogUserId: gogUserId
    }
    
    let bindRes
    try {
      bindRes = await platformsApi.bindPlatform(bindData)
    } catch (bindError) {
      // 处理409冲突错误（绑定已存在）
      if (bindError.response?.status === 409) {
        console.log('GOG平台已绑定，继续执行同步...')
        bindRes = { success: true } // 视为成功，继续执行
      } else {
        // 其他错误则抛出
        throw bindError
      }
    }
    
    // 无论绑定是否成功（包括409冲突），都继续执行同步
    if (!bindRes || !bindRes.success) {
      // 如果不是409错误，才提示绑定失败
      if (bindRes && !bindRes.success) {
        alert('GOG绑定失败，但将继续尝试同步数据')
      }
    }

    // 绑定成功后立即进行同步
    const userId = getCurrentUserId()
    if (!userId) {
      alert('无法获取用户ID')
      return
    }

    try {
      await gogApi.importData({
        userId,
        gogUserId,
        importGames: true
      })
      await new Promise(resolve => setTimeout(resolve, 1000))
      
      closeBindModal()
      await loadBindings()
      await new Promise(resolve => setTimeout(resolve, 2000))
      await refreshStats()
      alert('GOG绑定成功并已完成数据同步！')
    } catch (error) {
      console.error('GOG绑定后同步失败:', error)
      const errorMessage = error.response?.data?.message || error.message || '未知错误'
      alert('GOG数据同步失败: ' + errorMessage + '\n请稍后手动同步')
      closeBindModal()
      await loadBindings()
    }
  } catch (error) {
    console.error('GOG认证后处理失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    const errorStatus = error.response?.status
    
    // 根据错误类型给出不同的提示
    if (errorStatus === 409) {
      alert('GOG平台已绑定，正在同步数据...')
      // 409错误时，仍然尝试同步
      const gogUserId = authResponse?.data?.userId || bindForm.value.gogUserId
      if (gogUserId) {
        const userId = getCurrentUserId()
        if (userId) {
          try {
            await gogApi.importData({
              userId,
              gogUserId,
              importGames: true
            })
            closeBindModal()
            await loadBindings()
            await refreshStats()
            alert('GOG数据同步完成！')
          } catch (syncError) {
            console.error('GOG数据同步失败:', syncError)
            alert('GOG数据同步失败，请稍后手动同步')
          }
        }
      }
    } else {
      alert('GOG认证后处理失败: ' + errorMessage)
    }
  }
}

// PSN认证后的处理
const handlePsnPostAuth = async (authResponse) => {
  try {
    // 从认证响应中获取onlineId，如果没有则使用表单中的值
    const psnOnlineId = authResponse?.data?.onlineId || bindForm.value.psnOnlineId // 优先使用认证返回
    
    if (psnOnlineId) {
      // 绑定平台
      const bindData = {
        platformId: 6,
        psnOnlineId: psnOnlineId
      }
      const bindRes = await platformsApi.bindPlatform(bindData)
      
      if (bindRes.success) {
        // 导入数据
        const userId = getCurrentUserId()
        await psnApi.importData({
          userId,
          psnOnlineId,
          importGames: true,
          importTrophies: true
        })
        await new Promise(resolve => setTimeout(resolve, 1000))
        
        closeBindModal()
        await loadBindings()
        await new Promise(resolve => setTimeout(resolve, 2000))
        await refreshStats()
        alert('数据同步完成！')
      }
    } else {
      alert('PSN认证成功，但无法获取在线ID，请手动输入PSN在线ID')
    }
  } catch (error) {
    console.error('PSN认证后处理失败:', error)
    alert('PSN认证成功，但数据同步失败，请稍后手动同步')
  }
}

// Epic Games认证后的处理
const handleEpicPostAuth = async (authResponse) => {
  try {
    if (!authResponse.success || !authResponse.data?.success) {
      alert('Epic Games认证失败')
      return
    }

    // 修复：从正确的路径获取epicAccountId
    const epicAccountId = authResponse.data?.epicAccountId
    if (!epicAccountId) {
      alert('无法获取Epic账户ID')
      return
    }

    // 先绑定平台（如果已绑定，409错误是正常的，继续执行同步）
    const bindData = {
      platformId: 2,
      epicAccountId: epicAccountId
    }
    
    let bindRes
    try {
      bindRes = await platformsApi.bindPlatform(bindData)
    } catch (bindError) {
      // 处理409冲突错误（绑定已存在）
      if (bindError.response?.status === 409) {
        console.log('Epic Games平台已绑定，继续执行同步...')
        bindRes = { success: true } // 视为成功，继续执行
      } else {
        // 其他错误则抛出
        throw bindError
      }
    }
    
    // 无论绑定是否成功（包括409冲突），都继续执行同步
    if (!bindRes || !bindRes.success) {
      // 如果不是409错误，才提示绑定失败
      if (bindRes && !bindRes.success) {
        alert('Epic Games绑定失败，但将继续尝试同步数据')
      }
    }

    // 导入Epic Games数据
    const userId = getCurrentUserId()
    if (!userId) {
      alert('无法获取用户ID')
      return
    }

    try {
      await epicApi.importData({
        userId: userId,
        epicAccountId: epicAccountId,
        importGames: true,
        importAchievements: true
      })
      await new Promise(resolve => setTimeout(resolve, 1000))
      
      closeBindModal()
      await loadBindings()
      await new Promise(resolve => setTimeout(resolve, 2000))
      await refreshStats()
      alert('Epic Games数据同步完成！')
    } catch (error) {
      console.error('Epic Games数据导入失败:', error)
      const errorMessage = error.response?.data?.message || error.message || '未知错误'
      alert('Epic Games数据同步失败: ' + errorMessage + '\n请稍后手动同步')
      closeBindModal()
      await loadBindings()
    }
  } catch (error) {
    console.error('Epic Games认证后处理失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    const errorStatus = error.response?.status
    
    // 根据错误类型给出不同的提示
    if (errorStatus === 409) {
      alert('Epic Games平台已绑定，正在同步数据...')
      // 409错误时，仍然尝试同步
      const epicAccountId = authResponse?.data?.epicAccountId
      if (epicAccountId) {
        const userId = getCurrentUserId()
        if (userId) {
          try {
            await epicApi.importData({
              userId: userId,
              epicAccountId: epicAccountId,
              importGames: true,
              importAchievements: true
            })
            closeBindModal()
            await loadBindings()
            await refreshStats()
            alert('Epic Games数据同步完成！')
          } catch (syncError) {
            console.error('Epic Games数据同步失败:', syncError)
            alert('Epic Games数据同步失败，请稍后手动同步')
          }
        }
      }
    } else {
      alert('Epic Games认证后处理失败: ' + errorMessage)
    }
  }
}

const handleXboxPostAuth = async (authResponse) => {
  try {
    // 从认证响应中获取xuid，如果没有则使用表单中的值
    const xboxUserId = authResponse?.data?.xuid || bindForm.value.xboxUserId // 优先使用认证返回
    
    if (xboxUserId) {
      // 绑定平台
      const bindData = {
        platformId: 7,
        xboxUserId: xboxUserId
      }
      const bindRes = await platformsApi.bindPlatform(bindData)
      
      if (bindRes.success) {
        // 导入数据
        const userId = getCurrentUserId()
        await xboxApi.importData({
          userId,
          xboxUserId,
          importGames: true,
          importAchievements: true
        })
        await new Promise(resolve => setTimeout(resolve, 1000))
        
        closeBindModal()
        await loadBindings()
        await new Promise(resolve => setTimeout(resolve, 2000))
        await refreshStats()
        alert('数据同步完成！')
      }
    } else {
      alert('Xbox认证成功，但无法获取用户ID，请手动输入Xbox用户ID')
    }
  } catch (error) {
    console.error('Xbox认证后处理失败:', error)
    alert('Xbox认证成功，但数据同步失败，请稍后手动同步')
  }
}

// 检查Xbox认证状态（轮询）
const checkXboxAuthStatus = async () => {
  const maxAttempts = 30
  let attempts = 0
  
  const checkInterval = setInterval(async () => {
    attempts++
    try {
      const statusRes = await xboxApi.checkTokenStatus()
      if (statusRes.success && statusRes.data?.success) {
        clearInterval(checkInterval)
        xboxAuthStep.value = ''
        alert('Xbox认证成功！正在同步数据...')
        await handleXboxPostAuth(statusRes)
      } else if (attempts >= maxAttempts) {
        clearInterval(checkInterval)
        alert('认证超时，请重试')
      }
    } catch (error) {
      if (attempts >= maxAttempts) {
        clearInterval(checkInterval)
        alert('认证检查失败，请重试')
      }
    }
  }, 2000) // 每2秒检查一次
}

// 复制到剪贴板
const copyToClipboard = async (text) => {
  try {
    await navigator.clipboard.writeText(text)
    alert('已复制到剪贴板')
  } catch (error) {
    console.error('复制失败:', error)
    alert('复制失败，请手动复制')
  }
}

// 打开URL
const openUrl = (url) => {
  window.open(url, '_blank')
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
      
      case 2: // Epic Games - 每次同步都需要重新认证
        try {
          // Epic Games同步需要重新走认证流程
          const statusRes = await epicApi.checkTokenStatus()
          if (statusRes.success && statusRes.data?.success) {
            // 令牌有效，直接导入数据
            await epicApi.importData({
              userId,
              epicAccountId: binding.platformUserId,
              importGames: true,
              importAchievements: true
            })
            await new Promise(resolve => setTimeout(resolve, 500))
          } else {
            // 令牌无效，需要重新认证
            alert('Epic Games令牌已过期，请重新认证。请在绑定页面重新绑定Epic Games账号。')
            throw new Error('Epic Games令牌已过期，需要重新认证')
          }
        } catch (e) {
          console.error('Epic Games 数据导入失败:', e)
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


// 获取平台配置
const getPlatformConfig = (platformId) => {
  return platformConfig[platformId] || { name: 'Unknown', gradient: '' }
}

// 格式化时间（中国时区）- 改进样式
const formatTime = (dateString) => {
  if (!dateString) return '未知'
  
  try {
    // 确保传入的字符串被解析为UTC时间
    const date = new Date(dateString.endsWith('Z') ? dateString : dateString + 'Z')
    
    const now = new Date()
    const diffMinutes = Math.floor((now.getTime() - date.getTime()) / 60000)

    if (diffMinutes < 1) return '刚刚'
    if (diffMinutes < 60) return `${diffMinutes}分钟前`
    
    const diffHours = Math.floor(diffMinutes / 60)
    if (diffHours < 24) return `${diffHours}小时前`
    
    const diffDays = Math.floor(diffHours / 24)
    if (diffDays < 7) return `${diffDays}天前`

    // 超过7天，显示具体日期和时间（格式：YYYY-MM-DD HH:mm）
    const options = {
      timeZone: 'Asia/Shanghai',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    }
    
    // 使用 toLocaleString 格式化为中国时区时间
    const formatter = new Intl.DateTimeFormat('zh-CN', options)
    const formatted = formatter.format(date).replace(/\//g, '-')
    
    // 格式化为：YYYY-MM-DD HH:mm
    return formatted

  } catch (error) {
    console.error('格式化时间失败:', error, '输入:', dateString)
    return '未知'
  }
}

// 获取平台Logo
const getPlatformLogo = (platformId) => {
  const logos = {
    1: 'https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/1024px-Steam_icon_logo.svg.png',
    2: 'https://upload.wikimedia.org/wikipedia/commons/a/a7/Epic_Games_logo.png',
    5: 'https://w7.pngwing.com/pngs/403/46/png-transparent-gog-galaxy-alt-macos-bigsur-icon-thumbnail.png',
    6: 'https://images.seeklogo.com/logo-png/49/1/playstation-logo-png_seeklogo-494440.png',
    7: 'https://upload.wikimedia.org/wikipedia/commons/thumb/f/f9/Xbox_one_logo.svg/2048px-Xbox_one_logo.svg.png',
    // 8: 'https://images.seeklogo.com/logo-png/31/1/nintendo-switch-logo-png_seeklogo-315901.png' // 暂时隐藏
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
    7: '同步Xbox游戏和成就'
    // 8: '连接你的任天堂账号以同步Switch游戏' // 暂时隐藏
  }
  return descriptions[platformId] || '连接平台以同步游戏库'
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
  background: rgba(255, 255, 255, 0.03);
  color: #ffffff;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部样式 */
.header {
  position: sticky;
  top: 0;
  z-index: 50;
  background: rgba(255, 255, 255, 0.03);
  backdrop-filter: blur(24px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
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

.time-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.last-synced {
  font-size: 0.7rem;
  color: #71717a;
  line-height: 1.2;
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

/* Alert样式 */
.alert {
  padding: 1rem;
  border-radius: 0.75rem;
  margin-bottom: 1rem;
  font-size: 0.875rem;
  line-height: 1.5;
}

.alert-info {
  background: rgba(59, 130, 246, 0.1);
  border: 1px solid rgba(59, 130, 246, 0.3);
  color: #93c5fd;
}

.alert-success {
  background: rgba(34, 197, 94, 0.1);
  border: 1px solid rgba(34, 197, 94, 0.3);
  color: #86efac;
}

.alert p {
  margin: 0.25rem 0;
}

.alert p:first-child {
  margin-top: 0;
}

.alert p:last-child {
  margin-bottom: 0;
}

.alert strong {
  color: #ffffff;
  font-weight: 600;
}
</style>