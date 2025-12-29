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
                  @click="viewPriceHistory(item)"
                  class="flex-1 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2"
                >
                  <i data-lucide="chart-line" class="w-4 h-4"></i>
                  查看价格历史
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 价格历史对话框 -->
    <div v-if="showHistoryDialog" class="dialog-overlay" @click.self="closeHistoryDialog">
      <div class="dialog-content dialog-content-large">
        <div class="dialog-header">
          <h3>{{ historyDialogGame?.gameName }} - 价格历史</h3>
          <button class="dialog-close" @click="closeHistoryDialog">
            <i data-lucide="x" class="w-5 h-5"></i>
          </button>
        </div>
        <div class="dialog-body">
          <div v-if="loadingHistory" class="loading-center">
            <i data-lucide="loader-2" class="w-8 h-8 animate-spin text-zinc-400"></i>
            <span>加载中...</span>
          </div>
          <div v-else-if="priceHistoryData.length === 0" class="empty-center">
            <i data-lucide="chart-line" class="w-12 h-12 text-zinc-500 mb-3"></i>
            <p>暂无价格历史数据</p>
          </div>
          <div v-else class="history-list">
            <div 
              v-for="(item, index) in priceHistoryData" 
              :key="index"
              class="history-item"
            >
              <div class="history-date">{{ formatHistoryDate(item.date) }}</div>
              <div class="history-price-info">
                <div class="history-price-main">
                  <span class="price-current">¥{{ item.currentPrice.toFixed(2) }}</span>
                  <span v-if="item.isDiscount" class="discount-tag">-{{ item.discount }}%</span>
                </div>
                <div v-if="item.originalPrice > item.currentPrice" class="history-price-detail">
                  <span class="price-original-text">原价: ¥{{ item.originalPrice.toFixed(2) }}</span>
                  <span class="price-savings">节省: ¥{{ (item.originalPrice - item.currentPrice).toFixed(2) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

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
const totalPotentialSavings = priceStore.totalPotentialSavings

// 监控状态
const monitoringStatus = ref(null)
const lastUpdateTime = ref('')
const loading = ref(false)
const updating = ref(false)

// 格式化时间（转换为中国时区）
const formatTime = (dateString) => {
  if (!dateString) return '暂无数据'
  
  // 如果后端返回的是UTC时间，需要转换为中国时区（UTC+8）
  const date = new Date(dateString)
  const chinaTime = new Date(date.getTime() + 8 * 60 * 60 * 1000)
  const now = new Date()
  const diff = now - chinaTime
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  
  if (days === 0) {
    const hours = Math.floor(diff / (1000 * 60 * 60))
    if (hours === 0) {
      const minutes = Math.floor(diff / (1000 * 60))
      return minutes <= 0 ? '刚刚' : `${minutes}分钟前`
    }
    return `${hours}小时前`
  } else if (days === 1) {
    return '昨天 ' + chinaTime.toLocaleTimeString('zh-CN', { 
      hour: '2-digit', 
      minute: '2-digit',
      timeZone: 'Asia/Shanghai'
    })
  } else if (days < 7) {
    return `${days}天前`
  }
  return chinaTime.toLocaleString('zh-CN', { 
    year: 'numeric', 
    month: '2-digit', 
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Shanghai'
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


// 价格历史对话框状态
const showHistoryDialog = ref(false)
const historyDialogGame = ref(null)
const priceHistoryData = ref([])
const loadingHistory = ref(false)

// 查看价格历史
const viewPriceHistory = async (item) => {
  historyDialogGame.value = item
  showHistoryDialog.value = true
  loadingHistory.value = true
  priceHistoryData.value = []
  
  try {
    const gameId = item.gameId || item.id
    const response = await priceApi.getPriceHistory(gameId)
    
    if (response.success && response.data) {
      const data = response.data
      if (data.priceHistory && Array.isArray(data.priceHistory)) {
        priceHistoryData.value = data.priceHistory.map(h => ({
          date: h.Date || h.date,
          currentPrice: h.CurrentPrice || h.currentPrice || 0,
          originalPrice: h.OriginalPrice || h.originalPrice || 0,
          discount: h.Discount || h.discount || 0,
          isDiscount: h.IsDiscount || h.isDiscount || false
        })).reverse() // 按时间正序显示
      }
    }
  } catch (error) {
    console.error('加载价格历史失败:', error)
    alert('加载价格历史失败')
  } finally {
    loadingHistory.value = false
  }
}

// 关闭价格历史对话框
const closeHistoryDialog = () => {
  showHistoryDialog.value = false
  historyDialogGame.value = null
  priceHistoryData.value = []
}

// 格式化日期
const formatHistoryDate = (dateString) => {
  if (!dateString) return ''
  try {
    const date = new Date(dateString)
    return date.toLocaleDateString('zh-CN', { 
      year: 'numeric', 
      month: '2-digit', 
      day: '2-digit' 
    })
  } catch {
    return dateString
  }
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

/* 对话框样式 */
.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(4px);
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.dialog-content {
  background: rgba(20, 20, 23, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  width: 100%;
  max-width: 500px;
  max-height: 90vh;
  overflow-y: auto;
  backdrop-filter: blur(20px);
}

.dialog-content-large {
  max-width: 700px;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.dialog-header h3 {
  font-size: 18px;
  font-weight: 600;
  color: #f8fafc;
}

.dialog-close {
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  padding: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.dialog-close:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #f8fafc;
}

.dialog-body {
  padding: 24px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.game-info-preview {
  display: flex;
  gap: 12px;
  padding: 16px;
  background: rgba(15, 15, 19, 0.6);
  border-radius: 8px;
  margin-bottom: 24px;
}

.preview-image {
  width: 60px;
  height: 80px;
  object-fit: cover;
  border-radius: 6px;
}

.preview-info {
  flex: 1;
}

.preview-info h4 {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 8px;
  color: #f8fafc;
}

.preview-price {
  font-size: 14px;
  color: #94a3b8;
  display: flex;
  align-items: center;
  gap: 8px;
}

.preview-original {
  font-size: 12px;
  color: #64748b;
  text-decoration: line-through;
  margin-top: 4px;
}

.discount-badge-small {
  padding: 2px 6px;
  background: rgba(239, 68, 68, 0.2);
  color: #fca5a5;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.alert-options {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.option-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.option-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #e5e7eb;
  cursor: pointer;
}

.radio-input {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.option-input {
  margin-left: 26px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.input-field {
  padding: 10px 12px;
  background: rgba(15, 15, 19, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  color: #f8fafc;
  font-size: 14px;
  transition: all 0.2s;
}

.input-field:focus {
  outline: none;
  border-color: #8b5cf6;
  background: rgba(20, 20, 23, 0.9);
}

.input-hint {
  font-size: 12px;
  color: #64748b;
}

.btn-primary {
  padding: 10px 20px;
  background: #8b5cf6;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary:hover:not(:disabled) {
  background: #7c3aed;
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  padding: 10px 20px;
  background: rgba(20, 20, 23, 0.8);
  color: #94a3b8;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary:hover {
  background: rgba(30, 30, 35, 0.9);
  color: #f8fafc;
}

/* 价格历史对话框样式 */
.loading-center,
.empty-center {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #94a3b8;
  gap: 12px;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 500px;
  overflow-y: auto;
}

.history-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  background: rgba(15, 15, 19, 0.6);
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.05);
  transition: all 0.2s;
}

.history-item:hover {
  background: rgba(20, 20, 23, 0.8);
  border-color: rgba(139, 92, 246, 0.3);
}

.history-date {
  font-size: 14px;
  color: #94a3b8;
  font-weight: 500;
  min-width: 100px;
}

.history-price-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
}

.history-price-main {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.price-current {
  font-size: 18px;
  font-weight: 600;
  color: #f8fafc;
}

.discount-tag {
  padding: 2px 8px;
  background: rgba(239, 68, 68, 0.2);
  color: #fca5a5;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
}

.history-price-detail {
  display: flex;
  gap: 12px;
  font-size: 12px;
}

.price-original-text {
  color: #64748b;
  text-decoration: line-through;
}

.price-savings {
  color: #10b981;
  font-weight: 500;
}
</style>