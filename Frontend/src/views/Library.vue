<template>
  <div class="container">
    <h2>我的游戏库</h2>

    <div class="card">
      <h3>游戏库概览</h3>
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-value">{{ overview.totalGamesOwned }}</div>
          <div class="stat-label">拥有游戏</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ overview.gamesPlayed }}</div>
          <div class="stat-label">已玩游戏</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ formatPlaytime(overview.totalPlaytimeMinutes) }}</div>
          <div class="stat-label">总游戏时长</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ overview.unlockedAchievements }} / {{ overview.totalAchievements }}</div>
          <div class="stat-label">成就解锁</div>
        </div>
      </div>
    </div>

    <div class="card">
      <p class="info-text">注: 此功能需要登录并绑定游戏平台账号</p>
      <button class="btn btn-primary">绑定Steam账号</button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { libraryApi } from '../api'

const overview = ref({
  totalGamesOwned: 0,
  gamesPlayed: 0,
  totalPlaytimeMinutes: 0,
  totalAchievements: 0,
  unlockedAchievements: 0,
  recentlyPlayedCount: 0,
  recentPlaytimeMinutes: 0,
  platformStats: [],
  genreDistribution: []
})

const loadOverview = async () => {
  try {
    const response = await libraryApi.getOverview()
    if (response.success) {
      overview.value = response.data
    }
  } catch (err) {
    console.error('加载游戏库概览失败:', err)
  }
}

const formatPlaytime = (minutes) => {
  const hours = Math.floor(minutes / 60)
  return `${hours}小时`
}

onMounted(() => {
  loadOverview()
})
</script>

<style scoped>
.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 20px;
  margin-top: 20px;
}

.stat-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 30px;
  border-radius: 8px;
  text-align: center;
}

.stat-value {
  font-size: 32px;
  font-weight: bold;
  margin-bottom: 10px;
}

.stat-label {
  font-size: 14px;
  opacity: 0.9;
}

.info-text {
  margin-bottom: 15px;
  color: #666;
}
</style>

