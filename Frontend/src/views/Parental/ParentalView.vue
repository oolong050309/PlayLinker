<template>
  <div class="parental-container">
    <div class="parental-header">
      <div class="header-content">
        <h1 class="parental-title">家长监管</h1>
        <p class="parental-subtitle">管理孩子的游戏时间和内容访问</p>
      </div>
      <div class="header-status" :class="{ active: isEnabled }">
        <span class="status-dot"></span>
        <span class="status-text">{{ isEnabled ? '已启用' : '未启用' }}</span>
      </div>
    </div>

    <!-- 邀请子账户 -->
    <section class="parental-section">
      <div class="settings-card">
        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">邀请子账户</h3>
            <p class="setting-desc">
              通过输入子账户用户名，向对方发送家长监管邀请。<br />
              对方需在消息中心中同意邀请后，才会正式建立家长监管关系。
            </p>
          </div>
          <div class="setting-action invite-action">
            <input
              v-model="childUsername"
              type="text"
              class="setting-input"
              style="width: 200px;"
              placeholder="子账户用户名"
            />
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">附加留言（可选）</h3>
            <p class="setting-desc">例如：说明监管原因或约定的游戏时间等。</p>
          </div>
          <div class="setting-action invite-action">
            <textarea
              v-model="inviteMessage"
              class="setting-textarea"
              rows="3"
              placeholder="写一点想对孩子说的话...（可留空）"
            ></textarea>
          </div>
        </div>

        <div class="parental-actions">
          <button
            class="btn btn-primary"
            @click="handleSendInvitation"
            :disabled="inviting || !childUsername.trim()"
          >
            {{ inviting ? '发送中...' : '发送邀请' }}
          </button>
        </div>
      </div>
    </section>

    <!-- 监管开关 -->
    <section class="parental-section">
      <div class="settings-card">
        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">启用家长监管</h3>
            <p class="setting-desc">开启后，将限制游戏时间和内容访问</p>
          </div>
          <div class="setting-action">
            <label class="toggle-switch">
              <input 
                type="checkbox" 
                v-model="isEnabled"
                @change="handleToggle"
              />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
      </div>
    </section>

    <!-- 时间限制 -->
    <section class="parental-section" v-if="isEnabled">
      <h2 class="section-title">时间限制</h2>
      <div class="settings-card">
        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">每日游戏时长</h3>
            <p class="setting-desc">设置每天允许的游戏时间（小时）</p>
          </div>
          <div class="setting-action">
            <input 
              v-model.number="timeLimits.dailyHours" 
              type="number" 
              min="0"
              max="24"
              class="setting-input"
              style="width: 100px;"
            />
            <span class="setting-unit">小时/天</span>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">每周游戏时长</h3>
            <p class="setting-desc">设置每周允许的游戏时间（小时）</p>
          </div>
          <div class="setting-action">
            <input 
              v-model.number="timeLimits.weeklyHours" 
              type="number" 
              min="0"
              max="168"
              class="setting-input"
              style="width: 100px;"
            />
            <span class="setting-unit">小时/周</span>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">允许游戏时间段</h3>
            <p class="setting-desc">设置允许游戏的时间范围</p>
          </div>
          <div class="setting-action">
            <div class="time-range">
              <input 
                v-model="timeLimits.startTime" 
                type="time" 
                class="setting-input"
                style="width: 120px;"
              />
              <span class="time-separator">至</span>
              <input 
                v-model="timeLimits.endTime" 
                type="time" 
                class="setting-input"
                style="width: 120px;"
              />
            </div>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">允许游戏的日期</h3>
            <p class="setting-desc">选择允许游戏的星期</p>
          </div>
          <div class="setting-action">
            <div class="weekdays-selector">
              <label 
                v-for="day in weekdays" 
                :key="day.value"
                class="weekday-checkbox"
              >
                <input 
                  type="checkbox" 
                  v-model="timeLimits.allowedDays"
                  :value="day.value"
                />
                <span>{{ day.label }}</span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 内容限制 -->
    <section class="parental-section" v-if="isEnabled">
      <h2 class="section-title">内容限制</h2>
      <div class="settings-card">
        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">年龄分级限制</h3>
            <p class="setting-desc">限制可访问的游戏年龄分级</p>
          </div>
          <div class="setting-action">
            <select v-model="contentRestrictions.ageRating" class="setting-select">
              <option value="all">无限制</option>
              <option value="3">3+</option>
              <option value="7">7+</option>
              <option value="12">12+</option>
              <option value="16">16+</option>
              <option value="18">18+</option>
            </select>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">禁止内容类型</h3>
            <p class="setting-desc">选择要禁止的内容类型</p>
          </div>
          <div class="setting-action">
            <div class="content-tags">
              <label 
                v-for="tag in contentTags" 
                :key="tag.value"
                class="content-tag"
              >
                <input 
                  type="checkbox" 
                  v-model="contentRestrictions.blockedTags"
                  :value="tag.value"
                />
                <span>{{ tag.label }}</span>
              </label>
            </div>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">禁止特定游戏</h3>
            <p class="setting-desc">添加要禁止访问的游戏</p>
          </div>
          <div class="setting-action">
            <button class="btn btn-secondary" @click="showBlockGameDialog = true">
              添加游戏
            </button>
          </div>
        </div>

        <div v-if="blockedGames.length > 0" class="blocked-games-list">
          <div 
            v-for="game in blockedGames" 
            :key="game.id"
            class="blocked-game-item"
          >
            <span>{{ game.name }}</span>
            <button 
              class="btn-remove"
              @click="removeBlockedGame(game.id)"
            >
              ×
            </button>
          </div>
        </div>
      </div>
    </section>

    <!-- 活动监控 -->
    <section class="parental-section" v-if="isEnabled">
      <h2 class="section-title">活动监控</h2>
      <div class="settings-card">
        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">游戏活动报告</h3>
            <p class="setting-desc">定期发送游戏活动报告到邮箱</p>
          </div>
          <div class="setting-action">
            <label class="toggle-switch">
              <input 
                type="checkbox" 
                v-model="monitoring.sendReports"
              />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <div class="setting-item">
          <div class="setting-info">
            <h3 class="setting-label">报告频率</h3>
            <p class="setting-desc">选择报告发送频率</p>
          </div>
          <div class="setting-action">
            <select v-model="monitoring.reportFrequency" class="setting-select">
              <option value="daily">每日</option>
              <option value="weekly">每周</option>
              <option value="monthly">每月</option>
            </select>
          </div>
        </div>
      </div>
    </section>

    <!-- 保存按钮 -->
    <div class="parental-actions">
      <button class="btn btn-primary" @click="handleSave" :disabled="saving">
        {{ saving ? '保存中...' : '保存设置' }}
      </button>
      <button class="btn btn-secondary" @click="handleReset">
        重置
      </button>
    </div>

    <!-- 添加禁止游戏对话框 -->
    <div v-if="showBlockGameDialog" class="modal-overlay" @click="showBlockGameDialog = false">
      <div class="modal-content" @click.stop>
        <h3 class="modal-title">添加禁止游戏</h3>
        <div class="modal-body">
          <input 
            v-model="gameSearchQuery"
            type="text" 
            class="form-input"
            placeholder="搜索游戏名称..."
            @input="searchGames"
          />
          <div v-if="searchResults.length > 0" class="search-results">
            <div 
              v-for="game in searchResults" 
              :key="game.id"
              class="search-result-item"
              @click="addBlockedGame(game)"
            >
              {{ game.name }}
            </div>
          </div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-secondary" @click="showBlockGameDialog = false">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import parentalApi from '@/api/parental'

const isEnabled = ref(false)
const saving = ref(false)
const showBlockGameDialog = ref(false)
const gameSearchQuery = ref('')

// 邀请相关
const childUsername = ref('')
const inviteMessage = ref('')
const inviting = ref(false)

const timeLimits = ref({
  dailyHours: 2,
  weeklyHours: 14,
  startTime: '09:00',
  endTime: '21:00',
  allowedDays: [1, 2, 3, 4, 5, 6, 0] // 0-6 代表周日到周六
})

const contentRestrictions = ref({
  ageRating: 'all',
  blockedTags: []
})

const monitoring = ref({
  sendReports: true,
  reportFrequency: 'weekly'
})

const blockedGames = ref([])

const weekdays = [
  { value: 0, label: '日' },
  { value: 1, label: '一' },
  { value: 2, label: '二' },
  { value: 3, label: '三' },
  { value: 4, label: '四' },
  { value: 5, label: '五' },
  { value: 6, label: '六' }
]

const contentTags = [
  { value: 'violence', label: '暴力' },
  { value: 'blood', label: '血腥' },
  { value: 'language', label: '不当语言' },
  { value: 'sexual', label: '性内容' },
  { value: 'gambling', label: '赌博' },
  { value: 'horror', label: '恐怖' }
]

const searchResults = ref([])

const handleSendInvitation = async () => {
  if (!childUsername.value.trim()) {
    alert('请输入子账户用户名')
    return
  }

  inviting.value = true
  try {
    const payload = {
      childUsername: childUsername.value.trim()
    }
    if (inviteMessage.value.trim()) {
      payload.message = inviteMessage.value.trim()
    }

    const res = await parentalApi.createInvitation(payload)
    if (res && res.success !== false) {
      alert('邀请已发送，对方需要在消息中心同意后才会生效')
      childUsername.value = ''
      inviteMessage.value = ''
    }
  } catch (error) {
    console.error('发送家长邀请失败:', error)
    alert('发送邀请失败: ' + (error.message || '未知错误'))
  } finally {
    inviting.value = false
  }
}

const handleToggle = () => {
  // TODO: 调用 API 更新状态
}

const handleSave = async () => {
  saving.value = true
  try {
    // TODO: 调用 API 保存设置
    await new Promise(resolve => setTimeout(resolve, 500))
    alert('设置已保存')
  } catch (error) {
    alert('保存失败: ' + error.message)
  } finally {
    saving.value = false
  }
}

const handleReset = () => {
  if (confirm('确定要重置所有设置吗？')) {
    // 重置为默认值
    timeLimits.value = {
      dailyHours: 2,
      weeklyHours: 14,
      startTime: '09:00',
      endTime: '21:00',
      allowedDays: [1, 2, 3, 4, 5, 6, 0]
    }
    contentRestrictions.value = {
      ageRating: 'all',
      blockedTags: []
    }
    monitoring.value = {
      sendReports: true,
      reportFrequency: 'weekly'
    }
    blockedGames.value = []
  }
}

const searchGames = () => {
  // TODO: 调用 API 搜索游戏
  if (gameSearchQuery.value.trim()) {
    searchResults.value = [
      { id: 1, name: '示例游戏 1' },
      { id: 2, name: '示例游戏 2' }
    ]
  } else {
    searchResults.value = []
  }
}

const addBlockedGame = (game) => {
  if (!blockedGames.value.find(g => g.id === game.id)) {
    blockedGames.value.push(game)
  }
  showBlockGameDialog.value = false
  gameSearchQuery.value = ''
  searchResults.value = []
}

const removeBlockedGame = (gameId) => {
  blockedGames.value = blockedGames.value.filter(g => g.id !== gameId)
}

onMounted(() => {
  // TODO: 从 API 加载设置
})
</script>

<style scoped>
.parental-container {
  max-width: 900px;
  margin: 0 auto;
  padding: var(--spacing-lg);
}

.parental-header {
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

.parental-title {
  font-size: 32px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.parental-subtitle {
  font-size: 16px;
  color: var(--text-secondary);
}

.header-status {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-sm) var(--spacing-md);
  border-radius: var(--radius-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
}

.header-status.active {
  background: rgba(16, 185, 129, 0.1);
  border-color: var(--success-color);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--text-tertiary);
}

.header-status.active .status-dot {
  background: var(--success-color);
}

.status-text {
  font-size: 14px;
  color: var(--text-secondary);
}

.header-status.active .status-text {
  color: var(--success-color);
}

.parental-section {
  margin-bottom: var(--spacing-xl);
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--spacing-md);
}

.settings-card {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color-strong);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  box-shadow: var(--shadow-md);
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--spacing-md) 0;
  border-bottom: 1px solid var(--border-color-light);
}

.setting-item:last-child {
  border-bottom: none;
}

.setting-info {
  flex: 1;
}

.setting-label {
  font-size: 16px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.setting-desc {
  font-size: 14px;
  color: var(--text-secondary);
}

.setting-action {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
}

.setting-input,
.setting-select {
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  font-size: 14px;
}

.setting-input:focus,
.setting-select:focus {
  outline: none;
  border-color: var(--primary-color);
}

.setting-unit {
  font-size: 14px;
  color: var(--text-secondary);
}

.time-range {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
}

.time-separator {
  color: var(--text-secondary);
}

.weekdays-selector {
  display: flex;
  gap: var(--spacing-sm);
  flex-wrap: wrap;
}

.weekday-checkbox {
  display: flex;
  align-items: center;
  padding: var(--spacing-xs) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.3s;
}

.weekday-checkbox input[type="checkbox"] {
  display: none;
}

.weekday-checkbox input[type="checkbox"]:checked + span {
  color: var(--primary-color);
}

.weekday-checkbox:has(input[type="checkbox"]:checked) {
  background: rgba(99, 102, 241, 0.1);
  border-color: var(--primary-color);
}

.content-tags {
  display: flex;
  gap: var(--spacing-sm);
  flex-wrap: wrap;
}

.content-tag {
  display: flex;
  align-items: center;
  padding: var(--spacing-xs) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.3s;
}

.content-tag input[type="checkbox"] {
  display: none;
}

.content-tag:has(input[type="checkbox"]:checked) {
  background: rgba(239, 68, 68, 0.1);
  border-color: var(--error-color);
}

.blocked-games-list {
  margin-top: var(--spacing-md);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.blocked-game-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
}

.btn-remove {
  background: var(--error-color);
  color: white;
  border: none;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  cursor: pointer;
  font-size: 18px;
  line-height: 1;
}

.btn-remove:hover {
  background: #dc2626;
}

.toggle-switch {
  display: flex;
  align-items: center;
  cursor: pointer;
}

.toggle-switch input[type="checkbox"] {
  display: none;
}

.toggle-slider {
  position: relative;
  width: 44px;
  height: 24px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  transition: all 0.3s;
}

.toggle-slider::before {
  content: '';
  position: absolute;
  width: 18px;
  height: 18px;
  left: 2px;
  top: 2px;
  background: var(--text-secondary);
  border-radius: 50%;
  transition: all 0.3s;
}

.toggle-switch input[type="checkbox"]:checked + .toggle-slider {
  background: var(--primary-color);
  border-color: var(--primary-color);
}

.toggle-switch input[type="checkbox"]:checked + .toggle-slider::before {
  transform: translateX(20px);
  background: white;
}

.parental-actions {
  display: flex;
  gap: var(--spacing-md);
  justify-content: flex-end;
  margin-top: var(--spacing-xl);
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

.btn-secondary:hover {
  background: var(--bg-surface);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* 模态框样式 */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color-strong);
  border-radius: var(--radius-lg);
  padding: var(--spacing-xl);
  max-width: 500px;
  width: 90%;
  box-shadow: var(--shadow-lg);
}

.modal-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--spacing-lg);
}

.modal-body {
  margin-bottom: var(--spacing-lg);
}

.form-input {
  width: 100%;
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  font-size: 14px;
}

.form-input:focus {
  outline: none;
  border-color: var(--primary-color);
}

.search-results {
  margin-top: var(--spacing-md);
  max-height: 300px;
  overflow-y: auto;
}

.search-result-item {
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  margin-bottom: var(--spacing-xs);
  cursor: pointer;
  transition: all 0.3s;
}

.search-result-item:hover {
  background: var(--bg-surface);
  border-color: var(--primary-color);
}

.modal-actions {
  display: flex;
  gap: var(--spacing-md);
  justify-content: flex-end;
}
</style>

