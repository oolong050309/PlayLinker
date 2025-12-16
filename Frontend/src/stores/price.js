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
        wishlist.value = res.data.items
      }
    } finally {
      loading.value = false
    }
  }

  const fetchAlerts = async () => {
    const res = await priceApi.getSubscriptions()
    if (res.success) {
      alerts.value = res.data.items
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