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
          <div class="notification-action">
            <!-- 家长监管邀请：显示"同意 / 拒绝"两个按钮（仅未读且未处理过的邀请） -->
            <template v-if="notification.type === 'parental' && notification.action?.kind === 'parental_invite' && !notification.read && !notification.processed">
              <button 
                class="btn btn-sm btn-primary"
                @click.stop="handleParentalInvite(notification, true)"
              >
                同意
              </button>
              <button 
                class="btn btn-sm btn-secondary"
                style="margin-left: 8px;"
                @click.stop="handleParentalInvite(notification, false)"
              >
                拒绝
              </button>
            </template>
            <!-- 解除绑定通知：不显示任何按钮 -->
            <!-- 其他类型通知：保持原来的单按钮行为 -->
            <button 
              v-else-if="notification.action && notification.action.kind !== 'relationship_terminated'"
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
import notificationsApi from '@/api/notifications'
import parentalApi from '@/api/parental'

const router = useRouter()

const activeTab = ref('all')
const currentPage = ref(1)
const pageSize = 10
const loading = ref(false)

const notifications = ref([])

const tabs = computed(() => [
  { key: 'all', label: '全部', count: notifications.value.filter(n => !n.read).length },
  { key: 'system', label: '系统', count: notifications.value.filter(n => n.type === 'system' && !n.read).length },
  { key: 'game', label: '游戏', count: notifications.value.filter(n => (n.type === 'game' || n.type === 'achievement') && !n.read).length },
  { key: 'parental', label: '家长监管', count: notifications.value.filter(n => n.type === 'parental' && !n.read).length }
])

const filteredNotifications = computed(() => {
  let filtered = notifications.value

  // 按分类筛选
  if (activeTab.value === 'system') {
    filtered = filtered.filter(n => n.type === 'system')
  } else if (activeTab.value === 'game') {
    filtered = filtered.filter(n => n.type === 'game' || n.type === 'achievement')
  } else if (activeTab.value === 'parental') {
    filtered = filtered.filter(n => n.type === 'parental')
  }
  // 'all' 不筛选，显示所有

  // 排序：未读在上，已读在下，然后按时间倒序
  filtered = filtered.sort((a, b) => {
    // 先按已读状态排序：未读在前（false在前），已读在后（true在后）
    if (a.read !== b.read) {
      return a.read ? 1 : -1
    }
    // 如果已读状态相同，按时间倒序
    return b.createdAt - a.createdAt
  })

  const start = (currentPage.value - 1) * pageSize
  const end = start + pageSize
  return filtered.slice(start, end)
})

const totalPages = computed(() => {
  let filtered = notifications.value
  if (activeTab.value === 'system') {
    filtered = filtered.filter(n => n.type === 'system')
  } else if (activeTab.value === 'game') {
    filtered = filtered.filter(n => n.type === 'game' || n.type === 'achievement')
  } else if (activeTab.value === 'parental') {
    filtered = filtered.filter(n => n.type === 'parental')
  }
  return Math.ceil(filtered.length / pageSize)
})

const getIcon = (type) => {
  const iconMap = {
    system: Info,
    game: Gift,
    achievement: Trophy,
    parental: AlertCircle,
    warning: AlertCircle,
    success: CheckCircle
  }
  return iconMap[type] || Bell
}

const formatTime = (date) => {
  if (!date) return ''
  
  // 如果date是字符串，先转换为Date对象
  // 后端返回的时间通常是UTC时间字符串（如 "2024-01-01T12:00:00Z"）
  const dateObj = date instanceof Date ? date : new Date(date)
  
  // 获取当前时间（本地时间）
  const now = new Date()
  
  // 计算时间差（毫秒）
  const diff = now - dateObj
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)

  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  if (hours < 24) return `${hours}小时前`
  if (days < 7) return `${days}天前`
  
  // 超过7天，显示具体日期和时间（转换为中国时区显示）
  // 使用Intl.DateTimeFormat来正确转换时区
  const formatter = new Intl.DateTimeFormat('zh-CN', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  })
  
  const parts = formatter.formatToParts(dateObj)
  const year = parts.find(p => p.type === 'year').value
  const month = parts.find(p => p.type === 'month').value
  const day = parts.find(p => p.type === 'day').value
  const hour = parts.find(p => p.type === 'hour').value
  const minute = parts.find(p => p.type === 'minute').value
  
  return `${year}-${month}-${day} ${hour}:${minute}`
}

// 从后端加载通知
const loadNotifications = async () => {
  loading.value = true
  try {
    const res = await notificationsApi.getNotifications({ page: 1, pageSize: 50 })
    if (res.success && res.data) {
      const data = res.data || {}
      // 兼容后端 PascalCase / camelCase 命名
      const items = data.items || data.Items || []
      notifications.value = items.map(n => {
        const source = n.sourceModule || n.SourceModule || 'system'
        let type = 'system'
        if (source === 'parental_control') {
          type = 'parental'
        }

        const rawTitle = n.title || n.Title || ''
        const rawContent = n.content || n.Content || ''
        const rawId = n.notificationId || n.NotificationId
        const rawIsRead = (typeof n.isRead === 'boolean') ? n.isRead : !!n.IsRead
        const rawCreatedAt = n.createdAt || n.CreatedAt

        let message = rawContent || ''
        let action = null

        if (source === 'parental_control') {
          try {
            const payload = JSON.parse(rawContent || '{}')
            const parentName = payload.parentUsername || payload.ParentUsername || '家长'
            const token = payload.token || payload.Token
            const terminatedAt = payload.terminatedAt || payload.TerminatedAt
            const relationshipMessage = payload.message || payload.Message

            // 判断是邀请还是解除绑定通知
            if (token) {
              // 这是邀请通知
              const extra = relationshipMessage ? `：${relationshipMessage}` : ''
              message = `${parentName} 想要对你的账号开启家长监管${extra}`
              // 创建 parental_invite 动作
              action = {
                label: '处理邀请',
                kind: 'parental_invite',
                token,
                parentUsername: parentName
              }
            } else if (terminatedAt || relationshipMessage) {
              // 这是解除绑定通知
              message = relationshipMessage || `${parentName} 已解除与您的监管关系`
              // 不创建动作，不显示按钮
              action = {
                kind: 'relationship_terminated'
              }
            } else {
              // 其他类型的家长监管通知（如同意/拒绝结果）
              message = rawContent
              // 不创建动作
            }
          } catch (e) {
            // 内容不是 JSON 时，检查是否是纯文本的解除绑定消息
            if (rawContent && (rawContent.includes('解除') || rawContent.includes('监管关系'))) {
              message = rawContent
              action = {
                kind: 'relationship_terminated'
              }
            } else {
              // 其他情况，直接使用原始文本
              message = rawContent
            }
          }
        }

        return {
          id: rawId,
          rawId: rawId,
          type,
          title: rawTitle,
          message,
          read: rawIsRead,
          processed: rawIsRead, // 已读的通知视为已处理
          createdAt: rawCreatedAt ? new Date(rawCreatedAt) : new Date(),
          action
        }
      })
    }
  } catch (error) {
    console.error('加载通知失败:', error)
    alert('加载通知失败: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

const handleNotificationClick = async (notification) => {
  if (!notification.read) {
    notification.read = true
    try {
      await notificationsApi.markAsRead(notification.rawId || notification.id)
    } catch (error) {
      console.error('标记通知已读失败:', error)
    }
  }
}

const handleParentalInvite = async (notification, accept) => {
  const token = notification.action?.token
  if (!token) {
    alert('邀请信息缺失，无法处理')
    return
  }

  try {
    await parentalApi.respondInvitation({ token, accept })
    // 标记这条通知为已读和已处理
    notification.read = true
    notification.processed = true
    try {
      await notificationsApi.markAsRead(notification.rawId || notification.id)
    } catch (e) {
      console.error('标记家长邀请通知已读失败:', e)
    }
    alert(accept ? '已同意家长监管邀请' : '已拒绝家长监管邀请')
    await loadNotifications()
  } catch (error) {
    console.error('处理家长邀请失败:', error)
    alert('处理家长邀请失败: ' + (error.message || '未知错误'))
  }
}

const handleAction = async (notification) => {
  // 其他类型的动作：跳转路由
  if (notification.action?.path) {
    router.push(notification.action.path)
  }
}

const markAllAsRead = async () => {
  if (notifications.value.length === 0) return
  notifications.value.forEach(n => {
    n.read = true
  })
  try {
    await Promise.all(
      notifications.value.map(n => notificationsApi.markAsRead(n.rawId || n.id).catch(() => null))
    )
  } catch (error) {
    console.error('批量标记已读失败:', error)
  }
}

const clearAll = async () => {
  if (notifications.value.length === 0) return
  if (!confirm('确定要清空所有消息吗？')) return

  const current = [...notifications.value]
  notifications.value = []
  try {
    await Promise.all(
      current.map(n => notificationsApi.delete(n.rawId || n.id).catch(() => null))
    )
  } catch (error) {
    console.error('清空消息失败:', error)
  }
}

onMounted(() => {
  loadNotifications()
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

