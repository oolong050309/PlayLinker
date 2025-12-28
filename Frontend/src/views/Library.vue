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
          <div class="stat-icon bg-green">
            <Play class="icon" size="24" />
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ overview.gamesPlayed || 0 }}</div>
            <div class="stat-label">已玩游戏</div>
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
              v-if="game.headerImage" 
              :src="game.headerImage" 
              :alt="game.name"
            />
            <div v-else class="image-placeholder">
              <Gamepad2 size="32" />
            </div>
            <!-- 图片上的文字叠加 -->
            <div class="game-overlay">
              <h3 class="game-name-overlay">{{ game.name }}</h3>
              <div class="game-meta-overlay">
                <span v-if="game.platformName" class="platform-badge-overlay">{{ game.platformName }}</span>
                <span v-if="game.playtimeMinutes > 0" class="playtime-overlay">
                  {{ formatPlaytime(game.playtimeMinutes) }}
                </span>
              </div>
            </div>
          </div>
          <div class="game-info">
            <div v-if="game.achievementProgress" class="achievement-progress">
              <div class="progress-bar">
                <div 
                  class="progress-fill" 
                  :style="{ width: `${game.achievementProgress}%` }"
                ></div>
              </div>
              <span class="progress-text">{{ game.achievementProgress.toFixed(2) }}% 成就完成</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 分页 -->
      <div v-if="totalPages > 1" class="pagination">
        <button 
          @click="changePage(currentPage - 1)" 
          :disabled="currentPage === 1"
          class="page-btn"
        >
          上一页
        </button>
        <span class="page-info">第 {{ currentPage }} / {{ totalPages }} 页</span>
        <button 
          @click="changePage(currentPage + 1)" 
          :disabled="currentPage === totalPages"
          class="page-btn"
        >
          下一页
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { libraryApi, achievementApi } from '../api'
import { Gamepad2, Play, Clock, Trophy, Search } from 'lucide-vue-next'
import { useRouter } from 'vue-router'

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
const sortBy = ref('name')
const currentPage = ref(1)
const pageSize = ref(20)
const totalPages = ref(1)

const platforms = ref([
  { id: 1, name: 'Steam' },
  { id: 5, name: 'GOG' },
  { id: 6, name: 'PSN' },
  { id: 7, name: 'Xbox' }
])

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
        const unlocked = game.achievementsUnlocked ?? game.AchievementsUnlocked ?? 0
        const total = game.achievementsTotal ?? game.AchievementsTotal ?? 0
        const achievementProgress = total > 0 ? (unlocked / total * 100) : 0
        
        return {
          gameId: game.gameId ?? game.GameId,
          name: game.name ?? game.Name,
          headerImage: game.headerImage ?? game.HeaderImage,
          platformName: platformName,
          playtimeMinutes: game.playtimeMinutes ?? game.PlaytimeMinutes ?? 0,
          lastPlayed: game.lastPlayed ?? game.LastPlayed,
          achievementProgress: achievementProgress
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

const filteredGames = computed(() => {
  let result = games.value

  // 搜索过滤
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(game => 
      game.name.toLowerCase().includes(query)
    )
  }

  // 排序
  if (sortBy.value === 'name') {
    result = [...result].sort((a, b) => a.name.localeCompare(b.name))
  } else if (sortBy.value === 'playtime') {
    result = [...result].sort((a, b) => b.playtimeMinutes - a.playtimeMinutes)
  } else if (sortBy.value === 'lastPlayed') {
    result = [...result].sort((a, b) => {
      if (!a.lastPlayed && !b.lastPlayed) return 0
      if (!a.lastPlayed) return 1
      if (!b.lastPlayed) return -1
      return new Date(b.lastPlayed) - new Date(a.lastPlayed)
    })
  }

  return result
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

const changePage = (page) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page
    loadGames()
  }
}

const viewGameDetails = (gameId) => {
  router.push(`/games/${gameId}`)
}

// 监听筛选条件变化
const watchFilters = () => {
  currentPage.value = 1
  loadGames()
}

onMounted(() => {
  loadOverview()
  loadGames()
})
</script>

<style scoped>
.app-container {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  background: #0f0f13;
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
  background: rgba(20, 20, 23, 0.75);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
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
  background: rgba(20, 20, 23, 0.75);
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
  height: 200px;
  overflow: hidden;
  background: #1a1a1f;
  position: relative;
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

.platform-badge-overlay {
  background: rgba(139, 92, 246, 0.2);
  border: 1px solid rgba(139, 92, 246, 0.3);
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  color: #c4b5fd;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
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
  background: rgba(20, 20, 23, 0.75);
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

