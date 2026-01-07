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

    <!-- 筛选器按钮和当前启用的筛选规则 -->
    <div class="active-filters" v-if="!searchQuery">
      <!-- 筛选按钮 -->
      <div class="filter-buttons">
        <button 
          class="filter-btn" 
          @click="openFilterModal('genre')"
          :class="{ active: selectedGenres.length > 0 }"
        >
          游戏类型
          <span v-if="selectedGenres.length > 0" class="filter-count">({{ selectedGenres.length }})</span>
        </button>
        <button 
          class="filter-btn" 
          @click="openFilterModal('category')"
          :class="{ active: selectedCategories.length > 0 }"
        >
          游戏分类
          <span v-if="selectedCategories.length > 0" class="filter-count">({{ selectedCategories.length }})</span>
        </button>
        <button 
          class="filter-btn" 
          @click="openFilterModal('language')"
          :class="{ active: selectedLanguages.length > 0 }"
        >
          支持语言
          <span v-if="selectedLanguages.length > 0" class="filter-count">({{ selectedLanguages.length }})</span>
        </button>
      </div>

      <!-- 当前启用的筛选规则 -->
      <div class="active-filter-tags" v-if="selectedGenres.length > 0 || selectedCategories.length > 0 || selectedLanguages.length > 0">
        <span 
          v-for="genre in selectedGenres" 
          :key="`genre-${genre}`"
          class="active-tag"
        >
          类型: {{ genre }}
          <span class="remove-tag" @click="removeGenre(genre)">×</span>
        </span>
        <span 
          v-for="category in selectedCategories" 
          :key="`category-${category}`"
          class="active-tag"
        >
          分类: {{ category }}
          <span class="remove-tag" @click="removeCategory(category)">×</span>
        </span>
        <span 
          v-for="language in selectedLanguages" 
          :key="`language-${language}`"
          class="active-tag"
        >
          语言: {{ language }}
          <span class="remove-tag" @click="removeLanguage(language)">×</span>
        </span>
        <button @click="clearAllFilters" class="clear-all-btn">清除全部</button>
      </div>
    </div>

    <!-- 筛选弹窗 -->
    <div v-if="showFilterModal" class="modal-overlay" @click="closeFilterModal">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ modalTitle }}</h3>
          <button class="modal-close" @click="closeFilterModal">×</button>
        </div>
        <div class="modal-body">
          <div class="modal-filter-tags">
            <span 
              v-for="item in currentFilterItems" 
              :key="item.id"
              :class="['modal-filter-tag', { active: isItemSelected(item) }]"
              @click="toggleModalItem(item)"
            >
              {{ item.name }}
            </span>
          </div>
        </div>
        <div class="modal-footer">
          <button class="modal-btn modal-btn-secondary" @click="clearCurrentFilter">清除</button>
          <button class="modal-btn modal-btn-primary" @click="applyFilter">确定</button>
        </div>
      </div>
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
import { gameApi, metadataApi } from '../api'
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
// 多选筛选选项（数组）
const selectedGenres = ref([])
const selectedCategories = ref([])
const selectedLanguages = ref([])
// 筛选选项列表
const genres = ref([])
const categories = ref([])
const languages = ref([])
// 弹窗相关
const showFilterModal = ref(false)
const currentFilterType = ref('') // 'genre', 'category', 'language'
const tempSelectedItems = ref([]) // 临时选中的项（在弹窗中）
let searchTimeout = null 

const totalPages = computed(() => Math.ceil(total.value / pageSize.value))

// 弹窗标题
const modalTitle = computed(() => {
  switch (currentFilterType.value) {
    case 'genre':
      return '选择游戏类型'
    case 'category':
      return '选择游戏分类'
    case 'language':
      return '选择支持语言'
    default:
      return '筛选'
  }
})

// 当前筛选项列表
const currentFilterItems = computed(() => {
  switch (currentFilterType.value) {
    case 'genre':
      return genres.value.map(g => ({
        id: g.genreId || g.GenreId,
        name: g.name || g.Name
      }))
    case 'category':
      return categories.value.map(c => ({
        id: c.categoryId || c.CategoryId,
        name: c.name || c.Name
      }))
    case 'language':
      return languages.value.map(l => ({
        id: l.languageId || l.LanguageId,
        name: l.name || l.Name
      }))
    default:
      return []
  }
})

// 判断项是否被选中
const isItemSelected = (item) => {
  return tempSelectedItems.value.includes(item.name)
}

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
      // 传递多个筛选参数（数组）
      if (selectedGenres.value.length > 0) {
        params.genres = selectedGenres.value
      }
      if (selectedCategories.value.length > 0) {
        params.categories = selectedCategories.value
      }
      if (selectedLanguages.value.length > 0) {
        params.languages = selectedLanguages.value
      }
      
      console.log('发送筛选参数:', params)
      console.log('选中的类型:', selectedGenres.value)
      console.log('选中的分类:', selectedCategories.value)
      console.log('选中的语言:', selectedLanguages.value)
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

// 加载筛选选项
const loadFilterOptions = async () => {
  try {
    // 加载类型
    const genresResponse = await metadataApi.getGenres()
    if (genresResponse.success && genresResponse.data) {
      genres.value = genresResponse.data.items || genresResponse.data.Items || []
    }

    // 加载分类
    const categoriesResponse = await metadataApi.getCategories()
    if (categoriesResponse.success && categoriesResponse.data) {
      categories.value = categoriesResponse.data.items || categoriesResponse.data.Items || []
    }

    // 加载语言
    const languagesResponse = await metadataApi.getLanguages()
    if (languagesResponse.success && languagesResponse.data) {
      languages.value = languagesResponse.data.items || languagesResponse.data.Items || []
    }
  } catch (err) {
    console.error('加载筛选选项失败:', err)
  }
}

// 打开筛选弹窗
const openFilterModal = (type) => {
  currentFilterType.value = type
  // 初始化临时选中项为当前已选中的项
  switch (type) {
    case 'genre':
      tempSelectedItems.value = [...selectedGenres.value]
      break
    case 'category':
      tempSelectedItems.value = [...selectedCategories.value]
      break
    case 'language':
      tempSelectedItems.value = [...selectedLanguages.value]
      break
  }
  showFilterModal.value = true
}

// 关闭筛选弹窗
const closeFilterModal = () => {
  showFilterModal.value = false
  currentFilterType.value = ''
  tempSelectedItems.value = []
}

// 在弹窗中切换项选择
const toggleModalItem = (item) => {
  const index = tempSelectedItems.value.indexOf(item.name)
  if (index > -1) {
    tempSelectedItems.value.splice(index, 1)
  } else {
    tempSelectedItems.value.push(item.name)
  }
}

// 清除当前筛选
const clearCurrentFilter = () => {
  tempSelectedItems.value = []
}

// 应用筛选
const applyFilter = () => {
  switch (currentFilterType.value) {
    case 'genre':
      selectedGenres.value = [...tempSelectedItems.value]
      break
    case 'category':
      selectedCategories.value = [...tempSelectedItems.value]
      break
    case 'language':
      selectedLanguages.value = [...tempSelectedItems.value]
      break
  }
  page.value = 1
  loadGames()
  closeFilterModal()
}

// 移除单个筛选项
const removeGenre = (genreName) => {
  const index = selectedGenres.value.indexOf(genreName)
  if (index > -1) {
    selectedGenres.value.splice(index, 1)
    page.value = 1
    loadGames()
  }
}

const removeCategory = (categoryName) => {
  const index = selectedCategories.value.indexOf(categoryName)
  if (index > -1) {
    selectedCategories.value.splice(index, 1)
    page.value = 1
    loadGames()
  }
}

const removeLanguage = (languageName) => {
  const index = selectedLanguages.value.indexOf(languageName)
  if (index > -1) {
    selectedLanguages.value.splice(index, 1)
    page.value = 1
    loadGames()
  }
}

// 清除所有筛选
const clearAllFilters = () => {
  selectedGenres.value = []
  selectedCategories.value = []
  selectedLanguages.value = []
  page.value = 1
  loadGames()
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
  loadFilterOptions()
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
  gap: 12px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

/* 筛选器按钮和当前启用的筛选规则 */
.active-filters {
  margin-bottom: 24px;
}

.filter-buttons {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.filter-btn {
  padding: 10px 20px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.1);
  color: #cbd5e1;
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
  user-select: none;
  display: flex;
  align-items: center;
  gap: 6px;
}

.filter-btn:hover {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.5);
  color: #f8fafc;
  transform: translateY(-1px);
}

.filter-btn.active {
  background: rgba(139, 92, 246, 0.4);
  border-color: #8b5cf6;
  color: #f8fafc;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
}

.filter-count {
  font-size: 12px;
  opacity: 0.8;
}

.active-filter-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.active-tag {
  padding: 6px 12px;
  border: 1px solid rgba(139, 92, 246, 0.4);
  background: rgba(139, 92, 246, 0.2);
  color: #f8fafc;
  border-radius: 6px;
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.remove-tag {
  cursor: pointer;
  font-size: 16px;
  line-height: 1;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.remove-tag:hover {
  opacity: 1;
}

.clear-all-btn {
  padding: 6px 12px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.05);
  color: #94a3b8;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.clear-all-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
  border-color: rgba(255, 255, 255, 0.2);
}

/* 弹窗样式 */
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
  padding: 20px;
}

.modal-content {
  background: rgba(30, 30, 35, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  width: 100%;
  max-width: 600px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
}

.modal-header {
  padding: 20px 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.modal-header h3 {
  margin: 0;
  color: #f8fafc;
  font-size: 18px;
  font-weight: 600;
}

.modal-close {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 28px;
  line-height: 1;
  cursor: pointer;
  padding: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.modal-close:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #f8fafc;
}

.modal-body {
  padding: 24px;
  overflow-y: auto;
  flex: 1;
}

.modal-filter-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.modal-filter-tag {
  padding: 8px 16px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.1);
  color: #cbd5e1;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
  user-select: none;
}

.modal-filter-tag:hover {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.5);
  color: #f8fafc;
  transform: translateY(-1px);
}

.modal-filter-tag.active {
  background: rgba(139, 92, 246, 0.4);
  border-color: #8b5cf6;
  color: #f8fafc;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
}

.modal-footer {
  padding: 16px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.modal-btn {
  padding: 10px 20px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.modal-btn-secondary {
  background: rgba(255, 255, 255, 0.05);
  color: #94a3b8;
}

.modal-btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
  border-color: rgba(255, 255, 255, 0.2);
}

.modal-btn-primary {
  background: rgba(139, 92, 246, 0.4);
  border-color: #8b5cf6;
  color: #f8fafc;
  font-weight: 600;
}

.modal-btn-primary:hover {
  background: rgba(139, 92, 246, 0.6);
  box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
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