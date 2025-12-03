<template>
  <div class="container">
    <h2>游戏排行榜</h2>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else class="card">
      <table class="ranking-table">
        <thead>
          <tr>
            <th>排名</th>
            <th>游戏名称</th>
            <th>上周排名</th>
            <th>峰值玩家数</th>
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
                <span>{{ item.gameName }}</span>
              </div>
            </td>
            <td>{{ item.lastWeekRank || '-' }}</td>
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
  loading.value = true
  error.value = null
  try {
    const response = await gameApi.getRanking({ limit: 100 })
    if (response.success) {
      rankings.value = response.data.items
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

const goToDetail = (id) => {
  router.push(`/games/${id}`)
}

onMounted(() => {
  loadRankings()
})
</script>

<style scoped>
.ranking-table {
  width: 100%;
  border-collapse: collapse;
}

.ranking-table th {
  background-color: #f8f9fa;
  padding: 15px;
  text-align: left;
  font-weight: bold;
  border-bottom: 2px solid #dee2e6;
}

.ranking-table td {
  padding: 15px;
  border-bottom: 1px solid #dee2e6;
}

.ranking-table tbody tr {
  cursor: pointer;
  transition: background-color 0.2s;
}

.ranking-table tbody tr:hover {
  background-color: #f8f9fa;
}

.rank-col {
  text-align: center;
  font-weight: bold;
  font-size: 20px;
}

.rank {
  display: inline-block;
  width: 40px;
  height: 40px;
  line-height: 40px;
  border-radius: 50%;
  background-color: #6c757d;
  color: white;
}

.rank.top-three {
  background: linear-gradient(135deg, #ffd700, #ffed4e);
  color: #333;
}

.rank.top-ten {
  background: linear-gradient(135deg, #c0c0c0, #e8e8e8);
  color: #333;
}

.game-name-col {
  display: flex;
  align-items: center;
  gap: 15px;
}

.game-name-col img {
  width: 60px;
  height: auto;
  border-radius: 5px;
}
</style>

