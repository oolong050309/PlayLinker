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
        <div class="hero-background">
          <img :src="game.headerImage || noCoverImage" :alt="game.name" class="hero-bg-img" @error="handleImageError" />
          <div class="hero-gradient"></div>
        </div>

        <div class="hero-content">
          <div class="game-cover-wrapper">
            <img 
              :src="game.coverImage || game.headerImage || noCoverImage" 
              :alt="game.name" 
              class="game-cover"
              @error="handleImageError"
            />
          </div>

          <div class="game-header-info">
            <div class="game-badges">
              <span v-if="game.platform" class="platform-badge">{{ game.platform }}</span>
              <span v-if="game.genre" class="genre-badge">{{ game.genre }}</span>
            </div>
            <h1 class="game-title">{{ game.name || game.gameName }}</h1>
            
            <div class="game-stats">
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
            
            <!-- 跳转到游戏商店详情页的按钮 -->
            <div class="game-actions">
              <button class="btn-secondary" @click="goToStoreDetail">
                <Package class="icon" size="18" />
                查看游戏商店详情
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- 主要内容区域 -->
      <div class="content-grid">
        <!-- 左侧主要内容 -->
        <div class="main-content">
          <!-- 时间栏目 -->
          <section class="section-card playtime-section">
            <div class="playtime-content">
              <!-- 背景趋势图 -->
              <div class="playtime-chart-background">
                <svg class="playtime-chart" viewBox="0 0 100 100" preserveAspectRatio="none">
                  <polyline
                    :points="playtimeChartPoints"
                    fill="none"
                    stroke="rgba(139, 92, 246, 0.3)"
                    stroke-width="2"
                    vector-effect="non-scaling-stroke"
                  />
                  <polygon
                    :points="playtimeChartArea"
                    fill="url(#playtimeGradient)"
                    opacity="0.2"
                  />
                  <defs>
                    <linearGradient id="playtimeGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" style="stop-color:rgba(139, 92, 246, 0.4);stop-opacity:1" />
                      <stop offset="100%" style="stop-color:rgba(139, 92, 246, 0);stop-opacity:1" />
                    </linearGradient>
                  </defs>
                </svg>
              </div>
              
              <!-- 时间显示和平台统计 -->
              <div class="playtime-layout">
                <!-- 左侧：时间显示 -->
                <div class="playtime-display">
                  <h2 class="section-title">时间</h2>
                  <div class="playtime-value">{{ formatPlaytime(gamePlaytime) }}</div>
                </div>
                
                <!-- 右侧：平台时长条形图 -->
                <div class="platform-playtime-chart" v-if="platformPlaytimes.length > 0">
                  <div class="chart-title">平台时长</div>
                  <div class="bar-chart">
                    <div 
                      v-for="(item, index) in platformPlaytimes" 
                      :key="item.platformId"
                      class="bar-item"
                    >
                      <div class="bar-label">{{ item.platformName }}</div>
                      <div class="bar-container">
                        <div 
                          class="bar-fill" 
                          :style="{ width: `${item.percentage}%` }"
                          :class="`bar-color-${index % 4}`"
                        ></div>
                        <div class="bar-value">{{ formatPlaytime(item.playtimeHours) }}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>

          <!-- 一周内游戏时间变化图表 -->
          <section class="section-card playtime-trend-section">
            <h2 class="section-title">一周内游戏时间变化</h2>
            
            <div v-if="playtimeHistoryLoading" class="loading-state">
              <div class="loading-spinner"></div>
              <span>加载中...</span>
            </div>
            
            <div v-else-if="!playtimeHistory || playtimeHistory.length === 0" class="empty-state">
              <p>暂无数据</p>
            </div>
            
            <div v-else class="trend-chart-container">
              <!-- SVG 图表 -->
              <div class="chart-wrapper">
                <svg class="trend-chart-svg" viewBox="0 0 100 100" preserveAspectRatio="none">
                  <defs>
                    <linearGradient id="trendGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" style="stop-color:rgba(139, 92, 246, 0.5);stop-opacity:1" />
                      <stop offset="100%" style="stop-color:rgba(139, 92, 246, 0.05);stop-opacity:1" />
                    </linearGradient>
                  </defs>
                  
                  <!-- 背景网格线 -->
                  <g class="grid-lines">
                    <line
                      v-for="(tick, index) in yAxisTicks"
                      :key="'grid-' + index"
                      :x1="0"
                      :y1="tick.y"
                      :x2="100"
                      :y2="tick.y"
                      stroke="rgba(255, 255, 255, 0.05)"
                      stroke-width="0.3"
                    />
                  </g>
                  
                  <!-- 填充区域 -->
                  <polygon
                    :points="playtimeChartArea"
                    fill="url(#trendGradient)"
                    opacity="0.3"
                  />
                  
                  <!-- 趋势线 -->
                  <polyline
                    :points="playtimeChartPoints"
                    fill="none"
                    stroke="rgba(139, 92, 246, 0.9)"
                    stroke-width="2"
                    vector-effect="non-scaling-stroke"
                    class="trend-line"
                  />
                  
                  <!-- 悬停触发区域（透明，扩大范围） -->
                  <g class="hover-zones">
                    <circle
                      v-for="(item, index) in playtimeHistory"
                      :key="'hover-' + index"
                      :cx="getChartX(index)"
                      :cy="getChartY(item.playtimeMinutes)"
                      r="15"
                      fill="transparent"
                      class="hover-zone"
                      @mouseenter="hoveredPointIndex = index"
                      @mouseleave="hoveredPointIndex = null"
                    />
                  </g>
                  
                  <!-- 悬停指示线 -->
                  <g v-if="hoveredPointIndex !== null" class="hover-indicator">
                    <line
                      :x1="getChartX(hoveredPointIndex)"
                      :y1="0"
                      :x2="getChartX(hoveredPointIndex)"
                      :y2="100"
                      stroke="rgba(139, 92, 246, 0.5)"
                      stroke-width="1.5"
                      stroke-dasharray="4,4"
                    />
                  </g>
                </svg>
                
                <!-- 悬停提示框 -->
                <div 
                  v-if="hoveredPointIndex !== null && playtimeHistory[hoveredPointIndex]"
                  class="chart-tooltip"
                  :style="{ left: getChartX(hoveredPointIndex) + '%' }"
                >
                  <div class="tooltip-date">{{ formatTooltipDate(playtimeHistory[hoveredPointIndex].date) }}</div>
                  <div class="tooltip-value">
                    累计时长: <strong>{{ formatPlaytime(playtimeHistory[hoveredPointIndex].playtimeMinutes / 60) }}</strong>
                  </div>
                  <div v-if="hoveredPointIndex > 0" class="tooltip-change">
                    变化: <span :class="getChangeClass(hoveredPointIndex)">{{ getPlaytimeChange(hoveredPointIndex) }}</span>
                  </div>
                </div>
              </div>
              
              <!-- 坐标轴 -->
              <div class="chart-axes">
                <!-- X 轴（日期） -->
                <div class="x-axis">
                  <div 
                    v-for="(item, index) in playtimeHistory" 
                    :key="'x-' + index"
                    class="x-axis-label"
                    :style="{ left: getChartX(index) + '%' }"
                  >
                    {{ formatAxisDate(item.date) }}
                  </div>
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

            <!-- 平台筛选器 -->
            <div v-if="!achievementsLoading && achievements.length > 0" class="platform-filter">
              <button
                @click="selectedPlatformId = null"
                :class="['platform-filter-btn', { active: selectedPlatformId === null }]"
              >
                全部
              </button>
              <button
                v-for="group in groupedAchievements"
                :key="group.platformId"
                @click="selectedPlatformId = group.platformId"
                :class="['platform-filter-btn', { active: selectedPlatformId === group.platformId }]"
              >
                {{ group.platformName }}
                <span class="platform-badge" v-if="platformStats[group.platformId]">
                  {{ platformStats[group.platformId].unlocked }}/{{ platformStats[group.platformId].total }}
                </span>
              </button>
            </div>

            <div v-if="achievementsLoading" class="loading-small">
              <div class="loading-spinner-small"></div>
              <span>加载成就中...</span>
            </div>

            <div v-else-if="achievements.length === 0" class="empty-state">
              <Trophy class="empty-icon" size="48" />
              <p>暂无成就数据</p>
            </div>

            <div v-else-if="displayedAchievements.length === 0" class="empty-state">
              <Trophy class="empty-icon" size="48" />
              <p>该平台暂无成就数据</p>
            </div>

            <div v-else class="achievements-grid">
              <div 
                v-for="(achievement, index) in displayedAchievements" 
                :key="achievement.id || achievement.achievementId"
                class="achievement-card"
                :class="{ 
                  unlocked: achievement.isUnlocked,
                  hidden: achievement.hidden && !achievement.isUnlocked,
                  revealed: achievement.hidden && !achievement.isUnlocked && revealedHiddenAchievements.has(achievement.id),
                  hovered: hoveredAchievementId === achievement.id
                }"
                :style="{ animationDelay: `${index * 0.05}s` }"
                @mouseenter="hoveredAchievementId = achievement.id"
                @mouseleave="hoveredAchievementId = null"
                @click="handleHiddenAchievementClick(achievement)"
              >
                <div class="achievement-icon-wrapper">
                  <!-- 隐藏且未解锁且未点击：显示问号 -->
                  <div 
                    v-if="achievement.hidden && !achievement.isUnlocked && !revealedHiddenAchievements.has(achievement.id)"
                    class="achievement-icon-placeholder hidden-icon"
                  >
                    <span class="question-mark">?</span>
                  </div>
                  <!-- 已解锁：显示解锁图标 -->
                  <img 
                    v-else-if="achievement.iconUnlocked && achievement.isUnlocked" 
                    :src="achievement.iconUnlocked" 
                    :alt="achievement.name"
                    class="achievement-icon"
                    @error="handleImageError"
                  />
                  <!-- 未解锁（包括已显示剧透的隐藏成就）：显示锁定图标 -->
                  <img 
                    v-else-if="achievement.iconLocked" 
                    :src="achievement.iconLocked" 
                    :alt="achievement.name"
                    class="achievement-icon locked"
                    :class="{ 'reveal-fade-in': newlyRevealedAchievements.has(achievement.id) }"
                    @error="handleImageError"
                  />
                  <!-- 没有图标时的占位符 -->
                  <div v-else class="achievement-icon-placeholder">
                    <Trophy v-if="achievement.isUnlocked" class="placeholder-icon" size="24" />
                    <Lock v-else class="placeholder-icon" size="24" />
                  </div>
                </div>
                <div class="achievement-info">
                  <!-- 隐藏且未解锁且未点击：显示占位文本 -->
                  <template v-if="achievement.hidden && !achievement.isUnlocked && !revealedHiddenAchievements.has(achievement.id)">
                    <h3 
                      class="achievement-name"
                      :class="{ 'fade-out': hoveredAchievementId === achievement.id }"
                    >
                      隐藏的成就
                    </h3>
                    <p 
                      class="achievement-desc"
                      :class="{ 'fade-out': hoveredAchievementId === achievement.id }"
                    >
                      此成就的详情将在解锁后显示
                    </p>
                    <div 
                      class="achievement-spoiler-hint"
                      :class="{ 'fade-in': hoveredAchievementId === achievement.id }"
                    >
                      点击以显示剧透
                    </div>
                  </template>
                  <!-- 已解锁或已显示剧透：显示正常内容 -->
                  <template v-else>
                    <h3 
                      class="achievement-name"
                      :class="{ 'reveal-fade-in': newlyRevealedAchievements.has(achievement.id) }"
                    >
                      {{ achievement.name || achievement.displayName || achievement.achievementName }}
                    </h3>
                    <p 
                      class="achievement-desc"
                      :class="{ 'reveal-fade-in': newlyRevealedAchievements.has(achievement.id) }"
                    >
                      {{ achievement.description || achievement.achievementDescription || '暂无描述' }}
                    </p>
                  </template>
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
          <!-- Mod 管理 -->
          <section class="section-card">
            <h3 class="sidebar-title">已安装 Mod</h3>
            <div class="mod-manager">
              <!-- 加载中 -->
              <div v-if="modLoadStatus === 'loading'" class="mod-loading">
                <div class="loading-spinner-small"></div>
                <span>加载 Mod 中...</span>
              </div>
              
              <!-- 没有安装 Mod -->
              <div v-else-if="modLoadStatus === 'no-mods'" class="mod-empty">
                <Package class="mod-empty-icon" size="32" />
                <p class="mod-empty-text">暂未安装 Mod</p>
                <p class="mod-empty-hint">在「Mod与存档」页面添加本地 Mod</p>
                <button v-if="hasModSources" class="btn-outline" @click="handleBrowseMods">
                  浏览 Mod 商店
                </button>
              </div>
              
              <!-- 有已安装的 Mod -->
              <div v-else-if="modLoadStatus === 'has-mods'" class="mod-list">
                <div 
                  v-for="mod in mods.slice(0, 5)" 
                  :key="mod.id"
                  class="mod-item"
                  :class="{ 'mod-disabled': !mod.enabled }"
                >
                  <div class="mod-status" :class="{ enabled: mod.enabled }">
                    <span v-if="mod.enabled">✓</span>
                    <span v-else>○</span>
                  </div>
                  <div class="mod-info">
                    <h4 class="mod-name">{{ mod.name }}</h4>
                    <p class="mod-meta">
                      <span v-if="mod.version">v{{ mod.version }}</span>
                      <span v-if="mod.author"> • {{ mod.author }}</span>
                    </p>
                  </div>
                </div>
                <button class="btn-outline full-width" @click="handleManageMods">
                  <Package class="icon" size="16" />
                  管理 Mod
                </button>
              </div>
              
              <!-- 默认状态 -->
              <div v-else class="mod-empty">
                <Package class="mod-empty-icon" size="32" />
                <p class="mod-empty-text">暂未安装 Mod</p>
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
import modExploreApi from '@/api/modExplore'
import { getLocalGames } from '@/api/localGame'
import { ArrowLeft, Clock, Trophy, Calendar, Package, Lock } from 'lucide-vue-next'
import noCoverImage from '@/assets/no_cover.png'

const route = useRoute()
const router = useRouter()

// 状态管理
const loading = ref(true)
const error = ref(null)
const game = ref(null)
const achievements = ref([])
const achievementsLoading = ref(false)
const gamePlaytime = ref(0)
const playtimeHistory = ref([]) // 游戏时长历史数据
const playtimeHistoryLoading = ref(false) // 游戏时长历史加载状态
const platformPlaytimes = ref([]) // 不同平台的游戏时长数据
const hoveredPointIndex = ref(null) // 当前悬停的数据点索引

// 隐藏成就相关状态
const revealedHiddenAchievements = ref(new Set()) // 已点击显示剧透的隐藏成就ID集合
const hoveredAchievementId = ref(null) // 当前鼠标悬停的成就ID
const newlyRevealedAchievements = ref(new Set()) // 刚刚被点击显示的成就ID集合（用于触发动画）

// 平台筛选相关状态
const selectedPlatformId = ref(null) // 当前选中的平台ID，null表示显示所有平台
const platformGroups = ref([]) // 按平台分组的成就数据

// Mod 管理相关状态
const mods = ref([]) // 用户已安装的本地 Mod 列表
const modLoadStatus = ref('idle') // idle | loading | no-mods | has-mods
const hasModSources = ref(false) // 该游戏是否有 Mod 平台支持


// 平台ID到平台名称的映射
const platformNameMap = {
  1: 'Steam',
  2: 'Epic Games',
  3: 'Origin',
  4: 'Uplay',
  5: 'GOG',
  6: 'PSN',
  7: 'Xbox',
  8: 'Nintendo Switch'
}

// 计算属性 - 按平台分组的成就
const groupedAchievements = computed(() => {
  const groups = {}
  achievements.value.forEach(achievement => {
    const platformId = achievement.platformId || 1
    const platformName = achievement.platformName || platformNameMap[platformId] || `平台 ${platformId}`
    
    if (!groups[platformId]) {
      groups[platformId] = {
        platformId,
        platformName,
        achievements: []
      }
    }
    groups[platformId].achievements.push(achievement)
  })
  return Object.values(groups)
})

// 当前显示的成就列表（根据平台筛选）
const displayedAchievements = computed(() => {
  if (selectedPlatformId.value === null) {
    // 显示所有成就
    return achievements.value
  } else {
    // 只显示选中平台的成就
    return achievements.value.filter(a => (a.platformId || 1) === selectedPlatformId.value)
  }
})

// 计算属性 - 成就统计（根据当前筛选的平台）
const achievementStats = computed(() => {
  const achievementsToCount = displayedAchievements.value
  const total = achievementsToCount.length
  const unlocked = achievementsToCount.filter(a => a.isUnlocked).length
  return { total, unlocked }
})

const achievementProgress = computed(() => {
  if (achievementStats.value.total === 0) return 0
  return ((achievementStats.value.unlocked / achievementStats.value.total) * 100).toFixed(1)
})

// 按平台计算的成就统计
const platformStats = computed(() => {
  const stats = {}
  groupedAchievements.value.forEach(group => {
    const total = group.achievements.length
    const unlocked = group.achievements.filter(a => a.isUnlocked).length
    const progress = total > 0 ? ((unlocked / total) * 100).toFixed(1) : 0
    stats[group.platformId] = { total, unlocked, progress }
  })
  return stats
})

// 加载游戏详情
const loadGameDetail = async () => {
  loading.value = true
  error.value = null
  try {
    const gameId = route.params.id
    console.log('加载玩家游戏详情，游戏ID:', gameId)

    // 从游戏库API获取玩家数据
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
            platform: platformName || '未知平台',
            playtimeMinutes,
            lastPlayed: libraryGame.lastPlayed ?? libraryGame.LastPlayed,
            achievementsUnlocked: unlocked,
            achievementsTotal: total
          }

          // 将分钟转换为小时用于展示
          gamePlaytime.value = playtimeMinutes > 0 ? playtimeMinutes / 60 : 0
          
          // 处理平台游戏时长数据
          if (ownedPlatforms && ownedPlatforms.length > 0) {
            const totalPlaytime = ownedPlatforms.reduce((sum, p) => {
              return sum + (p.playtimeMinutes ?? p.PlaytimeMinutes ?? 0)
            }, 0)
            
            platformPlaytimes.value = ownedPlatforms
              .map(p => ({
                platformId: p.platformId ?? p.PlatformId,
                platformName: p.platformName ?? p.PlatformName,
                playtimeMinutes: p.playtimeMinutes ?? p.PlaytimeMinutes ?? 0,
                playtimeHours: (p.playtimeMinutes ?? p.PlaytimeMinutes ?? 0) / 60,
                percentage: totalPlaytime > 0 
                  ? ((p.playtimeMinutes ?? p.PlaytimeMinutes ?? 0) / totalPlaytime) * 100 
                  : 0
              }))
              .filter(p => p.playtimeMinutes > 0) // 只显示有游戏时长的平台
              .sort((a, b) => b.playtimeMinutes - a.playtimeMinutes) // 按时长降序排列
          } else {
            platformPlaytimes.value = []
          }
        } else {
          // 如果游戏库里没有，尝试从通用API获取基本信息
          const gameResponse = await gameApi.getGame(gameId)
          if (gameResponse.success && gameResponse.data) {
            const detail = gameResponse.data
            game.value = {
              id: detail.gameId ?? detail.GameId,
              name: detail.name ?? detail.Name,
              headerImage: detail.media?.headerImage ?? detail.Media?.HeaderImage,
              platform: '',
              playtimeMinutes: 0,
              lastPlayed: null,
              achievementsUnlocked: 0,
              achievementsTotal: 0
            }
          }
        }
      }
    } catch (libErr) {
      console.log('从游戏库获取失败:', libErr)
      // 如果游戏库获取失败，尝试从通用API获取基本信息
      try {
        const gameResponse = await gameApi.getGame(gameId)
        if (gameResponse.success && gameResponse.data) {
          const detail = gameResponse.data
          game.value = {
            id: detail.gameId ?? detail.GameId,
            name: detail.name ?? detail.Name,
            headerImage: detail.media?.headerImage ?? detail.Media?.HeaderImage,
            platform: '',
            playtimeMinutes: 0,
            lastPlayed: null,
            achievementsUnlocked: 0,
            achievementsTotal: 0
          }
        }
      } catch (gameErr) {
        console.error('从通用API获取失败:', gameErr)
      }
    }

    if (!game.value) {
      throw new Error('未找到游戏信息')
    }

    // 只加载成就数据（玩家信息）
    await loadAchievements()
    
    // 加载游戏时长历史数据
    await loadPlaytimeHistory()
    
    // 加载 Mod 数据
    await loadGameMods()
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
        const hidden = a.hidden ?? a.Hidden ?? false
        const platformId = a.platformId ?? a.PlatformId ?? 1
        const platformName = a.platformName ?? a.PlatformName

        return {
          id: a.id || a.achievementId || a.AchievementId,
          name: a.displayName || a.DisplayName || a.name || a.achievementName || a.AchievementName,
          description: a.description || a.achievementDescription || a.Description,
          iconUnlocked: a.iconUnlocked || a.IconUnlocked || a.icon,
          iconLocked: a.iconLocked || a.IconLocked,
          isUnlocked: unlocked !== undefined ? unlocked : (unlockTime != null),
          unlockTime,
          hidden,
          platformId,
          platformName
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

// 计算属性 - 游戏时长趋势图数据点
const playtimeChartPoints = computed(() => {
  if (!playtimeHistory.value || playtimeHistory.value.length === 0) {
    return '0,50 100,50'
  }
  
  const data = playtimeHistory.value
  const values = data.map(d => d.playtimeMinutes || 0)
  const maxValue = Math.max(...values)
  const minValue = Math.min(...values)
  
  // 计算合理的显示范围
  const range = maxValue - minValue
  
  // 如果所有值都相同或范围很小，使用固定范围
  if (range <= 10) {
    const displayMin = Math.max(0, minValue - 50) // 最小值向下扩展50
    const displayMax = maxValue + 50 // 最大值向上扩展50
    
    const pointCount = data.length
    const points = data.map((item, index) => {
      const x = pointCount > 1 ? (index / (pointCount - 1)) * 100 : 50
      const value = item.playtimeMinutes || 0
      const normalizedValue = displayMax > displayMin 
        ? ((value - displayMin) / (displayMax - displayMin))
        : 0.5
      const y = 100 - (normalizedValue * 90) - 5 // 留出5%的上边距和5%的下边距
      return `${x},${y}`
    }).join(' ')
    return points
  }
  
  // 如果范围较大，使用动态范围，让变化更明显
  // 最小值从0开始，或从实际最小值向下扩展10%
  const displayMin = Math.max(0, minValue - range * 0.1)
  // 最大值向上扩展15%，让顶部有空间
  const displayMax = maxValue + range * 0.15
  
  const pointCount = data.length
  
  const points = data.map((item, index) => {
    const x = pointCount > 1 ? (index / (pointCount - 1)) * 100 : 50
    const value = item.playtimeMinutes || 0
    const normalizedValue = ((value - displayMin) / (displayMax - displayMin))
    const y = 100 - (normalizedValue * 90) - 5 // 留出5%的上边距和5%的下边距
    return `${x},${y}`
  }).join(' ')
  
  return points
})

// 计算属性 - 游戏时长趋势图填充区域
const playtimeChartArea = computed(() => {
  if (!playtimeHistory.value || playtimeHistory.value.length === 0) {
    return '0,100 100,100 100,50 0,50'
  }
  
  const points = playtimeChartPoints.value
  const data = playtimeHistory.value
  const firstX = 0
  const lastX = 100
  
  return `${firstX},100 ${points} ${lastX},100`
})

// 加载游戏时长历史数据（7天内）
const loadPlaytimeHistory = async () => {
  if (!game.value) return
  
  playtimeHistoryLoading.value = true
  try {
    const gameId = game.value.id || route.params.id
    console.log('加载游戏时长历史数据，游戏ID:', gameId)
    
    // 调用后端API获取游戏时长历史数据（7天内）
    const response = await libraryApi.getGamePlaytimeHistory(gameId)
    
    if (response.success && response.data) {
      const data = response.data
      const items = data.items || []
      
      // 确保数据按日期排序
      const sortedItems = items.sort((a, b) => {
        const dateA = new Date(a.date || a.Date)
        const dateB = new Date(b.date || b.Date)
        return dateA - dateB
      })
      
      playtimeHistory.value = sortedItems.map(item => ({
        date: item.date || item.Date,
        playtimeMinutes: item.playtimeMinutes || item.PlaytimeMinutes || 0
      }))
      
      console.log('游戏时长历史数据加载成功，共', playtimeHistory.value.length, '条记录')
    } else {
      console.warn('获取游戏时长历史数据失败，响应:', response)
      playtimeHistory.value = []
    }
    
    // 如果没有历史数据，使用当前游戏时间生成7天的默认数据（显示为直线）
    if (playtimeHistory.value.length === 0 && gamePlaytime.value !== undefined && gamePlaytime.value !== null) {
      const currentPlaytimeMinutes = gamePlaytime.value * 60 // 转换为分钟
      const today = new Date()
      today.setHours(0, 0, 0, 0)
      
      // 生成过去7天的数据，每天都是相同的游戏时间
      playtimeHistory.value = []
      for (let i = 6; i >= 0; i--) {
        const date = new Date(today)
        date.setDate(date.getDate() - i)
        playtimeHistory.value.push({
          date: date.toISOString().split('T')[0],
          playtimeMinutes: currentPlaytimeMinutes
        })
      }
      console.log('生成默认游戏时长历史数据（7天直线），游戏时间:', gamePlaytime.value, '小时')
    }
  } catch (err) {
    console.error('加载游戏时长历史数据失败:', err)
    playtimeHistory.value = []
    
    // 即使出错，如果有当前游戏时间，也生成默认数据
    if (gamePlaytime.value !== undefined && gamePlaytime.value !== null) {
      const currentPlaytimeMinutes = gamePlaytime.value * 60
      const today = new Date()
      today.setHours(0, 0, 0, 0)
      
      playtimeHistory.value = []
      for (let i = 6; i >= 0; i--) {
        const date = new Date(today)
        date.setDate(date.getDate() - i)
        playtimeHistory.value.push({
          date: date.toISOString().split('T')[0],
          playtimeMinutes: currentPlaytimeMinutes
        })
      }
      console.log('生成默认游戏时长历史数据（7天直线），游戏时间:', gamePlaytime.value, '小时')
    }
  } finally {
    playtimeHistoryLoading.value = false
  }
}

// 图表相关计算方法
// 获取图表点的 X 坐标（百分比）
const getChartX = (index) => {
  if (!playtimeHistory.value || playtimeHistory.value.length === 0) return 50
  const count = playtimeHistory.value.length
  return count > 1 ? (index / (count - 1)) * 100 : 50
}

// 获取图表点的 Y 坐标（百分比）
const getChartY = (value) => {
  if (!playtimeHistory.value || playtimeHistory.value.length === 0) return 50
  
  const values = playtimeHistory.value.map(d => d.playtimeMinutes || 0)
  const maxValue = Math.max(...values)
  const minValue = Math.min(...values)
  const range = maxValue - minValue
  
  let displayMin, displayMax
  
  if (range <= 10) {
    displayMin = Math.max(0, minValue - 50)
    displayMax = maxValue + 50
  } else {
    displayMin = Math.max(0, minValue - range * 0.1)
    displayMax = maxValue + range * 0.15
  }
  
  const normalizedValue = displayMax > displayMin 
    ? ((value - displayMin) / (displayMax - displayMin))
    : 0.5
  
  return 100 - (normalizedValue * 90) - 5 // 5% 上下边距
}

// 计算 Y 轴刻度
const yAxisTicks = computed(() => {
  if (!playtimeHistory.value || playtimeHistory.value.length === 0) {
    return []
  }
  
  const values = playtimeHistory.value.map(d => d.playtimeMinutes || 0)
  const maxValue = Math.max(...values)
  const minValue = Math.min(...values)
  const range = maxValue - minValue
  
  let displayMin, displayMax
  
  if (range <= 10) {
    displayMin = Math.max(0, minValue - 50)
    displayMax = maxValue + 50
  } else {
    displayMin = Math.max(0, minValue - range * 0.1)
    displayMax = maxValue + range * 0.15
  }
  
  // 先确定上限和下限的位置
  // 上限对应图表顶部（5%位置），下限对应图表底部（95%位置）
  const topPosition = 5   // 顶部位置（对应最大值）
  const bottomPosition = 95  // 底部位置（对应最小值）
  
  // 计算上限和下限对应的Y坐标（用于绘制网格线）
  const topY = 100 - topPosition
  const bottomY = 100 - bottomPosition
  
  // 生成5个刻度点：上限、中间3个、下限
  const ticks = []
  for (let i = 0; i <= 4; i++) {
    // 位置从底部（95%）到顶部（5%）均匀分布
    const position = bottomPosition - (i / 4) * (bottomPosition - topPosition)
    const valueRatio = i / 4
    const value = (displayMin + (displayMax - displayMin) * (1 - valueRatio)) / 60 // 从最大值到最小值
    const y = 100 - position
    
    ticks.push({
      position,
      value,
      y
    })
  }
  
  return ticks
})

// 格式化游戏时长为小时格式（用于Y轴）
const formatPlaytimeHours = (hours) => {
  if (!hours || hours === 0) return '0h'
  // 如果小于1小时，显示为小数
  if (hours < 1) {
    return `${(hours * 60).toFixed(0)}m`
  }
  // 如果是整数，不显示小数
  if (hours % 1 === 0) {
    return `${hours.toFixed(0)}h`
  }
  // 否则显示一位小数
  return `${hours.toFixed(1)}h`
}

// 格式化坐标轴日期（简短）
const formatAxisDate = (dateStr) => {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return `${date.getMonth() + 1}/${date.getDate()}`
}

// 格式化提示框日期（完整）
const formatTooltipDate = (dateStr) => {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN', { month: 'long', day: 'numeric' })
}

// 计算相对前一个点的变化
const getPlaytimeChange = (index) => {
  if (index === 0 || !playtimeHistory.value[index] || !playtimeHistory.value[index - 1]) {
    return '—'
  }
  
  const current = playtimeHistory.value[index].playtimeMinutes || 0
  const previous = playtimeHistory.value[index - 1].playtimeMinutes || 0
  const change = current - previous
  
  if (change === 0) return '无变化'
  
  const changeHours = change / 60
  const sign = change > 0 ? '+' : ''
  return `${sign}${formatPlaytime(changeHours)}`
}

// 获取变化的CSS类
const getChangeClass = (index) => {
  if (index === 0 || !playtimeHistory.value[index] || !playtimeHistory.value[index - 1]) {
    return ''
  }
  
  const current = playtimeHistory.value[index].playtimeMinutes || 0
  const previous = playtimeHistory.value[index - 1].playtimeMinutes || 0
  const change = current - previous
  
  if (change > 0) return 'positive'
  if (change < 0) return 'negative'
  return 'neutral'
}

// 格式化日期（中国时区）
const formatDate = (date) => {
  if (!date) return ''
  try {
    const d = new Date(date)
    // 转换为中国时区（UTC+8）
    const chinaTime = new Date(d.toLocaleString('en-US', { timeZone: 'Asia/Shanghai' }))
    
    // 格式化显示：年月日 时分
    const year = chinaTime.getFullYear()
    const month = String(chinaTime.getMonth() + 1).padStart(2, '0')
    const day = String(chinaTime.getDate()).padStart(2, '0')
    const hour = String(chinaTime.getHours()).padStart(2, '0')
    const minute = String(chinaTime.getMinutes()).padStart(2, '0')
    
    return `${year}-${month}-${day} ${hour}:${minute}`
  } catch {
    return date
  }
}

// 图片加载错误处理
const handleImageError = (event) => {
  event.target.src = noCoverImage
}

// 处理隐藏成就点击
const handleHiddenAchievementClick = (achievement) => {
  if (achievement.hidden && !achievement.isUnlocked && !revealedHiddenAchievements.value.has(achievement.id)) {
    // 只允许显示一次，不允许再次隐藏
    revealedHiddenAchievements.value.add(achievement.id)
    // 标记为刚刚显示，用于触发动画
    newlyRevealedAchievements.value.add(achievement.id)
    // 动画结束后移除标记（动画持续0.5秒）
    setTimeout(() => {
      newlyRevealedAchievements.value.delete(achievement.id)
    }, 500)
  }
}


// 跳转到游戏商店详情页
const goToStoreDetail = () => {
  const gameId = game.value?.id || route.params.id
  router.push({ name: 'StoreDetail', params: { id: gameId } })
}

// 加载游戏 Mod 数据
const loadGameMods = async () => {
  if (!game.value) return
  
  modLoadStatus.value = 'loading'
  
  try {
    const gameId = game.value.id || route.params.id
    console.log('加载本地 Mod 数据，游戏ID:', gameId)
    
    // 先获取用户的本地游戏列表，找到该游戏的安装记录
    const localGamesRes = await getLocalGames()
    const localGamesList = localGamesRes.data?.items || localGamesRes.items || []
    
    // 查找该游戏的本地安装记录
    const localInstall = localGamesList.find(g => g.gameId == gameId)
    
    if (localInstall && localInstall.modsCount > 0) {
      // 如果有安装记录且有 Mod，从游戏详情中获取 Mod 列表
      // 注意：LocalGameListDto 已经包含 modsCount，但不包含详细 Mod 列表
      // 需要调用详情 API 获取完整 Mod 列表
      const { getLocalGameDetail } = await import('@/api/localGame')
      const detailRes = await getLocalGameDetail(localInstall.installId)
      
      if (detailRes.data?.mods && detailRes.data.mods.length > 0) {
        mods.value = detailRes.data.mods.map(mod => ({
          id: mod.modId || mod.id,
          name: mod.modName || mod.name,
          version: mod.version || 1,
          author: mod.author,
          enabled: mod.enabled !== false,
          filePath: mod.filePath,
          sizeGB: mod.sizeGB || 0
        }))
        modLoadStatus.value = 'has-mods'
      } else {
        mods.value = []
        modLoadStatus.value = 'no-mods'
      }
    } else {
      mods.value = []
      modLoadStatus.value = 'no-mods'
    }
    
    // 同时检查该游戏是否有 Mod 平台支持（用于显示"浏览更多"按钮）
    try {
      const sourcesResponse = await modExploreApi.getGameModSources(gameId)
      hasModSources.value = sourcesResponse.success && sourcesResponse.data?.sources?.length > 0
    } catch (e) {
      hasModSources.value = false
    }
  } catch (err) {
    console.log('加载本地 Mod 数据失败:', err)
    modLoadStatus.value = 'no-mods'
    mods.value = []
  }
}

// Mod 管理处理 - 跳转到 Mod 与存档页面
const handleManageMods = () => {
  router.push({ name: 'Mods' })
}

// 浏览 Mod 商店 - 跳转到 Mod 探索页面
const handleBrowseMods = () => {
  const gameId = game.value?.id || route.params.id
  router.push({ name: 'ModExplore', query: { gameId } })
}


// 监听游戏ID变化，重新加载数据
watch(() => route.params.id, () => {
  if (route.params.id) {
    loadGameDetail()
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

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.sidebar-title {
  font-size: 14px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #94a3b8;
  margin-bottom: 16px;
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

/* 时间栏目样式 */
.playtime-section {
  position: relative;
  overflow: hidden;
  min-height: 180px;
}

.playtime-content {
  position: relative;
  z-index: 2;
  height: 100%;
}

.playtime-layout {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 32px;
  align-items: center;
}

.playtime-chart-background {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1;
  opacity: 0.4;
  pointer-events: none;
}

.playtime-chart {
  width: 100%;
  height: 100%;
  overflow: visible;
}

.playtime-display {
  position: relative;
  z-index: 2;
}

.playtime-display .section-title {
  margin-bottom: 8px;
  font-size: 18px;
  color: #94a3b8;
}

.playtime-value {
  font-size: 48px;
  font-weight: 700;
  color: #f8fafc;
  line-height: 1.2;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

/* 平台时长条形图样式 */
.platform-playtime-chart {
  position: relative;
  z-index: 2;
}

.chart-title {
  font-size: 14px;
  font-weight: 600;
  color: #94a3b8;
  margin-bottom: 16px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.bar-chart {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.bar-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.bar-label {
  min-width: 80px;
  font-size: 13px;
  font-weight: 500;
  color: #cbd5e1;
  text-align: right;
}

.bar-container {
  flex: 1;
  position: relative;
  height: 28px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  overflow: hidden;
  display: flex;
  align-items: center;
}

.bar-fill {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  border-radius: 6px;
  transition: width 0.6s ease-out;
  opacity: 0.85;
}

.bar-color-0 {
  background: linear-gradient(90deg, rgba(139, 92, 246, 0.8), rgba(139, 92, 246, 0.6));
}

.bar-color-1 {
  background: linear-gradient(90deg, rgba(59, 130, 246, 0.8), rgba(59, 130, 246, 0.6));
}

.bar-color-2 {
  background: linear-gradient(90deg, rgba(236, 72, 153, 0.8), rgba(236, 72, 153, 0.6));
}

.bar-color-3 {
  background: linear-gradient(90deg, rgba(34, 197, 94, 0.8), rgba(34, 197, 94, 0.6));
}

.bar-value {
  position: absolute;
  right: 8px;
  font-size: 12px;
  font-weight: 600;
  color: #f8fafc;
  z-index: 1;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}

/* 一周内游戏时间变化图表样式 */
.playtime-trend-section {
  margin-top: 24px;
}

.trend-chart-container {
  position: relative;
  padding: 24px 0 48px 24px;
  margin-top: 16px;
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.02) 0%, rgba(139, 92, 246, 0.05) 100%);
  border-radius: 12px;
  border: 1px solid rgba(139, 92, 246, 0.1);
}

.chart-wrapper {
  position: relative;
  width: 100%;
  height: 280px;
  margin-bottom: 16px;
}

.trend-chart-svg {
  width: 100%;
  height: 100%;
  overflow: visible;
}

.trend-line {
  filter: drop-shadow(0 2px 6px rgba(139, 92, 246, 0.4));
  transition: stroke-width 0.2s ease;
}

.hover-zone {
  cursor: pointer;
}

.hover-indicator line {
  animation: dashOffset 1s linear infinite;
}

@keyframes dashOffset {
  to {
    stroke-dashoffset: -14;
  }
}

.chart-tooltip {
  position: absolute;
  top: -100px;
  transform: translateX(-50%);
  background: linear-gradient(135deg, rgba(25, 25, 30, 0.98) 0%, rgba(15, 15, 20, 0.98) 100%);
  border: 1px solid rgba(139, 92, 246, 0.5);
  border-radius: 12px;
  padding: 12px 16px;
  font-size: 13px;
  color: #f8fafc;
  pointer-events: none;
  z-index: 100;
  box-shadow: 
    0 8px 24px rgba(0, 0, 0, 0.5),
    0 0 0 1px rgba(139, 92, 246, 0.2) inset;
  white-space: nowrap;
  backdrop-filter: blur(12px);
  animation: tooltipFadeIn 0.2s ease;
}

@keyframes tooltipFadeIn {
  from {
    opacity: 0;
    transform: translateX(-50%) translateY(-5px);
  }
  to {
    opacity: 1;
    transform: translateX(-50%) translateY(0);
  }
}

.chart-tooltip::before {
  content: '';
  position: absolute;
  bottom: -7px;
  left: 50%;
  transform: translateX(-50%);
  width: 0;
  height: 0;
  border-left: 7px solid transparent;
  border-right: 7px solid transparent;
  border-top: 7px solid rgba(139, 92, 246, 0.5);
}

.chart-tooltip::after {
  content: '';
  position: absolute;
  bottom: -6px;
  left: 50%;
  transform: translateX(-50%);
  width: 0;
  height: 0;
  border-left: 6px solid transparent;
  border-right: 6px solid transparent;
  border-top: 6px solid rgba(25, 25, 30, 0.98);
}

.tooltip-date {
  font-weight: 600;
  margin-bottom: 8px;
  color: #cbd5e1;
  font-size: 13px;
  letter-spacing: 0.3px;
}

.tooltip-value {
  font-size: 14px;
  color: #e2e8f0;
  margin-bottom: 6px;
}

.tooltip-value strong {
  font-weight: 700;
  color: #8b5cf6;
  font-size: 15px;
}

.tooltip-change {
  font-size: 12px;
  padding-top: 6px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  gap: 6px;
}

.tooltip-change .positive {
  color: #10b981;
  font-weight: 600;
}

.tooltip-change .negative {
  color: #ef4444;
  font-weight: 600;
}

.tooltip-change .neutral {
  color: #94a3b8;
}

.chart-axes {
  position: relative;
  width: 100%;
}

.x-axis {
  position: relative;
  height: 36px;
  border-top: 1.5px solid rgba(139, 92, 246, 0.2);
  margin-top: 8px;
  padding-top: 10px;
}

.x-axis-label {
  position: absolute;
  transform: translateX(-50%);
  font-size: 12px;
  color: #94a3b8;
  white-space: nowrap;
  font-weight: 500;
  letter-spacing: 0.3px;
  transition: color 0.2s ease;
}

.x-axis-label:hover {
  color: #cbd5e1;
}

.y-axis {
  position: absolute;
  left: -70px;
  top: 0;
  bottom: 0;
  width: 65px;
  border-right: 1.5px solid rgba(139, 92, 246, 0.2);
  padding-right: 12px;
}

.y-axis-label {
  position: absolute;
  right: 12px;
  transform: translateY(50%);
  font-size: 12px;
  color: #94a3b8;
  text-align: right;
  font-weight: 500;
  letter-spacing: 0.3px;
  transition: color 0.2s ease;
}

.y-axis-label:hover {
  color: #cbd5e1;
}

.loading-state,
.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 48px 24px;
  color: #94a3b8;
  font-size: 14px;
}

.loading-spinner {
  width: 24px;
  height: 24px;
  border: 3px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* 平台筛选器 */
.platform-filter {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.platform-filter-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  background: rgba(20, 20, 23, 0.75);
  color: #94a3b8;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  backdrop-filter: blur(20px);
}

.platform-filter-btn:hover {
  background: rgba(30, 30, 35, 0.9);
  border-color: rgba(139, 92, 246, 0.3);
  color: #c4b5fd;
}

.platform-filter-btn.active {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.5);
  color: #c4b5fd;
}

.platform-filter .platform-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 40px;
  padding: 2px 8px;
  background: rgba(139, 92, 246, 0.15);
  border: 1px solid rgba(139, 92, 246, 0.3);
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  color: #c4b5fd;
}

/* 游戏介绍 */
.game-description {
  line-height: 1.6;
  transition: all 0.3s ease;
}

.description-content {
  margin-bottom: 12px;
  transition: all 0.3s ease;
}

.description-text {
  color: #cbd5e1;
  font-size: 15px;
  white-space: pre-wrap;
  word-wrap: break-word;
}

.description-text.text-muted {
  color: #64748b;
  font-style: italic;
}

/* HTML 格式的描述内容样式 */
.description-html {
  color: #cbd5e1;
  font-size: 15px;
  line-height: 1.6;
}

.description-html :deep(p) {
  margin-bottom: 12px;
  color: #cbd5e1;
}

.description-html :deep(h1),
.description-html :deep(h2),
.description-html :deep(h3),
.description-html :deep(h4),
.description-html :deep(h5),
.description-html :deep(h6) {
  color: #f8fafc;
  font-weight: 600;
  margin-top: 16px;
  margin-bottom: 8px;
}

.description-html :deep(strong),
.description-html :deep(b) {
  color: #f8fafc;
  font-weight: 600;
}

.description-html :deep(em),
.description-html :deep(i) {
  font-style: italic;
}

.description-html :deep(ul),
.description-html :deep(ol) {
  margin: 12px 0;
  padding-left: 24px;
}

.description-html :deep(li) {
  margin-bottom: 6px;
}

.description-html :deep(a) {
  color: #8b5cf6;
  text-decoration: none;
  transition: color 0.2s;
}

.description-html :deep(a:hover) {
  color: #7c3aed;
  text-decoration: underline;
}

.description-html :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
  margin: 12px 0;
}

.description-html :deep(blockquote) {
  border-left: 3px solid rgba(139, 92, 246, 0.5);
  padding-left: 16px;
  margin: 12px 0;
  color: #94a3b8;
  font-style: italic;
}

.description-html :deep(code) {
  background: rgba(20, 20, 23, 0.8);
  padding: 2px 6px;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  color: #c4b5fd;
}

.description-html :deep(pre) {
  background: rgba(20, 20, 23, 0.8);
  padding: 12px;
  border-radius: 8px;
  overflow-x: auto;
  margin: 12px 0;
}

.description-html :deep(pre code) {
  background: transparent;
  padding: 0;
  color: #cbd5e1;
}

.show-details-btn {
  background: transparent;
  border: none;
  color: #64748b;
  font-size: 14px;
  cursor: pointer;
  padding: 8px 0;
  transition: all 0.2s;
  text-align: left;
  font-weight: 500;
}

.show-details-btn:hover {
  color: #8b5cf6;
}

/* 游戏新闻 */
.news-list {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.news-item {
  padding: 16px;
  background: rgba(20, 20, 23, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  transition: all 0.2s;
}

.news-item:hover {
  border-color: rgba(139, 92, 246, 0.3);
  background: rgba(20, 20, 23, 0.7);
}

.news-header {
  margin-bottom: 12px;
}

.news-title {
  font-size: 16px;
  font-weight: 600;
  color: #f8fafc;
  margin-bottom: 8px;
  line-height: 1.4;
}

.news-link {
  color: #f8fafc;
  text-decoration: none;
  transition: color 0.2s;
}

.news-link:hover {
  color: #8b5cf6;
}

.news-meta {
  display: flex;
  gap: 12px;
  font-size: 13px;
  color: #94a3b8;
}

.news-author {
  color: #94a3b8;
}

.news-date {
  color: #64748b;
}

.news-content {
  color: #cbd5e1;
  font-size: 14px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-wrap: break-word;
}

.show-more-news-btn {
  margin-top: 8px;
  padding: 12px 24px;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  color: #94a3b8;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
}

.show-more-news-btn:hover {
  background: rgba(139, 92, 246, 0.1);
  border-color: rgba(139, 92, 246, 0.3);
  color: #8b5cf6;
}

.related-games {
  margin-top: 12px;
  font-size: 14px;
  color: #94a3b8;
}

.game-tag {
  display: inline-block;
  padding: 4px 10px;
  background: rgba(139, 92, 246, 0.1);
  border: 1px solid rgba(139, 92, 246, 0.2);
  border-radius: 4px;
  margin-left: 8px;
  color: #c4b5fd;
  font-size: 12px;
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
  opacity: 0;
  transform: translateY(20px);
  animation: fadeInUp 0.6s ease-out forwards;
}

.achievement-card:hover {
  background: rgba(20, 20, 23, 0.8);
  border-color: rgba(139, 92, 246, 0.3);
  transform: translateY(-2px);
}

.achievement-card.unlocked {
  border-color: rgba(234, 179, 8, 0.3);
}

.achievement-card.hidden {
  cursor: pointer;
}

.achievement-card.hidden:hover {
  border-color: rgba(139, 92, 246, 0.5);
}

/* 成就卡片出场动画 */
@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
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

.achievement-icon-placeholder.hidden-icon {
  background: rgba(30, 30, 35, 0.9);
  border-color: rgba(139, 92, 246, 0.2);
}

.question-mark {
  font-size: 32px;
  font-weight: bold;
  color: #64748b;
  user-select: none;
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
  position: relative;
}

.achievement-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #f8fafc;
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.achievement-card:not(.unlocked) .achievement-name {
  color: #64748b;
}

.achievement-desc {
  font-size: 12px;
  color: #94a3b8;
  margin-bottom: 8px;
  line-height: 1.4;
  transition: opacity 0.3s ease, transform 0.3s ease;
}

/* 渐变消失效果 */
.fade-out {
  opacity: 0;
  transform: translateY(-4px);
  pointer-events: none;
}

/* 渐变出现效果 */
.fade-in {
  opacity: 1;
  transform: translateY(0);
}

/* 隐藏成就显示原内容的渐变动画 */
.reveal-fade-in {
  animation: revealFadeIn 0.5s ease-out forwards;
}

@keyframes revealFadeIn {
  from {
    opacity: 0;
    transform: scale(0.95);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

/* 隐藏成就的剧透提示 */
.achievement-spoiler-hint {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  font-size: 13px;
  color: #94a3b8;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.3s ease, transform 0.3s ease;
  white-space: nowrap;
  font-weight: 500;
}

.achievement-spoiler-hint.fade-in {
  opacity: 1;
  transform: translate(-50%, -50%);
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
  color: #94a3b8;
  margin-bottom: 8px;
  font-size: 14px;
}

.mod-empty-hint {
  color: #64748b;
  font-size: 12px;
  margin-bottom: 16px;
}

.mod-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 32px 20px;
  color: #94a3b8;
  font-size: 14px;
}

.mod-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mod-item {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 12px;
  background: rgba(15, 15, 19, 0.6);
  border-radius: 8px;
  transition: opacity 0.2s;
}

.mod-item.mod-disabled {
  opacity: 0.5;
}

.mod-status {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  background: rgba(100, 116, 139, 0.3);
  color: #64748b;
  flex-shrink: 0;
}

.mod-status.enabled {
  background: rgba(34, 197, 94, 0.2);
  color: #22c55e;
}

.mod-thumbnail {
  width: 48px;
  height: 48px;
  border-radius: 6px;
  object-fit: cover;
  flex-shrink: 0;
}

.mod-info {
  flex: 1;
  min-width: 0;
}

.mod-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #f8fafc;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mod-meta {
  font-size: 12px;
  color: #64748b;
  display: flex;
  gap: 8px;
}

.mod-downloads {
  color: #818cf8;
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

/* 新闻详情弹窗 */
.news-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
  animation: fadeIn 0.2s ease-out;
}

.news-modal {
  background: #1a1a1f;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  width: 100%;
  max-width: 800px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
  animation: slideUp 0.3s ease-out;
}

.news-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.news-modal-title {
  font-size: 20px;
  font-weight: 600;
  color: #f8fafc;
  margin: 0;
  flex: 1;
  padding-right: 16px;
}

.news-modal-close {
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

.news-modal-close:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #f8fafc;
}

.news-modal-content {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}

.news-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.news-detail-meta {
  display: flex;
  gap: 16px;
  font-size: 14px;
  color: #94a3b8;
  padding-bottom: 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.news-detail-author,
.news-detail-date {
  color: #94a3b8;
}

.news-detail-body {
  color: #cbd5e1;
  font-size: 15px;
  line-height: 1.8;
  word-wrap: break-word;
}

.news-detail-body :deep(p) {
  margin-bottom: 12px;
  color: #cbd5e1;
}

.news-detail-body :deep(h1),
.news-detail-body :deep(h2),
.news-detail-body :deep(h3),
.news-detail-body :deep(h4),
.news-detail-body :deep(h5),
.news-detail-body :deep(h6) {
  color: #f8fafc;
  font-weight: 600;
  margin-top: 16px;
  margin-bottom: 8px;
}

.news-detail-body :deep(h1) {
  font-size: 24px;
}

.news-detail-body :deep(h2) {
  font-size: 20px;
}

.news-detail-body :deep(h3) {
  font-size: 18px;
}

.news-detail-body :deep(strong),
.news-detail-body :deep(b) {
  color: #f8fafc;
  font-weight: 600;
}

.news-detail-body :deep(em),
.news-detail-body :deep(i) {
  font-style: italic;
}

.news-detail-body :deep(ul),
.news-detail-body :deep(ol) {
  margin: 12px 0;
  padding-left: 24px;
}

.news-detail-body :deep(li) {
  margin-bottom: 6px;
  color: #cbd5e1;
}

.news-detail-body :deep(a) {
  color: #8b5cf6;
  text-decoration: none;
  transition: color 0.2s;
}

.news-detail-body :deep(a:hover) {
  color: #7c3aed;
  text-decoration: underline;
}

.news-detail-body :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
  margin: 12px 0;
  display: block;
}

.news-detail-body :deep(blockquote) {
  border-left: 3px solid rgba(139, 92, 246, 0.5);
  padding-left: 16px;
  margin: 12px 0;
  color: #94a3b8;
  font-style: italic;
  background: rgba(20, 20, 23, 0.5);
  padding: 12px 16px;
  border-radius: 4px;
}

.news-detail-body :deep(code) {
  background: rgba(20, 20, 23, 0.8);
  padding: 2px 6px;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  color: #c4b5fd;
}

.news-detail-body :deep(pre) {
  background: rgba(20, 20, 23, 0.8);
  padding: 12px;
  border-radius: 8px;
  overflow-x: auto;
  margin: 12px 0;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.news-detail-body :deep(pre code) {
  background: transparent;
  padding: 0;
  color: #cbd5e1;
}

.news-detail-body :deep(table) {
  width: 100%;
  border-collapse: collapse;
  margin: 12px 0;
}

.news-detail-body :deep(table th),
.news-detail-body :deep(table td) {
  padding: 8px 12px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  text-align: left;
}

.news-detail-body :deep(table th) {
  background: rgba(139, 92, 246, 0.1);
  color: #f8fafc;
  font-weight: 600;
}

.news-detail-body :deep(table tr:nth-child(even)) {
  background: rgba(20, 20, 23, 0.3);
}

.news-detail-body :deep(hr) {
  border: none;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  margin: 16px 0;
}

.news-detail-body :deep(br) {
  line-height: 1.8;
}

.news-detail-related {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  font-size: 14px;
  color: #94a3b8;
}

.news-modal-footer {
  display: flex;
  gap: 12px;
  padding: 20px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  justify-content: flex-end;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
