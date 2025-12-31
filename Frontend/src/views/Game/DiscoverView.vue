<template>
  <div class="discover-container">
    <div class="search-section">
      <div class="search-wrapper">
        <Search class="search-icon" size="20" />
        <input 
          v-model="searchQuery"
          @keyup.enter="handleSearch"
          type="text" 
          placeholder="搜索游戏、题材或平台..." 
          class="search-input"
        />
      </div>
      
      <div class="categories-wrapper">
        <button 
          v-for="cat in categories" 
          :key="cat"
          @click="activeCategory = cat"
          class="category-btn"
          :class="{ active: activeCategory === cat }"
        >
          {{ cat }}
        </button>
      </div>
    </div>

    <section class="section-card">
      <div class="section-header">
        <div class="section-header-left">
          <div class="ai-icon-wrapper-small">
            <Sparkles class="ai-icon-small" size="20" />
          </div>
          <div>
            <h2 class="section-title">{{ exploreTitle }}</h2>
            <p class="section-subtitle">发现更多符合您口味的宝藏游戏</p>
          </div>
        </div>
        <button @click="loadRecommendations(true)" class="refresh-btn">
          <RefreshCw class="icon" size="16" :class="{ 'spin': aiRecommendationsLoading }" />
          刷新
        </button>
      </div>

      <div v-if="aiRecommendationsLoading && aiRecommendations.length === 0" class="loading-small">
        <div class="loading-spinner-small"></div>
        <span>AI 正在计算中...</span>
      </div>

      <div v-else-if="aiRecommendations.length === 0" class="empty-state">
        <Sparkles class="empty-icon" size="48" />
        <p>暂无 AI 探索推荐</p>
      </div>

      <div v-else class="recommendations-grid">
        <div 
          v-for="(item, index) in aiRecommendations" 
          :key="item.gameId"
          class="game-card ai-game-card"
          :style="{ animationDelay: `${index * 0.1}s` }"
          @click="$router.push({ name: 'GameDetail', params: { id: item.gameId } })"
        >
          <div class="game-card-image">
            <img :src="item.headerImage || '/placeholder-game.png'" :alt="item.gameName" @error="handleImageError" />
            <div class="game-card-overlay">
              <div class="ai-badge">
                <Sparkles class="ai-badge-icon" size="12" />
                <span>AI 推荐</span>
              </div>
            </div>
          </div>
          <div class="game-card-content">
            <div class="game-tags">
              <span 
                v-for="feature in (item.uniqueFeatures || []).slice(0, 2)" 
                :key="feature"
                class="game-tag"
              >
                {{ feature }}
              </span>
            </div>
            <h3 class="game-card-title">{{ item.gameName }}</h3>
            <p class="game-card-reason">{{ item.whyExplore || 'AI 智能推荐' }}</p>
            <div class="game-card-footer">
              <div class="game-rating" v-if="item.reviewScore">
                <Star class="star-icon" size="14" />
                <span>{{ item.reviewScore }}</span>
              </div>
              <span class="game-date" v-if="item.releaseDate">{{ formatDate(item.releaseDate) }}</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { recommendationApi } from '@/api/recommendation'
import { Search, RefreshCw, Star, Sparkles } from 'lucide-vue-next'
import { formatDate } from '@/utils/format' // 假设有这个工具函数

const searchQuery = ref('')
const activeCategory = ref('全部')
const categories = ['全部', 'RPG', '动作', 'FPS', '策略', '独立游戏']
const recommendations = ref([]) // 用于存其他普通推荐
const aiRecommendations = ref([])
const aiRecommendationsLoading = ref(false)
const exploreTitle = ref('AI 智能探索')

const handleSearch = () => {
  // TODO: 实现搜索功能
  console.log('Search:', searchQuery.value)
}

const handleImageError = (event) => {
  event.target.src = '/placeholder-game.png'
}

// 修改：接受 refresh 参数，默认为 false
const loadRecommendations = async (refresh = false) => {
  aiRecommendationsLoading.value = true
  
  try {
    // 1. 如果不是强制刷新，且页面刚加载，也加载普通推荐
    if (!refresh) {
      const res = await recommendationApi.getRecommendations({ limit: 4 })
      if (res.success) {
        recommendations.value = res.data.items || []
      }
    }

    // 2. 加载 AI/探索推荐，传递 refresh 参数
    const exploreRes = await recommendationApi.exploreGames({ refresh })
    
    if (exploreRes.success && exploreRes.data) {
      aiRecommendations.value = exploreRes.data.items || []
      if (exploreRes.data.exploreCategory) {
        exploreTitle.value = exploreRes.data.exploreCategory
      }
    }
  } catch (error) {
    console.error('[Frontend] Failed to load recommendations:', error)
  } finally {
    aiRecommendationsLoading.value = false
  }
}

onMounted(() => {
  loadRecommendations(false) // 初始加载不强制刷新，使用缓存
})
</script>

<style scoped>
/* 保持原有 CSS 样式不变，只需增加 spin 动画 */
.refresh-btn .icon.spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* ... 其他原有样式 ... */
.discover-container {
  min-height: 100vh;
  background: #0f0f13;
  color: #f8fafc;
  padding: 24px;
}

/* 搜索区域 */
.search-section {
  margin-bottom: 32px;
}

.search-wrapper {
  position: relative;
  max-width: 800px;
  margin-bottom: 16px;
}

.search-icon {
  position: absolute;
  left: 16px;
  top: 50%;
  transform: translateY(-50%);
  color: #94a3b8;
  pointer-events: none;
}

.search-input {
  width: 100%;
  background: rgba(20, 20, 23, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 14px 16px 14px 48px;
  color: #f8fafc;
  font-size: 15px;
  transition: all 0.2s;
  backdrop-filter: blur(20px);
}

.search-input::placeholder {
  color: #64748b;
}

.search-input:focus {
  outline: none;
  border-color: rgba(139, 92, 246, 0.5);
  background: rgba(20, 20, 23, 0.9);
}

.categories-wrapper {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding-bottom: 4px;
}

.categories-wrapper::-webkit-scrollbar {
  display: none;
}

.category-btn {
  padding: 8px 16px;
  border-radius: 20px;
  font-size: 14px;
  font-weight: 500;
  background: rgba(20, 20, 23, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.08);
  color: #94a3b8;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
  backdrop-filter: blur(20px);
}

.category-btn:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #f8fafc;
  border-color: rgba(139, 92, 246, 0.3);
}

.category-btn.active {
  background: #8b5cf6;
  border-color: #8b5cf6;
  color: white;
}

/* 卡片样式 */
.section-card {
  background: rgba(20, 20, 23, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 24px;
  backdrop-filter: blur(20px);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
}

.section-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.ai-icon-wrapper-small {
  padding: 8px;
  background: rgba(139, 92, 246, 0.2);
  border-radius: 8px;
  flex-shrink: 0;
}

.ai-icon-small {
  color: #8b5cf6;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  margin-bottom: 4px;
  color: #f8fafc;
}

.section-subtitle {
  font-size: 13px;
  color: #94a3b8;
}

.refresh-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: rgba(20, 20, 23, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  color: #94a3b8;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.refresh-btn:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #8b5cf6;
  border-color: rgba(139, 92, 246, 0.3);
}

.refresh-btn .icon {
  color: #8b5cf6;
}

/* 推荐游戏网格 */
.recommendations-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.game-card {
  background: rgba(15, 15, 19, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s;
  opacity: 0;
  transform: translateY(20px);
  animation: fadeInUp 0.6s ease-out forwards;
}

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

.game-card:hover {
  transform: translateY(-4px);
  border-color: rgba(139, 92, 246, 0.5);
  box-shadow: 0 8px 24px rgba(139, 92, 246, 0.2);
}

.game-card-image {
  position: relative;
  width: 100%;
  height: 200px;
  overflow: hidden;
}

.game-card-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.5s;
}

.game-card:hover .game-card-image img {
  transform: scale(1.1);
}

.game-card-overlay {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.8) 0%, transparent 100%);
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding: 12px;
}

.match-badge {
  padding: 4px 12px;
  background: rgba(16, 185, 129, 0.9);
  color: white;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
}

.ai-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 12px;
  background: rgba(139, 92, 246, 0.9);
  color: white;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
}

.ai-badge-icon {
  width: 12px;
  height: 12px;
}

.ai-game-card {
  border-color: rgba(139, 92, 246, 0.3);
}

.ai-game-card:hover {
  border-color: rgba(139, 92, 246, 0.6);
  box-shadow: 0 8px 24px rgba(139, 92, 246, 0.3);
}

.game-card-content {
  padding: 16px;
}

.game-tags {
  display: flex;
  gap: 6px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}

.game-tag {
  padding: 4px 10px;
  background: rgba(139, 92, 246, 0.2);
  color: #c4b5fd;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
}

.game-card-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 6px;
  color: #f8fafc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.game-card-reason {
  font-size: 13px;
  color: #94a3b8;
  margin-bottom: 12px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.game-card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.game-rating {
  display: flex;
  align-items: center;
  gap: 4px;
  color: #fbbf24;
  font-size: 13px;
  font-weight: 600;
}

.star-icon {
  fill: currentColor;
}


/* 加载和空状态 */
.loading-small {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 40px 20px;
  color: #94a3b8;
  justify-content: center;
}

.loading-spinner-small {
  width: 20px;
  height: 20px;
  border: 2px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: #64748b;
}

.empty-icon {
  margin-bottom: 16px;
  opacity: 0.3;
  color: #64748b;
}

.game-date {
  font-size: 12px;
  color: #64748b;
}

/* 响应式设计 */
@media (max-width: 1024px) {
  .recommendations-grid {
    grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
    gap: 16px;
  }
}

@media (max-width: 768px) {
  .discover-container {
    padding: 16px;
  }
  
  .recommendations-grid {
    grid-template-columns: 1fr;
  }
  
  .search-wrapper {
    max-width: 100%;
  }
  
  .section-card {
    padding: 16px;
  }
}
</style>