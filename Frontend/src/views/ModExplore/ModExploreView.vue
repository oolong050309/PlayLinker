<template>
  <div class="mod-explore-page">
    <!-- 顶部区域 -->
    <div class="page-header">
      <div class="header-content">
        <h1 class="page-title">
          <Store class="title-icon" />
          Mod 商店
        </h1>
        <p class="page-subtitle">浏览和下载来自 NexusMods、3DM 等平台的 Mod</p>
      </div>
    </div>

    <!-- 游戏选择器 -->
    <div class="selector-section">
      <div class="game-selectors-row">
        <!-- 本地游戏选择器 -->
        <div class="game-selector-wrapper" :class="{ disabled: selectionMode === 'search' }">
          <div class="selector-label">
            <Gamepad2 class="label-icon" />
            <span>选择本地游戏</span>
            <button v-if="selectionMode === 'local'" class="reset-btn" @click.stop="resetSelection">
              <X :size="14" />
            </button>
          </div>
          <div class="custom-select" :class="{ open: dropdownOpen, disabled: selectionMode === 'search' }" @click="selectionMode !== 'search' && toggleDropdown()">
            <div class="select-display">
              <span v-if="selectedGame" class="selected-game">
                {{ selectedGame.gameName }}
              </span>
              <span v-else class="placeholder">请选择游戏...</span>
              <ChevronDown class="chevron" :class="{ rotated: dropdownOpen }" />
            </div>
            <div class="select-dropdown" v-show="dropdownOpen" @click.stop>
              <div v-if="localGames.length === 0" class="dropdown-empty">
                <Package class="empty-icon" />
                <p>暂无本地游戏</p>
                <router-link to="/app/mods" class="add-game-link">去添加游戏</router-link>
              </div>
              <div v-else class="dropdown-list">
                <div 
                  v-for="game in localGames" 
                  :key="game.gameId"
                  class="dropdown-item"
                  :class="{ active: selectedGameId === game.gameId }"
                  @click="selectGame(game)"
                >
                  <span class="game-name">{{ game.gameName }}</span>
                  <span class="game-meta">{{ game.platformName }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="selector-divider">或</div>

        <!-- 搜索所有游戏 -->
        <div class="game-search-wrapper" :class="{ disabled: selectionMode === 'local' }">
          <div class="selector-label">
            <Search class="label-icon" />
            <span>搜索游戏</span>
            <button v-if="selectionMode === 'search'" class="reset-btn" @click.stop="resetSelection">
              <X :size="14" />
            </button>
          </div>
          <div class="game-search-box" :class="{ disabled: selectionMode === 'local' }">
            <input 
              v-model="gameSearchQuery" 
              type="text" 
              placeholder="输入游戏名称搜索..."
              :disabled="selectionMode === 'local'"
              @input="handleGameSearchInput"
              @focus="showGameSearchResults = true"
            />
            <button v-if="gameSearchQuery && selectionMode !== 'local'" class="clear-btn" @click="clearGameSearch">
              <X :size="16" />
            </button>
          </div>
          <!-- 搜索结果下拉 -->
          <div v-if="showGameSearchResults && selectionMode !== 'local' && (gameSearchResults.length > 0 || gameSearchLoading)" class="game-search-dropdown">
            <div v-if="gameSearchLoading" class="search-loading">
              <div class="mini-loader"></div>
              <span>搜索中...</span>
            </div>
            <div v-else class="dropdown-list">
              <div 
                v-for="game in gameSearchResults" 
                :key="game.gameId"
                class="dropdown-item"
                @click="selectSearchedGame(game)"
              >
                <img v-if="game.headerImage" :src="game.headerImage" class="game-thumb" @error="handleImageError" />
                <div class="game-info">
                  <span class="game-name">{{ game.name }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Mod 来源标签 -->
    <div v-if="modSources.length > 0" class="source-section">
      <div class="source-tabs">
        <button 
          v-for="source in modSources" 
          :key="source.source"
          class="source-tab"
          :class="{ active: selectedSource === source.source }"
          @click="selectSource(source)"
        >
          <img v-if="source.iconUrl" :src="source.iconUrl" class="source-icon" @error="handleIconError" />
          <Globe v-else class="source-icon-fallback" />
          <span>{{ source.displayName }}</span>
        </button>
      </div>
      
      <!-- 搜索栏 -->
      <div class="search-section" v-if="selectedSource">
        <div class="search-box">
          <Search class="search-icon" />
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="搜索 Mod..."
            @keyup.enter="handleSearch"
          />
        </div>
        <button class="btn-search" @click="handleSearch">搜索</button>
      </div>
    </div>

    <!-- 无 Mod 来源提示 -->
    <div v-else-if="selectedGameId && !sourcesLoading" class="empty-state no-source">
      <Package class="empty-icon" />
      <h3>该游戏暂无 Mod 平台支持</h3>
      <p>我们尚未收录 <strong>{{ selectedGame?.gameName }}</strong> 的 Mod 来源</p>
      <p class="hint">目前支持 NexusMods、3DM 等平台的热门游戏</p>
    </div>

    <!-- 加载来源状态 -->
    <div v-if="sourcesLoading" class="loading-state">
      <div class="loader"></div>
      <p>正在加载 Mod 来源...</p>
    </div>

    <!-- 加载 Mod 列表状态 -->
    <div v-else-if="loading" class="loading-state">
      <div class="loader"></div>
      <p>正在加载 Mod 列表...</p>
    </div>

    <!-- Mod 列表 -->
    <div v-else-if="mods.length > 0" class="mod-grid">
      <div v-for="mod in mods" :key="mod.modId" class="mod-card">
        <div class="card-image">
          <img v-if="mod.thumbnailUrl" :src="mod.thumbnailUrl" :alt="mod.name" @error="handleImageError" />
          <div v-else class="image-placeholder">
            <Package />
          </div>
          <span v-if="mod.adultContent" class="nsfw-tag">18+</span>
          <div class="card-overlay">
            <a :href="mod.modPageUrl" target="_blank" class="overlay-btn">查看详情</a>
          </div>
        </div>
        <div class="card-body">
          <h3 class="mod-title">{{ mod.name }}</h3>
          <p class="mod-author">by {{ mod.author || '未知作者' }}</p>
          <p class="mod-desc">{{ truncate(mod.summary, 60) }}</p>
          <div class="mod-stats">
            <span class="stat"><Download :size="14" /> {{ formatNumber(mod.downloads) }}</span>
            <span class="stat"><ThumbsUp :size="14" /> {{ formatNumber(mod.endorsements) }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- 空状态 - 选择了游戏和来源但没有 Mod -->
    <div v-else-if="selectedGameId && selectedSource && !loading && !sourcesLoading && modSources.length > 0" class="empty-state">
      <Package class="empty-icon" />
      <h3>在 {{ selectedSourceName }} 上暂无 Mod</h3>
      <p>该游戏在 {{ selectedSourceName }} 平台上目前没有可用的 Mod</p>
      <p class="hint">可以尝试切换其他 Mod 来源平台</p>
    </div>

    <!-- 空状态 - 未选择游戏 -->
    <div v-else-if="!selectedGameId" class="empty-state">
      <Gamepad2 class="empty-icon" />
      <h3 v-if="localGames.length === 0">暂无本地游戏</h3>
      <h3 v-else>请选择一个游戏</h3>
      <p v-if="localGames.length === 0">请先在「Mod与存档」页面添加本地游戏</p>
      <p v-else>选择游戏后将显示可用的 Mod 来源</p>
      <router-link v-if="localGames.length === 0" to="/app/mods" class="btn-primary">
        <Plus :size="16" /> 添加本地游戏
      </router-link>
    </div>

    <!-- 分页 -->
    <div v-if="totalPages > 1" class="pagination">
      <button class="page-btn" :disabled="currentPage === 1" @click="changePage(currentPage - 1)">
        <ChevronLeft :size="16" /> 上一页
      </button>
      <div class="page-numbers">
        <span class="current">{{ currentPage }}</span>
        <span class="separator">/</span>
        <span class="total">{{ totalPages }}</span>
      </div>
      <button class="page-btn" :disabled="currentPage === totalPages" @click="changePage(currentPage + 1)">
        下一页 <ChevronRight :size="16" />
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { 
  Store, Gamepad2, ChevronDown, ChevronLeft, ChevronRight, 
  Search, Download, ThumbsUp, Package, Globe, Plus, X 
} from 'lucide-vue-next'
import { getGameModSources, getModList, searchMods } from '@/api/modExplore'
import { getLocalGames } from '@/api/localGame'
import { searchGames } from '@/api/games'

// State
const loading = ref(false)
const sourcesLoading = ref(false)
const dropdownOpen = ref(false)
const localGames = ref([])
const selectedGameId = ref(null)
const selectedGame = ref(null)
const modSources = ref([])
const selectedSource = ref('')
const selectedDomain = ref('')
const mods = ref([])
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = ref(20)
const totalMods = ref(0)

// 游戏搜索相关状态
const gameSearchQuery = ref('')
const gameSearchResults = ref([])
const gameSearchLoading = ref(false)
const showGameSearchResults = ref(false)
let searchDebounceTimer = null

// 选择模式: null=未选择, 'local'=本地游戏, 'search'=搜索游戏
const selectionMode = ref(null)

// Computed
const totalPages = computed(() => Math.ceil(totalMods.value / pageSize.value))
const selectedSourceName = computed(() => {
  const source = modSources.value.find(s => s.source === selectedSource.value)
  return source?.displayName || selectedSource.value
})

// 点击外部关闭下拉框
const handleClickOutside = (e) => {
  if (!e.target.closest('.custom-select')) {
    dropdownOpen.value = false
  }
  if (!e.target.closest('.game-search-wrapper')) {
    showGameSearchResults.value = false
  }
}

onMounted(() => {
  loadLocalGames()
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
})

// Methods
const loadLocalGames = async () => {
  try {
    const res = await getLocalGames()
    console.log('本地游戏响应:', res)
    // 响应拦截器返回 response.data，结构是 { success, data: { items, meta } }
    if (res.data?.items) {
      localGames.value = res.data.items
    } else if (res.items) {
      localGames.value = res.items
    } else if (Array.isArray(res.data)) {
      localGames.value = res.data
    } else if (Array.isArray(res)) {
      localGames.value = res
    }
    console.log('解析后游戏列表:', localGames.value)
  } catch (error) {
    console.error('加载游戏列表失败:', error)
  }
}

const toggleDropdown = () => {
  dropdownOpen.value = !dropdownOpen.value
}

const selectGame = async (game) => {
  selectedGame.value = game
  selectedGameId.value = game.gameId
  dropdownOpen.value = false
  selectionMode.value = 'local'  // 设置选择模式
  modSources.value = []
  selectedSource.value = ''
  mods.value = []
  sourcesLoading.value = true
  
  // 清空游戏搜索
  gameSearchQuery.value = ''
  gameSearchResults.value = []
  showGameSearchResults.value = false
  
  // 加载该游戏的 Mod 来源
  try {
    console.log('获取游戏 Mod 来源, gameId:', game.gameId)
    const res = await getGameModSources(game.gameId)
    console.log('Mod 来源响应:', res)
    
    // 解析响应
    const sources = res.data?.sources || res.sources || []
    modSources.value = sources
    
    if (sources.length > 0) {
      selectSource(sources[0])
    }
  } catch (error) {
    console.error('加载 Mod 来源失败:', error)
    modSources.value = []
  } finally {
    sourcesLoading.value = false
  }
}

// 重置选择
const resetSelection = () => {
  selectionMode.value = null
  selectedGame.value = null
  selectedGameId.value = null
  gameSearchQuery.value = ''
  gameSearchResults.value = []
  showGameSearchResults.value = false
  modSources.value = []
  selectedSource.value = ''
  mods.value = []
  totalMods.value = 0
}

// 游戏搜索相关方法
const handleGameSearchInput = () => {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  
  if (!gameSearchQuery.value.trim()) {
    gameSearchResults.value = []
    showGameSearchResults.value = false
    return
  }
  
  searchDebounceTimer = setTimeout(async () => {
    gameSearchLoading.value = true
    showGameSearchResults.value = true
    
    try {
      const res = await searchGames(gameSearchQuery.value, 1, 10)
      const data = res.data || res
      gameSearchResults.value = data.items || []
    } catch (error) {
      console.error('搜索游戏失败:', error)
      gameSearchResults.value = []
    } finally {
      gameSearchLoading.value = false
    }
  }, 300)
}

const clearGameSearch = () => {
  gameSearchQuery.value = ''
  gameSearchResults.value = []
  showGameSearchResults.value = false
}

const selectSearchedGame = async (game) => {
  // 构造与本地游戏相同的格式
  selectedGame.value = {
    gameId: game.gameId,
    gameName: game.name,
    headerImage: game.headerImage
  }
  selectedGameId.value = game.gameId
  selectionMode.value = 'search'  // 设置选择模式
  showGameSearchResults.value = false
  gameSearchQuery.value = game.name
  modSources.value = []
  selectedSource.value = ''
  mods.value = []
  sourcesLoading.value = true
  
  // 加载该游戏的 Mod 来源
  try {
    console.log('获取搜索游戏 Mod 来源, gameId:', game.gameId)
    const res = await getGameModSources(game.gameId)
    console.log('Mod 来源响应:', res)
    
    const sources = res.data?.sources || res.sources || []
    modSources.value = sources
    
    if (sources.length > 0) {
      selectSource(sources[0])
    }
  } catch (error) {
    console.error('加载 Mod 来源失败:', error)
    modSources.value = []
  } finally {
    sourcesLoading.value = false
  }
}

const selectSource = async (source) => {
  selectedSource.value = source.source
  selectedDomain.value = source.externalDomain || ''
  currentPage.value = 1
  await loadMods()
}

const loadMods = async () => {
  if (!selectedGameId.value || !selectedSource.value) return

  loading.value = true
  mods.value = []
  
  try {
    console.log('加载 Mod 列表:', { gameId: selectedGameId.value, source: selectedSource.value })
    const res = await getModList({
      gameId: selectedGameId.value,
      source: selectedSource.value,
      page: currentPage.value,
      pageSize: pageSize.value
    })
    console.log('Mod 列表响应:', res)
    
    // 解析响应
    const data = res.data || res
    mods.value = data.mods || []
    totalMods.value = data.total || 0
  } catch (error) {
    console.error('加载 Mod 列表失败:', error)
    mods.value = []
  } finally {
    loading.value = false
  }
}

const handleSearch = async () => {
  if (!searchQuery.value.trim()) {
    loadMods()
    return
  }

  loading.value = true
  try {
    const res = await searchMods(selectedSource.value, searchQuery.value, selectedDomain.value, currentPage.value)
    const data = res.data || res
    mods.value = data.mods || []
    totalMods.value = data.total || 0
  } catch (error) {
    console.error('搜索失败:', error)
  } finally {
    loading.value = false
  }
}

const changePage = (page) => {
  currentPage.value = page
  searchQuery.value.trim() ? handleSearch() : loadMods()
}

const truncate = (text, len) => text?.length > len ? text.substring(0, len) + '...' : (text || '')
const formatNumber = (num) => {
  if (!num) return '0'
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
  return num.toString()
}

const handleImageError = (e) => { e.target.style.display = 'none' }
const handleIconError = (e) => { e.target.style.display = 'none' }
</script>

<style scoped>
.mod-explore-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #0f0f1a 0%, #1a1a2e 100%);
  padding: 32px;
  color: #e2e8f0;
}

/* Header */
.page-header {
  margin-bottom: 32px;
}

.header-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.page-title {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 32px;
  font-weight: 700;
  color: #fff;
  margin: 0;
}

.title-icon {
  width: 36px;
  height: 36px;
  color: #8b5cf6;
}

.page-subtitle {
  color: #94a3b8;
  margin: 0;
  font-size: 15px;
}

/* Game Selector */
.selector-section {
  margin-bottom: 24px;
}

.game-selector-wrapper {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 320px;
  transition: opacity 0.2s;
}

.game-selector-wrapper.disabled {
  opacity: 0.5;
  pointer-events: none;
}

.selector-label {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #94a3b8;
  font-size: 14px;
  font-weight: 500;
}

.label-icon {
  width: 18px;
  height: 18px;
}

.reset-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-left: auto;
  padding: 4px;
  background: rgba(239, 68, 68, 0.2);
  border: none;
  border-radius: 4px;
  color: #ef4444;
  cursor: pointer;
  transition: all 0.15s;
}

.reset-btn:hover {
  background: rgba(239, 68, 68, 0.3);
}

.custom-select {
  position: relative;
}

.custom-select.disabled {
  opacity: 0.5;
  pointer-events: none;
}

.select-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.select-display:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(139, 92, 246, 0.5);
}

.custom-select.open .select-display {
  border-color: #8b5cf6;
  box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.2);
}

.selected-game {
  color: #fff;
  font-weight: 500;
}

.placeholder {
  color: #64748b;
}

.chevron {
  width: 18px;
  height: 18px;
  color: #64748b;
  transition: transform 0.2s;
}

.chevron.rotated {
  transform: rotate(180deg);
}

.select-dropdown {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  right: 0;
  background: #1e1e2e;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
  z-index: 100;
  max-height: 320px;
  overflow-y: auto;
}

.dropdown-empty {
  padding: 32px;
  text-align: center;
  color: #64748b;
}

.dropdown-empty .empty-icon {
  width: 40px;
  height: 40px;
  margin-bottom: 12px;
  opacity: 0.5;
}

.dropdown-empty p {
  margin: 0 0 12px 0;
}

.add-game-link {
  color: #8b5cf6;
  text-decoration: none;
  font-weight: 500;
}

.add-game-link:hover {
  text-decoration: underline;
}

.dropdown-list {
  padding: 8px;
}

.dropdown-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-radius: 8px;
  cursor: pointer;
  transition: background 0.15s;
}

.dropdown-item:hover {
  background: rgba(139, 92, 246, 0.1);
}

.dropdown-item.active {
  background: rgba(139, 92, 246, 0.2);
}

.game-name {
  color: #e2e8f0;
  font-weight: 500;
}

.game-meta {
  color: #64748b;
  font-size: 13px;
}

/* Source Tabs */
.source-section {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
  padding: 16px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 12px;
}

.source-tabs {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.source-tab {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 18px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: #94a3b8;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.source-tab:hover {
  background: rgba(139, 92, 246, 0.1);
  border-color: rgba(139, 92, 246, 0.3);
  color: #e2e8f0;
}

.source-tab.active {
  background: linear-gradient(135deg, #8b5cf6, #6366f1);
  border-color: transparent;
  color: #fff;
}

.source-icon {
  width: 18px;
  height: 18px;
  border-radius: 4px;
}

.source-icon-fallback {
  width: 18px;
  height: 18px;
}

/* Search */
.search-section {
  display: flex;
  gap: 12px;
  margin-left: auto;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  min-width: 260px;
}

.search-box:focus-within {
  border-color: #8b5cf6;
}

.search-icon {
  width: 16px;
  height: 16px;
  color: #64748b;
}

.search-box input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  color: #e2e8f0;
  font-size: 14px;
}

.search-box input::placeholder {
  color: #64748b;
}

.btn-search {
  padding: 10px 20px;
  background: linear-gradient(135deg, #8b5cf6, #6366f1);
  border: none;
  border-radius: 8px;
  color: #fff;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: opacity 0.2s;
}

.btn-search:hover {
  opacity: 0.9;
}

/* Loading */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px;
  color: #94a3b8;
}

.loader {
  width: 48px;
  height: 48px;
  border: 3px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Mod Grid */
.mod-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 24px;
}

.mod-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 16px;
  overflow: hidden;
  transition: all 0.3s;
}

.mod-card:hover {
  transform: translateY(-4px);
  border-color: rgba(139, 92, 246, 0.3);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
}

.card-image {
  position: relative;
  height: 160px;
  background: rgba(0, 0, 0, 0.3);
  overflow: hidden;
}

.card-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.3s;
}

.mod-card:hover .card-image img {
  transform: scale(1.05);
}

.image-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #4a5568;
}

.image-placeholder svg {
  width: 48px;
  height: 48px;
}

.nsfw-tag {
  position: absolute;
  top: 10px;
  right: 10px;
  padding: 4px 10px;
  background: #ef4444;
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  border-radius: 4px;
}

.card-overlay {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s;
}

.mod-card:hover .card-overlay {
  opacity: 1;
}

.overlay-btn {
  padding: 10px 24px;
  background: #8b5cf6;
  color: #fff;
  text-decoration: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  transition: background 0.2s;
}

.overlay-btn:hover {
  background: #7c3aed;
}

.card-body {
  padding: 16px;
}

.mod-title {
  font-size: 16px;
  font-weight: 600;
  color: #fff;
  margin: 0 0 6px 0;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.mod-author {
  font-size: 13px;
  color: #8b5cf6;
  margin: 0 0 8px 0;
}

.mod-desc {
  font-size: 13px;
  color: #94a3b8;
  margin: 0 0 12px 0;
  line-height: 1.5;
  min-height: 40px;
}

.mod-stats {
  display: flex;
  gap: 16px;
}

.stat {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #64748b;
}

/* Empty State */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 20px;
  text-align: center;
}

.empty-state .empty-icon {
  width: 72px;
  height: 72px;
  color: #4a5568;
  margin-bottom: 20px;
}

.empty-state h3 {
  font-size: 20px;
  color: #e2e8f0;
  margin: 0 0 8px 0;
}

.empty-state p {
  color: #64748b;
  margin: 0 0 8px 0;
}

.empty-state .hint {
  font-size: 13px;
  color: #4a5568;
}

.empty-state.no-source {
  background: rgba(255, 255, 255, 0.02);
  border: 1px dashed rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  margin-top: 20px;
}

.empty-state.no-source strong {
  color: #8b5cf6;
}

.btn-primary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-top: 16px;
  padding: 12px 24px;
  background: linear-gradient(135deg, #8b5cf6, #6366f1);
  color: #fff;
  text-decoration: none;
  border-radius: 10px;
  font-weight: 500;
  transition: opacity 0.2s;
}

.btn-primary:hover {
  opacity: 0.9;
}

/* Pagination */
.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 20px;
  margin-top: 40px;
  padding-top: 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.06);
}

.page-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: #e2e8f0;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.page-btn:hover:not(:disabled) {
  background: rgba(139, 92, 246, 0.1);
  border-color: rgba(139, 92, 246, 0.3);
}

.page-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.page-numbers {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
}

.page-numbers .current {
  color: #8b5cf6;
  font-weight: 600;
}

.page-numbers .separator {
  color: #4a5568;
}

.page-numbers .total {
  color: #64748b;
}

/* Scrollbar */
.select-dropdown::-webkit-scrollbar {
  width: 6px;
}

.select-dropdown::-webkit-scrollbar-track {
  background: transparent;
}

.select-dropdown::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}

/* Game Selectors Row */
.game-selectors-row {
  display: flex;
  align-items: flex-end;
  gap: 24px;
  flex-wrap: wrap;
}

.selector-divider {
  display: flex;
  align-items: center;
  padding-bottom: 12px;
  color: #64748b;
  font-size: 14px;
  font-weight: 500;
}

/* Game Search */
.game-search-wrapper {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 320px;
  transition: opacity 0.2s;
}

.game-search-wrapper.disabled {
  opacity: 0.5;
  pointer-events: none;
}

.game-search-box {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 18px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  transition: all 0.2s;
}

.game-search-box.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.game-search-box:focus-within:not(.disabled) {
  border-color: #8b5cf6;
  box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.2);
}

.game-search-box .search-icon {
  color: #64748b;
  flex-shrink: 0;
}

.game-search-box input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  color: #e2e8f0;
  font-size: 14px;
}

.game-search-box input::placeholder {
  color: #64748b;
}

.game-search-box .clear-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 4px;
  background: rgba(255, 255, 255, 0.1);
  border: none;
  border-radius: 4px;
  color: #94a3b8;
  cursor: pointer;
  transition: all 0.15s;
}

.game-search-box .clear-btn:hover {
  background: rgba(255, 255, 255, 0.2);
  color: #fff;
}

.game-search-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: #1e1e2e;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
  z-index: 100;
  max-height: 320px;
  overflow-y: auto;
}

.game-search-dropdown .dropdown-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
}

.game-search-dropdown .game-thumb {
  width: 60px;
  height: 28px;
  object-fit: cover;
  border-radius: 4px;
  flex-shrink: 0;
}

.game-search-dropdown .game-info {
  flex: 1;
  min-width: 0;
}

.game-search-dropdown .game-info .game-name {
  display: block;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.search-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 20px;
  color: #94a3b8;
  font-size: 14px;
}

.mini-loader {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

/* Responsive */
@media (max-width: 768px) {
  .mod-explore-page {
    padding: 20px;
  }
  
  .game-selectors-row {
    flex-direction: column;
    gap: 16px;
  }
  
  .selector-divider {
    padding-top: 0;
    justify-content: center;
  }
  
  .game-selector-wrapper {
    flex-direction: column;
    align-items: stretch;
    width: 100%;
  }
  
  .game-search-wrapper {
    min-width: 100%;
  }
  
  .custom-select {
    min-width: 100%;
  }
  
  .source-section {
    flex-direction: column;
    align-items: stretch;
  }
  
  .search-section {
    margin-left: 0;
    width: 100%;
  }
  
  .search-box {
    flex: 1;
    min-width: auto;
  }
}
</style>
