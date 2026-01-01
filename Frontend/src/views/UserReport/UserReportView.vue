<template>
  <div class="user-report-container">
    <!-- Header -->
    <div class="report-header">
      <div class="header-left">
        <h1 class="page-title">我的游戏报表</h1>
        <p class="page-desc">
          Steam 个人数据概览
          <span v-if="refreshing" class="refreshing-hint">
            <RefreshCw class="icon spinning" /> 更新中...
          </span>
          <span v-else-if="lastUpdateTime" class="last-update">
            上次更新: {{ lastUpdateTime }}
          </span>
        </p>
      </div>
      <div class="header-right">
        <select v-model="selectedPeriod" class="period-select" @change="handlePeriodChange">
          <option value="week">最近7天</option>
          <option value="month">最近30天</option>
          <option value="year">今年</option>
          <option value="all">全部</option>
        </select>
        <button class="btn-export" @click="showExportDialog = true">
          <Download class="icon" />
          导出报表
        </button>
      </div>
    </div>

    <!-- Loading State (only show if no cached data) -->
    <div v-if="loading && !hasCachedData" class="loading-container">
      <div class="loading-spinner"></div>
      <p>加载数据中...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error && !hasCachedData" class="error-container">
      <AlertCircle class="error-icon" />
      <p>{{ error }}</p>
      <button class="btn-retry" @click="loadData(true)">重试</button>
    </div>

    <!-- Content (show with cached data or fresh data) -->
    <template v-else>
      <!-- Profile Card -->
      <div class="profile-section">
        <div class="profile-card">
          <div class="profile-avatar">
            <img v-if="profile.avatarUrl" :src="profile.avatarUrl" alt="avatar" />
            <User v-else class="avatar-placeholder" />
          </div>
          <div class="profile-info">
            <h2 class="profile-name">{{ profile.steamProfileName || profile.username }}</h2>
            <p class="profile-id" v-if="profile.steamId">Steam ID: {{ profile.steamId }}</p>
          </div>
          <div class="profile-stats">
            <div class="profile-stat">
              <span class="stat-value">{{ profile.steamLevel }}</span>
              <span class="stat-label">等级</span>
            </div>
            <div class="profile-stat">
              <span class="stat-value">{{ profile.badgeCount }}</span>
              <span class="stat-label">徽章</span>
            </div>
            <div class="profile-stat">
              <span class="stat-value">{{ profile.friendCount }}</span>
              <span class="stat-label">好友</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Stats Overview -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">游戏库</span>
            <div class="stat-icon indigo">
              <Gamepad2 class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ gameLibrary.totalGames }}</div>
          <div class="stat-desc">已玩 {{ gameLibrary.playedGames }} 款</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">总游戏时长</span>
            <div class="stat-icon emerald">
              <Clock class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ gameLibrary.totalPlaytimeFormatted }}</div>
          <div class="stat-desc">最近2周 {{ formatMinutes(gameLibrary.recentPlaytimeMinutes) }}</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">成就解锁</span>
            <div class="stat-icon amber">
              <Trophy class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ achievements.unlockedAchievements }}</div>
          <div class="stat-desc">完成率 {{ achievements.completionRate }}%</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">愿望单</span>
            <div class="stat-icon rose">
              <Heart class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ wishlist.totalItems }}</div>
          <div class="stat-desc">{{ wishlist.onSaleCount }} 款在打折</div>
        </div>
      </div>

      <!-- Platform Stats -->
      <div v-if="gameLibrary.platformStats?.length" class="platform-stats-section">
        <h3 class="section-title">📊 平台统计</h3>
        <div class="platform-stats-grid">
          <div v-for="platform in gameLibrary.platformStats" :key="platform.platformId" class="platform-stat-card">
            <div class="platform-header">
              <span class="platform-icon">{{ getPlatformIcon(platform.platformName) }}</span>
              <span class="platform-name">{{ platform.platformName }}</span>
            </div>
            <div class="platform-stats">
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.gameCount }}</span>
                <span class="platform-stat-label">游戏</span>
              </div>
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.playtimeFormatted }}</span>
                <span class="platform-stat-label">时长</span>
              </div>
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.percentage }}%</span>
                <span class="platform-stat-label">占比</span>
              </div>
            </div>
            <div class="platform-bar">
              <div class="platform-bar-fill" :style="{ width: platform.percentage + '%' }"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="content-grid">
        <!-- Left Column -->
        <div class="main-column">
          <!-- Playtime by Genre (Pie Chart) -->
          <div class="chart-card">
            <h3 class="chart-title">游戏时长分布</h3>
            <div class="chart-container">
              <canvas ref="genreChartRef"></canvas>
            </div>
            <div class="genre-legend" v-if="gameLibrary.playtimeByGenre?.length">
              <div v-for="(genre, index) in gameLibrary.playtimeByGenre.slice(0, 6)" :key="genre.genre" class="legend-item">
                <span class="legend-color" :style="{ background: chartColors[index] }"></span>
                <span class="legend-name">{{ genre.genre }}</span>
                <span class="legend-value">{{ genre.percentage }}%</span>
              </div>
            </div>
          </div>

          <!-- Top Played Games -->
          <div class="chart-card">
            <h3 class="chart-title">最常玩的游戏</h3>
            <div v-if="gameLibrary.topPlayedGames?.length" class="games-list">
              <div v-for="(game, index) in gameLibrary.topPlayedGames" :key="game.gameId" class="game-item">
                <div class="game-rank" :class="getRankClass(index)">{{ index + 1 }}</div>
                <img :src="game.headerImage || noCoverImage" class="game-image" @error="handleImageError" />
                <div class="game-info">
                  <h4 class="game-name">{{ game.gameName }}</h4>
                  <p class="game-meta">
                    <span v-if="game.achievementsUnlocked !== null">
                      成就 {{ game.achievementsUnlocked }}/{{ game.achievementsTotal }}
                    </span>
                  </p>
                </div>
                <div class="game-playtime">
                  <span class="playtime-value">{{ game.playtimeFormatted }}</span>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无游戏数据</div>
          </div>
        </div>

        <!-- Right Column -->
        <div class="side-column">
          <!-- Achievement Progress -->
          <div class="chart-card">
            <h3 class="chart-title">成就进度</h3>
            <div class="achievement-ring">
              <svg class="progress-ring" viewBox="0 0 128 128">
                <circle class="ring-bg" cx="64" cy="64" r="56" stroke-width="8" fill="none" />
                <circle 
                  class="ring-progress" 
                  cx="64" cy="64" r="56" 
                  stroke-width="8" 
                  fill="none"
                  :stroke-dasharray="351.86"
                  :stroke-dashoffset="351.86 * (1 - achievements.completionRate / 100)"
                />
              </svg>
              <div class="ring-center">
                <span class="ring-value">{{ achievements.completionRate }}%</span>
                <span class="ring-label">完成</span>
              </div>
            </div>
            <div class="achievement-stats">
              <p>已解锁 {{ achievements.unlockedAchievements }} / {{ achievements.totalAchievements }}</p>
              <p class="perfect-games">🏆 完美游戏 {{ achievements.perfectGames }} 款</p>
            </div>
          </div>

          <!-- Genre Stats -->
          <div class="chart-card">
            <h3 class="chart-title">🎮 游戏类型统计</h3>
            <div v-if="genreStats.length" class="genre-stats-list">
              <div v-for="(genre, index) in genreStats.slice(0, 6)" :key="genre.genre" class="genre-stat-item">
                <div class="genre-stat-header">
                  <span class="genre-stat-color" :style="{ background: chartColors[index] }"></span>
                  <span class="genre-stat-name">{{ genre.genre }}</span>
                  <span class="genre-stat-count">{{ genre.gameCount }} 款</span>
                </div>
                <div class="genre-stat-bar-bg">
                  <div class="genre-stat-bar-fill" :style="{ width: genre.percentage + '%', background: chartColors[index] }"></div>
                </div>
                <div class="genre-stat-details">
                  <span>{{ formatMinutes(genre.playtimeMinutes) }}</span>
                  <span>{{ genre.percentage }}%</span>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无类型数据</div>
          </div>

          <!-- Recent Played -->
          <div class="chart-card">
            <h3 class="chart-title">最近游玩</h3>
            <div v-if="recentPlayed.length" class="recent-list">
              <div v-for="game in recentPlayed.slice(0, 5)" :key="game.gameId" class="recent-item">
                <img :src="game.headerImage || noCoverImage" class="recent-image" @error="handleImageError" />
                <div class="recent-info">
                  <h4 class="recent-name">{{ game.gameName }}</h4>
                  <p class="recent-time">{{ formatMinutes(game.recentPlaytimeMinutes) }} (2周内)</p>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无最近游玩记录</div>
          </div>

          <!-- Game Achievement Progress -->
          <div class="chart-card">
            <h3 class="chart-title">各游戏成就进度</h3>
            <div v-if="achievements.gameProgress?.length" class="progress-list">
              <div v-for="game in achievements.gameProgress.slice(0, 5)" :key="game.gameId" class="progress-item">
                <div class="progress-header">
                  <span class="progress-name">{{ game.gameName }}</span>
                  <span class="progress-percent">{{ game.completionRate }}%</span>
                </div>
                <div class="progress-bar-bg">
                  <div class="progress-bar-fill" :style="{ width: game.completionRate + '%' }"></div>
                </div>
                <p class="progress-detail">{{ game.unlockedAchievements }}/{{ game.totalAchievements }}</p>
              </div>
            </div>
            <div v-else class="empty-state">暂无成就数据</div>
          </div>
        </div>
      </div>

      <!-- Wishlist Section -->
      <div class="wishlist-section">
        <div class="section-header">
          <h2 class="section-title">愿望单</h2>
          <span class="sale-badge" v-if="wishlist.onSaleCount > 0">
            🔥 {{ wishlist.onSaleCount }} 款在打折
          </span>
        </div>
        <div v-if="wishlist.items?.length" class="wishlist-grid">
          <div v-for="item in wishlist.items.slice(0, 8)" :key="item.steamAppId" class="wishlist-item" :class="{ 'on-sale': item.isOnSale }">
            <img :src="item.headerImage || noCoverImage" class="wishlist-image" @error="handleImageError" />
            <div class="wishlist-info">
              <h4 class="wishlist-name">{{ item.gameName }}</h4>
              <div class="wishlist-price" v-if="item.currentPrice">
                <span v-if="item.isOnSale" class="discount-badge">-{{ item.discountPercent }}%</span>
                <span class="price">¥{{ (item.currentPrice / 100).toFixed(2) }}</span>
              </div>
              <p class="wishlist-added" v-if="item.addedTime">添加于 {{ item.addedTime }}</p>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">愿望单为空</div>
      </div>

      <!-- Reports Section -->
      <div class="reports-section">
        <div class="section-header">
          <h2 class="section-title">📊 报告生成</h2>
        </div>
        <div class="reports-grid">
          <!-- Monthly Report -->
          <div class="report-card">
            <div class="report-icon monthly">
              <Calendar class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">月度游戏报告</h3>
              <p class="report-desc">游戏时长、成就、消费等统计</p>
              <div class="report-options">
                <div class="date-selector">
                  <select v-model="monthlyYear" class="date-select">
                    <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</option>
                  </select>
                  <select v-model="monthlyMonth" class="date-select">
                    <option v-for="m in 12" :key="m" :value="m">{{ m }}月</option>
                  </select>
                </div>
              </div>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateMonthlyReport('pdf')" :disabled="generating.monthly">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report csv" @click="generateMonthlyReport('csv')" :disabled="generating.monthly">
                <FileSpreadsheet class="btn-icon" /> CSV
              </button>
              <button class="btn-report html" @click="generateMonthlyReport('html')" :disabled="generating.monthly">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>

          <!-- Yearly Report -->
          <div class="report-card">
            <div class="report-icon yearly">
              <Award class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">年度总结报告</h3>
              <p class="report-desc">年度游戏数据全面分析</p>
              <div class="report-options">
                <div class="date-selector">
                  <select v-model="yearlyYear" class="date-select">
                    <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</option>
                  </select>
                </div>
              </div>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateYearlyReport('pdf')" :disabled="generating.yearly">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report html" @click="generateYearlyReport('html')" :disabled="generating.yearly">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>

          <!-- Inventory Report -->
          <div class="report-card">
            <div class="report-icon inventory">
              <Package class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">游戏库存报告</h3>
              <p class="report-desc">游戏收藏、安装、存档统计</p>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateInventoryReport('pdf')" :disabled="generating.inventory">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report csv" @click="generateInventoryReport('csv')" :disabled="generating.inventory">
                <FileSpreadsheet class="btn-icon" /> CSV
              </button>
              <button class="btn-report html" @click="generateInventoryReport('html')" :disabled="generating.inventory">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Recent Reports Section -->
      <div class="recent-reports-section">
        <div class="section-header">
          <h2 class="section-title">📋 最近报表</h2>
          <button class="btn-refresh-reports" @click="loadReportHistory">
            <RefreshCw class="icon" :class="{ spinning: loadingHistory }" />
            刷新
          </button>
        </div>
        <div v-if="recentReports.length" class="reports-list">
          <div v-for="report in recentReports" :key="report.id" class="report-history-item">
            <div class="report-history-icon" :class="getReportTypeClass(report.type)">
              <component :is="getReportTypeIcon(report.type)" class="icon" />
            </div>
            <div class="report-history-info">
              <h4 class="report-history-name">{{ report.name }}</h4>
              <div class="report-history-meta">
                <span>生成于 {{ formatReportDate(report.generatedAt) }}</span>
                <span>•</span>
                <span>{{ report.fileSize }}</span>
                <span>•</span>
                <span class="report-status" :class="report.status">{{ getStatusText(report.status) }}</span>
              </div>
            </div>
            <div class="report-history-actions">
              <button v-if="report.status === 'completed'" class="btn-action download" @click="redownloadReport(report)">
                <Download class="icon" />
                下载
              </button>
              <button class="btn-action delete" @click="deleteReportHistory(report.id)">
                <Trash2 class="icon" />
                删除
              </button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">暂无报表记录</div>
      </div>
    </template>

    <!-- Export Dialog -->
    <div v-if="showExportDialog" class="dialog-overlay" @click.self="showExportDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">导出报表</h3>
        <p class="dialog-desc">选择报表类型和格式</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>报表类型</label>
            <select v-model="exportForm.type" class="form-select">
              <option value="monthly">月度游戏报告</option>
              <option value="yearly">年度总结报告</option>
              <option value="inventory">游戏库存报告</option>
            </select>
          </div>
          
          <div class="form-group" v-if="exportForm.type === 'monthly'">
            <label>选择月份</label>
            <div class="date-row">
              <select v-model="exportForm.year" class="form-select">
                <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</option>
              </select>
              <select v-model="exportForm.month" class="form-select">
                <option v-for="m in 12" :key="m" :value="m">{{ m }}月</option>
              </select>
            </div>
          </div>
          
          <div class="form-group" v-if="exportForm.type === 'yearly'">
            <label>选择年份</label>
            <select v-model="exportForm.year" class="form-select">
              <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</option>
            </select>
          </div>
          
          <div class="form-group">
            <label>输出格式</label>
            <div class="format-options">
              <label class="format-option" v-for="fmt in availableFormats" :key="fmt">
                <input type="radio" v-model="exportForm.format" :value="fmt" />
                <span class="format-label">{{ fmt.toUpperCase() }}</span>
              </label>
            </div>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showExportDialog = false">取消</button>
          <button class="btn-confirm" @click="handleExport" :disabled="exporting">
            {{ exporting ? '导出中...' : '导出报表' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>


<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { 
  RefreshCw, AlertCircle, User, Gamepad2, Clock, Trophy, Heart,
  Calendar, Award, Package, FileText, FileSpreadsheet, Globe, Download, Trash2
} from 'lucide-vue-next'
import Chart from 'chart.js/auto'
import noCoverImage from '@/assets/no_cover.png'
import { 
  getUserReportOverview, 
  syncFromSteam,
  getMonthlyReportUrl,
  getYearlyReportUrl,
  getInventoryReportUrl,
  downloadReport,
  openHtmlReport
} from '@/api/userReport'

// Cache key
const CACHE_KEY = 'user_report_cache'
const CACHE_EXPIRY = 30 * 60 * 1000 // 30 minutes

// Refs
const genreChartRef = ref(null)
let genreChart = null

const loading = ref(true)
const refreshing = ref(false)
const error = ref(null)
const syncing = ref(false)
const hasCachedData = ref(false)
const lastUpdateTime = ref('')

// Time period selector
const selectedPeriod = ref('all')

// Report generation state
const generating = ref({
  monthly: false,
  yearly: false,
  inventory: false
})

// Export dialog
const showExportDialog = ref(false)
const exporting = ref(false)
const exportForm = ref({
  type: 'monthly',
  year: new Date().getFullYear(),
  month: new Date().getMonth() + 1,
  format: 'pdf'
})

// Report date selectors
const currentYear = new Date().getFullYear()
const currentMonth = new Date().getMonth() + 1
const monthlyYear = ref(currentYear)
const monthlyMonth = ref(currentMonth)
const yearlyYear = ref(currentYear)

// Year options (last 5 years)
const yearOptions = computed(() => {
  const years = []
  for (let i = currentYear; i >= currentYear - 4; i--) {
    years.push(i)
  }
  return years
})

// Available formats based on report type
const availableFormats = computed(() => {
  if (exportForm.value.type === 'yearly') {
    return ['pdf', 'html']
  }
  return ['pdf', 'csv', 'html']
})

// Data
const profile = ref({})
const gameLibrary = ref({
  totalGames: 0,
  totalPlaytimeMinutes: 0,
  totalPlaytimeFormatted: '0小时',
  playedGames: 0,
  neverPlayedGames: 0,
  recentPlaytimeMinutes: 0,
  playtimeByGenre: [],
  topPlayedGames: []
})
const achievements = ref({
  totalAchievements: 0,
  unlockedAchievements: 0,
  completionRate: 0,
  perfectGames: 0,
  recentUnlocks: [],
  gameProgress: []
})
const recentPlayed = ref([])
const wishlist = ref({
  totalItems: 0,
  onSaleCount: 0,
  items: []
})
const genreStats = ref([])

// Recent reports history (stored in localStorage)
const REPORTS_HISTORY_KEY = 'user_report_history'
const recentReports = ref([])
const loadingHistory = ref(false)

// Chart colors
const chartColors = [
  '#6366f1', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', 
  '#ec4899', '#14b8a6', '#f97316', '#06b6d4', '#84cc16'
]

// Cache functions
const saveToCache = (data) => {
  try {
    const cacheData = {
      data,
      timestamp: Date.now()
    }
    localStorage.setItem(CACHE_KEY, JSON.stringify(cacheData))
  } catch (e) {
    console.warn('Failed to save cache:', e)
  }
}

const loadFromCache = () => {
  try {
    const cached = localStorage.getItem(CACHE_KEY)
    if (cached) {
      const cacheData = JSON.parse(cached)
      // Check if cache is still valid (30 minutes)
      if (Date.now() - cacheData.timestamp < CACHE_EXPIRY) {
        return cacheData
      }
    }
  } catch (e) {
    console.warn('Failed to load cache:', e)
  }
  return null
}

const formatCacheTime = (timestamp) => {
  const date = new Date(timestamp)
  const now = new Date()
  const diff = now - date
  
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  return date.toLocaleString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// 处理图片加载错误
const handleImageError = (e) => {
  e.target.src = noCoverImage
}

const applyData = (data) => {
  profile.value = data.profile || {}
  gameLibrary.value = data.gameLibrary || gameLibrary.value
  achievements.value = data.achievements || achievements.value
  recentPlayed.value = data.recentPlayed || []
  wishlist.value = data.wishlist || wishlist.value
  
  // Extract genre stats from playtimeByGenre
  if (data.gameLibrary?.playtimeByGenre) {
    genreStats.value = data.gameLibrary.playtimeByGenre.map(g => ({
      genre: g.genre,
      playtimeMinutes: g.playtimeMinutes,
      percentage: g.percentage,
      gameCount: g.gameCount || Math.round(g.percentage / 10) || 1 // Estimate if not provided
    }))
  }
}

// Methods
const formatMinutes = (minutes) => {
  if (!minutes) return '0分钟'
  if (minutes < 60) return `${minutes}分钟`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours >= 100) return `${hours}小时`
  return mins > 0 ? `${hours}小时${mins}分钟` : `${hours}小时`
}

const getRankClass = (index) => {
  if (index === 0) return 'gold'
  if (index === 1) return 'silver'
  if (index === 2) return 'bronze'
  return ''
}

const getPlatformIcon = (platformName) => {
  const icons = {
    'Steam': '🎮',
    'Xbox': '🎯',
    'PlayStation': '🎲',
    'PSN': '🎲',
    'GOG': '🌟',
    'Epic': '⚡',
    'Nintendo': '🍄',
    'Origin': '🔶',
    'Ubisoft': '🔷',
    'Battle.net': '💠'
  }
  return icons[platformName] || '🎮'
}

const loadData = async (forceRefresh = false) => {
  error.value = null

  // Try to load from cache first
  if (!forceRefresh) {
    const cachedData = loadFromCache()
    if (cachedData) {
      hasCachedData.value = true
      applyData(cachedData.data)
      lastUpdateTime.value = formatCacheTime(cachedData.timestamp)
      loading.value = false
      setTimeout(() => initCharts(), 100)
      
      // Refresh in background
      refreshing.value = true
      try {
        const res = await getUserReportOverview()
        if (res.data) {
          applyData(res.data)
          saveToCache({ data: res.data, timestamp: Date.now() })
          lastUpdateTime.value = '刚刚'
          setTimeout(() => initCharts(), 100)
        }
      } catch (err) {
        console.warn('Background refresh failed:', err)
      } finally {
        refreshing.value = false
      }
      return
    }
  }

  // No cache, load fresh
  loading.value = true
  try {
    const res = await getUserReportOverview()
    if (res.data) {
      applyData(res.data)
      saveToCache({ data: res.data, timestamp: Date.now() })
      lastUpdateTime.value = '刚刚'
      hasCachedData.value = true
    }
    setTimeout(() => initCharts(), 100)
  } catch (err) {
    console.error('加载数据失败:', err)
    error.value = '加载数据失败，请确保已绑定Steam账号'
  } finally {
    loading.value = false
  }
}

const handleSync = async () => {
  syncing.value = true
  try {
    const res = await syncFromSteam()
    if (res.data?.success) {
      alert(`同步成功！游戏: ${res.data.gamesSync}, 成就: ${res.data.achievementsSync}`)
      // Clear cache and reload
      localStorage.removeItem(CACHE_KEY)
      loadData(true)
    } else {
      alert(res.data?.message || '同步失败')
    }
  } catch (err) {
    console.error('同步失败:', err)
    alert('同步失败: ' + (err.message || '未知错误'))
  } finally {
    syncing.value = false
  }
}

const initCharts = () => {
  if (genreChartRef.value && gameLibrary.value.playtimeByGenre?.length > 0) {
    const ctx = genreChartRef.value.getContext('2d')
    
    if (genreChart) genreChart.destroy()

    const data = gameLibrary.value.playtimeByGenre.slice(0, 8)
    
    genreChart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: data.map(g => g.genre),
        datasets: [{
          data: data.map(g => g.playtimeMinutes),
          backgroundColor: chartColors.slice(0, data.length),
          borderWidth: 0
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '60%',
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (context) => {
                const minutes = context.raw
                const hours = Math.floor(minutes / 60)
                return `${context.label}: ${hours}小时`
              }
            }
          }
        }
      }
    })
  }
}

// Report generation methods
const generateMonthlyReport = async (format) => {
  generating.value.monthly = true
  try {
    const url = getMonthlyReportUrl(format, monthlyYear.value, monthlyMonth.value)
    const filename = `monthly_report_${monthlyYear.value}_${String(monthlyMonth.value).padStart(2, '0')}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = `${monthlyYear.value}年${monthlyMonth.value}月 月度报告`
    saveReportToHistory('monthly', reportName, format, format === 'pdf' ? '~2.5 MB' : '~0.5 MB')
  } catch (err) {
    console.error('生成月度报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.monthly = false
  }
}

const generateYearlyReport = async (format) => {
  generating.value.yearly = true
  try {
    const url = getYearlyReportUrl(format, yearlyYear.value)
    const filename = `yearly_report_${yearlyYear.value}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = `${yearlyYear.value}年 年度总结`
    saveReportToHistory('yearly', reportName, format, format === 'pdf' ? '~1.8 MB' : '~0.3 MB')
  } catch (err) {
    console.error('生成年度报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.yearly = false
  }
}

const generateInventoryReport = async (format) => {
  generating.value.inventory = true
  try {
    const url = getInventoryReportUrl(format)
    const filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = '游戏库存报告'
    saveReportToHistory('inventory', reportName, format, format === 'pdf' ? '~1.2 MB' : '~0.4 MB')
  } catch (err) {
    console.error('生成库存报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.inventory = false
  }
}

// Report history functions
const loadReportHistory = () => {
  loadingHistory.value = true
  try {
    const saved = localStorage.getItem(REPORTS_HISTORY_KEY)
    if (saved) {
      recentReports.value = JSON.parse(saved)
    }
  } catch (e) {
    console.warn('Failed to load report history:', e)
  } finally {
    loadingHistory.value = false
  }
}

const saveReportToHistory = (type, name, format, fileSize) => {
  const report = {
    id: Date.now(),
    type,
    name,
    format,
    fileSize,
    generatedAt: new Date().toISOString(),
    status: 'completed',
    url: null // We don't store the actual URL for security
  }
  
  recentReports.value.unshift(report)
  // Keep only last 10 reports
  if (recentReports.value.length > 10) {
    recentReports.value = recentReports.value.slice(0, 10)
  }
  
  try {
    localStorage.setItem(REPORTS_HISTORY_KEY, JSON.stringify(recentReports.value))
  } catch (e) {
    console.warn('Failed to save report history:', e)
  }
}

const deleteReportHistory = (id) => {
  if (!confirm('确定要删除这条记录吗？')) return
  
  recentReports.value = recentReports.value.filter(r => r.id !== id)
  try {
    localStorage.setItem(REPORTS_HISTORY_KEY, JSON.stringify(recentReports.value))
  } catch (e) {
    console.warn('Failed to save report history:', e)
  }
}

const redownloadReport = async (report) => {
  // Re-generate the report based on stored info
  try {
    let url, filename
    const format = report.format
    
    // Parse the report name to get parameters
    if (report.type === 'monthly') {
      const match = report.name.match(/(\d{4})年(\d{1,2})月/)
      if (match) {
        const year = parseInt(match[1])
        const month = parseInt(match[2])
        url = getMonthlyReportUrl(format, year, month)
        filename = `monthly_report_${year}_${String(month).padStart(2, '0')}.${format}`
      }
    } else if (report.type === 'yearly') {
      const match = report.name.match(/(\d{4})年/)
      if (match) {
        const year = parseInt(match[1])
        url = getYearlyReportUrl(format, year)
        filename = `yearly_report_${year}.${format}`
      }
    } else if (report.type === 'inventory') {
      url = getInventoryReportUrl(format)
      filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
    }
    
    if (url) {
      if (format === 'html') {
        await openHtmlReport(url)
      } else {
        await downloadReport(url, filename)
      }
    }
  } catch (err) {
    console.error('重新下载失败:', err)
    alert('下载失败: ' + (err.message || '未知错误'))
  }
}

const formatReportDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

const getStatusText = (status) => {
  const map = {
    completed: '已完成',
    generating: '生成中',
    failed: '失败'
  }
  return map[status] || status
}

const getReportTypeClass = (type) => {
  const map = {
    monthly: 'monthly',
    yearly: 'yearly',
    inventory: 'inventory'
  }
  return map[type] || 'monthly'
}

const getReportTypeIcon = (type) => {
  const map = {
    monthly: Calendar,
    yearly: Award,
    inventory: Package
  }
  return map[type] || FileText
}

// Period change handler (for future backend implementation)
const handlePeriodChange = () => {
  // TODO: 当后端实现差值计算后，这里会根据选择的时间范围重新加载数据
  console.log('Selected period:', selectedPeriod.value)
  // 目前只是UI展示，后端还没有实现按时间范围筛选
  // loadData() // 未来启用
}

// Export dialog handler
const handleExport = async () => {
  exporting.value = true
  try {
    let url, filename
    const format = exportForm.value.format
    
    switch (exportForm.value.type) {
      case 'monthly':
        url = getMonthlyReportUrl(format, exportForm.value.year, exportForm.value.month)
        filename = `monthly_report_${exportForm.value.year}_${String(exportForm.value.month).padStart(2, '0')}.${format}`
        break
      case 'yearly':
        url = getYearlyReportUrl(format, exportForm.value.year)
        filename = `yearly_report_${exportForm.value.year}.${format}`
        break
      case 'inventory':
        url = getInventoryReportUrl(format)
        filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
        break
    }
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    showExportDialog.value = false
  } catch (err) {
    console.error('导出报表失败:', err)
    alert('导出失败: ' + (err.message || '未知错误'))
  } finally {
    exporting.value = false
  }
}

onMounted(() => {
  loadData()
  loadReportHistory()
})

onUnmounted(() => {
  if (genreChart) genreChart.destroy()
})
</script>

<style scoped>
.user-report-container {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.report-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.page-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-top: 4px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.refreshing-hint {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--primary-color);
  font-size: 12px;
}

.refreshing-hint .icon {
  width: 14px;
  height: 14px;
}

.last-update {
  font-size: 12px;
  color: var(--text-secondary);
  opacity: 0.7;
}

.btn-sync {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-sync:hover:not(:disabled) {
  background: var(--primary-hover);
}

.btn-sync:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-sync .icon {
  width: 16px;
  height: 16px;
}

.btn-sync .icon.spinning {
  animation: spin 1s linear infinite;
}

.period-select {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 10px 14px;
  color: white;
  font-size: 14px;
  cursor: pointer;
  outline: none;
}

.period-select:focus {
  border-color: var(--primary-color);
}

.period-select option {
  background: #1f1f23;
  color: white;
}

.btn-export {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
  border: 1px solid rgba(16, 185, 129, 0.3);
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-export:hover {
  background: rgba(16, 185, 129, 0.3);
  border-color: rgba(16, 185, 129, 0.5);
}

.btn-export .icon {
  width: 16px;
  height: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Loading & Error */
.loading-container, .error-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 20px;
  color: var(--text-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid rgba(255, 255, 255, 0.1);
  border-top-color: var(--primary-color);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

.error-icon {
  width: 48px;
  height: 48px;
  color: #f87171;
  margin-bottom: 16px;
}

.btn-retry {
  margin-top: 16px;
  padding: 8px 24px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
}

.empty-state {
  padding: 40px 20px;
  text-align: center;
  color: var(--text-secondary);
}

/* Profile Section */
.profile-section {
  margin-bottom: 24px;
}

.profile-card {
  display: flex;
  align-items: center;
  gap: 24px;
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(139, 92, 246, 0.1));
  border: 1px solid rgba(99, 102, 241, 0.3);
  border-radius: 16px;
  padding: 24px;
}

.profile-avatar {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
}

.profile-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-placeholder {
  width: 40px;
  height: 40px;
  color: var(--text-secondary);
}

.profile-info {
  flex: 1;
}

.profile-name {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.profile-id {
  font-size: 14px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.profile-stats {
  display: flex;
  gap: 32px;
}

.profile-stat {
  text-align: center;
}

.profile-stat .stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-primary);
}

.profile-stat .stat-label {
  font-size: 12px;
  color: var(--text-secondary);
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

/* Platform Stats Section */
.platform-stats-section {
  margin-bottom: 24px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.platform-stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.platform-stat-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 16px;
}

.platform-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.platform-icon {
  font-size: 20px;
}

.platform-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.platform-stats {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.platform-stat {
  text-align: center;
}

.platform-stat-value {
  display: block;
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
}

.platform-stat-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.platform-bar {
  height: 4px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
  overflow: hidden;
}

.platform-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #818cf8, #6366f1);
  border-radius: 2px;
  transition: width 0.3s ease;
}

.stat-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
}

.stat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.stat-label {
  font-size: 14px;
  color: var(--text-secondary);
}

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-icon.indigo { background: rgba(99, 102, 241, 0.2); color: #818cf8; }
.stat-icon.emerald { background: rgba(16, 185, 129, 0.2); color: #34d399; }
.stat-icon.amber { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
.stat-icon.rose { background: rgba(244, 63, 94, 0.2); color: #fb7185; }

.stat-icon .icon {
  width: 20px;
  height: 20px;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.stat-desc {
  font-size: 12px;
  color: var(--text-secondary);
}

/* Content Grid */
.content-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 24px;
  margin-bottom: 32px;
}

.main-column, .side-column {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* Chart Card */
.chart-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
}

.chart-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.chart-container {
  height: 250px;
  margin-bottom: 16px;
}

/* Genre Legend */
.genre-legend {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.legend-color {
  width: 12px;
  height: 12px;
  border-radius: 3px;
}

.legend-name {
  flex: 1;
  font-size: 12px;
  color: var(--text-secondary);
}

.legend-value {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

/* Games List */
.games-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.game-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-radius: 12px;
  transition: background 0.2s;
}

.game-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.game-rank {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.game-rank.gold { background: linear-gradient(135deg, #fbbf24, #f59e0b); color: white; }
.game-rank.silver { background: linear-gradient(135deg, #9ca3af, #6b7280); color: white; }
.game-rank.bronze { background: linear-gradient(135deg, #d97706, #b45309); color: white; }

.game-image {
  width: 80px;
  height: 38px;
  border-radius: 4px;
  object-fit: cover;
}

.game-info {
  flex: 1;
}

.game-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.game-meta {
  font-size: 12px;
  color: var(--text-secondary);
}

.game-playtime {
  text-align: right;
}

.playtime-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--primary-color);
}

/* Achievement Ring */
.achievement-ring {
  position: relative;
  width: 128px;
  height: 128px;
  margin: 0 auto 16px;
}

.progress-ring {
  transform: rotate(-90deg);
  width: 100%;
  height: 100%;
}

.ring-bg { stroke: #27272a; }
.ring-progress {
  stroke: #6366f1;
  stroke-linecap: round;
  transition: stroke-dashoffset 0.5s ease;
}

.ring-center {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  text-align: center;
}

.ring-value {
  display: block;
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
}

.ring-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.achievement-stats {
  text-align: center;
  font-size: 14px;
  color: var(--text-secondary);
}

.perfect-games {
  color: #fbbf24;
  margin-top: 8px;
}

/* Recent List */
.recent-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.recent-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.recent-image {
  width: 60px;
  height: 28px;
  border-radius: 4px;
  object-fit: cover;
}

.recent-info {
  flex: 1;
}

.recent-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
}

.recent-time {
  font-size: 11px;
  color: var(--text-secondary);
}

/* Progress List */
.progress-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.progress-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.progress-header {
  display: flex;
  justify-content: space-between;
}

.progress-name {
  font-size: 13px;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 150px;
}

.progress-percent {
  font-size: 13px;
  font-weight: 600;
  color: var(--primary-color);
}

.progress-bar-bg {
  height: 6px;
  background: #27272a;
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.progress-detail {
  font-size: 11px;
  color: var(--text-secondary);
}

/* Wishlist Section */
.wishlist-section {
  margin-top: 32px;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
}

.sale-badge {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 500;
}

.wishlist-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.wishlist-item {
  background: rgba(24, 24, 27, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  overflow: hidden;
  transition: all 0.2s;
}

.wishlist-item:hover {
  border-color: rgba(99, 102, 241, 0.5);
}

.wishlist-item.on-sale {
  border-color: rgba(239, 68, 68, 0.5);
}

.wishlist-image {
  width: 100%;
  height: 90px;
  object-fit: cover;
}

.wishlist-info {
  padding: 12px;
}

.wishlist-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: 8px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.wishlist-price {
  display: flex;
  align-items: center;
  gap: 8px;
}

.discount-badge {
  background: #ef4444;
  color: white;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.price {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.wishlist-added {
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 4px;
}

/* Genre Stats */
.genre-stats-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.genre-stat-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.genre-stat-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.genre-stat-color {
  width: 10px;
  height: 10px;
  border-radius: 3px;
}

.genre-stat-name {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
}

.genre-stat-count {
  font-size: 12px;
  color: var(--text-secondary);
}

.genre-stat-bar-bg {
  height: 6px;
  background: #27272a;
  border-radius: 3px;
  overflow: hidden;
}

.genre-stat-bar-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.genre-stat-details {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-secondary);
}

/* Recent Reports Section */
.recent-reports-section {
  margin-top: 32px;
}

.recent-reports-section .section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.btn-refresh-reports {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-refresh-reports:hover {
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-primary);
}

.btn-refresh-reports .icon {
  width: 14px;
  height: 14px;
}

.reports-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.report-history-item {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(24, 24, 27, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 16px;
  transition: all 0.2s;
}

.report-history-item:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.report-history-icon {
  width: 44px;
  height: 44px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.report-history-icon .icon {
  width: 22px;
  height: 22px;
}

.report-history-icon.monthly {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.report-history-icon.yearly {
  background: rgba(245, 158, 11, 0.2);
  color: #fcd34d;
}

.report-history-icon.inventory {
  background: rgba(16, 185, 129, 0.2);
  color: #6ee7b7;
}

.report-history-info {
  flex: 1;
  min-width: 0;
}

.report-history-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.report-history-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}

.report-status {
  font-weight: 500;
}

.report-status.completed {
  color: #34d399;
}

.report-status.generating {
  color: #60a5fa;
}

.report-status.failed {
  color: #f87171;
}

.report-history-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.btn-action {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-action .icon {
  width: 14px;
  height: 14px;
}

.btn-action.download {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
}

.btn-action.download:hover {
  background: rgba(99, 102, 241, 0.3);
}

.btn-action.delete {
  background: rgba(239, 68, 68, 0.15);
  color: #f87171;
}

.btn-action.delete:hover {
  background: rgba(239, 68, 68, 0.25);
}

/* Responsive */
@media (max-width: 1200px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  
  .content-grid {
    grid-template-columns: 1fr;
  }
  
  .wishlist-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .profile-card {
    flex-direction: column;
    text-align: center;
  }
  
  .stats-grid {
    grid-template-columns: 1fr;
  }
  
  .wishlist-grid {
    grid-template-columns: 1fr;
  }
}

/* Reports Section */
.reports-section {
  margin-top: 32px;
}

.reports-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
}

.report-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: all 0.2s;
}

.report-card:hover {
  border-color: rgba(99, 102, 241, 0.3);
}

.report-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.report-icon .icon {
  width: 28px;
  height: 28px;
}

.report-icon.monthly {
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.3), rgba(139, 92, 246, 0.2));
  color: #a5b4fc;
}

.report-icon.yearly {
  background: linear-gradient(135deg, rgba(245, 158, 11, 0.3), rgba(234, 88, 12, 0.2));
  color: #fcd34d;
}

.report-icon.inventory {
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.3), rgba(5, 150, 105, 0.2));
  color: #6ee7b7;
}

.report-info {
  flex: 1;
}

.report-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 6px;
}

.report-desc {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 12px;
}

.report-options {
  margin-top: 8px;
}

.date-selector {
  display: flex;
  gap: 8px;
}

.date-select {
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 13px;
  cursor: pointer;
  outline: none;
}

.date-select:focus {
  border-color: var(--primary-color);
}

.date-select option {
  background: #1f1f23;
  color: var(--text-primary);
}

.report-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.btn-report {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-report:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-report .btn-icon {
  width: 16px;
  height: 16px;
}

.btn-report.pdf {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
}

.btn-report.pdf:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.3);
}

.btn-report.csv {
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
}

.btn-report.csv:hover:not(:disabled) {
  background: rgba(16, 185, 129, 0.3);
}

.btn-report.html {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
}

.btn-report.html:hover:not(:disabled) {
  background: rgba(99, 102, 241, 0.3);
}

@media (max-width: 1200px) {
  .reports-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .reports-grid {
    grid-template-columns: 1fr;
  }
}

/* Dialog Styles */
.dialog-overlay {
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

.dialog-content {
  background: #1f1f23;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 24px;
  width: 100%;
  max-width: 420px;
}

.dialog-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.dialog-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 24px;
}

.dialog-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-bottom: 24px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.form-select {
  padding: 10px 14px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 14px;
  cursor: pointer;
  outline: none;
}

.form-select:focus {
  border-color: var(--primary-color);
}

.form-select option {
  background: #1f1f23;
  color: var(--text-primary);
}

.date-row {
  display: flex;
  gap: 12px;
}

.date-row .form-select {
  flex: 1;
}

.format-options {
  display: flex;
  gap: 16px;
}

.format-option {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.format-option input[type="radio"] {
  width: 18px;
  height: 18px;
  accent-color: var(--primary-color);
}

.format-label {
  font-size: 14px;
  color: var(--text-primary);
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.btn-cancel {
  padding: 10px 20px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-cancel:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-confirm {
  padding: 10px 20px;
  background: var(--primary-color);
  border: none;
  border-radius: 8px;
  color: white;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-confirm:hover:not(:disabled) {
  background: var(--primary-hover);
}

.btn-confirm:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
