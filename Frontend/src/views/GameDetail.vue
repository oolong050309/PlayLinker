<template>
  <div class="container">
    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else-if="game" class="game-detail">
      <div class="game-header">
        <img :src="game.media.headerImage" :alt="game.name" class="header-img" />
        <div class="game-info-header">
          <h1>{{ game.name }}</h1>
          <p class="release-date">发布日期: {{ game.releaseDate }}</p>
          <p class="price">{{ game.isFree ? '免费游戏' : '付费游戏' }}</p>
          <div class="platforms">
            <span v-if="game.platforms.windows">Windows</span>
            <span v-if="game.platforms.mac">Mac</span>
            <span v-if="game.platforms.linux">Linux</span>
          </div>
        </div>
      </div>

      <div class="card">
        <h3>游戏简介</h3>
        <p>{{ game.shortDescription }}</p>
      </div>

      <div class="card" v-if="game.detailedDescription">
        <h3>详细描述</h3>
        <div v-html="game.detailedDescription"></div>
      </div>

      <div class="card">
        <h3>游戏评价</h3>
        <p>评分: {{ game.reviews.score }}分 - {{ game.reviews.scoreDesc }}</p>
        <p>总评价数: {{ game.reviews.totalReviews }}</p>
        <p>好评数: {{ game.reviews.totalPositive }}</p>
      </div>

      <div class="card" v-if="game.genres.length > 0">
        <h3>游戏题材</h3>
        <div class="tags">
          <span v-for="genre in game.genres" :key="genre.genreId" class="tag">
            {{ genre.name }}
          </span>
        </div>
      </div>

      <div class="card" v-if="game.developers.length > 0">
        <h3>开发商</h3>
        <p>{{ game.developers.map(d => d.name).join(', ') }}</p>
      </div>

      <div class="card" v-if="game.publishers.length > 0">
        <h3>发行商</h3>
        <p>{{ game.publishers.map(p => p.name).join(', ') }}</p>
      </div>

      <div class="card">
        <h3>系统需求</h3>
        <div class="requirements">
          <div v-if="game.requirements.pcMinimum">
            <h4>PC最低配置</h4>
            <p>{{ game.requirements.pcMinimum }}</p>
          </div>
          <div v-if="game.requirements.pcRecommended">
            <h4>PC推荐配置</h4>
            <p>{{ game.requirements.pcRecommended }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { gameApi } from '../api'

const route = useRoute()
const game = ref(null)
const loading = ref(false)
const error = ref(null)

const loadGameDetail = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await gameApi.getGame(route.params.id)
    if (response.success) {
      game.value = response.data
    }
  } catch (err) {
    error.value = '加载游戏详情失败: ' + err.message
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadGameDetail()
})
</script>

<style scoped>
.game-header {
  display: flex;
  gap: 30px;
  margin-bottom: 30px;
}

.header-img {
  width: 460px;
  height: auto;
  border-radius: 8px;
}

.game-info-header h1 {
  margin-bottom: 15px;
}

.platforms {
  display: flex;
  gap: 10px;
  margin-top: 15px;
}

.platforms span {
  padding: 5px 15px;
  background-color: #007bff;
  color: white;
  border-radius: 5px;
  font-size: 14px;
}

.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.tag {
  padding: 5px 15px;
  background-color: #e9ecef;
  border-radius: 5px;
  font-size: 14px;
}

.requirements {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.requirements h4 {
  margin-bottom: 10px;
  color: #007bff;
}
</style>

