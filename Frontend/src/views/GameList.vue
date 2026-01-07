<template>
  <div class="container">
    <h2>游戏列表</h2>

    <div class="filters">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="搜索游戏..."
        class="search-input"
        @input="handleSearchInput"
      />
      <select 
        v-model="sortBy" 
        @change="handleSortChange" 
        class="filter-select"
        :disabled="!!searchQuery" 
        title="搜索模式下暂不支持自定义排序"
      >
        <option value="">默认排序</option>
        <option value="release_date">发布日期</option>
        <option value="name">游戏名称</option>
        <option value="popularity">人气</option>
      </select>
    </div>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <div v-if="games.length > 0" class="grid">
        <div
          v-for="game in games"
          :key="game.gameId"
          class="game-card"
          @click="goToDetail(game.gameId)"
        >
          <img :src="game.headerImage || noCoverImage" :alt="game.name" @error="handleImageError" />
          <div class="game-card-body">
            <h3 class="game-title">{{ game.name }}</h3>
            <p class="game-info">发布日期: {{ formatDate(game.releaseDate) }}</p>
            <p class="game-info">
              {{ game.isFree ? '免费游戏' : '付费游戏' }}
            </p>
            <p class="game-info rating">
              评分: 
              <span v-if="game.reviewScore > 0" class="score">{{ game.reviewScore }}分</span>
              <span v-else class="no-score">暂无评分</span>
            </p>
          </div>
        </div>
      </div>
      <div v-else class="empty-state">
        <p>暂无相关游戏</p>
      </div>

      <Pagination
        :current-page="page"
        :total-pages="totalPages"
        @page-change="changePage"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { gameApi } from '../api'
import noCoverImage from '@/assets/no_cover.png'
import Pagination from '@/components/common/Pagination.vue'

const router = useRouter()
const games = ref([])
const loading = ref(false)
const error = ref(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const searchQuery = ref('')
// 默认排序改为按人气
const sortBy = ref('popularity')
let searchTimeout = null 

const totalPages = computed(() => Math.ceil(total.value / pageSize.value))

const loadGames = async () => {
  loading.value = true
  error.value = null
  try {
    let response
    
    if (searchQuery.value && searchQuery.value.trim() !== '') {
      response = await gameApi.searchGames({
        query: searchQuery.value.trim(), 
        page: page.value,
        page_size: pageSize.value
      })
    } 
    else {
      const params = {
        page: page.value,
        page_size: pageSize.value
      }
      if (sortBy.value) {
        params.sortBy = sortBy.value 
      }
      response = await gameApi.getGames(params)
    }

    if (response.success) {
      games.value = response.data.items
      total.value = response.data.meta.total
    } else {
      error.value = response.message || '加载失败'
    }
  } catch (err) {
    console.error(err)
    error.value = '加载游戏列表失败，请稍后重试'
  } finally {
    loading.value = false
  }
}

const handleSearchInput = () => {
  if (searchTimeout) clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    page.value = 1 
    loadGames()
  }, 500) 
}

const handleSortChange = () => {
  page.value = 1
  loadGames()
}

const changePage = (newPage) => {
  if (newPage < 1 || newPage > totalPages.value) return
  page.value = newPage
  loadGames()
  window.scrollTo(0, 0)
}

const goToDetail = (id) => {
  router.push({ name: 'StoreDetail', params: { id } })
}

const handleImageError = (e) => {
  e.target.src = noCoverImage
}

const formatDate = (dateStr) => {
  if (!dateStr) return '未知'
  try {
    return new Date(dateStr).toLocaleDateString()
  } catch {
    return dateStr
  }
}

onMounted(() => {
  loadGames()
})
</script>

<style scoped>
.container {
  padding: 24px;
  /* 确保页面背景也是深色（如果全局未设置） */
  color: #f8fafc; 
}

h2 {
  margin-bottom: 24px;
  color: #fff;
  font-size: 24px;
  font-weight: 600;
}

.filters {
  display: flex;
  gap: 16px;
  margin-bottom: 32px;
}

/* 输入框保持白底黑字，确保可读性 */
.search-input,
.filter-select {
  padding: 10px 16px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  font-size: 14px;
  background-color: #ffffff; 
  color: #333333;
}

.search-input {
  flex: 1;
}

.filter-select:disabled {
  background-color: #e5e5e5;
  cursor: not-allowed;
  color: #999;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 24px;
}

/* [修改] 游戏卡片样式：深色背景 */
.game-card {
  background: rgba(30, 30, 35, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  overflow: hidden;
  transition: transform 0.2s, box-shadow 0.2s, border-color 0.2s;
  cursor: pointer;
  display: flex;
  flex-direction: column;
}

.game-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 10px 20px rgba(0,0,0,0.3);
  border-color: rgba(255, 255, 255, 0.1);
}

.game-card img {
  width: 100%;
  height: 160px;
  object-fit: cover;
}

.game-card-body {
  padding: 16px;
  flex: 1;
  display: flex;
  flex-direction: column;
}

/* [修改] 标题颜色：浅白 */
.game-title {
  margin: 0 0 8px 0;
  font-size: 16px;
  font-weight: 600;
  color: #f1f5f9;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* [修改] 信息文字颜色：浅灰 */
.game-info {
  margin: 4px 0;
  font-size: 13px;
  color: #94a3b8;
}

.game-info.rating {
  margin-top: auto; 
  padding-top: 12px;
  font-weight: 500;
}

.score {
  color: #fbbf24; /* 金黄色评分 */
  font-weight: bold;
}

.no-score {
  color: #64748b; /* 深灰色暂无评分 */
  font-style: italic;
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
  margin-top: 40px;
}

.page-info {
  color: #94a3b8;
  font-size: 14px;
}

.empty-state {
  text-align: center;
  padding: 60px 0;
  color: #94a3b8;
  font-size: 16px;
}

.btn {
  padding: 8px 16px;
  border: 1px solid rgba(255,255,255,0.1);
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}

.btn-secondary {
  background-color: transparent;
  color: #e2e8f0;
}

.btn-secondary:hover:not(:disabled) {
  background-color: rgba(255,255,255,0.1);
  border-color: rgba(255,255,255,0.2);
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Loading */
.loading {
  text-align: center;
  padding: 40px;
  color: #94a3b8;
}

.error {
  text-align: center;
  padding: 40px;
  color: #ef4444;
}
</style>