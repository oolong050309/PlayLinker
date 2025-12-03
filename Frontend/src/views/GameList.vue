<template>
  <div class="container">
    <h2>游戏列表</h2>

    <div class="filters">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="搜索游戏..."
        class="search-input"
        @input="handleSearch"
      />
      <select v-model="sortBy" @change="loadGames" class="filter-select">
        <option value="">默认排序</option>
        <option value="release_date">发布日期</option>
        <option value="name">游戏名称</option>
        <option value="popularity">人气</option>
      </select>
    </div>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <div class="grid">
        <div
          v-for="game in games"
          :key="game.gameId"
          class="game-card"
          @click="goToDetail(game.gameId)"
        >
          <img :src="game.headerImage" :alt="game.name" />
          <div class="game-card-body">
            <h3 class="game-title">{{ game.name }}</h3>
            <p class="game-info">发布日期: {{ game.releaseDate }}</p>
            <p class="game-info">
              {{ game.isFree ? '免费游戏' : '付费游戏' }}
            </p>
            <p class="game-info">评分: {{ game.reviewScore }}分</p>
          </div>
        </div>
      </div>

      <div class="pagination">
        <button
          @click="changePage(page - 1)"
          :disabled="page === 1"
          class="btn btn-primary"
        >
          上一页
        </button>
        <span>第 {{ page }} 页 / 共 {{ totalPages }} 页</span>
        <button
          @click="changePage(page + 1)"
          :disabled="page >= totalPages"
          class="btn btn-primary"
        >
          下一页
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { gameApi } from '../api'

const router = useRouter()
const games = ref([])
const loading = ref(false)
const error = ref(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const searchQuery = ref('')
const sortBy = ref('')

const totalPages = computed(() => Math.ceil(total.value / pageSize.value))

const loadGames = async () => {
  loading.value = true
  error.value = null
  try {
    const params = {
      page: page.value,
      page_size: pageSize.value
    }
    if (sortBy.value) {
      params.sort_by = sortBy.value
    }

    const response = await gameApi.getGames(params)
    if (response.success) {
      games.value = response.data.items
      total.value = response.data.meta.total
    }
  } catch (err) {
    error.value = '加载游戏列表失败: ' + err.message
  } finally {
    loading.value = false
  }
}

const handleSearch = async () => {
  if (searchQuery.value.trim()) {
    try {
      const response = await gameApi.searchGames({
        q: searchQuery.value,
        page: page.value,
        page_size: pageSize.value
      })
      if (response.success) {
        games.value = response.data.items
        total.value = response.data.meta.total
      }
    } catch (err) {
      error.value = '搜索失败: ' + err.message
    }
  } else {
    loadGames()
  }
}

const changePage = (newPage) => {
  page.value = newPage
  loadGames()
  window.scrollTo(0, 0)
}

const goToDetail = (id) => {
  router.push(`/games/${id}`)
}

onMounted(() => {
  loadGames()
})
</script>

<style scoped>
h2 {
  margin-bottom: 20px;
  color: #333;
}

.filters {
  display: flex;
  gap: 15px;
  margin-bottom: 30px;
}

.search-input,
.filter-select {
  padding: 10px 15px;
  border: 1px solid #ddd;
  border-radius: 5px;
  font-size: 14px;
}

.search-input {
  flex: 1;
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 20px;
  margin-top: 30px;
}

.pagination button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>

