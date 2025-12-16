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
              <span v-for="tag in game.tags.slice(0, 2)" :key="tag" class="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-400 text-xs font-medium">{{ tag }}</span>
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

    <section class="mb-12">
      <div class="glass-panel p-6 rounded-2xl border-2 border-indigo-500/30">
        <div class="flex items-start gap-4 mb-4">
          <div class="p-3 bg-indigo-500/20 rounded-xl">
            <i data-lucide="sparkles" class="w-6 h-6 text-indigo-400"></i>
          </div>
          <div class="flex-1">
            <h3 class="text-lg font-bold mb-2">AI 智能推荐</h3>
            <p class="text-sm text-zinc-400 mb-4">基于你在《Destiny 2》的 850 小时游玩记录以及对 "Looter Shooter" 的偏好：</p>
            <div class="flex flex-col md:flex-row items-center gap-4">
              <img src="https://image.api.playstation.com/vulcan/ap/rnd/202504/2220/5227e8c726a457fcde0b59b32ca996360c2193a85d8e0b3a.jpg" class="w-20 h-20 rounded-lg object-cover">
              <div class="flex-1">
                <h4 class="font-bold text-lg mb-1">Tom Clancy's The Division 2</h4>
                <div class="flex gap-2 mb-1">
                   <span class="px-2 py-0.5 rounded bg-white/10 text-zinc-400 text-xs">RPG</span>
                   <span class="px-2 py-0.5 rounded bg-white/10 text-zinc-400 text-xs">Co-op</span>
                </div>
              </div>
              <div class="flex gap-2">
                <button class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-sm font-medium">查看详情</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

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

const handleSearch = () => {
  // 搜索逻辑
  console.log('Search:', searchQuery.value)
}

const loadRecommendations = async () => {
  try {
    const res = await recommendationApi.getRecommendations({ limit: 4 })
    if (res.success) {
      recommendations.value = res.data.items
    }
  } catch (error) {
    console.error(error)
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