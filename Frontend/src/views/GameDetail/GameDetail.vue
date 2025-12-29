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

          <!-- 基本信息：发行日期 / 开发商 / 发行商 / 分类 / 类型 / 语言 -->
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
              <div class="basic-info-item" v-if="game.categories && game.categories.length">
                <div class="basic-label">分类</div>
                <div class="basic-value">
                  {{ formatGameCategories(game.categories) }}
                </div>
              </div>
              <div class="basic-info-item" v-if="game.genres && game.genres.length">
                <div class="basic-label">类型</div>
                <div class="basic-value">
                  {{ formatGameGenres(game.genres) }}
                </div>
              </div>
              <div class="basic-info-item" v-if="game.languages && game.languages.length">
                <div class="basic-label">支持语言</div>
                <div class="basic-value">
                  {{ formatGameLanguages(game.languages) }}
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
          <!-- 价格监控（免费游戏不显示） -->
          <section v-if="game.isFree !== true" class="section-card">
            <h3 class="sidebar-title">价格监控</h3>
            <div class="price-monitor">
              <div v-if="priceLoading" class="price-loading">
                <div class="loading-spinner-small"></div>
                <span>加载价格中...</span>
              </div>
              <div v-else-if="priceError" class="price-error">
                <span>{{ priceError }}</span>
              </div>
              <div v-else>
                <div class="price-current">
                  <span class="price-label">当前价格</span>
                  <div class="price-display">
                    <span class="price-value" v-if="priceInfo.currentPrice !== null && priceInfo.currentPrice > 0">
                      ¥{{ priceInfo.currentPrice.toFixed(2) }}
                    </span>
                    <span class="price-value" v-else-if="priceInfo.currentPrice === 0">免费</span>
                    <span class="price-value" v-else>暂无数据</span>
                    <span v-if="priceInfo.isDiscount && priceInfo.discountRate > 0" class="discount-badge">
                      -{{ priceInfo.discountRate }}%
                    </span>
                  </div>
                  <div v-if="priceInfo.originalPrice && priceInfo.currentPrice && priceInfo.originalPrice > priceInfo.currentPrice" class="price-original">
                    <span class="original-price">原价: ¥{{ priceInfo.originalPrice.toFixed(2) }}</span>
                    <span class="savings">节省: ¥{{ (priceInfo.originalPrice - priceInfo.currentPrice).toFixed(2) }}</span>
                  </div>
                  <div v-else-if="priceInfo.originalPrice && priceInfo.originalPrice > 0 && (!priceInfo.currentPrice || priceInfo.currentPrice === 0)" class="price-original">
                    <span class="original-price">原价: ¥{{ priceInfo.originalPrice.toFixed(2) }}</span>
                  </div>
                  <div v-if="priceInfo.lowestPrice && priceInfo.currentPrice && priceInfo.lowestPrice < priceInfo.currentPrice" class="price-lowest">
                    <span class="lowest-label">历史最低: ¥{{ priceInfo.lowestPrice.toFixed(2) }}</span>
                  </div>
                </div>
                <div v-if="priceHistory.length > 0" class="price-chart-container">
                  <div class="chart-bars">
                    <div 
                      v-for="(item, index) in priceHistory.slice(0, 7).reverse()" 
                      :key="index"
                      class="chart-bar-wrapper"
                      :title="`${item.date}: ¥${item.currentPrice.toFixed(2)}`"
                    >
                      <div 
                        class="chart-bar"
                        :style="{ height: `${getChartBarHeight(item.currentPrice)}%` }"
                      ></div>
                      <span class="chart-label">{{ formatChartDate(item.date) }}</span>
                    </div>
                  </div>
                </div>
                <div v-else class="price-chart-empty">
                  <span>暂无价格历史数据</span>
                </div>
                <div class="price-actions">
                  <button class="btn-outline" @click="showPriceAlertDialog">
                    <Bell class="icon" size="16" />
                    {{ hasPriceAlert ? '管理提醒' : '设置价格提醒' }}
                  </button>
                </div>
              </div>
            </div>
          </section>

          <!-- 价格提醒设置对话框 -->
          <div v-if="showAlertDialog" class="dialog-overlay" @click.self="closeAlertDialog">
            <div class="dialog-content">
              <div class="dialog-header">
                <h3>{{ currentSubscription ? '管理价格提醒' : '设置价格提醒' }}</h3>
                <button class="dialog-close" @click="closeAlertDialog">
                  <X class="icon" size="20" />
                </button>
              </div>
              <div class="dialog-body">
                <div class="game-info-preview">
                  <img v-if="game.headerImage" :src="game.headerImage" class="preview-image" />
                  <div class="preview-info">
                    <h4>{{ game.name }}</h4>
                    <p class="preview-price">
                      当前价格: ¥{{ priceInfo.currentPrice?.toFixed(2) || '0.00' }}
                      <span v-if="priceInfo.isDiscount && priceInfo.discountRate > 0" class="discount-badge-small">
                        -{{ priceInfo.discountRate }}%
                      </span>
                    </p>
                    <p v-if="priceInfo.originalPrice && priceInfo.originalPrice > 0" class="preview-original">
                      原价: ¥{{ priceInfo.originalPrice.toFixed(2) }}
                    </p>
                  </div>
                </div>
                <div v-if="currentSubscription && !currentSubscription.isActive" class="subscription-inactive-notice">
                  <span>⚠️ 此提醒已触发，当前为非活跃状态</span>
                </div>
                <div class="alert-options">
                  <div class="option-group">
                    <label class="option-label">
                      <input 
                        type="radio" 
                        v-model="alertType" 
                        value="price"
                        class="radio-input"
                      />
                      <span>目标价格提醒</span>
                    </label>
                    <div v-if="alertType === 'price'" class="option-input">
                      <input 
                        type="number" 
                        v-model.number="targetPrice" 
                        placeholder="输入目标价格"
                        class="input-field"
                        step="0.01"
                        min="0"
                      />
                      <span class="input-hint">当价格降至或低于此价格时提醒</span>
                    </div>
                  </div>
                  <div class="option-group">
                    <label class="option-label">
                      <input 
                        type="radio" 
                        v-model="alertType" 
                        value="discount"
                        class="radio-input"
                      />
                      <span>目标折扣提醒</span>
                    </label>
                    <div v-if="alertType === 'discount'" class="option-input">
                      <input 
                        type="number" 
                        v-model.number="targetDiscount" 
                        placeholder="输入目标折扣百分比"
                        class="input-field"
                        min="0"
                        max="100"
                      />
                      <span class="input-hint">当折扣达到或超过此百分比时提醒</span>
                    </div>
                  </div>
                  <div class="option-group">
                    <label class="option-label">
                      <input 
                        type="radio" 
                        v-model="alertType" 
                        value="none"
                        class="radio-input"
                      />
                      <span>取消提醒</span>
                    </label>
                  </div>
                </div>
              </div>
              <div class="dialog-footer">
                <button class="btn-secondary" @click="closeAlertDialog">取消</button>
                <button 
                  v-if="currentSubscription && alertType === 'none'" 
                  class="btn-danger" 
                  @click="deletePriceAlert" 
                  :disabled="savingAlert"
                >
                  {{ savingAlert ? '删除中...' : '删除提醒' }}
                </button>
                <button 
                  v-else-if="alertType !== 'none'" 
                  class="btn-primary" 
                  @click="savePriceAlert" 
                  :disabled="savingAlert"
                >
                  {{ savingAlert ? '保存中...' : (currentSubscription ? '更新' : '保存') }}
                </button>
              </div>
            </div>
          </div>

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
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { gameApi, achievementApi, libraryApi } from '@/api'
import { priceApi } from '@/api/price'
import { ArrowLeft, Clock, Trophy, Calendar, Play, Settings, Bell, Package, Lock, X } from 'lucide-vue-next'

const route = useRoute()
const router = useRouter()

// 状态管理
const loading = ref(true)
const error = ref(null)
const game = ref(null)
const achievements = ref([])
const achievementsLoading = ref(false)
const gamePlaytime = ref(0)
const priceInfo = ref({
  currentPrice: null,
  originalPrice: null,
  discountRate: 0,
  isDiscount: false,
  lowestPrice: null,
  lowestDate: null
})
const priceHistory = ref([])
const priceLoading = ref(false)
const priceError = ref(null)
const hasPriceAlert = ref(false)
const currentSubscription = ref(null) // 当前游戏的订阅信息
const mods = ref([]) // 预留的 Mod 列表

// 价格提醒对话框
const showAlertDialog = ref(false)
const alertType = ref('price')
const targetPrice = ref(null)
const targetDiscount = ref(null)
const savingAlert = ref(false)

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
            categories: [],
            genres: [],
            languages: [],
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

    // 2. 使用通用游戏API获取"公共详情"（题材/开发商/系统需求等）
    const gameResponse = await gameApi.getGame(gameId)
    console.log('通用游戏详情响应:', gameResponse)
    if (gameResponse.success && gameResponse.data) {
      const detail = gameResponse.data

      // 提取分类、类型和语言数据（支持多种命名格式）
      const categories = detail.categories ?? detail.Categories ?? []
      const genres = detail.genres ?? detail.Genres ?? []
      const languages = detail.languages ?? detail.Languages ?? []
      
      // 格式化数据为统一格式
      const formattedCategories = Array.isArray(categories) ? categories : []
      const formattedGenres = Array.isArray(genres) ? genres : []
      const formattedLanguages = Array.isArray(languages) ? languages : []

      if (!game.value) {
        // 如果游戏库里没有记录，就完全使用公共详情
        const genreNames = formattedGenres.map(g => g.name ?? g.Name ?? g)
        const developers = detail.developers ?? detail.Developers ?? []
        const publishers = detail.publishers ?? detail.Publishers ?? []

        game.value = {
          id: detail.gameId ?? detail.GameId,
          name: detail.name ?? detail.Name,
          headerImage: detail.media?.headerImage ?? detail.Media?.HeaderImage,
          description: detail.shortDescription ?? detail.ShortDescription ?? detail.detailedDescription ?? detail.DetailedDescription,
          platform: detail.platforms ? formatPlatforms(detail.platforms) : '',
          genre: genreNames.join(' / '),
          isFree: detail.isFree ?? detail.IsFree ?? null,
          releaseDate: detail.releaseDate ?? detail.ReleaseDate ?? '',
          shortDescription: detail.shortDescription ?? detail.ShortDescription,
          detailedDescription: detail.detailedDescription ?? detail.DetailedDescription,
          requirements: detail.requirements ?? detail.Requirements,
          reviews: detail.reviews ?? detail.Reviews,
          developers,
          publishers,
          categories: formattedCategories,
          genres: formattedGenres,
          languages: formattedLanguages,
          playtimeMinutes: 0,
          lastPlayed: null,
          achievementsUnlocked: 0,
          achievementsTotal: 0
        }
      } else {
        // 合并个人数据与公共详情
        const genreNames = formattedGenres.map(g => g.name ?? g.Name ?? g)
        const developers = detail.developers ?? detail.Developers ?? game.value.developers ?? []
        const publishers = detail.publishers ?? detail.Publishers ?? game.value.publishers ?? []

        game.value = {
          ...game.value,
          name: detail.name || detail.Name || game.value.name,
          headerImage: game.value.headerImage || detail.media?.headerImage || detail.Media?.HeaderImage,
          description: game.value.description || detail.shortDescription || detail.ShortDescription || detail.detailedDescription || detail.DetailedDescription,
          platform: game.value.platform || (detail.platforms ? formatPlatforms(detail.platforms) : ''),
          genre: genreNames.join(' / ') || game.value.genre,
          isFree: detail.isFree ?? detail.IsFree ?? game.value.isFree,
          releaseDate: detail.releaseDate ?? detail.ReleaseDate ?? game.value.releaseDate,
          shortDescription: detail.shortDescription ?? detail.ShortDescription ?? game.value.shortDescription,
          detailedDescription: detail.detailedDescription ?? detail.DetailedDescription ?? game.value.detailedDescription,
          requirements: detail.requirements ?? detail.Requirements ?? game.value.requirements,
          reviews: detail.reviews ?? detail.Reviews ?? game.value.reviews,
          developers,
          publishers,
          categories: formattedCategories.length > 0 ? formattedCategories : (game.value.categories ?? []),
          genres: formattedGenres.length > 0 ? formattedGenres : (game.value.genres ?? []),
          languages: formattedLanguages.length > 0 ? formattedLanguages : (game.value.languages ?? [])
        }
      }
    }

    if (!game.value) {
      throw new Error('未找到游戏信息')
    }

    // 加载成就数据
    await loadAchievements()
    
    // 加载价格数据
    await loadPriceData()
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

// 格式化游戏分类
const formatGameCategories = (categories) => {
  if (!categories || !Array.isArray(categories) || categories.length === 0) return ''
  return categories.map(cat => {
    if (typeof cat === 'string') return cat
    return cat.name ?? cat.Name ?? cat.description ?? cat.Description ?? cat.id ?? cat.Id ?? ''
  }).filter(Boolean).join('，')
}

// 格式化游戏类型
const formatGameGenres = (genres) => {
  if (!genres || !Array.isArray(genres) || genres.length === 0) return ''
  return genres.map(genre => {
    if (typeof genre === 'string') return genre
    return genre.name ?? genre.Name ?? genre.description ?? genre.Description ?? genre.id ?? genre.Id ?? ''
  }).filter(Boolean).join('，')
}

// 格式化游戏语言
const formatGameLanguages = (languages) => {
  if (!languages || !Array.isArray(languages) || languages.length === 0) return ''
  return languages.map(lang => {
    if (typeof lang === 'string') return lang
    // 语言可能有 name、description、languageName 等字段
    return lang.name ?? lang.Name ?? lang.languageName ?? lang.LanguageName ?? lang.description ?? lang.Description ?? lang.id ?? lang.Id ?? ''
  }).filter(Boolean).join('，')
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

// 加载价格数据
const loadPriceData = async () => {
  if (!game.value) return
  
  const gameId = game.value.id || route.params.id
  priceLoading.value = true
  priceError.value = null
  
  try {
    // 获取价格历史
    const historyResponse = await priceApi.getPriceHistory(gameId)
    if (historyResponse.success && historyResponse.data) {
      const data = historyResponse.data
      
      // 更新价格信息（适配后端返回的数据结构）
      priceInfo.value = {
        currentPrice: data.currentPrice ?? data.CurrentPrice ?? null,
        originalPrice: data.originalPrice ?? data.OriginalPrice ?? null,
        discountRate: data.discount ?? data.discountRate ?? data.Discount ?? data.DiscountRate ?? 0,
        isDiscount: data.isDiscount ?? data.IsDiscount ?? false,
        lowestPrice: data.lowestPrice ?? data.LowestPrice ?? null,
        lowestDate: data.lowestDate ?? data.LowestDate ?? null
      }
      
      // 如果没有当前价格，尝试从历史记录中获取最新的
      if (priceInfo.value.currentPrice === null && data.priceHistory && Array.isArray(data.priceHistory) && data.priceHistory.length > 0) {
        const latest = data.priceHistory[0]
        priceInfo.value.currentPrice = latest.CurrentPrice ?? latest.currentPrice ?? null
        priceInfo.value.originalPrice = latest.OriginalPrice ?? latest.originalPrice ?? priceInfo.value.originalPrice
        priceInfo.value.discountRate = latest.Discount ?? latest.discount ?? priceInfo.value.discountRate
        priceInfo.value.isDiscount = latest.IsDiscount ?? latest.isDiscount ?? priceInfo.value.isDiscount
      }
      
      // 处理价格历史数据
      if (data.priceHistory && Array.isArray(data.priceHistory)) {
        priceHistory.value = data.priceHistory.map(item => ({
          date: item.Date ?? item.date,
          currentPrice: item.CurrentPrice ?? item.currentPrice ?? 0,
          originalPrice: item.OriginalPrice ?? item.originalPrice ?? 0,
          discount: item.Discount ?? item.discount ?? item.DiscountRate ?? item.discountRate ?? 0,
          isDiscount: item.IsDiscount ?? item.isDiscount ?? false
        }))
      }
    }
    
    // 检查是否已有价格提醒
    await checkPriceAlert()
  } catch (err) {
    console.error('加载价格数据失败:', err)
    priceError.value = '加载价格数据失败'
  } finally {
    priceLoading.value = false
  }
}

// 检查是否已有价格提醒
const checkPriceAlert = async () => {
  try {
    const response = await priceApi.getSubscriptions()
    if (response.success && response.data) {
      const subscriptions = response.data.subscriptions || response.data.items || []
      const gameId = parseInt(game.value?.id || route.params.id)
      
      // 查找当前游戏的订阅
      currentSubscription.value = subscriptions.find(sub => 
        parseInt(sub.gameId) === gameId
      ) || null
      
      // 检查是否有活跃的订阅
      hasPriceAlert.value = currentSubscription.value !== null
    }
  } catch (err) {
    console.error('检查价格提醒失败:', err)
  }
}

// 计算图表柱状图高度
const getChartBarHeight = (price) => {
  if (!price || priceHistory.value.length === 0) return 0
  const prices = priceHistory.value.map(p => p.currentPrice).filter(p => p > 0)
  if (prices.length === 0) return 0
  const maxPrice = Math.max(...prices)
  const minPrice = Math.min(...prices)
  if (maxPrice === minPrice) return 50
  return ((price - minPrice) / (maxPrice - minPrice)) * 80 + 10
}

// 格式化图表日期
const formatChartDate = (dateString) => {
  if (!dateString) return ''
  try {
    const date = new Date(dateString)
    const month = date.getMonth() + 1
    const day = date.getDate()
    return `${month}/${day}`
  } catch {
    return dateString
  }
}

// 显示价格提醒对话框
const showPriceAlertDialog = async () => {
  // 确保先加载最新的订阅信息
  await checkPriceAlert()
  
  showAlertDialog.value = true
  
  // 如果有现有订阅，加载其设置
  if (currentSubscription.value) {
    if (currentSubscription.value.targetPrice !== null && currentSubscription.value.targetPrice !== undefined) {
      alertType.value = 'price'
      targetPrice.value = currentSubscription.value.targetPrice
      targetDiscount.value = null
    } else if (currentSubscription.value.targetDiscount !== null && currentSubscription.value.targetDiscount !== undefined) {
      alertType.value = 'discount'
      targetDiscount.value = currentSubscription.value.targetDiscount
      targetPrice.value = null
    } else {
      alertType.value = 'price'
      targetPrice.value = null
      targetDiscount.value = null
    }
  } else {
    // 重置表单
    alertType.value = 'price'
    targetPrice.value = null
    targetDiscount.value = null
  }
}

// 关闭价格提醒对话框
const closeAlertDialog = () => {
  showAlertDialog.value = false
  // 重置表单
  alertType.value = 'price'
  targetPrice.value = null
  targetDiscount.value = null
}

// 保存价格提醒
const savePriceAlert = async () => {
  if (!game.value) return
  
  // 验证输入
  if (alertType.value === 'price' && (!targetPrice.value || targetPrice.value <= 0)) {
    alert('请输入有效的目标价格')
    return
  }
  if (alertType.value === 'discount' && (!targetDiscount.value || targetDiscount.value < 0 || targetDiscount.value > 100)) {
    alert('请输入有效的折扣百分比（0-100）')
    return
  }
  
  savingAlert.value = true
  try {
    const gameId = parseInt(game.value.id || route.params.id)
    // 获取平台ID，默认为Steam (1)
    const platformId = 1 // 可以根据实际情况获取
    
    const data = {
      gameId: gameId,
      platformId: platformId,
      targetPrice: alertType.value === 'price' ? targetPrice.value : null,
      targetDiscount: alertType.value === 'discount' ? targetDiscount.value : null
    }
    
    let response
    if (currentSubscription.value) {
      // 更新现有订阅
      response = await priceApi.updateSubscription(currentSubscription.value.subscriptionId, data)
    } else {
      // 创建新订阅
      response = await priceApi.trackPrice(data)
    }
    
    if (response.success) {
      // 刷新订阅信息
      await checkPriceAlert()
      closeAlertDialog()
    } else {
      alert(response.message || '操作失败，请重试')
    }
  } catch (err) {
    console.error('保存价格提醒失败:', err)
    alert('操作失败: ' + (err.message || '未知错误'))
  } finally {
    savingAlert.value = false
  }
}

// 删除价格提醒
const deletePriceAlert = async () => {
  if (!currentSubscription.value) return
  
  if (!confirm('确定要删除这个价格提醒吗？')) return
  
  savingAlert.value = true
  try {
    const response = await priceApi.unsubscribeAlert(currentSubscription.value.subscriptionId)
    if (response.success) {
      // 刷新订阅信息
      await checkPriceAlert()
      closeAlertDialog()
    } else {
      alert(response.message || '删除失败，请重试')
    }
  } catch (err) {
    console.error('删除价格提醒失败:', err)
    alert('删除失败: ' + (err.message || '未知错误'))
  } finally {
    savingAlert.value = false
  }
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

// 监听游戏ID变化，重新加载价格数据
watch(() => route.params.id, () => {
  if (route.params.id) {
    loadPriceData()
  }
})

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

  .price-loading,
  .price-error {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 20px;
    color: #94a3b8;
    font-size: 14px;
  }

  .price-error {
    color: #ef4444;
  }

  .price-display {
    display: flex;
    align-items: baseline;
    gap: 8px;
    flex-wrap: wrap;
  }

  .discount-badge {
    padding: 2px 8px;
    background: rgba(239, 68, 68, 0.2);
    color: #fca5a5;
    border-radius: 4px;
    font-size: 12px;
    font-weight: 600;
  }

  .price-original {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin-top: 8px;
    font-size: 12px;
  }

  .original-price {
    color: #64748b;
    text-decoration: line-through;
  }

  .savings {
    color: #10b981;
    font-weight: 600;
  }

  .price-lowest {
    margin-top: 8px;
    font-size: 12px;
    color: #94a3b8;
  }

  .lowest-label {
    color: #8b5cf6;
  }

  .price-chart-container {
    height: 120px;
    background: rgba(15, 15, 19, 0.6);
    border-radius: 8px;
    padding: 12px 8px 8px;
    display: flex;
    align-items: flex-end;
  }

  .price-chart-empty {
    height: 80px;
    background: rgba(15, 15, 19, 0.6);
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #64748b;
    font-size: 12px;
  }

  .chart-bars {
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    width: 100%;
    height: 100%;
    gap: 4px;
  }

  .chart-bar-wrapper {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    height: 100%;
    gap: 4px;
  }

  .chart-bar {
    width: 100%;
    background: linear-gradient(to top, #8b5cf6, #a78bfa);
    border-radius: 2px 2px 0 0;
    min-height: 10%;
    transition: all 0.2s;
    cursor: pointer;
  }

  .chart-bar:hover {
    background: linear-gradient(to top, #7c3aed, #8b5cf6);
    opacity: 0.9;
  }

  .chart-label {
    font-size: 10px;
    color: #64748b;
    white-space: nowrap;
  }

  /* 价格提醒对话框 */
  .dialog-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.75);
    backdrop-filter: blur(4px);
    z-index: 1000;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 20px;
  }

  .dialog-content {
    background: rgba(20, 20, 23, 0.95);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 12px;
    width: 100%;
    max-width: 500px;
    max-height: 90vh;
    overflow-y: auto;
    backdrop-filter: blur(20px);
  }

  .dialog-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 20px 24px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  }

  .dialog-header h3 {
    font-size: 18px;
    font-weight: 600;
    color: #f8fafc;
  }

  .dialog-close {
    background: transparent;
    border: none;
    color: #94a3b8;
    cursor: pointer;
    padding: 4px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 4px;
    transition: all 0.2s;
  }

  .dialog-close:hover {
    background: rgba(255, 255, 255, 0.05);
    color: #f8fafc;
  }

  .dialog-body {
    padding: 24px;
  }

  .game-info-preview {
    display: flex;
    gap: 12px;
    padding: 16px;
    background: rgba(15, 15, 19, 0.6);
    border-radius: 8px;
    margin-bottom: 24px;
  }

  .preview-image {
    width: 60px;
    height: 80px;
    object-fit: cover;
    border-radius: 6px;
  }

  .preview-info {
    flex: 1;
  }

  .preview-info h4 {
    font-size: 16px;
    font-weight: 600;
    margin-bottom: 8px;
    color: #f8fafc;
  }

  .preview-price {
    font-size: 14px;
    color: #94a3b8;
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .preview-original {
    font-size: 12px;
    color: #64748b;
    text-decoration: line-through;
    margin-top: 4px;
  }

  .discount-badge-small {
    padding: 2px 6px;
    background: rgba(239, 68, 68, 0.2);
    color: #fca5a5;
    border-radius: 4px;
    font-size: 11px;
    font-weight: 600;
  }

  .subscription-inactive-notice {
    padding: 12px;
    background: rgba(234, 179, 8, 0.1);
    border: 1px solid rgba(234, 179, 8, 0.3);
    border-radius: 8px;
    margin-bottom: 20px;
    font-size: 13px;
    color: #fbbf24;
  }

  .alert-options {
    display: flex;
    flex-direction: column;
    gap: 20px;
  }

  .option-group {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .option-label {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 14px;
    font-weight: 500;
    color: #e5e7eb;
    cursor: pointer;
  }

  .radio-input {
    width: 18px;
    height: 18px;
    cursor: pointer;
  }

  .option-input {
    margin-left: 26px;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .input-field {
    padding: 10px 12px;
    background: rgba(15, 15, 19, 0.8);
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 6px;
    color: #f8fafc;
    font-size: 14px;
    transition: all 0.2s;
  }

  .input-field:focus {
    outline: none;
    border-color: #8b5cf6;
    background: rgba(20, 20, 23, 0.9);
  }

  .input-hint {
    font-size: 12px;
    color: #64748b;
  }

  .dialog-footer {
    display: flex;
    justify-content: flex-end;
    gap: 12px;
    padding: 20px 24px;
    border-top: 1px solid rgba(255, 255, 255, 0.08);
  }

  .btn-danger {
    padding: 10px 20px;
    background: rgba(239, 68, 68, 0.2);
    color: #fca5a5;
    border: 1px solid rgba(239, 68, 68, 0.3);
    border-radius: 6px;
    font-size: 14px;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
  }

  .btn-danger:hover:not(:disabled) {
    background: rgba(239, 68, 68, 0.3);
    border-color: rgba(239, 68, 68, 0.5);
    color: #f87171;
  }

  .btn-danger:disabled {
    opacity: 0.5;
    cursor: not-allowed;
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
