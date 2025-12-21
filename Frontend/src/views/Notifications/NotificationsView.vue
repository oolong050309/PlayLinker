<template>
  <div class="notifications-container">
    <div class="notifications-header">
      <div class="header-content">
        <h1 class="notifications-title">消息中心</h1>
        <p class="notifications-subtitle">查看您的通知和消息</p>
      </div>
      <div class="header-actions">
        <button 
          class="btn btn-secondary" 
          @click="markAllAsRead"
          :disabled="notifications.length === 0"
        >
          全部标记为已读
        </button>
        <button 
          class="btn btn-danger" 
          @click="clearAll"
          :disabled="notifications.length === 0"
        >
          清空全部
        </button>
      </div>
    </div>

    <!-- 筛选标签 -->
    <div class="filter-tabs">
      <button 
        v-for="tab in tabs" 
        :key="tab.key"
        class="filter-tab"
        :class="{ active: activeTab === tab.key }"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
        <span v-if="tab.count > 0" class="tab-badge">{{ tab.count }}</span>
      </button>
    </div>

    <!-- 消息列表 -->
    <div class="notifications-list">
      <div v-if="filteredNotifications.length === 0" class="empty-state">
        <div class="empty-icon">📭</div>
        <h3 class="empty-title">暂无消息</h3>
        <p class="empty-desc">您还没有收到任何消息</p>
      </div>

      <div 
        v-for="notification in filteredNotifications" 
        :key="notification.id"
        class="notification-item"
        :class="{ unread: !notification.read }"
        @click="handleNotificationClick(notification)"
      >
        <div class="notification-icon" :class="notification.type">
          <component :is="getIcon(notification.type)" class="icon" />
        </div>
        <div class="notification-content">
          <div class="notification-header">
            <h3 class="notification-title">{{ notification.title }}</h3>
            <span class="notification-time">{{ formatTime(notification.createdAt) }}</span>
          </div>
          <p class="notification-message">{{ notification.message }}</p>
          <div v-if="notification.action" class="notification-action">
            <button 
              class="btn btn-sm btn-primary"
              @click.stop="handleAction(notification)"
            >
              {{ notification.action.label }}
            </button>
          </div>
        </div>
        <div v-if="!notification.read" class="unread-dot"></div>
      </div>
    </div>

    <!-- 分页 -->
    <div v-if="totalPages > 1" class="pagination">
      <button 
        class="btn btn-secondary"
        @click="currentPage--"
        :disabled="currentPage === 1"
      >
        上一页
      </button>
      <span class="page-info">第 {{ currentPage }} / {{ totalPages }} 页</span>
      <button 
        class="btn btn-secondary"
        @click="currentPage++"
        :disabled="currentPage >= totalPages"
      >
        下一页
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Bell, CheckCircle, AlertCircle, Info, Gift, Trophy } from 'lucide-vue-next'

const router = useRouter()

const activeTab = ref('all')
const currentPage = ref(1)
const pageSize = 10

const tabs = computed(() => [
  { key: 'all', label: '全部', count: notifications.value.length },
  { key: 'unread', label: '未读', count: notifications.value.filter(n => !n.read).length },
  { key: 'system', label: '系统', count: notifications.value.filter(n => n.type === 'system').length },
  { key: 'game', label: '游戏', count: notifications.value.filter(n => n.type === 'game').length }
])

const notifications = ref([
  {
    id: 1,
    type: 'system',
    title: '欢迎使用 PlayLinker',
    message: '感谢您注册 PlayLinker！开始探索您的游戏库吧。',
    read: false,
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000),
    action: null
  },
  {
    id: 2,
    type: 'game',
    title: '新游戏入库',
    message: '您的 Steam 账号新增了 3 款游戏，已自动同步到游戏库。',
    read: false,
    createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000),
    action: { label: '查看游戏库', path: '/app/library' }
  },
  {
    id: 3,
    type: 'achievement',
    title: '成就解锁',
    message: '恭喜！您在《赛博朋克2077》中解锁了新成就「夜之城传奇」。',
    read: true,
    createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000),
    action: { label: '查看成就', path: '/app/achievements' }
  },
  {
    id: 4,
    type: 'system',
    title: '平台同步完成',
    message: 'Steam 平台数据同步已完成，共同步 156 款游戏。',
    read: true,
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000),
    action: null
  },
  {
    id: 5,
    type: 'game',
    title: '游戏更新提醒',
    message: '《艾尔登法环》有新版本更新可用，立即更新以获得最佳游戏体验。',
    read: false,
    createdAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000),
    action: { label: '查看详情', path: '/app/list' }
  }
])

const filteredNotifications = computed(() => {
  let filtered = notifications.value

  if (activeTab.value === 'unread') {
    filtered = filtered.filter(n => !n.read)
  } else if (activeTab.value === 'system') {
    filtered = filtered.filter(n => n.type === 'system')
  } else if (activeTab.value === 'game') {
    filtered = filtered.filter(n => n.type === 'game' || n.type === 'achievement')
  }

  const start = (currentPage.value - 1) * pageSize
  const end = start + pageSize
  return filtered.slice(start, end)
})

const totalPages = computed(() => {
  let filtered = notifications.value
  if (activeTab.value === 'unread') {
    filtered = filtered.filter(n => !n.read)
  } else if (activeTab.value === 'system') {
    filtered = filtered.filter(n => n.type === 'system')
  } else if (activeTab.value === 'game') {
    filtered = filtered.filter(n => n.type === 'game' || n.type === 'achievement')
  }
  return Math.ceil(filtered.length / pageSize)
})

const getIcon = (type) => {
  const iconMap = {
    system: Info,
    game: Gift,
    achievement: Trophy,
    warning: AlertCircle,
    success: CheckCircle
  }
  return iconMap[type] || Bell
}

const formatTime = (date) => {
  const now = new Date()
  const diff = now - date
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)

  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  if (hours < 24) return `${hours}小时前`
  if (days < 7) return `${days}天前`
  return date.toLocaleDateString('zh-CN')
}

const handleNotificationClick = (notification) => {
  if (!notification.read) {
    notification.read = true
    // TODO: 调用 API 标记为已读
  }
}

const handleAction = (notification) => {
  if (notification.action?.path) {
    router.push(notification.action.path)
  }
}

const markAllAsRead = () => {
  notifications.value.forEach(n => {
    n.read = true
  })
  // TODO: 调用 API 标记全部为已读
}

const clearAll = () => {
  if (confirm('确定要清空所有消息吗？')) {
    notifications.value = []
    // TODO: 调用 API 清空消息
  }
}

onMounted(() => {
  // TODO: 从 API 加载消息
})
</script>

<style scoped>
.notifications-container {
  max-width: 1000px;
  margin: 0 auto;
  padding: var(--spacing-lg);
}

.notifications-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: var(--spacing-xl);
  flex-wrap: wrap;
  gap: var(--spacing-md);
}

.header-content {
  flex: 1;
}

.notifications-title {
  font-size: 32px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.notifications-subtitle {
  font-size: 16px;
  color: var(--text-secondary);
}

.header-actions {
  display: flex;
  gap: var(--spacing-sm);
}

.filter-tabs {
  display: flex;
  gap: var(--spacing-sm);
  margin-bottom: var(--spacing-lg);
  border-bottom: 1px solid var(--border-color);
  overflow-x: auto;
}

.filter-tab {
  padding: var(--spacing-sm) var(--spacing-md);
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.3s;
  display: flex;
  align-items: center;
  gap: var(--spacing-xs);
  white-space: nowrap;
}

.filter-tab:hover {
  color: var(--text-primary);
}

.filter-tab.active {
  color: var(--primary-color);
  border-bottom-color: var(--primary-color);
}

.tab-badge {
  background: var(--primary-color);
  color: white;
  font-size: 12px;
  padding: 2px 6px;
  border-radius: 10px;
  min-width: 18px;
  text-align: center;
}

.notifications-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.notification-item {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color-strong);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  display: flex;
  gap: var(--spacing-md);
  cursor: pointer;
  transition: all 0.3s;
  position: relative;
}

.notification-item:hover {
  background: var(--bg-secondary);
  border-color: var(--border-color);
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.notification-item.unread {
  border-left: 3px solid var(--primary-color);
  background: rgba(99, 102, 241, 0.05);
}

.notification-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.notification-icon.system {
  background: rgba(59, 130, 246, 0.2);
  color: #3b82f6;
}

.notification-icon.game {
  background: rgba(139, 92, 246, 0.2);
  color: #8b5cf6;
}

.notification-icon.achievement {
  background: rgba(251, 191, 36, 0.2);
  color: #fbbf24;
}

.notification-icon .icon {
  width: 24px;
  height: 24px;
}

.notification-content {
  flex: 1;
  min-width: 0;
}

.notification-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: var(--spacing-xs);
  gap: var(--spacing-md);
}

.notification-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.notification-time {
  font-size: 12px;
  color: var(--text-tertiary);
  white-space: nowrap;
}

.notification-message {
  font-size: 14px;
  color: var(--text-secondary);
  margin: var(--spacing-xs) 0;
  line-height: 1.5;
}

.notification-action {
  margin-top: var(--spacing-sm);
}

.unread-dot {
  position: absolute;
  top: var(--spacing-md);
  right: var(--spacing-md);
  width: 8px;
  height: 8px;
  background: var(--primary-color);
  border-radius: 50%;
}

.empty-state {
  text-align: center;
  padding: var(--spacing-xl) * 2;
}

.empty-icon {
  font-size: 64px;
  margin-bottom: var(--spacing-md);
}

.empty-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.empty-desc {
  font-size: 14px;
  color: var(--text-secondary);
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: var(--spacing-md);
  margin-top: var(--spacing-xl);
}

.page-info {
  font-size: 14px;
  color: var(--text-secondary);
}

.btn {
  padding: var(--spacing-sm) var(--spacing-lg);
  border: none;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.3s;
}

.btn-primary {
  background: var(--primary-color);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--primary-hover);
}

.btn-secondary {
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-color);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--bg-surface);
}

.btn-danger {
  background: var(--error-color);
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background: #dc2626;
}

.btn-sm {
  padding: var(--spacing-xs) var(--spacing-md);
  font-size: 12px;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>

