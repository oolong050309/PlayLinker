<template>
  <div class="flex-1 overflow-y-auto p-8">
    
    <div class="mb-8">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h2 class="text-2xl font-bold">价格监控</h2>
          <div class="text-sm text-zinc-400 mt-1 flex items-center gap-2">
            <i data-lucide="clock" class="w-4 h-4"></i>
            <span>最后更新: {{ lastUpdateTime || '暂无数据' }}</span>
            <span v-if="monitoringStatus?.isTodayUpdated" class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs ml-2">
              今日已更新
            </span>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <div class="glass-panel px-4 py-3 rounded-2xl">
            <div class="text-sm text-zinc-400 mb-1">潜在总节省</div>
            <div class="text-lg font-bold text-emerald-400">¥{{ totalPotentialSavings.toFixed(2) }}</div>
          </div>
          <button 
            @click="triggerUpdate" 
            :disabled="updating"
            class="glass-panel px-4 py-2 rounded-xl hover:bg-white/5 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <i data-lucide="refresh-cw" :class="['w-4 h-4', updating && 'animate-spin']"></i>
            <span>手动更新</span>
          </button>
        </div>
      </div>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
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
              <div class="text-sm text-zinc-400">今日更新</div>
              <div class="text-xl font-bold">{{ monitoringStatus?.todayRecordCount || 0 }}</div>
              <div class="text-xs text-zinc-500 mt-1">
                进度: {{ Math.round(monitoringStatus?.updateProgress || 0) }}%
              </div>
            </div>
            <div class="p-2 bg-amber-500/20 rounded-lg">
              <i data-lucide="database" class="w-5 h-5 text-amber-400"></i>
            </div>
          </div>
        </div>
        <div class="glass-panel p-4 rounded-xl border border-purple-500/30">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-sm text-zinc-400">价格变化</div>
              <div class="text-xl font-bold">{{ monitoringStatus?.priceChangedCount || 0 }}</div>
              <div class="text-xs text-zinc-500 mt-1">今日</div>
            </div>
            <div class="p-2 bg-purple-500/20 rounded-lg">
              <i data-lucide="trending-down" class="w-5 h-5 text-purple-400"></i>
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
      <div v-if="loading" class="flex items-center justify-center py-12">
        <i data-lucide="loader-2" class="w-8 h-8 animate-spin text-zinc-400"></i>
      </div>
      <div v-else-if="wishlist.length === 0" class="glass-panel rounded-2xl p-12 text-center">
        <i data-lucide="inbox" class="w-16 h-16 text-zinc-500 mx-auto mb-4"></i>
        <p class="text-zinc-400">愿望单为空，快去添加你感兴趣的游戏吧！</p>
      </div>
      <div v-else class="space-y-4">
        <div 
          v-for="item in wishlist" 
          :key="item.gameId || item.id"
          class="glass-panel rounded-2xl p-6 border border-white/5 transition-all hover:border-indigo-500/30"
        >
          <div class="flex flex-col md:flex-row gap-6">
            <div class="w-24 h-24 md:w-32 md:h-32 rounded-lg overflow-hidden flex-shrink-0 bg-zinc-800">
              <img 
                :src="item.headerImage || item.gameImage || '/placeholder-game.png'" 
                class="w-full h-full object-cover"
                @error="handleImageError"
              >
            </div>
            <div class="flex-1">
              <div class="flex items-start justify-between mb-3">
                <div>
                  <h4 class="text-lg font-bold mb-1">{{ item.gameName }}</h4>
                  <div class="text-sm text-zinc-400">{{ item.platformName || item.platform || 'Steam' }}</div>
                </div>
                <button 
                  @click="removeFromWishlist(item.gameId || item.id)"
                  class="text-zinc-500 hover:text-zinc-300"
                >
                  <i data-lucide="x" class="w-5 h-5"></i>
                </button>
              </div>
              <div class="flex items-center gap-4 mb-4">
                <div>
                  <div class="text-sm text-zinc-400 mb-1">当前价格</div>
                  <div class="text-xl font-bold text-white">
                    ¥{{ (item.currentPrice || 0).toFixed(2) }}
                    <span v-if="item.isDiscount" class="ml-2 px-2 py-0.5 rounded bg-red-500/20 text-red-400 text-sm">
                      -{{ item.discountRate || 0 }}%
                    </span>
                  </div>
                </div>
                <div v-if="item.originalPrice && item.originalPrice > item.currentPrice">
                  <div class="text-sm text-zinc-400 mb-1">原价</div>
                  <div class="text-lg text-zinc-500 line-through">¥{{ (item.originalPrice || 0).toFixed(2) }}</div>
                </div>
                <div v-if="item.currentPrice < item.originalPrice">
                  <div class="text-sm text-zinc-400 mb-1">节省</div>
                  <div class="text-lg font-bold text-emerald-400">
                    ¥{{ ((item.originalPrice || 0) - (item.currentPrice || 0)).toFixed(2) }}
                  </div>
                </div>
              </div>
              <div class="flex items-center gap-3">
                <button 
                  @click="showPriceAlertDialog(item)"
                  class="flex-1 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2"
                >
                  <i data-lucide="bell" class="w-4 h-4"></i>
                  设置提醒
                </button>
                <button 
                  @click="viewPriceHistory(item)"
                  class="px-4 py-2 bg-white/5 hover:bg-white/10 text-white rounded-lg text-sm font-medium"
                >
                  <i data-lucide="chart-line" class="w-4 h-4"></i>
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
        <div v-if="alerts.length === 0" class="text-center py-8 text-zinc-400">
          <i data-lucide="bell-off" class="w-12 h-12 mx-auto mb-3 opacity-50"></i>
          <p>暂无价格提醒</p>
        </div>
        <div v-else class="space-y-4">
          <div 
            v-for="alert in alerts" 
            :key="alert.subscriptionId"
            class="flex items-center justify-between p-3 rounded-xl bg-white/5"
          >
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg overflow-hidden bg-zinc-800">
                <img 
                  :src="alert.headerImage || '/placeholder-game.png'" 
                  class="w-full h-full object-cover"
                  @error="handleImageError"
                >
              </div>
              <div>
                <div class="font-medium">{{ alert.gameName }}</div>
                <div class="text-sm text-zinc-400">
                  <span v-if="alert.targetPrice">当价格低于 ¥{{ alert.targetPrice.toFixed(2) }} 时提醒我</span>
                  <span v-else-if="alert.targetDiscount">当折扣达到 {{ alert.targetDiscount }}% 时提醒我</span>
                  <span v-else>价格提醒已设置</span>
                </div>
                <div v-if="alert.currentPrice !== null" class="text-xs text-zinc-500 mt-1">
                  当前价格: ¥{{ alert.currentPrice.toFixed(2) }}
                  <span v-if="alert.isDiscount" class="text-red-400 ml-1">(-{{ alert.discountRate }}%)</span>
                </div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs">活跃</span>
              <button 
                @click="unsubscribeAlert(alert.subscriptionId)"
                class="text-zinc-500 hover:text-zinc-300"
              >
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
import { ref, onMounted, nextTick, computed } from 'vue'
import { usePriceStore } from '@/stores/price'
import { priceApi } from '@/api/price'
import { wishlistApi } from '@/api/wishlist'
import { createIcons, icons } from 'lucide'

const priceStore = usePriceStore()

// 获取store中的状态
const wishlist = priceStore.wishlist
const alerts = priceStore.alerts
const totalPotentialSavings = priceStore.totalPotentialSavings
const activeAlertsCount = priceStore.activeAlertsCount

// 监控状态
const monitoringStatus = ref(null)
const lastUpdateTime = ref('')
const loading = ref(false)
const updating = ref(false)

// 格式化时间
const formatTime = (dateString) => {
  if (!dateString) return '暂无数据'
  const date = new Date(dateString)
  const now = new Date()
  const diff = now - date
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  
  if (days === 0) {
    const hours = Math.floor(diff / (1000 * 60 * 60))
    if (hours === 0) {
      const minutes = Math.floor(diff / (1000 * 60))
      return minutes <= 0 ? '刚刚' : `${minutes}分钟前`
    }
    return `${hours}小时前`
  } else if (days === 1) {
    return '昨天 ' + date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  } else if (days < 7) {
    return `${days}天前`
  }
  return date.toLocaleString('zh-CN', { 
    year: 'numeric', 
    month: '2-digit', 
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// 获取监控状态
const fetchMonitoringStatus = async () => {
  try {
    const res = await priceApi.getMonitoringStatus()
    if (res.success) {
      monitoringStatus.value = res.data
      if (res.data.latestRecordTime) {
        lastUpdateTime.value = formatTime(res.data.latestRecordTime)
      }
    }
  } catch (error) {
    console.error('获取监控状态失败:', error)
  }
}

// 手动触发更新
const triggerUpdate = async () => {
  if (updating.value) return
  updating.value = true
  try {
    const res = await priceApi.triggerPriceUpdate()
    if (res.success) {
      alert('价格更新任务已触发，将在后台执行')
      // 等待一段时间后刷新状态
      setTimeout(() => {
        fetchMonitoringStatus()
      }, 2000)
    }
  } catch (error) {
    console.error('触发更新失败:', error)
    alert('触发更新失败，请稍后重试')
  } finally {
    updating.value = false
  }
}

// 移除愿望单
const removeFromWishlist = async (gameId) => {
  if (!confirm('确定要从愿望单移除这个游戏吗？')) return
  try {
    const res = await wishlistApi.removeFromWishlist(gameId)
    if (res.success) {
      await priceStore.fetchWishlist()
    }
  } catch (error) {
    console.error('移除失败:', error)
  }
}

// 取消订阅
const unsubscribeAlert = async (subscriptionId) => {
  if (!confirm('确定要取消这个价格提醒吗？')) return
  try {
    const res = await priceApi.unsubscribeAlert(subscriptionId)
    if (res.success) {
      await priceStore.fetchAlerts()
    }
  } catch (error) {
    console.error('取消订阅失败:', error)
  }
}

// 显示价格提醒对话框
const showPriceAlertDialog = (item) => {
  // TODO: 实现价格提醒设置对话框
  alert(`设置 ${item.gameName} 的价格提醒功能开发中...`)
}

// 查看价格历史
const viewPriceHistory = async (item) => {
  // TODO: 实现价格历史查看
  alert(`查看 ${item.gameName} 的价格历史功能开发中...`)
}

// 图片加载错误处理
const handleImageError = (event) => {
  event.target.src = '/placeholder-game.png'
}

// 计算折扣游戏数量
const discountGamesCount = computed(() => {
  return wishlist.value.filter(item => {
    const currentPrice = item.currentPrice || 0
    const originalPrice = item.originalPrice || 0
    return currentPrice < originalPrice && currentPrice > 0
  }).length
})

onMounted(async () => {
  loading.value = true
  try {
    await Promise.all([
      priceStore.fetchWishlist(),
      priceStore.fetchAlerts(),
      fetchMonitoringStatus()
    ])
  } finally {
    loading.value = false
  }
  nextTick(() => createIcons({ icons }))
  
  // 定期刷新监控状态（每5分钟）
  setInterval(() => {
    fetchMonitoringStatus()
  }, 5 * 60 * 1000)
})
</script>

<style scoped>
.glass-panel {
  background: rgba(255,255,255,0.03);
  border: 1px solid rgba(255,255,255,0.08);
  backdrop-filter: blur(10px);
}
</style>