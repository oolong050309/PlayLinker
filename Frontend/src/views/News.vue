<template>
  <div class="container">
    <h2>游戏新闻</h2>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <div v-for="newsItem in newsList" :key="newsItem.newsId" class="news-card">
        <h3>{{ newsItem.title }}</h3>
        <div class="news-meta">
          <span>作者: {{ newsItem.author }}</span>
          <span>日期: {{ formatDate(newsItem.date) }}</span>
        </div>
        <p class="news-content">{{ newsItem.contents }}</p>
        <div v-if="newsItem.relatedGames && newsItem.relatedGames.length > 0" class="related-games">
          <span>相关游戏: </span>
          <span v-for="game in newsItem.relatedGames" :key="game.gameId" class="game-tag">
            {{ game.gameName }}
          </span>
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
        <span>第 {{ page }} 页</span>
        <button @click="changePage(page + 1)" class="btn btn-primary">
          下一页
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { newsApi } from '../api'

const newsList = ref([])
const loading = ref(false)
const error = ref(null)
const page = ref(1)
const pageSize = ref(20)

const loadNews = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await newsApi.getNews({
      page: page.value,
      page_size: pageSize.value
    })
    if (response.success) {
      newsList.value = response.data.items
    }
  } catch (err) {
    error.value = '加载新闻失败: ' + err.message
  } finally {
    loading.value = false
  }
}

const formatDate = (timestamp) => {
  const date = new Date(timestamp * 1000)
  return date.toLocaleDateString('zh-CN')
}

const changePage = (newPage) => {
  page.value = newPage
  loadNews()
  window.scrollTo(0, 0)
}

onMounted(() => {
  loadNews()
})
</script>

<style scoped>
.news-card {
  background: white;
  border-radius: 8px;
  padding: 20px;
  margin-bottom: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.news-card h3 {
  margin-bottom: 10px;
  color: #333;
}

.news-meta {
  display: flex;
  gap: 20px;
  font-size: 14px;
  color: #666;
  margin-bottom: 15px;
}

.news-content {
  line-height: 1.6;
  color: #555;
  margin-bottom: 15px;
}

.related-games {
  font-size: 14px;
  color: #666;
}

.game-tag {
  display: inline-block;
  padding: 3px 10px;
  background-color: #e9ecef;
  border-radius: 3px;
  margin-left: 5px;
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 20px;
  margin-top: 30px;
}
</style>

