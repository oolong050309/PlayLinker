<template>
  <div class="flex-1 overflow-y-auto p-8">
    
    <div class="mb-8">
      <div class="relative max-w-2xl">
        <i data-lucide="search" class="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-zinc-500"></i>
        <input 
          v-model="searchQuery"
          @keyup.enter="handleSearch"
          type="text" 
          placeholder="搜索游戏、题材或平台..." 
          class="w-full bg-white/5 border border-white/10 rounded-2xl pl-12 pr-4 py-4 text-white placeholder-zinc-500 focus:outline-none focus:border-indigo-500 transition-colors"
        >
      </div>
      <div class="flex gap-2 mt-4 overflow-x-auto scroll-hide">
        <button 
          v-for="cat in categories" 
          :key="cat"
          @click="activeCategory = cat"
          class="px-4 py-2 rounded-full text-sm font-medium transition-colors whitespace-nowrap"
          :class="activeCategory === cat ? 'bg-indigo-600 text-white' : 'bg-white/5 text-zinc-400 hover:text-white'"
        >
          {{ cat }}
        </button>
      </div>
    </div>

    <section class="mb-12">
      <div class="flex items-center justify-between mb-6">
        <div>
          <h2 class="text-2xl font-bold mb-1">为你推荐</h2>
          <p class="text-sm text-zinc-400">基于你的游戏偏好和游玩时长</p>
        </div>
        <button @click="loadRecommendations" class="text-sm text-indigo-400 hover:text-indigo-300 flex items-center gap-2">
          刷新 <i data-lucide="refresh-cw" class="w-4 h-4"></i>
        </button>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div 
          v-for="game in recommendations" 
          :key="game.recommendationId"
          class="glass-panel rounded-2xl overflow-hidden group cursor-pointer hover:border-indigo-500/50 transition-all block relative"
          @click="$router.push({ name: 'GameDetail', params: { id: game.gameId } })"
        >
          <div class="relative h-64 overflow-hidden">
            <img :src="game.headerImage" class="w-full h-full object-cover group-hover:scale-110 transition-transform duration-500">
            <div class="absolute top-3 right-3 px-2 py-1 rounded-lg bg-emerald-500 text-white text-xs font-bold">
              {{ Math.round(game.score * 100) }}% 匹配
            </div>
            <div class="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent"></div>
          </div>
          <div class="p-4">
            <div class="flex flex-wrap gap-2 mb-2">
              <span v-for="tag in (game.tags || []).slice(0, 2)" :key="tag" class="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-400 text-xs font-medium">{{ tag }}</span>
            </div>
            <h3 class="font-bold text-lg mb-1 truncate">{{ game.gameName }}</h3>
            <p class="text-sm text-zinc-400 mb-3 line-clamp-2">{{ game.reason }}</p>
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-1 text-amber-400">
                <i data-lucide="star" class="w-4 h-4 fill-current"></i>
                <span class="text-sm font-bold">{{ game.reviewScore }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section class="mb-12" v-if="aiRecommendations.length > 0">
      <div class="glass-panel p-6 rounded-2xl border-2 border-indigo-500/30">
        <div class="flex items-start gap-4 mb-4">
          <div class="p-3 bg-indigo-500/20 rounded-xl">
            <i data-lucide="sparkles" class="w-6 h-6 text-indigo-400"></i>
          </div>
          <div class="flex-1">
            <h3 class="text-lg font-bold mb-2">{{ exploreTitle }}</h3>
            <p class="text-sm text-zinc-400 mb-4">发现更多符合您口味的宝藏游戏：</p>
            
            <div class="grid grid-cols-1 gap-4">
              <div 
                v-for="item in aiRecommendations" 
                :key="item.gameId"
                class="flex flex-col md:flex-row items-center gap-4 p-3 rounded-xl hover:bg-white/5 transition-colors cursor-pointer"
                @click="$router.push({ name: 'GameDetail', params: { id: item.gameId } })"
              >
                <img :src="item.headerImage" class="w-48 h-28 rounded-lg object-cover flex-shrink-0 bg-zinc-800">
                
                <div class="flex-1 w-full text-center md:text-left">
                  <h4 class="font-bold text-lg mb-1">{{ item.gameName }}</h4>
                  <div class="flex gap-2 mb-2 justify-center md:justify-start">
                     <span 
                       v-for="feature in (item.uniqueFeatures || []).slice(0, 3)" 
                       :key="feature"
                       class="px-2 py-0.5 rounded bg-white/10 text-zinc-400 text-xs"
                     >
                       {{ feature }}
                     </span>
                  </div>
                  <p class="text-xs text-zinc-500 line-clamp-1">{{ item.whyExplore }}</p>
                </div>
                
                <div class="flex gap-2">
                  <button class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-sm font-medium whitespace-nowrap">
                    查看详情
                  </button>
                </div>
              </div>
            </div>
            
          </div>
        </div>
      </div>
    </section>
    
    <div v-if="aiRecommendations.length === 0" class="text-center text-zinc-500 py-4">
       [调试] 暂无探索推荐数据，请查看控制台日志。
    </div>

  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import { recommendationApi } from '@/api/recommendation'
import { createIcons, icons } from 'lucide'

const searchQuery = ref('')
const activeCategory = ref('全部')
const categories = ['全部', 'RPG', '动作', 'FPS', '策略', '独立游戏']
const recommendations = ref([])
const aiRecommendations = ref([])
const exploreTitle = ref('AI 智能探索')

const handleSearch = () => {
  console.log('Search:', searchQuery.value)
}

const loadRecommendations = async () => {
  console.log('[Frontend] Starting loadRecommendations...')
  try {
    // 1. 加载常规推荐
    const res = await recommendationApi.getRecommendations({ limit: 4 })
    console.log('[Frontend] GetRecommendations Response:', res)
    if (res.success) {
      recommendations.value = res.data.items || []
    }

    // 2. 加载 AI/探索推荐
    console.log('[Frontend] Calling exploreGames API...')
    const exploreRes = await recommendationApi.exploreGames()
    console.log('[Frontend] ExploreGames Response:', exploreRes)
    
    if (exploreRes.success && exploreRes.data) {
      aiRecommendations.value = exploreRes.data.items || []
      console.log('[Frontend] Loaded explore items:', aiRecommendations.value.length)
      if (exploreRes.data.exploreCategory) {
        exploreTitle.value = exploreRes.data.exploreCategory
      }
    } else {
      console.warn('[Frontend] ExploreGames returned no success flag or empty data.')
    }
  } catch (error) {
    console.error('[Frontend] Failed to load recommendations:', error)
  }
}

onMounted(() => {
  loadRecommendations()
  nextTick(() => createIcons({ icons }))
})
</script>

<style scoped>
.glass-panel {
  background: rgba(255,255,255,0.03);
  border: 1px solid rgba(255,255,255,0.08);
  backdrop-filter: blur(10px);
}
.scroll-hide::-webkit-scrollbar { display: none; }
</style>