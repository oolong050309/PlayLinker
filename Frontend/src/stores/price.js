import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { wishlistApi } from '@/api/wishlist'
import { priceApi } from '@/api/price'

export const usePriceStore = defineStore('price', () => {
  // State
  const wishlist = ref([])
  const alerts = ref([])
  const loading = ref(false)
  
  // Getters
  const totalPotentialSavings = computed(() => {
    // 假设后端没有直接返回总额，前端计算示例
    return wishlist.value.reduce((acc, item) => {
      if (item.currentPrice < item.originalPrice) {
        return acc + (item.originalPrice - item.currentPrice)
      }
      return acc
    }, 0)
  })

  const activeAlertsCount = computed(() => alerts.value.filter(a => a.isActive).length)

  // Actions
  const fetchWishlist = async () => {
    loading.value = true
    try {
      const res = await wishlistApi.getWishlist()
      if (res.success) {
        // 适配后端返回的数据结构
        const items = res.data.items || res.data.wishlist || []
        // 获取这些游戏的最新价格
        if (items.length > 0) {
          const gameIds = items.map(item => item.gameId || item.id).join(',')
          try {
            const priceRes = await priceApi.getCurrentPrices({ game_ids: gameIds })
            if (priceRes.success && priceRes.data.prices) {
              const priceMap = new Map(priceRes.data.prices.map(p => [p.gameId, p]))
              wishlist.value = items.map(item => {
                const gameId = item.gameId || item.id
                const priceInfo = priceMap.get(gameId)
                return {
                  ...item,
                  currentPrice: priceInfo?.currentPrice || item.currentPrice || 0,
                  originalPrice: priceInfo?.originalPrice || item.originalPrice || 0,
                  discountRate: priceInfo?.discount || item.discountRate || 0,
                  isDiscount: priceInfo?.isDiscount || item.isDiscount || false
                }
              })
            } else {
              wishlist.value = items
            }
          } catch (error) {
            console.error('获取价格信息失败:', error)
            wishlist.value = items
          }
        } else {
          wishlist.value = []
        }
      }
    } catch (error) {
      console.error('获取愿望单失败:', error)
      wishlist.value = []
    } finally {
      loading.value = false
    }
  }

  const fetchAlerts = async () => {
    try {
      const res = await priceApi.getSubscriptions()
      if (res.success) {
        // 适配后端返回的数据结构
        alerts.value = res.data.subscriptions || res.data.items || []
      }
    } catch (error) {
      console.error('获取价格提醒失败:', error)
      alerts.value = []
    }
  }

  return {
    wishlist,
    alerts,
    loading,
    totalPotentialSavings,
    activeAlertsCount,
    fetchWishlist,
    fetchAlerts
  }
})