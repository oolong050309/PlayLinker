<template>
  <div class="wishlist-container">
    <!-- 愿望单监控区域（核心功能保留，样式优化） -->
    <section class="wishlist-section">
      <div class="section-header mb-6">
        <h3 class="section-title">愿望单监控</h3>
      </div>
      
      <!-- 加载状态 -->
      <div v-if="loading" class="loading-container">
        <i data-lucide="loader-2" class="loading-icon"></i>
      </div>
      
      <!-- 空状态 -->
      <div v-else-if="wishlist.length === 0" class="glass-panel empty-container">
        <i data-lucide="inbox" class="empty-icon"></i>
        <p class="empty-text">愿望单为空，暂无监控游戏！</p>
      </div>
      
      <!-- 愿望单列表 -->
      <div v-else class="wishlist-list">
        <div 
          v-for="item in wishlist" 
          :key="item.gameId || item.id"
          class="glass-panel game-card"
          @click="goToGameDetail(item.gameId || item.id)"
        >
          <div class="game-card-inner">
            <!-- 游戏封面图 -->
            <div class="game-cover-wrapper">
              <img 
                :src="item.headerImage || item.gameImage || noCoverImage" 
                class="game-cover"
                @error="handleImageError"
                alt="游戏封面"
              >
            </div>
            
            <!-- 游戏信息 -->
            <div class="game-info">
              <div class="game-header">
                <div class="game-basic-info">
                  <h4 class="game-name">{{ item.gameName }}</h4>
                  <div class="game-platform">{{ item.platformName || item.platform || 'Steam' }}</div>
                </div>
                <button 
                  @click.stop="removeFromWishlist(item.gameId || item.id)"
                  class="remove-btn"
                  title="移除该游戏"
                >
                  <i data-lucide="x" class="remove-icon"></i>
                </button>
              </div>
              
              <!-- 价格信息 -->
              <div class="price-info-group">
                <div class="price-item current-price">
                  <div class="price-label">当前价格</div>
                  <div class="price-value">
                    ¥{{ (item.currentPrice || 0).toFixed(2) }}
                    <span v-if="item.isDiscount" class="discount-tag">
                      -{{ item.discountRate || 0 }}%
                    </span>
                  </div>
                </div>
                
                <div v-if="item.originalPrice && item.originalPrice > item.currentPrice" class="price-item original-price">
                  <div class="price-label">原价</div>
                  <div class="price-value">¥{{ (item.originalPrice || 0).toFixed(2) }}</div>
                </div>
                
                <div v-if="item.currentPrice < item.originalPrice" class="price-item save-price">
                  <div class="price-label">节省金额</div>
                  <div class="price-value">¥{{ ((item.originalPrice || 0) - (item.currentPrice || 0)).toFixed(2) }}</div>
                </div>
              </div>
              
              <!-- 价格订阅策略 -->
              <div class="subscription-section">
                <div class="subscription-header">
                  <span class="subscription-label">价格提醒策略</span>
                  <button 
                    @click.stop="editSubscription(item)"
                    class="edit-subscription-btn"
                    title="编辑价格提醒策略"
                  >
                    <i data-lucide="edit-2" class="edit-icon"></i>
                    <span>{{ getSubscription(item) ? '编辑' : '设置' }}</span>
                  </button>
                </div>
                <div class="subscription-content">
                  <div v-if="getSubscription(item)" class="subscription-info">
                    <div v-if="getSubscription(item).targetPrice" class="subscription-item">
                      <i data-lucide="tag" class="subscription-icon"></i>
                      <span>目标价格: ¥{{ getSubscription(item).targetPrice.toFixed(2) }}</span>
                    </div>
                    <div v-if="getSubscription(item).targetDiscount" class="subscription-item">
                      <i data-lucide="percent" class="subscription-icon"></i>
                      <span>目标折扣: {{ getSubscription(item).targetDiscount }}%</span>
                    </div>
                    <div v-if="!getSubscription(item).targetPrice && !getSubscription(item).targetDiscount" class="subscription-item">
                      <i data-lucide="bell-off" class="subscription-icon"></i>
                      <span>未设置提醒策略</span>
                    </div>
                  </div>
                  <div v-else class="subscription-empty">
                    <i data-lucide="bell-off" class="subscription-icon"></i>
                    <span>未设置价格提醒</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 价格订阅策略编辑对话框 -->
    <div v-if="showEditDialog" class="dialog-overlay" @click.self="closeEditDialog">
      <div class="dialog-content">
        <div class="dialog-header">
          <h3>编辑价格提醒策略</h3>
          <button class="dialog-close" @click="closeEditDialog">
            <i data-lucide="x" class="w-5 h-5"></i>
          </button>
        </div>
        <div class="dialog-body" v-if="editingGame">
          <div class="game-info-preview">
            <img 
              :src="editingGame.headerImage || editingGame.gameImage || noCoverImage" 
              class="preview-image"
              @error="handleImageError"
            />
            <div class="preview-info">
              <h4>{{ editingGame.gameName }}</h4>
              <p class="preview-price">
                当前价格: ¥{{ (editingGame.currentPrice || 0).toFixed(2) }}
                <span v-if="editingGame.isDiscount" class="discount-badge-small">
                  -{{ editingGame.discountRate || 0 }}%
                </span>
              </p>
              <p v-if="editingGame.originalPrice" class="preview-original">
                原价: ¥{{ editingGame.originalPrice.toFixed(2) }}
              </p>
            </div>
          </div>
          <div class="alert-options">
            <div class="option-group">
              <label class="option-label">
                <input 
                  type="radio" 
                  v-model="alertType" 
                  value="price"
                  class="radio-input"
                />
                <span>目标价格提醒</span>
              </label>
              <div v-if="alertType === 'price'" class="option-input">
                <input 
                  type="number" 
                  v-model.number="targetPrice" 
                  placeholder="输入目标价格"
                  class="input-field"
                  step="0.01"
                  min="0"
                />
                <span class="input-hint">当价格降至或低于此价格时提醒</span>
              </div>
            </div>
            <div class="option-group">
              <label class="option-label">
                <input 
                  type="radio" 
                  v-model="alertType" 
                  value="discount"
                  class="radio-input"
                />
                <span>目标折扣提醒</span>
              </label>
              <div v-if="alertType === 'discount'" class="option-input">
                <input 
                  type="number" 
                  v-model.number="targetDiscount" 
                  placeholder="输入目标折扣百分比"
                  class="input-field"
                  min="0"
                  max="100"
                />
                <span class="input-hint">当折扣达到或超过此百分比时提醒</span>
              </div>
            </div>
            <div class="option-group">
              <label class="option-label">
                <input 
                  type="radio" 
                  v-model="alertType" 
                  value="none"
                  class="radio-input"
                />
                <span>取消提醒</span>
              </label>
            </div>
          </div>
        </div>
        <div class="dialog-footer">
          <button class="btn-secondary" @click="closeEditDialog">取消</button>
          <button class="btn-primary" @click="saveSubscription" :disabled="savingSubscription">
            {{ savingSubscription ? '保存中...' : '保存' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onActivated, nextTick, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { usePriceStore } from '@/stores/price'
import { priceApi } from '@/api/price'
import { wishlistApi } from '@/api/wishlist'
import { createIcons, icons } from 'lucide'
import noCoverImage from '@/assets/no_cover.png'

const router = useRouter()
const priceStore = usePriceStore()

// 核心数据保留 - 使用 computed 确保响应式
const wishlist = computed(() => priceStore.wishlist)
const loading = computed(() => priceStore.loading)

// 价格订阅数据
const subscriptions = ref([])
const subscriptionsMap = computed(() => {
  const map = new Map()
  subscriptions.value.forEach(sub => {
    map.set(sub.gameId, sub)
  })
  return map
})

// 获取游戏的价格订阅信息
const getSubscription = (item) => {
  const gameId = item.gameId || item.id
  return subscriptionsMap.value.get(gameId)
}

// 编辑订阅对话框状态
const showEditDialog = ref(false)
const editingGame = ref(null)
const editingSubscription = ref(null)
const alertType = ref('price')
const targetPrice = ref(null)
const targetDiscount = ref(null)
const savingSubscription = ref(false)

// 加载价格订阅列表
const loadSubscriptions = async () => {
  try {
    const res = await priceApi.getSubscriptions()
    if (res.success && res.data) {
      subscriptions.value = res.data.subscriptions || res.data.items || []
    }
  } catch (error) {
    console.error('加载价格订阅失败:', error)
  }
}

// 跳转到游戏详情页
const goToGameDetail = (gameId) => {
  router.push(`/app/game/${gameId}`)
}

// 编辑价格订阅策略
const editSubscription = (item) => {
  editingGame.value = item
  const subscription = getSubscription(item)
  editingSubscription.value = subscription
  
  // 重置表单
  targetPrice.value = null
  targetDiscount.value = null
  
  if (subscription) {
    // 已有订阅，加载现有设置
    if (subscription.targetPrice !== null && subscription.targetPrice !== undefined) {
      alertType.value = 'price'
      targetPrice.value = subscription.targetPrice
    } else if (subscription.targetDiscount !== null && subscription.targetDiscount !== undefined) {
      alertType.value = 'discount'
      targetDiscount.value = subscription.targetDiscount
    } else {
      // 有订阅但没有设置目标，默认选择价格提醒
      alertType.value = 'price'
    }
  } else {
    // 新建订阅，默认选择价格提醒
    alertType.value = 'price'
  }
  
  showEditDialog.value = true
}

// 关闭编辑对话框
const closeEditDialog = () => {
  showEditDialog.value = false
  editingGame.value = null
  editingSubscription.value = null
  alertType.value = 'price'
  targetPrice.value = null
  targetDiscount.value = null
}

// 保存价格订阅策略
const saveSubscription = async () => {
  if (!editingGame.value) return
  
  // 如果选择取消提醒
  if (alertType.value === 'none') {
    if (editingSubscription.value) {
      // 删除现有订阅
      try {
        const res = await priceApi.unsubscribeAlert(editingSubscription.value.subscriptionId)
        if (res.success) {
          // 刷新数据
          await Promise.all([
            loadSubscriptions(),
            priceStore.fetchWishlist()
          ])
          closeEditDialog()
        } else {
          alert(res.message || '取消订阅失败')
        }
      } catch (error) {
        console.error('取消订阅失败:', error)
        alert('取消订阅失败: ' + (error.message || '未知错误'))
      }
    } else {
      // 没有订阅，直接关闭
      closeEditDialog()
    }
    return
  }
  
  // 验证输入
  if (alertType.value === 'price' && (!targetPrice.value || targetPrice.value <= 0)) {
    alert('请输入有效的目标价格')
    return
  }
  if (alertType.value === 'discount' && (!targetDiscount.value || targetDiscount.value < 0 || targetDiscount.value > 100)) {
    alert('请输入有效的折扣百分比（0-100）')
    return
  }
  
  savingSubscription.value = true
  try {
    const gameId = editingGame.value.gameId || editingGame.value.id
    const platformId = editingGame.value.platformId || 1 // 默认Steam
    
    if (editingSubscription.value) {
      // 更新现有订阅
      const data = {
        gameId: parseInt(gameId),
        platformId: platformId,
        targetPrice: alertType.value === 'price' ? targetPrice.value : null,
        targetDiscount: alertType.value === 'discount' ? targetDiscount.value : null
      }
      
      const response = await priceApi.updateSubscription(editingSubscription.value.subscriptionId, data)
      if (response.success) {
        // 刷新数据
        await Promise.all([
          loadSubscriptions(),
          priceStore.fetchWishlist()
        ])
        closeEditDialog()
      } else {
        alert(response.message || '更新失败，请重试')
      }
    } else {
      // 创建新订阅
      const data = {
        gameId: parseInt(gameId),
        platformId: platformId,
        targetPrice: alertType.value === 'price' ? targetPrice.value : null,
        targetDiscount: alertType.value === 'discount' ? targetDiscount.value : null
      }
      
      const response = await priceApi.trackPrice(data)
      if (response.success) {
        // 刷新数据
        await Promise.all([
          loadSubscriptions(),
          priceStore.fetchWishlist()
        ])
        closeEditDialog()
      } else {
        alert(response.message || '设置失败，请重试')
      }
    }
  } catch (error) {
    console.error('保存价格订阅失败:', error)
    alert('保存失败: ' + (error.message || '未知错误'))
  } finally {
    savingSubscription.value = false
  }
}

// 移除愿望单功能保留
const removeFromWishlist = async (gameId) => {
  if (!confirm('确定要从愿望单移除这个游戏吗？')) return
  try {
    await wishlistApi.removeFromWishlist(gameId)
    // 同时移除价格订阅
    const subscription = subscriptionsMap.value.get(gameId)
    if (subscription) {
      try {
        await priceApi.unsubscribeAlert(subscription.subscriptionId)
      } catch (error) {
        console.error('移除价格订阅失败:', error)
      }
    }
    // 刷新所有数据
    await Promise.all([
      priceStore.fetchWishlist(),
      loadSubscriptions()
    ])
  } catch (error) {
    console.error('移除失败:', error)
    alert('移除失败: ' + (error.message || '未知错误'))
  }
}

// 图片加载错误处理保留
const handleImageError = (event) => {
  event.target.src = noCoverImage
}

// 刷新数据函数
const refreshData = async () => {
  try {
    await Promise.all([
      priceStore.fetchWishlist(),
      loadSubscriptions()
    ])
  } catch (error) {
    console.error('刷新数据失败:', error)
  } finally {
    nextTick(() => createIcons({ icons }))
  }
}

onMounted(async () => {
  // 确保在挂载时立即加载数据
  await refreshData()
})

// 当路由激活时（从其他路由返回时）也刷新数据
onActivated(async () => {
  // 如果数据为空或加载完成，则刷新数据
  if (wishlist.value.length === 0 && !loading.value) {
    await refreshData()
  } else {
    // 即使有数据，也刷新以确保数据是最新的
    await refreshData()
  }
})

// 监听 store 中的 wishlist 变化，确保图标更新
watch(() => priceStore.wishlist, () => {
  nextTick(() => createIcons({ icons }))
}, { deep: true })
</script>

<style scoped>
/* 全局容器优化 */
.wishlist-container {
  flex: 1;
  overflow-y: auto;
  padding: 2rem;
  background-color: #09090b; /* 背景色统一，增强沉浸感 */
}

/* 玻璃态基础样式优化（增强质感） */
.glass-panel {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(12px);
  border-radius: 1.25rem;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15); /* 增加轻微阴影，提升层次感 */
  transition: all 0.3s ease-in-out;
}

/* 区域头部样式 */
.section-header {
  display: flex;
  align-items: center;
  justify-content: flex-start;
}

.section-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: #f8fafc; /* 文字更亮，提升可读性 */
  letter-spacing: 0.5px;
}

/* 加载状态美化 */
.loading-container {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 6rem 2rem;
}

.loading-icon {
  width: 2rem;
  height: 2rem;
  color: #94a3b8;
  animation: spin 1.5s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* 空状态美化 */
.empty-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 7rem 2rem;
  text-align: center;
}

.empty-icon {
  width: 4rem;
  height: 4rem;
  color: #64748b;
  margin-bottom: 1.5rem;
}

.empty-text {
  color: #94a3b8;
  font-size: 1rem;
  line-height: 1.6;
}

/* 愿望单列表间距优化 - 一行两个 */
.wishlist-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1.5rem; /* 列表项间距优化，更舒展 */
}

@media (max-width: 768px) {
  .wishlist-list {
    grid-template-columns: 1fr; /* 移动端单列显示 */
  }
}

/* 游戏卡片美化（核心美化项） */
.game-card {
  padding: 1rem;
  border-color: rgba(255, 255, 255, 0.05);
  cursor: pointer; /* 整个卡片可点击 */
  transition: all 0.3s ease-in-out;
}

.game-card:hover {
  border-color: rgba(99, 102, 241, 0.4); /* hover边框变色更柔和醒目 */
  background: rgba(255, 255, 255, 0.05);
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.2); /* hover增强阴影，提升交互感 */
  transform: translateY(-2px); /* 轻微上浮，增加灵动性 */
}

.game-card-inner {
  display: flex;
  flex-direction: row;
  align-items: flex-start;
  gap: 1rem;
}

/* 游戏封面美化 */
.game-cover-wrapper {
  width: 10rem;
  height: 15rem;
  border-radius: 0.5rem;
  overflow: hidden;
  background-color: #18181b;
  flex-shrink: 0;
}

.game-cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: all 0.3s ease;
}

.game-card:hover .game-cover {
  transform: scale(1.05); /* 封面轻微放大，增强hover交互 */
}

/* 游戏信息区域美化 */
.game-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 0; /* 防止内容溢出 */
}

.game-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.game-basic-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
  min-width: 0;
}

.game-name {
  font-size: 1rem;
  font-weight: 600;
  color: #f8fafc;
  line-height: 1.3;
  transition: color 0.2s ease;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
}

.game-platform {
  font-size: 0.75rem;
  color: #94a3b8;
}

/* 移除按钮美化 */
.remove-btn {
  background: transparent;
  border: none;
  border-radius: 50%;
  width: 2rem;
  height: 2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #94a3b8;
  cursor: pointer;
  transition: all 0.2s ease;
}

.remove-btn:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #f8fafc;
}

.remove-icon {
  width: 1rem;
  height: 1rem;
}

/* 价格信息组美化 */
.price-info-group {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1rem; /* 价格项间距优化 */
}

.price-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.price-label {
  font-size: 0.75rem;
  color: #94a3b8;
}

.price-value {
  font-size: 0.875rem;
  font-weight: 600;
  color: #f8fafc;
}

/* 不同价格项样式区分 */
.original-price .price-value {
  color: #64748b;
  text-decoration: line-through;
  font-weight: 400;
}

.save-price .price-value {
  color: #10b981; /* 节省金额绿色更醒目，提升视觉层次 */
}

/* 折扣标签美化 */
.discount-tag {
  display: inline-block;
  margin-left: 0.75rem;
  padding: 0.25rem 0.5rem;
  background: rgba(239, 68, 68, 0.2);
  color: #fca5a5;
  border-radius: 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
}

/* 价格订阅策略区域 */
.subscription-section {
  margin-top: 0.5rem;
  padding-top: 0.75rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.subscription-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.subscription-label {
  font-size: 0.75rem;
  font-weight: 500;
  color: #94a3b8;
}

.edit-subscription-btn {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.5rem;
  background: rgba(99, 102, 241, 0.1);
  border: 1px solid rgba(99, 102, 241, 0.3);
  border-radius: 0.375rem;
  color: #818cf8;
  font-size: 0.7rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.edit-subscription-btn:hover {
  background: rgba(99, 102, 241, 0.2);
  border-color: rgba(99, 102, 241, 0.5);
  color: #a5b4fc;
}

.edit-icon {
  width: 0.875rem;
  height: 0.875rem;
}

.subscription-content {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  padding: 0.5rem;
  background: rgba(15, 15, 19, 0.4);
  border-radius: 0.375rem;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.subscription-info {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.subscription-item {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #cbd5e1;
  padding: 0.25rem 0;
}

.subscription-empty {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #64748b;
  font-style: italic;
  padding: 0.25rem 0;
}

.subscription-icon {
  width: 0.875rem;
  height: 0.875rem;
  color: #8b5cf6;
  flex-shrink: 0;
}

.subscription-empty .subscription-icon {
  color: #64748b;
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

/* 响应式优化，确保移动端展示舒适 */
@media (max-width: 480px) {
  .wishlist-container {
    padding: 1rem;
  }
  
  .price-info-group {
    gap: 1.5rem;
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>