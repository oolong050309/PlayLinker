<template>
  <div class="flex-1 overflow-y-auto p-8">
    
    <div class="mb-8">
      <div class="flex items-center justify-between mb-4">
        <h2 class="text-2xl font-bold">价格监控</h2>
        <div class="glass-panel px-4 py-3 rounded-2xl">
          <div class="text-sm text-zinc-400 mb-1">潜在总节省</div>
          <div class="text-lg font-bold text-emerald-400">¥{{ totalPotentialSavings.toFixed(2) }}</div>
        </div>
      </div>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div class="glass-panel p-4 rounded-xl border border-emerald-500/30">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-sm text-zinc-400">愿望单游戏</div>
              <div class="text-xl font-bold">{{ wishlist.length }}</div>
            </div>
            <div class="p-2 bg-emerald-500/20 rounded-lg">
              <i data-lucide="gamepad-2" class="w-5 h-5 text-emerald-400"></i>
            </div>
          </div>
        </div>
        <div class="glass-panel p-4 rounded-xl border border-indigo-500/30">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-sm text-zinc-400">活跃提醒</div>
              <div class="text-xl font-bold">{{ activeAlertsCount }}</div>
            </div>
            <div class="p-2 bg-indigo-500/20 rounded-lg">
              <i data-lucide="bell" class="w-5 h-5 text-indigo-400"></i>
            </div>
          </div>
        </div>
        <div class="glass-panel p-4 rounded-xl border border-amber-500/30">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-sm text-zinc-400">当前折扣</div>
              <div class="text-xl font-bold">{{ discountGamesCount }}</div>
            </div>
            <div class="p-2 bg-amber-500/20 rounded-lg">
              <i data-lucide="tag" class="w-5 h-5 text-amber-400"></i>
            </div>
          </div>
        </div>
      </div>
    </div>

    <section class="mb-12">
      <div class="flex items-center justify-between mb-6">
        <h3 class="text-xl font-bold">愿望单监控</h3>
        <button class="text-sm text-indigo-400 hover:text-indigo-300 flex items-center gap-2">
          <i data-lucide="plus" class="w-4 h-4"></i> 添加游戏
        </button>
      </div>
      <div class="space-y-4">
        <div 
          v-for="item in wishlist" 
          :key="item.id"
          class="glass-panel rounded-2xl p-6 border border-white/5 transition-all hover:border-indigo-500/30"
        >
          <div class="flex flex-col md:flex-row gap-6">
            <div class="w-24 h-24 md:w-32 md:h-32 rounded-lg overflow-hidden flex-shrink-0">
              <img :src="item.gameImage" class="w-full h-full object-cover">
            </div>
            <div class="flex-1">
              <div class="flex items-start justify-between mb-3">
                <div>
                  <h4 class="text-lg font-bold mb-1">{{ item.gameName }}</h4>
                  <div class="text-sm text-zinc-400">{{ item.platform }}</div>
                </div>
                <button class="text-zinc-500 hover:text-zinc-300">
                  <i data-lucide="x" class="w-5 h-5"></i>
                </button>
              </div>
              <div class="flex items-center gap-4 mb-4">
                <div>
                  <div class="text-sm text-zinc-400 mb-1">当前价格</div>
                  <div class="text-xl font-bold text-white">¥{{ item.currentPrice.toFixed(2) }}</div>
                </div>
                <div>
                  <div class="text-sm text-zinc-400 mb-1">原价</div>
                  <div class="text-lg text-zinc-500 line-through">¥{{ item.originalPrice.toFixed(2) }}</div>
                </div>
                <div v-if="item.currentPrice < item.originalPrice">
                  <div class="text-sm text-zinc-400 mb-1">节省</div>
                  <div class="text-lg font-bold text-emerald-400">¥{{ (item.originalPrice - item.currentPrice).toFixed(2) }}</div>
                </div>
              </div>
              <div class="flex items-center gap-3">
                <button 
                  class="flex-1 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2"
                >
                  <i data-lucide="bell" class="w-4 h-4"></i>
                  设置提醒
                </button>
                <button class="px-4 py-2 bg-white/5 hover:bg-white/10 text-white rounded-lg text-sm font-medium">
                  <i data-lucide="eye" class="w-4 h-4"></i>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section>
      <div class="flex items-center justify-between mb-6">
        <h3 class="text-xl font-bold">价格提醒</h3>
        <button class="text-sm text-indigo-400 hover:text-indigo-300">
          管理所有 <i data-lucide="chevron-right" class="w-4 h-4 inline-block ml-1"></i>
        </button>
      </div>
      <div class="glass-panel rounded-2xl p-6">
        <div class="space-y-4">
          <div class="flex items-center justify-between p-3 rounded-xl bg-white/5">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg overflow-hidden">
                <img src="https://image.api.playstation.com/vulcan/ap/rnd/202504/2220/5227e8c726a457fcde0b59b32ca996360c2193a85d8e0b3a.jpg" class="w-full h-full object-cover">
              </div>
              <div>
                <div class="font-medium">Tom Clancy's The Division 2</div>
                <div class="text-sm text-zinc-400">当价格低于 ¥199.00 时提醒我</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs">活跃</span>
              <button class="text-zinc-500 hover:text-zinc-300">
                <i data-lucide="x" class="w-4 h-4"></i>
              </button>
            </div>
          </div>
          <div class="flex items-center justify-between p-3 rounded-xl bg-white/5">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg overflow-hidden">
                <img src="https://cdn2.steamgriddb.com/file/sgdb-cdn/icon/872c47b9e80182962ee56a9fa116a31b/32/32.png" class="w-full h-full object-cover">
              </div>
              <div>
                <div class="font-medium">Stardew Valley</div>
                <div class="text-sm text-zinc-400">当价格低于 ¥38.00 时提醒我</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs">活跃</span>
              <button class="text-zinc-500 hover:text-zinc-300">
                <i data-lucide="x" class="w-4 h-4"></i>
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>

  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import { usePriceStore } from '@/stores/price'
import { createIcons, icons } from 'lucide'

const priceStore = usePriceStore()

// 获取store中的状态
const wishlist = priceStore.wishlist
const totalPotentialSavings = priceStore.totalPotentialSavings
const activeAlertsCount = priceStore.activeAlertsCount

// 模拟数据
wishlist.value = [
  {
    id: 1,
    gameName: 'Destiny 2',
    gameImage: 'https://cdn2.steamgriddb.com/file/sgdb-cdn/icon/298020c4c6d075b9e69486045e4c25c1/32/32.png',
    platform: 'Steam',
    currentPrice: 99.00,
    originalPrice: 299.00
  },
  {
    id: 2,
    gameName: 'Cyberpunk 2077',
    gameImage: 'https://cdn2.steamgriddb.com/file/sgdb-cdn/icon/60a54158676f938e243b475c26b56536/32/32.png',
    platform: 'Epic Games',
    currentPrice: 149.00,
    originalPrice: 299.00
  },
  {
    id: 3,
    gameName: 'The Legend of Zelda: Tears of the Kingdom',
    gameImage: 'https://cdn2.steamgriddb.com/file/sgdb-cdn/icon/939c338b4c4745c0b3a39c3a17183c72/32/32.png',
    platform: 'Nintendo Switch',
    currentPrice: 429.00,
    originalPrice: 429.00
  }
]

// 计算折扣游戏数量
const discountGamesCount = ref(wishlist.value.filter(item => item.currentPrice < item.originalPrice).length)

onMounted(() => {
  priceStore.fetchWishlist()
  priceStore.fetchAlerts()
  nextTick(() => createIcons({ icons }))
})
</script>

<style scoped>
.glass-panel {
  background: rgba(255,255,255,0.03);
  border: 1px solid rgba(255,255,255,0.08);
  backdrop-filter: blur(10px);
}
</style>