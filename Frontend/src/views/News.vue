<template>
  <div class="news-container">
    <header class="header">
      <h1>游戏新闻</h1>
      <p>查看最新的游戏资讯和更新</p>
    </header>

    <div v-if="loading" class="loading-small">
      <div class="loading-spinner-small"></div>
      <span>加载新闻中...</span>
    </div>
    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
    </div>
    <div v-else-if="newsList.length === 0" class="empty-state">
      <p>暂无新闻</p>
    </div>
    <div v-else class="news-list">
      <div 
        v-for="newsItem in newsList" 
        :key="newsItem.newsId || newsItem.NewsId"
        class="news-item"
      >
        <div class="news-header">
          <h3 class="news-title">
            <a 
              href="javascript:void(0)"
              @click="openNewsModal(newsItem)"
              class="news-link"
            >
              {{ newsItem.title || newsItem.Title }}
            </a>
          </h3>
          <div class="news-meta">
            <span v-if="newsItem.author || newsItem.Author" class="news-author">
              作者: {{ newsItem.author || newsItem.Author }}
            </span>
            <span v-if="newsItem.date || newsItem.Date" class="news-date">
              日期: {{ formatNewsDate(newsItem.date || newsItem.Date) }}
            </span>
          </div>
        </div>
        <div 
          v-if="newsItem.contents || newsItem.Contents" 
          class="news-content"
        >
          {{ formatNewsContent(newsItem.contents || newsItem.Contents) }}
        </div>
        <div v-if="newsItem.relatedGames && newsItem.relatedGames.length > 0" class="related-games">
          <span>相关游戏: </span>
          <span v-for="game in newsItem.relatedGames" :key="game.gameId || game.GameId" class="game-tag">
            {{ game.gameName || game.GameName }}
          </span>
        </div>
      </div>

      <Pagination
        :current-page="page"
        :total-pages="totalPages"
        @page-change="changePage"
      />
    </div>

    <!-- 新闻详情弹窗 -->
    <div v-if="showNewsModal" class="news-modal-overlay" @click.self="closeNewsModal">
      <div class="news-modal">
        <div class="news-modal-header">
          <h2 class="news-modal-title">{{ currentNews?.title || currentNews?.Title || '新闻详情' }}</h2>
          <button @click="closeNewsModal" class="news-modal-close">
            <X size="24" />
          </button>
        </div>
        <div class="news-modal-content">
          <div v-if="newsDetailLoading" class="loading-small">
            <div class="loading-spinner-small"></div>
            <span>加载中...</span>
          </div>
          <div v-else class="news-detail">
            <div class="news-detail-meta">
              <span v-if="currentNews?.author || currentNews?.Author" class="news-detail-author">
                作者: {{ currentNews.author || currentNews.Author }}
              </span>
              <span v-if="currentNews?.date || currentNews?.Date" class="news-detail-date">
                日期: {{ formatNewsDate(currentNews.date || currentNews.Date) }}
              </span>
            </div>
            <div class="news-detail-body" v-html="newsDetailContent || (currentNews?.contents || currentNews?.Contents || '暂无内容')"></div>
            <div v-if="currentNews?.relatedGames && currentNews.relatedGames.length > 0" class="news-detail-related">
              <span>相关游戏: </span>
              <span v-for="game in currentNews.relatedGames" :key="game.gameId || game.GameId" class="game-tag">
                {{ game.gameName || game.GameName }}
              </span>
            </div>
          </div>
        </div>
        <div class="news-modal-footer">
          <button 
            v-if="currentNews?.newsUrl || currentNews?.NewsUrl"
            @click="openOriginalNews"
            class="btn-primary"
          >
            显示原文
          </button>
          <button @click="closeNewsModal" class="btn-secondary">
            关闭
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { newsApi } from '../api'
import { X } from 'lucide-vue-next'
import Pagination from '@/components/common/Pagination.vue'

const newsList = ref([])
const loading = ref(false)
const error = ref(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

// 新闻详情弹窗状态
const showNewsModal = ref(false)
const currentNews = ref(null)
const newsDetailLoading = ref(false)
const newsDetailContent = ref('')

const hasMore = computed(() => {
  return page.value * pageSize.value < total.value
})

const totalPages = computed(() => {
  return Math.ceil(total.value / pageSize.value) || 1
})

const loadNews = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await newsApi.getNews({
      page: page.value,
      page_size: pageSize.value
    })
    if (response.success && response.data) {
      const data = response.data
      newsList.value = data.items || data.Items || []
      const meta = data.meta || data.Meta
      if (meta) {
        total.value = meta.total || meta.Total || 0
      }
    } else {
      error.value = '加载新闻失败'
    }
  } catch (err) {
    error.value = '加载新闻失败: ' + (err.message || '未知错误')
    console.error('加载新闻失败:', err)
  } finally {
    loading.value = false
  }
}

// 格式化新闻日期
const formatNewsDate = (timestamp) => {
  if (!timestamp) return ''
  const date = new Date(timestamp * 1000) // Steam 日期是 Unix 时间戳（秒）
  const now = new Date()
  const diff = now - date
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  
  if (days === 0) {
    const hours = Math.floor(diff / (1000 * 60 * 60))
    if (hours === 0) {
      const minutes = Math.floor(diff / (1000 * 60))
      return minutes <= 0 ? '刚刚' : `${minutes} 分钟前`
    }
    return `${hours} 小时前`
  } else if (days < 7) {
    return `${days} 天前`
  } else {
    return date.toLocaleDateString('zh-CN', { 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric' 
    })
  }
}

// 格式化新闻内容（截取前200字符）
const formatNewsContent = (content) => {
  if (!content) return ''
  // 移除 HTML 标签，只保留文本用于预览
  const text = content.replace(/<[^>]*>/g, '')
  // 截取前200字符
  if (text.length > 200) {
    return text.substring(0, 200) + '...'
  }
  return text
}

// 打开新闻详情弹窗
const openNewsModal = async (news) => {
  currentNews.value = news
  showNewsModal.value = true
  newsDetailLoading.value = true
  newsDetailContent.value = ''
  
  try {
    // 尝试获取新闻详情（如果有 newsId）
    const newsId = news.newsId || news.NewsId
    if (newsId) {
      const response = await newsApi.getNewsDetail(newsId)
      if (response.success && response.data) {
        const data = response.data
        newsDetailContent.value = data.contents || data.Contents || news.contents || news.Contents || ''
        // 更新当前新闻数据（包含相关游戏等信息）
        if (data.relatedGames) {
          currentNews.value = { ...currentNews.value, relatedGames: data.relatedGames }
        }
      } else {
        // 如果获取详情失败，使用列表中的内容
        newsDetailContent.value = news.contents || news.Contents || ''
      }
    } else {
      // 没有 newsId，直接使用列表中的内容
      newsDetailContent.value = news.contents || news.Contents || ''
    }
  } catch (err) {
    console.error('加载新闻详情失败:', err)
    // 失败时使用列表中的内容
    newsDetailContent.value = news.contents || news.Contents || ''
  } finally {
    newsDetailLoading.value = false
  }
}

// 关闭新闻详情弹窗
const closeNewsModal = () => {
  showNewsModal.value = false
  currentNews.value = null
  newsDetailContent.value = ''
}

// 打开原文链接
const openOriginalNews = () => {
  const url = currentNews.value?.newsUrl || currentNews.value?.NewsUrl
  if (url) {
    window.open(url, '_blank')
  }
}

const changePage = (newPage) => {
  if (newPage >= 1 && (!hasMore.value || newPage <= Math.ceil(total.value / pageSize.value))) {
    page.value = newPage
    loadNews()
    window.scrollTo(0, 0)
  }
}

onMounted(() => {
  loadNews()
})
</script>

<style scoped>
.news-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 24px;
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

.loading-small,
.empty-state,
.error-state {
  text-align: center;
  padding: 60px 20px;
  color: #94a3b8;
}

.loading-spinner-small {
  width: 24px;
  height: 24px;
  border: 3px solid rgba(139, 92, 246, 0.2);
  border-top-color: #8b5cf6;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 12px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

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

.btn-primary {
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
  background: #8b5cf6;
  color: white;
}

.btn-primary:hover {
  background: #7c3aed;
  box-shadow: 0 0 20px rgba(139, 92, 246, 0.4);
}

.btn-secondary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 12px 24px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  background: rgba(20, 20, 23, 0.75);
  color: #94a3b8;
}

.btn-secondary:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #f8fafc;
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
