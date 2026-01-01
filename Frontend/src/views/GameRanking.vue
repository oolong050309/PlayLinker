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
              <div class="trend" v-if="item.lastWeekRank">
                 <span v-if="item.lastWeekRank > item.currentRank" class="up">▲ {{ item.lastWeekRank - item.currentRank }}</span>
                 <span v-else-if="item.lastWeekRank < item.currentRank" class="down">▼ {{ item.currentRank - item.lastWeekRank }}</span>
                 <span v-else class="flat">-</span>
              </div>
              <div class="trend new" v-else>
                  NEW
              </div>
            </td>
            <td>
              <div class="game-name-col">
                <img :src="item.headerImage || noCoverImage" :alt="item.gameName" @error="handleImageError" />
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
import noCoverImage from '@/assets/no_cover.png'

const router = useRouter()
const rankings = ref([])
const loading = ref(false)
const error = ref(null)

const loadRankings = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await gameApi.getRanking({ limit: 100 })
    if (response.success) {
      rankings.value = response.data.items || []
    } else {
      error.value = response.message || '加载失败'
    }
  } catch (err) {
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

// [修复] 使用命名路由跳转，确保匹配 /game/:id
const goToDetail = (id) => {
  router.push({ name: 'GameDetail', params: { id } })
}

const handleImageError = (e) => {
  e.target.src = noCoverImage
}

onMounted(() => {
  loadRankings()
})
</script>

<style scoped>
.page-title {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: bold;
}

.ranking-card {
  overflow-x: auto;
}

.ranking-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.w-20 { width: 80px; }
.w-32 { width: 150px; }

.ranking-table th {
  padding: 16px;
  text-align: center;
  font-weight: 600;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-secondary);
}

.ranking-table td {
  padding: 16px;
  border-bottom: 1px solid var(--border-color-light);
  text-align: center;
  vertical-align: middle;
  color: var(--text-primary);
}

.ranking-table tbody tr {
  cursor: pointer;
  transition: background-color 0.2s;
}

.ranking-table tbody tr:hover {
  background-color: rgba(255, 255, 255, 0.05);
}

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
  background-color: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
  font-size: 16px;
}

.rank.top-three {
  background: linear-gradient(135deg, #f59e0b, #d97706);
  color: white;
  box-shadow: 0 2px 8px rgba(245, 158, 11, 0.3);
}

.rank.top-ten {
  background: linear-gradient(135deg, #71717a, #52525b);
  color: white;
}

.game-name-col {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 16px;
  width: 300px; 
  max-width: 100%;
  margin: 0 auto;
}

.game-name-col img {
  width: 60px;
  height: 40px;
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
  background-color: var(--bg-secondary);
}

.game-name-col span {
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 500;
  color: var(--text-primary);
}

.trend {
  font-size: 12px;
  margin-top: 4px;
}
.trend .up { color: #10b981; }
.trend .down { color: #ef4444; }
.trend .flat { color: #94a3b8; }
.trend.new { color: #f59e0b; font-weight: bold; }
</style>