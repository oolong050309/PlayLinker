<template>
  <div class="discover-container">
    <div class="header-control">
      <button 
        @click="loadRecommendations(true)" 
        class="refresh-btn" 
        :disabled="aiRecommendationsLoading"
      >
        <RefreshCw class="icon" size="16" :class="{ 'spin': aiRecommendationsLoading }" />
        {{ aiRecommendationsLoading ? '正在分析数据...' : '刷新全部推荐' }}
      </button>
    </div>

    <section class="section-card">
      <div class="section-header">
        <div class="section-header-left">
          <div class="ai-icon-wrapper-small">
            <Sparkles class="ai-icon-small" size="20" />
          </div>
          <div>
            <h2 class="section-title">{{ aiTitle }}</h2>
            <p class="section-subtitle">基于大模型深度分析您的偏好</p>
          </div>
        </div>
      </div>

      <div v-if="aiRecommendationsLoading && aiItems.length === 0" class="loading-small">
        <div class="loading-spinner-small"></div>
        <span>AI 正在思考中...</span>
      </div>

      <div v-else-if="aiItems.length === 0" class="empty-state">
        <p>AI 暂时没有灵感，请稍后再试</p>
      </div>

      <div v-else class="recommendations-grid">
        <div v-for="item in aiItems" :key="item.recommendationId" class="game-card ai-game-card" :style="{ animationDelay: `0.1s` }">
          <div class="game-card-image" @click="$router.push({ name: 'GameDetail', params: { id: item.gameId } })">
            <img :src="item.headerImage || noCoverImage" :alt="item.gameName" @error="handleImageError" />
            <div class="game-card-overlay">
              <div class="ai-badge"><span>AI 甄选</span></div>
            </div>
          </div>
          <div class="game-card-content">
            <h3 class="game-card-title">{{ item.gameName }}</h3>
            <div class="reason-box"><p class="game-card-reason">{{ item.whyExplore }}</p></div>
            <div class="game-card-footer">
              <div class="spacer"></div> <div class="feedback-actions">
                <button class="action-btn like-btn" :class="{ active: item.userFeedback === 1 }" @click="handleFeedback(item, 1)"><ThumbsUp size="14" /></button>
                <button class="action-btn dislike-btn" :class="{ active: item.userFeedback === 2 }" @click="handleFeedback(item, 2)"><ThumbsDown size="14" /></button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section class="section-card">
      <div class="section-header">
        <div class="section-header-left">
          <div class="rule-icon-wrapper-small">
            <Zap class="rule-icon-small" size="20" />
          </div>
          <div>
            <h2 class="section-title">{{ ruleTitle }}</h2>
            <p class="section-subtitle">基于您的游戏库风格或当前热门</p>
          </div>
        </div>
      </div>

      <div v-if="aiRecommendationsLoading && ruleItems.length === 0" class="loading-small">
        <span>加载中...</span>
      </div>

      <div v-else class="recommendations-grid">
        <div v-for="item in ruleItems" :key="item.recommendationId" class="game-card rule-game-card">
          <div class="game-card-image" @click="$router.push({ name: 'GameDetail', params: { id: item.gameId } })">
            <img :src="item.headerImage || noCoverImage" :alt="item.gameName" @error="handleImageError" />
          </div>
          <div class="game-card-content">
            <h3 class="game-card-title">{{ item.gameName }}</h3>
            <p class="game-card-simple-reason">{{ item.whyExplore }}</p>
            <div class="game-card-footer">
              <div class="spacer"></div> <div class="feedback-actions">
                <button class="action-btn like-btn" :class="{ active: item.userFeedback === 1 }" @click="handleFeedback(item, 1)"><ThumbsUp size="14" /></button>
                <button class="action-btn dislike-btn" :class="{ active: item.userFeedback === 2 }" @click="handleFeedback(item, 2)"><ThumbsDown size="14" /></button>
              </div>
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
import { RefreshCw, Sparkles, ThumbsUp, ThumbsDown, Zap } from 'lucide-vue-next'
import noCoverImage from '@/assets/no_cover.png'

const aiItems = ref([])
const ruleItems = ref([])
const aiRecommendationsLoading = ref(false)
const aiTitle = ref('AI 智能探索')
const ruleTitle = ref('热门精选')

const handleImageError = (e) => { e.target.src = noCoverImage }

const loadRecommendations = async (refresh = false) => {
  aiRecommendationsLoading.value = true
  try {
    const res = await recommendationApi.exploreGames({ refresh })
    if (res.success && res.data) {
      aiItems.value = res.data.aiItems || []
      ruleItems.value = res.data.ruleItems || []
      if (res.data.aiCategory) aiTitle.value = res.data.aiCategory
      if (res.data.ruleCategory) ruleTitle.value = res.data.ruleCategory
    }
  } catch (error) {
    console.error('Failed to load recommendations', error)
  } finally {
    aiRecommendationsLoading.value = false
  }
}

const handleFeedback = async (item, result) => {
  const newResult = item.userFeedback === result ? 0 : result
  const oldResult = item.userFeedback
  item.userFeedback = newResult
  try {
    await recommendationApi.submitFeedback(item.recommendationId, {
      feedbackResult: newResult,
      remark: newResult === 1 ? "Click Like" : "Click Dislike"
    })
  } catch (error) {
    item.userFeedback = oldResult
  }
}

onMounted(() => {
  loadRecommendations(false)
})
</script>

<style scoped>
.discover-container { min-height: 100vh; background: #0f0f13; color: #f8fafc; padding: 24px; }

/* 头部控制栏：现在只包含刷新按钮 */
.header-control { display: flex; justify-content: flex-end; margin-bottom: 20px; }

.section-card { background: rgba(20, 20, 23, 0.5); border: 1px solid rgba(255, 255, 255, 0.05); border-radius: 12px; padding: 24px; margin-bottom: 30px; }
.section-header { display: flex; align-items: center; margin-bottom: 20px; }
.section-header-left { display: flex; gap: 12px; align-items: center; }
.section-title { font-size: 20px; font-weight: 600; margin: 0; color: #fff; }
.section-subtitle { font-size: 13px; color: #94a3b8; margin: 0; }

.ai-icon-wrapper-small { padding: 8px; background: rgba(139, 92, 246, 0.2); border-radius: 8px; color: #a78bfa; }
.rule-icon-wrapper-small { padding: 8px; background: rgba(245, 158, 11, 0.2); border-radius: 8px; color: #fbbf24; }

.recommendations-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 20px; }

.game-card { background: rgba(30, 30, 35, 0.6); border-radius: 12px; overflow: hidden; transition: transform 0.2s; border: 1px solid rgba(255,255,255,0.05); }
.game-card:hover { transform: translateY(-4px); }
.ai-game-card { border-color: rgba(139, 92, 246, 0.3); } 
.rule-game-card { border-color: rgba(245, 158, 11, 0.3); } 

.game-card-image { position: relative; height: 160px; cursor: pointer; }
.game-card-image img { width: 100%; height: 100%; object-fit: cover; }
.ai-badge { position: absolute; top: 8px; right: 8px; background: #8b5cf6; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px; }

.game-card-content { padding: 14px; }
.game-card-title { font-size: 16px; font-weight: 600; margin-bottom: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.reason-box { background: rgba(139, 92, 246, 0.1); border-left: 2px solid #8b5cf6; padding: 6px; margin-bottom: 10px; border-radius: 0 4px 4px 0; }
.game-card-reason { font-size: 12px; color: #ddd; margin: 0; line-height: 1.4; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.game-card-simple-reason { font-size: 12px; color: #94a3b8; margin-bottom: 10px; }

/* 底部区域调整：移除评分后，确保反馈按钮靠右对齐 */
.game-card-footer { display: flex; justify-content: space-between; align-items: center; }
.spacer { flex: 1; } /* 占位符，把反馈按钮推到右边 */

.feedback-actions { display: flex; gap: 6px; }
.action-btn { background: transparent; border: 1px solid rgba(255,255,255,0.1); color: #64748b; padding: 4px; border-radius: 4px; cursor: pointer; display: flex; }
.action-btn:hover { background: rgba(255,255,255,0.1); }
.like-btn.active { color: #10b981; border-color: #10b981; background: rgba(16, 185, 129, 0.1); }
.dislike-btn.active { color: #ef4444; border-color: #ef4444; background: rgba(239, 68, 68, 0.1); }

.refresh-btn { display: inline-flex; align-items: center; gap: 6px; padding: 8px 16px; background: #8b5cf6; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
.refresh-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.loading-small { text-align: center; color: #94a3b8; padding: 20px; display: flex; justify-content: center; gap: 10px; }
.loading-spinner-small { width: 16px; height: 16px; border: 2px solid #8b5cf6; border-top-color: transparent; border-radius: 50%; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
</style>