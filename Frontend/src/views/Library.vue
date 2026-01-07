<template>
  <div class="app-container">
    <header class="header">
      <h1>我的游戏库</h1>
      <p>查看和管理你的游戏收藏</p>
    </header>

    <!-- 游戏库概览 -->
    <div class="overview-section">
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon bg-blue">
            <Gamepad2 class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ overview.totalGamesOwned || 0 }}</div>
            <div class="stat-label">拥有游戏</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon bg-purple">
            <Clock class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ formatPlaytime(overview.totalPlaytimeMinutes || 0) }}</div>
            <div class="stat-label">总游戏时长</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon bg-amber">
            <Trophy class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ overview.unlockedAchievements || 0 }} / {{ overview.totalAchievements || 0 }}</div>
            <div class="stat-label">成就解锁</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 筛选和搜索 -->
    <div class="filters-section">
      <div class="search-box">
        <Search class="search-icon" size="20" />
        <input 
          v-model="searchQuery" 
          type="text" 
          placeholder="搜索游戏..." 
          class="search-input"
        />
      </div>
      <div class="filter-group">
        <select v-model="selectedPlatform" class="filter-select">
          <option value="">所有平台</option>
          <option v-for="platform in platforms" :key="platform.id" :value="platform.id">
            {{ platform.name }}
          </option>
        </select>
        <select v-model="sortBy" class="filter-select">
          <option value="name">按名称</option>
          <option value="playtime">按游戏时长</option>
          <option value="lastPlayed">按最近游玩</option>
        </select>
      </div>
    </div>

    <!-- 游戏列表 -->
    <div class="games-section">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="filteredGames.length === 0" class="empty-state">
        <Gamepad2 class="empty-icon" size="64" />
        <p>暂无游戏</p>
        <p class="empty-hint">请先绑定游戏平台账号并同步游戏库</p>
      </div>
      <div v-else class="games-grid">
        <div 
          v-for="game in filteredGames" 
          :key="game.gameId" 
          class="game-card"
          @click="viewGameDetails(game.gameId)"
        >
          <div class="game-image">
            <img 
              :src="game.headerImage || noCoverImage" 
              :alt="game.name"
              @error="handleImageError"
            />
            <!-- 右上角平台信息 -->
            <div class="platform-badges-top">
              <span 
                v-for="platform in game.allPlatforms" 
                :key="platform.platformId"
                class="platform-badge-top"
                :class="{ 'owned': platform.isOwned, 'not-owned': !platform.isOwned }"
              >
                {{ platform.platformName }}
              </span>
            </div>
            <!-- 图片上的文字叠加 -->
            <div class="game-overlay">
              <h3 class="game-name-overlay">{{ game.name }}</h3>
              <div class="game-meta-overlay">
                <span v-if="game.playtimeMinutes > 0" class="playtime-overlay">
                  {{ formatPlaytime(game.playtimeMinutes) }}
                </span>
              </div>
            </div>
          </div>
          <div class="game-info">
            <!-- 有成就（总数 > 0）才展示成就区域；即便解锁为 0 也展示 -->
            <div v-if="game.achievementsTotal > 0" class="achievement-progress">
              <div class="progress-bar">
                <div 
                  class="progress-fill" 
                  :style="{ width: `${game.achievementProgress}%` }"
                ></div>
              </div>
              <span class="progress-text">
                已解锁 {{ game.achievementsUnlocked }} / {{ game.achievementsTotal }} 个成就
                <template v-if="game.achievementProgress > 0">
                  （{{ game.achievementProgress.toFixed(2) }}% 完成）
                </template>
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- 分页 -->
      <Pagination
        :current-page="currentPage"
        :total-pages="totalPages"
        @page-change="changePage"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onActivated, computed, watch } from 'vue'
import { libraryApi, achievementApi } from '../api'
import { Gamepad2, Play, Clock, Trophy, Search } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import noCoverImage from '@/assets/no_cover.png'
import Pagination from '@/components/common/Pagination.vue'

const router = useRouter()

const overview = ref({
  totalGamesOwned: 0,
  gamesPlayed: 0,
  totalPlaytimeMinutes: 0,
  totalAchievements: 0,
  unlockedAchievements: 0,
  recentlyPlayedCount: 0,
  recentPlaytimeMinutes: 0,
  platformStats: [],
  genreDistribution: []
})

const games = ref([])
const loading = ref(false)
const searchQuery = ref('')
const selectedPlatform = ref('')
const sortBy = ref('playtime')
const currentPage = ref(1)
const pageSize = ref(20)
const totalPages = ref(1)

const platforms = ref([
  { id: 1, name: 'Steam' },
  { id: 2, name: 'Epic Games' },
  { id: 5, name: 'GOG' },
  { id: 6, name: 'PSN' },
  { id: 7, name: 'Xbox' }
])

// 搜索防抖定时器
let searchTimeout = null

const loadOverview = async () => {
  try {
    const response = await libraryApi.getOverview()
    if (response.success && response.data) {
      const data = response.data
      overview.value = {
        totalGamesOwned: data.totalGamesOwned ?? data.TotalGamesOwned ?? 0,
        gamesPlayed: data.gamesPlayed ?? data.GamesPlayed ?? 0,
        totalPlaytimeMinutes: data.totalPlaytimeMinutes ?? data.TotalPlaytimeMinutes ?? 0,
        totalAchievements: data.totalAchievements ?? data.TotalAchievements ?? 0,
        unlockedAchievements: data.unlockedAchievements ?? data.UnlockedAchievements ?? 0,
        recentlyPlayedCount: data.recentlyPlayedCount ?? data.RecentlyPlayedCount ?? 0,
        recentPlaytimeMinutes: data.recentPlaytimeMinutes ?? data.RecentPlaytimeMinutes ?? 0,
        platformStats: data.platformStats ?? data.PlatformStats ?? [],
        genreDistribution: data.genreDistribution ?? data.GenreDistribution ?? []
      }
    }
  } catch (err) {
    console.error('加载游戏库概览失败:', err)
  }
}

const loadGames = async () => {
  loading.value = true
  try {
    const params = {
      page: currentPage.value,
      pageSize: pageSize.value
    }
    if (selectedPlatform.value) {
      params.platform = selectedPlatform.value
    }
    if (sortBy.value) {
      params.sortBy = sortBy.value
    }
    // 添加搜索参数
    if (searchQuery.value && searchQuery.value.trim()) {
      params.search = searchQuery.value.trim()
    }

    console.log('请求游戏列表，参数:', params)
    const response = await libraryApi.getGames(params)
    console.log('游戏列表响应:', response)
    
    if (response.success && response.data) {
      const data = response.data
      console.log('游戏数据:', data)
      const items = data.items ?? data.Items ?? []
      console.log('游戏项数量:', items.length, items)
      
      games.value = items.map(game => {
        const ownedPlatforms = game.ownedPlatforms ?? game.OwnedPlatforms ?? []
        const platformName = ownedPlatforms.map(p => p.platformName ?? p.PlatformName).join(', ') || '未知平台'
        
        // 获取按平台分组的成就数据
        const platformAchievements = game.platformAchievements ?? game.PlatformAchievements ?? []
        
        // 获取用户拥有的平台ID集合
        const ownedPlatformIds = new Set(ownedPlatforms.map(p => p.platformId ?? p.PlatformId))
        
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
        
        // 构建所有平台列表
        const allPlatformsMap = new Map()
        
        // 先从成就数据中获取所有平台（包含平台名称）
        platformAchievements.forEach(pa => {
          const platformId = pa.platformId ?? pa.PlatformId
          const platformName = pa.platformName ?? pa.PlatformName
          if (platformId) {
            allPlatformsMap.set(platformId, {
              platformId,
              platformName: platformName || platformNameMap[platformId] || `平台 ${platformId}`,
              isOwned: ownedPlatformIds.has(platformId)
            })
          }
        })
        
        // 从游戏支持的平台列表中添加（如果成就数据中没有）
        const gamePlatforms = game.platforms ?? game.Platforms ?? []
        gamePlatforms.forEach(platformId => {
          if (!allPlatformsMap.has(platformId)) {
            allPlatformsMap.set(platformId, {
              platformId,
              platformName: platformNameMap[platformId] || `平台 ${platformId}`,
              isOwned: ownedPlatformIds.has(platformId)
            })
          }
        })
        
        // 如果还是没有平台信息，则从拥有的平台中获取
        if (allPlatformsMap.size === 0) {
          ownedPlatforms.forEach(p => {
            const platformId = p.platformId ?? p.PlatformId
            const platformName = p.platformName ?? p.PlatformName
            if (platformId) {
              allPlatformsMap.set(platformId, {
                platformId,
                platformName: platformName || platformNameMap[platformId] || `平台 ${platformId}`,
                isOwned: true
              })
            }
          })
        }
        
        const allPlatforms = Array.from(allPlatformsMap.values())
        
        // 找到解锁率最高的平台
        let bestPlatform = null
        if (platformAchievements.length > 0) {
          bestPlatform = platformAchievements.reduce((best, current) => {
            if (!best) return current
            // 优先选择解锁率更高的平台
            if (current.unlockRate > best.unlockRate) {
              return current
            }
            // 如果解锁率相同，选择成就总数更多的平台
            if (current.unlockRate === best.unlockRate && current.achievementsTotal > best.achievementsTotal) {
              return current
            }
            return best
          }, null)
        }
        
        // 使用最佳平台的成就数据，如果没有则使用总数据
        const unlocked = bestPlatform 
          ? (bestPlatform.achievementsUnlocked ?? bestPlatform.AchievementsUnlocked ?? 0)
          : (game.achievementsUnlocked ?? game.AchievementsUnlocked ?? 0)
        const total = bestPlatform
          ? (bestPlatform.achievementsTotal ?? bestPlatform.AchievementsTotal ?? 0)
          : (game.achievementsTotal ?? game.AchievementsTotal ?? 0)
        const achievementProgress = total > 0 ? (unlocked / total * 100) : 0
        
        return {
          gameId: game.gameId ?? game.GameId,
          name: game.name ?? game.Name,
          headerImage: game.headerImage ?? game.HeaderImage,
          platformName: platformName,
          playtimeMinutes: game.playtimeMinutes ?? game.PlaytimeMinutes ?? 0,
          lastPlayed: game.lastPlayed ?? game.LastPlayed,
          // 成就相关：使用最佳平台的数据
          achievementsUnlocked: unlocked,
          achievementsTotal: total,
          achievementProgress: achievementProgress,
          bestPlatformName: bestPlatform ? (bestPlatform.platformName ?? bestPlatform.PlatformName) : null,
          // 所有平台列表
          allPlatforms: allPlatforms
        }
      })
      
      console.log('处理后的游戏列表:', games.value)
      
      const meta = data.meta ?? data.Meta
      if (meta) {
        totalPages.value = Math.ceil((meta.total ?? meta.Total ?? 0) / pageSize.value) || 1
      }
    } else {
      console.warn('API返回失败或数据为空:', response)
    }
  } catch (err) {
    console.error('加载游戏列表失败:', err)
    alert('加载游戏列表失败: ' + (err.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

// 直接使用后端返回的游戏列表，不再进行前端过滤
// 排序和搜索都在后端完成
const filteredGames = computed(() => {
  return games.value
})

const formatPlaytime = (minutes) => {
  if (!minutes || minutes === 0) return '0小时'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours > 0 && mins > 0) {
    return `${hours}小时${mins}分钟`
  } else if (hours > 0) {
    return `${hours}小时`
  } else {
    return `${mins}分钟`
  }
}

// 处理图片加载错误
const handleImageError = (e) => {
  e.target.src = noCoverImage
}

const changePage = (page) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page
    loadGames()
  }
}

const viewGameDetails = (gameId) => {
  router.push(`/app/game/${gameId}`)
}

// 监听筛选条件变化
watch([selectedPlatform, sortBy], () => {
  currentPage.value = 1
  loadGames()
})

// 监听搜索关键词变化，使用防抖
watch(searchQuery, () => {
  // 清除之前的定时器
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  
  // 重置到第一页
  currentPage.value = 1
  
  // 防抖：500ms 后执行搜索
  searchTimeout = setTimeout(() => {
    loadGames()
  }, 500)
})

onMounted(() => {
  loadOverview()
  loadGames()
})

// 当组件被激活时（从其他页面返回时）刷新数据
onActivated(() => {
  loadOverview()
  loadGames()
})
</script>

<style scoped>
.app-container {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  background: template;
  min-height: 100vh;
  color: #f8fafc;
}

.header {
  margin-bottom: 30px;
}

.header h1 {
  font-size: 32px;
  font-weight: bold;
  margin-bottom: 8px;
  color: #f8fafc;
}

.header p {
  color: #94a3b8;
  font-size: 16px;
}

.overview-section {
  margin-bottom: 30px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 20px;
}

.stat-card {
  background: template;
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 4px 16px template;
  display: flex;
  align-items: center;
  gap: 16px;
  transition: all 0.3s ease;
}

.stat-card:hover {
  border-color: rgba(139, 92, 246, 0.3);
  box-shadow: 0 8px 24px rgba(139, 92, 246, 0.2);
}

.stat-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
}

.stat-icon.bg-blue { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
.stat-icon.bg-green { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); }
.stat-icon.bg-purple { background: linear-gradient(135deg, #a8edea 0%, #fed6e3 100%); }
.stat-icon.bg-amber { background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); }

.stat-info {
  flex: 1;
}

.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #f8fafc;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 14px;
  color: #94a3b8;
}

.filters-section {
  display: flex;
  gap: 16px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

.search-box {
  flex: 1;
  min-width: 200px;
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 12px;
  color: #999;
}

.search-input {
  width: 100%;
  padding: 12px 12px 12px 40px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  font-size: 14px;
  background: rgba(20, 20, 23, 0.75);
  color: #f8fafc;
  backdrop-filter: blur(20px);
}

.search-input:focus {
  outline: none;
  border-color: rgba(139, 92, 246, 0.5);
  box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.1);
}

.search-input::placeholder {
  color: #94a3b8;
}

.filter-group {
  display: flex;
  gap: 12px;
}

.filter-select {
  padding: 12px 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  font-size: 14px;
  background: rgba(20, 20, 23, 0.75);
  color: #f8fafc;
  cursor: pointer;
  backdrop-filter: blur(20px);
}

.filter-select:focus {
  outline: none;
  border-color: rgba(139, 92, 246, 0.5);
}

.games-section {
  margin-top: 24px;
}

.loading, .empty-state {
  text-align: center;
  padding: 60px 20px;
  color: #94a3b8;
}

.empty-icon {
  margin-bottom: 16px;
  opacity: 0.3;
  color: #94a3b8;
}

.empty-hint {
  font-size: 14px;
  color: #64748b;
  margin-top: 8px;
}

.games-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.game-card {
  background: template;
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
  cursor: pointer;
  transition: all 0.3s ease;
}

.game-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 24px rgba(139, 92, 246, 0.3);
  border-color: rgba(139, 92, 246, 0.3);
}

.game-image {
  width: 100%;
  height: 160px;
  overflow: hidden;
  background: #1a1a1f;
  position: relative;
}

/* 右上角平台徽章 */
.platform-badges-top {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  z-index: 2;
  max-width: calc(100% - 16px);
  justify-content: flex-end;
}

.platform-badge-top {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  background: template;
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
  color: #cbd5e1;
  text-shadow: 0 1px 2px template;
  white-space: nowrap;
}

.platform-badge-top.owned {
  background: rgba(139, 92, 246, 0.3);
  border-color: rgba(139, 92, 246, 0.5);
  color: #c4b5fd;
}

.platform-badge-top.not-owned {
  color: #64748b;
  opacity: 0.7;
}

.game-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #64748b;
  background: linear-gradient(135deg, #1a1a1f 0%, #2d2d35 100%);
}

/* 图片上的文字叠加 */
.game-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.9) 0%, rgba(0, 0, 0, 0.7) 50%, transparent 100%);
  padding: 16px;
  color: #f8fafc;
}

.game-name-overlay {
  font-size: 16px;
  font-weight: bold;
  margin-bottom: 8px;
  color: #f8fafc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);
}

.game-meta-overlay {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.playtime-overlay {
  font-size: 12px;
  color: #cbd5e1;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}


.game-info {
  padding: 5px 16px;
  background: rgba(20, 20, 23, 0.5);
}

.achievement-progress {
  margin-top: 0;
}

.progress-bar {
  width: 100%;
  height: 5px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 2px;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #8b5cf6 0%, #6366f1 100%);
  transition: width 0.3s;
  box-shadow: 0 0 8px rgba(139, 92, 246, 0.5);
}

.progress-text {
  font-size: 12px;
  color: #cbd5e1;
  font-weight: 500;
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
  margin-top: 32px;
}

.page-btn {
  padding: 8px 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  background: template;
  color: #f8fafc;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
  backdrop-filter: blur(20px);
}

.page-btn:hover:not(:disabled) {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.3);
  color: #c4b5fd;
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.page-info {
  font-size: 14px;
  color: #94a3b8;
}
</style>

