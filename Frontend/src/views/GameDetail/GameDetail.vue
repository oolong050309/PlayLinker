<template>
  <div class="game-detail-container">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading">
      <div class="loading-spinner"></div>
      <p>加载中...</p>
    </div>

    <!-- 错误状态 -->
    <div v-else-if="error" class="error">
      <p>{{ error }}</p>
      <button @click="loadGameDetail" class="retry-btn">重试</button>
    </div>

    <!-- 游戏详情内容 -->
    <div v-else-if="game" class="game-detail">
      <!-- 返回按钮 -->
      <button @click="$router.back()" class="back-btn">
        <ArrowLeft class="icon" size="20" />
        返回
      </button>

      <!-- Hero 区域 -->
      <div class="hero-section">
        <div class="hero-background" v-if="game.headerImage">
          <img :src="game.headerImage" :alt="game.name" class="hero-bg-img" />
          <div class="hero-gradient"></div>
        </div>

        <div class="hero-content">
          <div class="game-cover-wrapper">
            <img 
              :src="game.coverImage || game.headerImage || '/placeholder-game.png'" 
              :alt="game.name" 
              class="game-cover"
            />
          </div>

          <div class="game-header-info">
            <div class="game-badges">
              <span v-if="game.platform" class="platform-badge">{{ game.platform }}</span>
              <span v-if="game.genre" class="genre-badge">{{ game.genre }}</span>
            </div>
            <h1 class="game-title">{{ game.name || game.gameName }}</h1>
            
            <div class="game-stats">
              <div class="stat-item" v-if="game.releaseDate">
                <Calendar class="stat-icon" size="16" />
                <span>发行日期：{{ game.releaseDate }}</span>
              </div>
              <div class="stat-item" v-if="game.isFree !== undefined && game.isFree !== null">
                <span>{{ game.isFree ? '免费游戏' : '付费游戏' }}</span>
              </div>
              <div class="stat-item" v-if="gamePlaytime">
                <Clock class="stat-icon" size="16" />
                <span>{{ formatPlaytime(gamePlaytime) }}</span>
              </div>
              <div class="stat-item" v-if="achievementStats.total > 0">
                <Trophy class="stat-icon" size="16" />
                <span>{{ achievementStats.unlocked }} / {{ achievementStats.total }}</span>
              </div>
              <div class="stat-item" v-if="game.lastPlayed">
                <Calendar class="stat-icon" size="16" />
                <span>{{ formatDate(game.lastPlayed) }}</span>
              </div>
            </div>

            <div class="game-actions">
              <button class="btn-primary">
                <Play class="icon" size="18" />
                开始游戏
              </button>
              <button class="btn-secondary">
                <Settings class="icon" size="18" />
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- 主要内容区域 -->
      <div class="content-grid">
        <!-- 左侧主要内容 -->
        <div class="main-content">
          <!-- 游戏介绍 -->
          <section class="section-card">
            <h2 class="section-title">游戏介绍</h2>
            <div class="game-description">
              <p v-if="game.description" class="description-text">{{ game.description }}</p>
              <p v-else-if="game.shortDescription" class="description-text">{{ game.shortDescription }}</p>
              <p v-else class="description-text text-muted">暂无游戏介绍</p>
            </div>
          </section>

          <!-- 基本信息：发行日期 / 开发商 / 发行商 -->
          <section class="section-card">
            <h2 class="section-title">基本信息</h2>
            <div class="basic-info-grid">
              <div class="basic-info-item" v-if="game.releaseDate">
                <div class="basic-label">发行日期</div>
                <div class="basic-value">{{ game.releaseDate }}</div>
              </div>
              <div class="basic-info-item" v-if="game.developers && game.developers.length">
                <div class="basic-label">开发商</div>
                <div class="basic-value">
                  {{ game.developers.map(d => d.name || d.Name).join('，') }}
                </div>
              </div>
              <div class="basic-info-item" v-if="game.publishers && game.publishers.length">
                <div class="basic-label">发行商</div>
                <div class="basic-value">
                  {{ game.publishers.map(p => p.name || p.Name).join('，') }}
                </div>
              </div>
            </div>
          </section>

          <!-- 成就展示 -->
          <section class="section-card">
            <div class="section-header">
              <h2 class="section-title">成就</h2>
              <div class="achievement-progress" v-if="achievementStats.total > 0">
                {{ achievementStats.unlocked }} / {{ achievementStats.total }} 已解锁
                ({{ achievementProgress }}%)
              </div>
            </div>

            <div v-if="achievementsLoading" class="loading-small">
              <div class="loading-spinner-small"></div>
              <span>加载成就中...</span>
            </div>

            <div v-else-if="achievements.length === 0" class="empty-state">
              <Trophy class="empty-icon" size="48" />
              <p>暂无成就数据</p>
            </div>

            <div v-else class="achievements-grid">
              <div 
                v-for="achievement in achievements" 
                :key="achievement.id || achievement.achievementId"
                class="achievement-card"
                :class="{ unlocked: achievement.isUnlocked }"
              >
                <div class="achievement-icon-wrapper">
                  <img 
                    v-if="achievement.iconUnlocked && achievement.isUnlocked" 
                    :src="achievement.iconUnlocked" 
                    :alt="achievement.name"
                    class="achievement-icon"
                    @error="handleImageError"
                  />
                  <img 
                    v-else-if="achievement.iconLocked && !achievement.isUnlocked" 
                    :src="achievement.iconLocked" 
                    :alt="achievement.name"
                    class="achievement-icon locked"
                    @error="handleImageError"
                  />
                  <div v-else class="achievement-icon-placeholder">
                    <Trophy v-if="achievement.isUnlocked" class="placeholder-icon" size="24" />
                    <Lock v-else class="placeholder-icon" size="24" />
                  </div>
                </div>
                <div class="achievement-info">
                  <h3 class="achievement-name">{{ achievement.name || achievement.achievementName }}</h3>
                  <p class="achievement-desc">{{ achievement.description || achievement.achievementDescription || '暂无描述' }}</p>
                  <div class="achievement-meta">
                    <span v-if="achievement.unlockTime" class="unlock-time">
                      {{ formatDate(achievement.unlockTime) }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>

        <!-- 右侧边栏 -->
        <div class="sidebar">
          <!-- 价格监控 -->
          <section class="section-card">
            <h3 class="sidebar-title">价格监控</h3>
            <div class="price-monitor">
              <div class="price-current">
                <span class="price-label">当前价格</span>
                <span class="price-value">{{ currentPrice || '暂无数据' }}</span>
              </div>
              <div class="price-chart-placeholder">
                <div class="chart-bars">
                  <div 
                    v-for="(bar, index) in priceChartData" 
                    :key="index"
                    class="chart-bar"
                    :style="{ height: `${bar}%` }"
                  ></div>
                </div>
              </div>
              <div class="price-actions">
                <button class="btn-outline" @click="handlePriceMonitor">
                  <Bell class="icon" size="16" />
                  设置价格提醒
                </button>
              </div>
            </div>
          </section>

          <!-- Mod 管理 -->
          <section class="section-card">
            <h3 class="sidebar-title">Mod 管理</h3>
            <div class="mod-manager">
              <div v-if="mods.length === 0" class="mod-empty">
                <Package class="mod-empty-icon" size="32" />
                <p class="mod-empty-text">暂无已安装的 Mod</p>
                <button class="btn-outline" @click="handleManageMods">
                  管理 Mod
                </button>
              </div>
              <div v-else class="mod-list">
                <div 
                  v-for="mod in mods" 
                  :key="mod.id"
                  class="mod-item"
                >
                  <div class="mod-info">
                    <h4 class="mod-name">{{ mod.name }}</h4>
                    <p class="mod-version">{{ mod.version }}</p>
                  </div>
                  <div class="mod-toggle">
                    <input 
                      type="checkbox" 
                      :id="`mod-${mod.id}`"
                      v-model="mod.enabled"
                      @change="handleToggleMod(mod)"
                      class="toggle-switch"
                    />
                    <label :for="`mod-${mod.id}`" class="toggle-label"></label>
                  </div>
                </div>
                <button class="btn-outline full-width" @click="handleManageMods">
                  <Package class="icon" size="16" />
                  管理更多 Mod
                </button>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { gameApi, achievementApi, libraryApi } from '@/api'
import { ArrowLeft, Clock, Trophy, Calendar, Play, Settings, Bell, Package, Lock } from 'lucide-vue-next'

const route = useRoute()
const router = useRouter()

// 状态管理
const loading = ref(true)
const error = ref(null)
const game = ref(null)
const achievements = ref([])
const achievementsLoading = ref(false)
const gamePlaytime = ref(0)
const currentPrice = ref(null)
const priceChartData = ref([45, 52, 38, 61, 48, 55, 42]) // 模拟价格数据
const mods = ref([]) // 预留的 Mod 列表

// 计算属性
const achievementStats = computed(() => {
  const total = achievements.value.length
  const unlocked = achievements.value.filter(a => a.isUnlocked).length
  return { total, unlocked }
})

const achievementProgress = computed(() => {
  if (achievementStats.value.total === 0) return 0
  return ((achievementStats.value.unlocked / achievementStats.value.total) * 100).toFixed(1)
})

// 加载游戏详情
const loadGameDetail = async () => {
  loading.value = true
  error.value = null
  try {
    const gameId = route.params.id
    console.log('加载游戏详情，游戏ID:', gameId)

    // 1. 从游戏库API获取“个人数据”（只查当前游戏）
    try {
      const libraryResponse = await libraryApi.getGames({ gameId, page: 1, pageSize: 1 })
      console.log('游戏库个人数据响应:', libraryResponse)
      if (libraryResponse.success && libraryResponse.data) {
        const data = libraryResponse.data
        const items = data.items ?? data.Items ?? []
        if (items.length > 0) {
          const libraryGame = items[0]
          const ownedPlatforms = libraryGame.ownedPlatforms ?? libraryGame.OwnedPlatforms ?? []
          const platformName = ownedPlatforms.map(p => p.platformName ?? p.PlatformName).join(', ')
          
          const playtimeMinutes = libraryGame.playtimeMinutes ?? libraryGame.PlaytimeMinutes ?? 0
          const unlocked = libraryGame.achievementsUnlocked ?? libraryGame.AchievementsUnlocked ?? 0
          const total = libraryGame.achievementsTotal ?? libraryGame.AchievementsTotal ?? 0

          game.value = {
            id: libraryGame.gameId ?? libraryGame.GameId,
            name: libraryGame.name ?? libraryGame.Name,
            headerImage: libraryGame.headerImage ?? libraryGame.HeaderImage ?? '',
            description: '',
            platform: platformName || '未知平台',
            genre: '',
            isFree: null,
            releaseDate: null,
            shortDescription: '',
            detailedDescription: '',
            requirements: null,
            reviews: null,
            developers: [],
            publishers: [],
            playtimeMinutes,
            lastPlayed: libraryGame.lastPlayed ?? libraryGame.LastPlayed,
            achievementsUnlocked: unlocked,
            achievementsTotal: total
          }

          // 将分钟转换为小时用于展示
          gamePlaytime.value = playtimeMinutes > 0 ? playtimeMinutes / 60 : 0
        }
      }
    } catch (libErr) {
      console.log('从游戏库获取失败:', libErr)
    }

    // 2. 使用通用游戏API获取“公共详情”（题材/开发商/系统需求等）
    const gameResponse = await gameApi.getGame(gameId)
    console.log('通用游戏详情响应:', gameResponse)
    if (gameResponse.success && gameResponse.data) {
      const detail = gameResponse.data

      if (!game.value) {
        // 如果游戏库里没有记录，就完全使用公共详情
        const genres = (detail.genres ?? detail.Genres ?? []).map(g => g.name ?? g.Name)
        const developers = detail.developers ?? detail.Developers ?? []
        const publishers = detail.publishers ?? detail.Publishers ?? []

        game.value = {
          id: detail.gameId ?? detail.GameId,
          name: detail.name ?? detail.Name,
          headerImage: detail.media?.headerImage ?? detail.Media?.HeaderImage,
          description: detail.shortDescription ?? detail.ShortDescription ?? detail.detailedDescription ?? detail.DetailedDescription,
          platform: detail.platforms ? formatPlatforms(detail.platforms) : '',
          genre: genres.join(' / '),
          isFree: detail.isFree ?? detail.IsFree ?? null,
          releaseDate: detail.releaseDate ?? detail.ReleaseDate ?? '',
          shortDescription: detail.shortDescription ?? detail.ShortDescription,
          detailedDescription: detail.detailedDescription ?? detail.DetailedDescription,
          requirements: detail.requirements ?? detail.Requirements,
          reviews: detail.reviews ?? detail.Reviews,
          developers,
          publishers,
          playtimeMinutes: 0,
          lastPlayed: null,
          achievementsUnlocked: 0,
          achievementsTotal: 0
        }
      } else {
        // 合并个人数据与公共详情
        const genres = (detail.genres ?? detail.Genres ?? []).map(g => g.name ?? g.Name)
        const developers = detail.developers ?? detail.Developers ?? game.value.developers ?? []
        const publishers = detail.publishers ?? detail.Publishers ?? game.value.publishers ?? []

        game.value = {
          ...game.value,
          name: detail.name || detail.Name || game.value.name,
          headerImage: game.value.headerImage || detail.media?.headerImage || detail.Media?.HeaderImage,
          description: game.value.description || detail.shortDescription || detail.ShortDescription || detail.detailedDescription || detail.DetailedDescription,
          platform: game.value.platform || (detail.platforms ? formatPlatforms(detail.platforms) : ''),
          genre: genres.join(' / ') || game.value.genre,
          isFree: detail.isFree ?? detail.IsFree ?? game.value.isFree,
          releaseDate: detail.releaseDate ?? detail.ReleaseDate ?? game.value.releaseDate,
          shortDescription: detail.shortDescription ?? detail.ShortDescription ?? game.value.shortDescription,
          detailedDescription: detail.detailedDescription ?? detail.DetailedDescription ?? game.value.detailedDescription,
          requirements: detail.requirements ?? detail.Requirements ?? game.value.requirements,
          reviews: detail.reviews ?? detail.Reviews ?? game.value.reviews,
          developers,
          publishers
        }
      }
    }

    if (!game.value) {
      throw new Error('未找到游戏信息')
    }

    // 加载成就数据
    await loadAchievements()
  } catch (err) {
    console.error('加载游戏详情失败:', err)
    error.value = '加载游戏详情失败: ' + (err.message || '未知错误')
  } finally {
    loading.value = false
  }
}

// 加载成就数据
const loadAchievements = async () => {
  if (!game.value) return
  
  achievementsLoading.value = true
  try {
    const gameId = game.value.id || route.params.id
    console.log('加载成就数据，游戏ID:', gameId)
    
    const response = await achievementApi.getUserGameAchievements(gameId)
    console.log('成就API响应:', response)
    
    if (response.success && response.data) {
      const data = response.data
      // 标准结构：{ gameId, gameName, achievements: [] }
      const list = Array.isArray(data)
        ? data
        : (data.achievements && Array.isArray(data.achievements) ? data.achievements : [])

      achievements.value = list.map(a => {
        const unlocked = a.unlocked ?? a.Unlocked
        const unlockTime = a.unlockTime ?? a.UnlockTime

        return {
          id: a.id || a.achievementId || a.AchievementId,
          name: a.name || a.achievementName || a.AchievementName,
          description: a.description || a.achievementDescription || a.Description,
          iconUnlocked: a.iconUnlocked || a.IconUnlocked || a.icon,
          iconLocked: a.iconLocked || a.IconLocked,
          isUnlocked: unlocked !== undefined ? unlocked : (unlockTime != null),
          unlockTime
        }
      })
      console.log('处理后的成就数据:', achievements.value.length, achievements.value)
    }
  } catch (err) {
    console.error('加载成就失败:', err)
    // 不显示错误，只是没有成就数据
    achievements.value = []
  } finally {
    achievementsLoading.value = false
  }
}

// 格式化游戏时长
const formatPlaytime = (hours) => {
  if (!hours) return '0 小时'
  if (hours < 1) return `${Math.round(hours * 60)} 分钟`
  return `${hours.toFixed(1)} 小时`
}

// 将平台支持对象格式化为字符串
const formatPlatforms = (platforms) => {
  const list = []
  if (platforms.windows) list.push('Windows')
  if (platforms.mac) list.push('Mac')
  if (platforms.linux) list.push('Linux')
  return list.join(' / ')
}

// 格式化日期
const formatDate = (date) => {
  if (!date) return ''
  try {
    const d = new Date(date)
    return d.toLocaleDateString('zh-CN', { year: 'numeric', month: 'short', day: 'numeric' })
  } catch {
    return date
  }
}

// 图片加载错误处理
const handleImageError = (event) => {
  event.target.style.display = 'none'
}

// 价格监控处理（预留）
const handlePriceMonitor = () => {
  console.log('打开价格监控设置')
  // TODO: 实现价格监控功能
}

// Mod 管理处理（预留）
const handleManageMods = () => {
  console.log('打开 Mod 管理')
  // TODO: 实现 Mod 管理功能
}

// 切换 Mod 启用状态（预留）
const handleToggleMod = (mod) => {
  console.log('切换 Mod 状态:', mod.name, mod.enabled)
  // TODO: 实现 Mod 启用/禁用功能
}

onMounted(() => {
  loadGameDetail()
})
</script>

<style scoped>
.game-detail-container {
  min-height: 100vh;
  background: #0f0f13;
  color: #f8fafc;
  padding: 24px;
}

/* 加载和错误状态 */
.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
  gap: 16px;
}

.loading-spinner {
  width: 48px;
  height: 48px;
  border: 4px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

.loading-spinner-small {
  width: 20px;
  height: 20px;
  border: 2px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

.loading-small {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 20px;
  color: #94a3b8;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error {
  text-align: center;
  padding: 60px 20px;
  color: #ef4444;
}

.retry-btn {
  margin-top: 16px;
  padding: 10px 20px;
  background: #8b5cf6;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
}

.retry-btn:hover {
  background: #7c3aed;
}

/* 返回按钮 */
.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  margin-bottom: 24px;
  background: rgba(20, 20, 23, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
  backdrop-filter: blur(20px);
  transition: all 0.2s;
}

.back-btn:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #f8fafc;
  border-color: rgba(139, 92, 246, 0.3);
}

/* Hero 区域 */
.hero-section {
  position: relative;
  margin-bottom: 32px;
  border-radius: 16px;
  overflow: hidden;
}

.hero-background {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 500px;
  z-index: 0;
}

.hero-bg-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  opacity: 0.4;
}

.hero-gradient {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, #0f0f13 10%, transparent 100%);
}

.hero-content {
  position: relative;
  z-index: 1;
  display: flex;
  gap: 32px;
  padding: 64px 32px 32px;
  align-items: flex-end;
}

.game-cover-wrapper {
  flex-shrink: 0;
}

.game-cover {
  width: 192px;
  height: 256px;
  border-radius: 12px;
  object-fit: cover;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
  border: 2px solid rgba(255, 255, 255, 0.1);
}

.game-header-info {
  flex: 1;
  padding-bottom: 8px;
}

.game-badges {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.platform-badge,
.genre-badge {
  padding: 4px 12px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
}

.platform-badge {
  background: rgba(139, 92, 246, 0.2);
  color: #c4b5fd;
  border: 1px solid rgba(139, 92, 246, 0.3);
}

.genre-badge {
  background: rgba(20, 20, 23, 0.8);
  color: #94a3b8;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.game-title {
  font-size: 48px;
  font-weight: bold;
  margin-bottom: 16px;
  color: #f8fafc;
  line-height: 1.2;
}

.game-stats {
  display: flex;
  gap: 24px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #94a3b8;
  font-size: 14px;
}

.stat-icon {
  color: #8b5cf6;
}

.game-actions {
  display: flex;
  gap: 12px;
}

.btn-primary,
.btn-secondary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 12px 24px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary {
  background: #8b5cf6;
  color: white;
}

.btn-primary:hover {
  background: #7c3aed;
  box-shadow: 0 0 20px rgba(139, 92, 246, 0.4);
}

.btn-secondary {
  background: rgba(20, 20, 23, 0.75);
  color: #94a3b8;
  border: 1px solid rgba(255, 255, 255, 0.08);
  padding: 12px;
}

.btn-secondary:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #f8fafc;
}

/* 内容网格 */
.content-grid {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 24px;
}

.main-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* 卡片样式 */
.section-card {
  background: rgba(20, 20, 23, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 24px;
  backdrop-filter: blur(20px);
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  margin-bottom: 16px;
  color: #f8fafc;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.achievement-progress {
  font-size: 14px;
  color: #94a3b8;
}

/* 游戏介绍 */
.game-description {
  line-height: 1.6;
}

.description-text {
  color: #cbd5e1;
  font-size: 15px;
}

.description-text.text-muted {
  color: #64748b;
  font-style: italic;
}

/* 基本信息 */
.basic-info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.basic-info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.basic-label {
  font-size: 12px;
  color: #94a3b8;
}

.basic-value {
  font-size: 14px;
  color: #e5e7eb;
}

/* 成就网格 */
.achievements-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.achievement-card {
  background: rgba(15, 15, 19, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  padding: 16px;
  display: flex;
  gap: 12px;
  transition: all 0.2s;
}

.achievement-card:hover {
  background: rgba(20, 20, 23, 0.8);
  border-color: rgba(139, 92, 246, 0.3);
  transform: translateY(-2px);
}

.achievement-card.unlocked {
  border-color: rgba(234, 179, 8, 0.3);
}

.achievement-icon-wrapper {
  flex-shrink: 0;
  width: 64px;
  height: 64px;
}

.achievement-icon {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 8px;
}

.achievement-icon.locked {
  filter: grayscale(100%);
  opacity: 0.5;
}

.achievement-icon-placeholder {
  width: 100%;
  height: 100%;
  background: rgba(20, 20, 23, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.placeholder-icon {
  color: #64748b;
}

.achievement-card.unlocked .placeholder-icon {
  color: #eab308;
}

.achievement-info {
  flex: 1;
  min-width: 0;
}

.achievement-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #f8fafc;
}

.achievement-card:not(.unlocked) .achievement-name {
  color: #64748b;
}

.achievement-desc {
  font-size: 12px;
  color: #94a3b8;
  margin-bottom: 8px;
  line-height: 1.4;
}

.achievement-meta {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: #64748b;
}

.unlock-time {
  color: #8b5cf6;
}

.unlock-rate {
  color: #94a3b8;
}

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 40px 20px;
  color: #64748b;
}

.empty-icon {
  margin-bottom: 12px;
  opacity: 0.3;
}

/* 价格监控 */
.sidebar-title {
  font-size: 14px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #94a3b8;
  margin-bottom: 16px;
}

.price-monitor {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.price-current {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.price-label {
  font-size: 12px;
  color: #94a3b8;
}

.price-value {
  font-size: 24px;
  font-weight: bold;
  color: #f8fafc;
}

.price-chart-placeholder {
  height: 100px;
  background: rgba(15, 15, 19, 0.6);
  border-radius: 8px;
  padding: 8px;
  display: flex;
  align-items: flex-end;
}

.chart-bars {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  width: 100%;
  height: 100%;
  gap: 4px;
}

.chart-bar {
  flex: 1;
  background: linear-gradient(to top, #8b5cf6, #a78bfa);
  border-radius: 2px 2px 0 0;
  min-height: 10%;
}

.price-actions {
  margin-top: 8px;
}

/* Mod 管理 */
.mod-manager {
  display: flex;
  flex-direction: column;
}

.mod-empty {
  text-align: center;
  padding: 32px 20px;
}

.mod-empty-icon {
  margin: 0 auto 12px;
  color: #64748b;
  opacity: 0.5;
}

.mod-empty-text {
  color: #64748b;
  margin-bottom: 16px;
  font-size: 14px;
}

.mod-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mod-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  background: rgba(15, 15, 19, 0.6);
  border-radius: 8px;
}

.mod-info {
  flex: 1;
}

.mod-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #f8fafc;
}

.mod-version {
  font-size: 12px;
  color: #64748b;
}

.mod-toggle {
  position: relative;
}

.toggle-switch {
  display: none;
}

.toggle-label {
  display: block;
  width: 44px;
  height: 24px;
  background: rgba(20, 20, 23, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  cursor: pointer;
  position: relative;
  transition: all 0.2s;
}

.toggle-label::after {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 18px;
  height: 18px;
  background: #64748b;
  border-radius: 50%;
  transition: all 0.2s;
}

.toggle-switch:checked + .toggle-label {
  background: #8b5cf6;
  border-color: #8b5cf6;
}

.toggle-switch:checked + .toggle-label::after {
  left: 22px;
  background: white;
}

/* 按钮样式 */
.btn-outline {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  color: #94a3b8;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-outline:hover {
  background: rgba(20, 20, 23, 0.8);
  border-color: rgba(139, 92, 246, 0.3);
  color: #f8fafc;
}

.btn-outline.full-width {
  width: 100%;
  justify-content: center;
}

.icon {
  flex-shrink: 0;
}

/* 响应式设计 */
@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .hero-content {
    flex-direction: column;
    align-items: flex-start;
  }

  .game-cover {
    width: 160px;
    height: 213px;
  }

  .game-title {
    font-size: 36px;
  }
}

@media (max-width: 768px) {
  .game-detail-container {
    padding: 16px;
  }

  .achievements-grid {
    grid-template-columns: 1fr;
  }

  .game-stats {
    flex-direction: column;
    gap: 12px;
  }
}
</style>
