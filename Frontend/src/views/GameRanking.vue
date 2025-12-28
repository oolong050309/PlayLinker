<template>
  <div class="container">
    <h2 class="page-title">游戏排行榜</h2>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else class="card ranking-card">
      <table class="ranking-table">
        <thead>
          <tr>
            <th class="w-20">排名</th>
            <th>游戏名称</th>
            <th class="w-32">峰值玩家数</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in rankings" :key="item.rankId" @click="goToDetail(item.gameId)">
            <td class="rank-col">
              <span class="rank" :class="getRankClass(item.currentRank)">
                {{ item.currentRank }}
              </span>
            </td>
            <td>
              <div class="game-name-col">
                <img :src="item.headerImage" :alt="item.gameName" />
                <span :title="item.gameName">{{ item.gameName }}</span>
              </div>
            </td>
            <td>{{ formatNumber(item.peakPlayers) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { gameApi } from '../api'

const router = useRouter()
const rankings = ref([])
const loading = ref(false)
const error = ref(null)

const loadRankings = async () => {
  console.log('[Frontend] Starting loadRankings...')
  loading.value = true
  error.value = null
  try {
    const response = await gameApi.getRanking({ limit: 100 })
    console.log('[Frontend] Ranking API Response:', response)

    if (response.success) {
      rankings.value = response.data.items || []
      console.log(`[Frontend] Loaded ${rankings.value.length} ranking items.`)
    } else {
      console.warn('[Frontend] API returned success=false', response)
      error.value = response.message || '加载失败'
    }
  } catch (err) {
    console.error('[Frontend] Error loading rankings:', err)
    error.value = '加载排行榜失败: ' + err.message
  } finally {
    loading.value = false
  }
}

const getRankClass = (rank) => {
  if (rank <= 3) return 'top-three'
  if (rank <= 10) return 'top-ten'
  return ''
}

const formatNumber = (num) => {
  if (!num) return '-'
  return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',')
}

const goToDetail = (id) => {
  router.push(`/games/${id}`)
}

onMounted(() => {
  loadRankings()
})
</script>

<style scoped>
/* 页面标题微调，颜色继承全局 */
.page-title {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: bold;
}

/* * 关键修改：
 * 移除了 .card 的 background: white 等硬编码 
 * 现在它会自动使用 style.css 中的 .card 样式（深色背景 + 模糊效果）
 */
.ranking-card {
  overflow-x: auto; /* 防止小屏幕表格溢出 */
}

.ranking-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed; /* 保持固定布局 */
}

/* 列宽控制 */
.w-20 { width: 80px; }
.w-32 { width: 150px; }

/* 表头样式：继承全局，强制居中 */
.ranking-table th {
  padding: 16px;
  text-align: center; /* 居中 */
  font-weight: 600;
  border-bottom: 1px solid var(--border-color); /* 使用全局变量 */
  color: var(--text-secondary); /* 使用全局变量 */
}

/* 单元格样式：继承全局，强制居中 */
.ranking-table td {
  padding: 16px;
  border-bottom: 1px solid var(--border-color-light); /* 使用全局变量 */
  text-align: center; /* 默认居中 */
  vertical-align: middle;
  color: var(--text-primary); /* 使用全局变量，白色文字 */
}

/* 行悬停效果：继承全局或微调 */
.ranking-table tbody tr {
  cursor: pointer;
  transition: background-color 0.2s;
}

.ranking-table tbody tr:hover {
  background-color: rgba(255, 255, 255, 0.05); /* 深色模式下的悬停高亮 */
}

/* 排名数字样式 */
.rank-col {
  text-align: center;
  font-weight: bold;
  font-size: 20px;
}

.rank {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background-color: rgba(255, 255, 255, 0.1); /* 深色模式下的默认排名背景 */
  color: var(--text-secondary);
  font-size: 16px;
}

.rank.top-three {
  background: linear-gradient(135deg, #f59e0b, #d97706); /* 金色/橙色 */
  color: white;
  box-shadow: 0 2px 8px rgba(245, 158, 11, 0.3);
}

.rank.top-ten {
  background: linear-gradient(135deg, #71717a, #52525b); /* 银灰色 */
  color: white;
}

/* * 核心布局保留：游戏名称列 
 * 1. 外层 text-align: center (由 td 决定)
 * 2. 内部 .game-name-col 使用 margin: 0 auto 居中
 * 3. flex 内容 justify-content: start 左对齐
 */
.game-name-col {
  display: flex;
  align-items: center;
  justify-content: flex-start; /* 内容左对齐 */
  gap: 16px;
  
  width: 300px; 
  max-width: 100%;
  margin: 0 auto; /* 容器整体在单元格内居中 */
}

.game-name-col img {
  width: 60px;
  height: 40px; /* 稍微调整高度比例 */
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
  background-color: var(--bg-secondary); /* 图片加载前的占位色 */
}

.game-name-col span {
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 500;
  color: var(--text-primary); /* 确保文字是亮的 */
}
</style>