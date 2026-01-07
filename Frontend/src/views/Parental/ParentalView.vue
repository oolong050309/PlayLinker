<template>
  <div class="parental-container">
    <div class="parental-header">
      <div class="header-content">
        <h1 class="parental-title">家长监管</h1>
        <p class="parental-subtitle">管理孩子的游戏时间和内容访问</p>
      </div>
      <button 
        class="header-status role-toggle-btn" 
        :class="{ active: isParent }"
        @click="handleToggleRole"
        :disabled="switchingRole || (isParent && children.length > 0) || (!isParent && parentInfo)"
      >
        <span class="status-dot"></span>
        <span class="status-text">{{ isParent ? '家长角色' : '普通用户' }}</span>
        <span v-if="switchingRole" class="switching-indicator">切换中...</span>
      </button>
    </div>

    <!-- 关系列表 -->
    <section class="parental-section">
      <h2 class="section-title">{{ isParent ? '我的孩子' : '我的家长' }}</h2>
      <div class="settings-card">
        <div v-if="loadingRelationships" class="loading-state">
          <p>加载中...</p>
        </div>
        <div v-else-if="isParent && children.length === 0" class="empty-state">
          <p>您还没有监管任何子账户</p>
        </div>
        <div v-else-if="!isParent && !parentInfo" class="empty-state">
          <p>您还没有建立监管关系</p>
        </div>
        <div v-else>
          <!-- 家长视角：显示孩子列表 -->
          <div v-if="isParent" class="relationships-list">
            <div 
              v-for="child in children" 
              :key="child.childUserId"
              class="relationship-item expanded"
            >
              <div class="relationship-header" @click="toggleChildExpanded(child.childUserId)">
                <div class="relationship-info">
                  <h4 class="relationship-name">{{ child.childUsername }}</h4>
                  <div class="relationship-stats">
                    <span>活跃规则: {{ child.activeRulesCount || 0 }}</span>
                    <span>昨日游戏时长: {{ formatPlaytime(child.todayPlaytime || 0) }}</span>
                    <span>近期违规: {{ child.recentAlerts || 0 }} 次</span>
                  </div>
                  <!-- 游戏时长进度条（如果有每日限制规则） -->
                  <div v-if="getDailyLimitRule(child.childUserId)" class="playtime-summary">
                    <div class="playtime-summary-info">
                      <span class="summary-label">昨日进度:</span>
                      <span class="summary-value">
                        {{ formatPlaytime(child.todayPlaytime || 0) }} / {{ formatPlaytime(getDailyLimitRule(child.childUserId)?.ruleValue?.limitMinutes || 0) }}
                      </span>
                    </div>
                    <div class="playtime-progress-bar">
                      <div 
                        class="progress-fill-summary" 
                        :class="{ 
                          'exceeded': (child.todayPlaytime || 0) >= (getDailyLimitRule(child.childUserId)?.ruleValue?.limitMinutes || 0),
                          'warning': (child.todayPlaytime || 0) >= (getDailyLimitRule(child.childUserId)?.ruleValue?.warningMinutes || 0) && (child.todayPlaytime || 0) < (getDailyLimitRule(child.childUserId)?.ruleValue?.limitMinutes || 0)
                        }"
                        :style="{ 
                          width: `${Math.min(100, ((child.todayPlaytime || 0) / (getDailyLimitRule(child.childUserId)?.ruleValue?.limitMinutes || 1)) * 100)}%` 
                        }"
                      ></div>
                    </div>
                  </div>
                </div>
                <div class="relationship-actions">
                  <button 
                    class="btn btn-secondary"
                    @click.stop="openRuleDialog(child)"
                  >
                    添加规则
                  </button>
                  <button 
                    class="btn btn-danger"
                    @click.stop="handleDeleteRelationship(child.childUserId)"
                  >
                    解除关系
                  </button>
                  <span class="expand-icon" :class="{ expanded: expandedChildren[child.childUserId] }">▼</span>
                </div>
              </div>
              <!-- 规则列表 -->
              <div v-if="expandedChildren[child.childUserId]" class="rules-container">
                <!-- 过去一周游玩时间统计 -->
                <div class="weekly-playtime-section">
                  <h4 class="weekly-playtime-title">过去一周游玩时间</h4>
                  <div v-if="loadingWeeklyPlaytime[child.childUserId]" class="loading-state">
                    <p>加载中...</p>
                  </div>
                  <div v-else-if="weeklyPlaytime[child.childUserId] && weeklyPlaytime[child.childUserId].length > 0" class="weekly-playtime-chart">
                    <div class="weekly-playtime-bars">
                      <div 
                        v-for="(day, index) in weeklyPlaytime[child.childUserId]" 
                        :key="index"
                        class="playtime-bar-item"
                      >
                        <div class="bar-container">
                          <div 
                            class="playtime-bar"
                            :style="{ 
                              height: `${Math.max(5, (day.playtimeMinutes / Math.max(1, getMaxPlaytime(child.childUserId))) * 100)}%` 
                            }"
                            :title="`${day.dayOfWeek} ${day.date}: ${formatPlaytime(day.playtimeMinutes)}`"
                          ></div>
                        </div>
                        <div class="bar-label">
                          <div class="bar-date">{{ day.dayOfWeek }}</div>
                          <div class="bar-time">{{ formatPlaytimeShort(day.playtimeMinutes) }}</div>
                        </div>
                      </div>
                    </div>
                    <div class="weekly-playtime-summary">
                      <span>一周总计: {{ formatPlaytime(getWeeklyTotal(child.childUserId)) }}</span>
                    </div>
                  </div>
                  <div v-else class="empty-state">
                    <p>暂无游玩时间数据</p>
                  </div>
                </div>

                <div v-if="loadingRules[child.childUserId]" class="loading-state">
                  <p>加载规则中...</p>
                </div>
                <div v-else-if="childRules[child.childUserId] && childRules[child.childUserId].length === 0" class="empty-state">
                  <p>暂无规则，点击"添加规则"创建</p>
                </div>
                <div v-else class="rules-list">
                  <div 
                    v-for="rule in childRules[child.childUserId]" 
                    :key="rule.ruleId"
                    class="rule-item"
                  >
                    <div class="rule-info">
                      <div class="rule-header">
                        <span class="rule-type">{{ getRuleTypeLabel(rule.ruleType) }}</span>
                        <span class="rule-status" :class="{ active: rule.isActive, inactive: !rule.isActive }">
                          {{ rule.isActive ? '已启用' : '已禁用' }}
                        </span>
                      </div>
                      <div class="rule-details">
                        <div class="rule-value-display">
                          <div v-if="rule.ruleType === 'playtime_daily_limit'" class="playtime-limit-info">
                            <div class="limit-item">
                              <span class="limit-label">每日限制:</span>
                              <span class="limit-value">{{ formatPlaytime(rule.ruleValue?.limitMinutes || 0) }}</span>
                            </div>
                            <div class="limit-item" v-if="rule.ruleValue?.warningMinutes">
                              <span class="limit-label">警告阈值:</span>
                              <span class="limit-value warning">{{ formatPlaytime(rule.ruleValue.warningMinutes) }}</span>
                            </div>
                            <div class="limit-item" v-if="child && rule.ruleType === 'playtime_daily_limit'">
                              <span class="limit-label">昨日已用:</span>
                              <span class="limit-value" :class="{ 
                                'exceeded': (child.todayPlaytime || 0) >= (rule.ruleValue?.limitMinutes || 0),
                                'warning': (child.todayPlaytime || 0) >= (rule.ruleValue?.warningMinutes || 0) && (child.todayPlaytime || 0) < (rule.ruleValue?.limitMinutes || 0)
                              }">
                                {{ formatPlaytime(child.todayPlaytime || 0) }}
                              </span>
                            </div>
                            <div class="limit-item" v-if="child && rule.ruleType === 'playtime_daily_limit'">
                              <span class="limit-label">剩余时长:</span>
                              <span class="limit-value" :class="{ 
                                'exceeded': (child.todayPlaytime || 0) >= (rule.ruleValue?.limitMinutes || 0)
                              }">
                                {{ formatPlaytime(Math.max(0, (rule.ruleValue?.limitMinutes || 0) - (child.todayPlaytime || 0))) }}
                              </span>
                            </div>
                            <div v-if="child && rule.ruleType === 'playtime_daily_limit'" class="playtime-progress">
                              <div class="progress-bar">
                                <div 
                                  class="progress-fill" 
                                  :class="{ 
                                    'exceeded': (child.todayPlaytime || 0) >= (rule.ruleValue?.limitMinutes || 0),
                                    'warning': (child.todayPlaytime || 0) >= (rule.ruleValue?.warningMinutes || 0) && (child.todayPlaytime || 0) < (rule.ruleValue?.limitMinutes || 0)
                                  }"
                                  :style="{ 
                                    width: `${Math.min(100, ((child.todayPlaytime || 0) / (rule.ruleValue?.limitMinutes || 1)) * 100)}%` 
                                  }"
                                ></div>
                              </div>
                            </div>
                          </div>
                          <div v-else-if="rule.ruleType === 'game_restriction'" class="game-restriction-info">
                            <div class="limit-item">
                              <span class="limit-label">禁止游戏:</span>
                              <span class="limit-value">
                                {{ Array.isArray(rule.ruleValue?.blockedGameNames) ? rule.ruleValue.blockedGameNames.length + ' 个游戏' : '未设置' }}
                              </span>
                            </div>
                            <div v-if="Array.isArray(rule.ruleValue?.blockedGameNames) && rule.ruleValue.blockedGameNames.length > 0" class="blocked-games-tags">
                              <span v-for="(gameName, index) in rule.ruleValue.blockedGameNames.slice(0, 5)" :key="index" class="game-tag">
                                {{ gameName }}
                              </span>
                              <span v-if="rule.ruleValue.blockedGameNames.length > 5" class="game-tag more">
                                +{{ rule.ruleValue.blockedGameNames.length - 5 }} 更多
                              </span>
                            </div>
                          </div>
                          <div v-else-if="rule.ruleType === 'age_restriction'" class="age-restriction-info">
                            <div class="limit-item">
                              <span class="limit-label">最大年龄分级:</span>
                              <span class="limit-value">{{ rule.ruleValue?.maxAgeRating || '--' }}+</span>
                            </div>
                          </div>
                          <div v-else class="rule-value-raw">
                            <pre class="rule-value">{{ formatRuleValue(rule.ruleValue) }}</pre>
                          </div>
                        </div>
                        <div class="rule-statistics" v-if="rule.statistics">
                          <span>总违规: {{ rule.statistics.totalViolations || 0 }}</span>
                          <span>近期违规: {{ rule.statistics.recentViolations || 0 }}</span>
                        </div>
                      </div>
                    </div>
                    <div class="rule-actions">
                      <button 
                        v-if="rule.isActive"
                        class="btn btn-sm btn-warning"
                        @click="handleToggleRuleStatus(rule, child.childUserId, false)"
                        :disabled="togglingRuleStatus[rule.ruleId]"
                      >
                        {{ togglingRuleStatus[rule.ruleId] ? '停用中...' : '停用' }}
                      </button>
                      <button 
                        v-else
                        class="btn btn-sm btn-success"
                        @click="handleToggleRuleStatus(rule, child.childUserId, true)"
                        :disabled="togglingRuleStatus[rule.ruleId]"
                      >
                        {{ togglingRuleStatus[rule.ruleId] ? '启用中...' : '启用' }}
                      </button>
                  <button 
                    class="btn btn-sm btn-secondary"
                    @click="openEditRuleDialog(child, rule)"
                  >
                    编辑
                  </button>
                      <button 
                        class="btn btn-sm btn-danger"
                        @click="handleDeleteRule(rule.ruleId, child.childUserId)"
                      >
                        删除
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <!-- 孩子视角：显示家长信息 -->
          <div v-else-if="parentInfo" class="relationship-item">
            <div class="relationship-info">
              <h4 class="relationship-name">{{ parentInfo.parentUsername }}</h4>
              <div class="relationship-stats">
                <span>建立时间: {{ formatDate(parentInfo.createdAt) }}</span>
              </div>
            </div>
            <div class="relationship-actions">
              <p class="relationship-note">您无法主动解除监管关系，如需解除请联系家长</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 邀请子账户（仅家长可见） -->
    <section v-if="isParent" class="parental-section">
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



    <!-- 规则设置对话框 -->
    <div v-if="showRuleDialog" class="modal-overlay" @click="showRuleDialog = false">
      <div class="modal-content rule-dialog" @click.stop>
        <h3 class="modal-title">{{ editingRule ? '编辑' : '添加' }}监管规则 - {{ selectedChild?.childUsername }}</h3>
        <div class="modal-body">
          <div class="form-group">
            <label class="form-label">规则类型</label>
            <select 
              v-model="ruleForm.ruleType" 
              class="form-input" 
              @change="updateRuleValue"
              :disabled="!!editingRule"
            >
              <option value="playtime_daily_limit">每日时长限制</option>
              <option value="game_restriction">游戏限制</option>
              <option value="age_restriction">年龄限制</option>
            </select>
            <p v-if="editingRule" class="form-hint">规则类型创建后不可修改</p>
          </div>

          <!-- 每日时长限制 -->
          <div v-if="ruleForm.ruleType === 'playtime_daily_limit'" class="form-group">
            <label class="form-label">每日限制时长</label>
            <div class="time-input-group">
              <input 
                v-model.number="ruleForm.ruleValue.limitMinutes"
                type="number"
                class="form-input"
                min="1"
                placeholder="120"
                @input="validatePlaytimeLimit"
              />
              <span class="input-unit">分钟</span>
            </div>
            <p class="form-hint">
              建议值：小学生60-90分钟，中学生90-120分钟，高中生120-180分钟
            </p>
            <div v-if="selectedChild" class="current-playtime-info">
              <span class="info-label">昨日已用时长:</span>
              <span class="info-value" :class="{ 
                'exceeded': (selectedChild.todayPlaytime || 0) >= (ruleForm.ruleValue.limitMinutes || 0),
                'warning': (selectedChild.todayPlaytime || 0) >= (ruleForm.ruleValue.limitMinutes * 0.8 || 0)
              }">
                {{ formatPlaytime(selectedChild.todayPlaytime || 0) }}
              </span>
            </div>
            
            <label class="form-label" style="margin-top: 15px;">警告时长（可选）</label>
            <div class="time-input-group">
              <input 
                v-model.number="ruleForm.ruleValue.warningMinutes"
                type="number"
                class="form-input"
                min="0"
                :max="ruleForm.ruleValue.limitMinutes || 9999"
                placeholder="100"
              />
              <span class="input-unit">分钟</span>
            </div>
            <p class="form-hint">
              当游戏时长达到此值时，系统会提前发送警告通知（建议设置为限制时长的80%）
            </p>
            
            <label class="form-label" style="margin-top: 15px;">每日重置时间</label>
            <input 
              v-model="ruleForm.ruleValue.resetTime"
              type="time"
              class="form-input"
              placeholder="00:00"
            />
            <p class="form-hint">
              每日在此时间重置游戏时长统计（默认00:00，即午夜）
            </p>
          </div>

          <!-- 游戏限制 -->
          <div v-if="ruleForm.ruleType === 'game_restriction'" class="form-group">
            <label class="form-label">禁止的游戏名称列表（用逗号分隔）</label>
            <input 
              :value="Array.isArray(ruleForm.ruleValue.blockedGameNames) ? ruleForm.ruleValue.blockedGameNames.join(',') : (typeof ruleForm.ruleValue.blockedGameNames === 'string' ? ruleForm.ruleValue.blockedGameNames : '')"
              type="text"
              class="form-input"
              placeholder="例如: 游戏1,游戏2,游戏3"
              @input="(e) => updateGameRestriction(e.target.value)"
            />
            <p class="form-hint">请输入游戏名称，多个游戏用逗号分隔</p>
          </div>

          <!-- 年龄限制 -->
          <div v-if="ruleForm.ruleType === 'age_restriction'" class="form-group">
            <label class="form-label">最大年龄分级</label>
            <select v-model.number="ruleForm.ruleValue.maxAgeRating" class="form-input">
              <option :value="3">3+</option>
              <option :value="7">7+</option>
              <option :value="12">12+</option>
              <option :value="16">16+</option>
              <option :value="18">18+</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">
              <input 
                type="checkbox"
                v-model="ruleForm.isActive"
                style="margin-right: 8px;"
              />
              立即生效
            </label>
          </div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-secondary" @click="showRuleDialog = false">取消</button>
          <button 
            class="btn btn-primary" 
            @click="handleSaveRule"
            :disabled="savingRule"
          >
            {{ savingRule ? '保存中...' : (editingRule ? '更新规则' : '保存规则') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import parentalApi from '@/api/parental'
import usersApi from '@/api/users'

// 关系相关
const isParent = ref(false)
const children = ref([])
const parentInfo = ref(null)
const loadingRelationships = ref(true)
const expandedChildren = ref({})
const childRules = ref({})
const loadingRules = ref({})
const togglingRuleStatus = ref({})
const switchingRole = ref(false)
const weeklyPlaytime = ref({})
const loadingWeeklyPlaytime = ref({})

// 邀请相关
const childUsername = ref('')
const inviteMessage = ref('')
const inviting = ref(false)

// 规则设置相关
const showRuleDialog = ref(false)
const selectedChild = ref(null)
const editingRule = ref(null) // 编辑中的规则（如果有）
const ruleForm = ref({
  ruleType: 'playtime_daily_limit',
  ruleValue: {},
  isActive: true
})
const savingRule = ref(false)

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
      // 注意：这里不刷新关系列表，因为邀请还未被接受
    }
  } catch (error) {
    console.error('发送家长邀请失败:', error)
    alert('发送邀请失败: ' + (error.message || '未知错误'))
  } finally {
    inviting.value = false
  }
}

// 切换子账户展开/折叠
const toggleChildExpanded = async (childId) => {
  expandedChildren.value[childId] = !expandedChildren.value[childId]
  // 每次展开时都重新加载规则和游玩时间，确保获取最新状态
  if (expandedChildren.value[childId]) {
    await Promise.all([
      loadChildRules(childId),
      loadWeeklyPlaytime(childId)
    ])
  }
}

// 加载子账户过去一周的游玩时间
const loadWeeklyPlaytime = async (childId) => {
  loadingWeeklyPlaytime.value[childId] = true
  try {
    const res = await parentalApi.getChildWeeklyPlaytime(childId)
    if (res && res.success !== false && res.data) {
      weeklyPlaytime.value[childId] = res.data.weeklyData || []
    }
  } catch (error) {
    console.error('加载游玩时间失败:', error)
    weeklyPlaytime.value[childId] = []
  } finally {
    loadingWeeklyPlaytime.value[childId] = false
  }
}

// 获取一周中的最大游玩时间（用于计算柱状图高度）
const getMaxPlaytime = (childId) => {
  const data = weeklyPlaytime.value[childId]
  if (!data || data.length === 0) return 1
  return Math.max(...data.map(d => d.playtimeMinutes), 1)
}

// 获取一周总游玩时间
const getWeeklyTotal = (childId) => {
  const data = weeklyPlaytime.value[childId]
  if (!data || data.length === 0) return 0
  return data.reduce((sum, d) => sum + (d.playtimeMinutes || 0), 0)
}

// 格式化游玩时间（简短版，用于图表）
const formatPlaytimeShort = (minutes) => {
  if (!minutes || minutes === 0) return '0'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours > 0 && mins > 0) {
    return `${hours}h${mins}m`
  } else if (hours > 0) {
    return `${hours}h`
  } else {
    return `${mins}m`
  }
}

// 加载子账户的规则列表
const loadChildRules = async (childId) => {
  loadingRules.value[childId] = true
  try {
    const res = await parentalApi.getRules(childId)
    if (res && res.success !== false && res.data) {
      childRules.value[childId] = res.data.rules || []
    }
  } catch (error) {
    console.error('加载规则失败:', error)
    childRules.value[childId] = []
  } finally {
    loadingRules.value[childId] = false
  }
}

// 获取规则类型标签
const getRuleTypeLabel = (ruleType) => {
  const labels = {
    'playtime_daily_limit': '每日时长限制',
    'game_restriction': '游戏限制',
    'age_restriction': '年龄限制'
  }
  return labels[ruleType] || ruleType
}

// 格式化规则值显示
const formatRuleValue = (ruleValue) => {
  if (!ruleValue) return '无'
  try {
    return JSON.stringify(ruleValue, null, 2)
  } catch {
    return String(ruleValue)
  }
}

// 格式化游戏时长（分钟转小时和分钟）
const formatPlaytime = (minutes) => {
  if (!minutes || minutes === 0) return '0分钟'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours > 0 && mins > 0) {
    return `${hours}小时${mins}分钟`
  } else if (hours > 0) {
    return `${hours}小时`
  } else {
    return `${mins}分钟`
  }
}

// 获取子账户的每日限制规则
const getDailyLimitRule = (childId) => {
  const rules = childRules.value[childId]
  if (!rules || !Array.isArray(rules)) return null
  return rules.find(r => r.ruleType === 'playtime_daily_limit' && r.isActive)
}

// 切换规则状态（启用/停用）
const handleToggleRuleStatus = async (rule, childId, isActive) => {
  togglingRuleStatus.value[rule.ruleId] = true
  try {
    // 传递当前的规则值，确保不会丢失规则内容
    const res = await parentalApi.toggleRuleStatus(rule.ruleId, rule.ruleValue, isActive)
    if (res && res.success !== false) {
      // 更新本地规则状态，避免重新加载整个列表
      const rules = childRules.value[childId]
      if (rules) {
        const foundRule = rules.find(r => r.ruleId === rule.ruleId)
        if (foundRule) {
          foundRule.isActive = isActive
        }
      }
      // 刷新关系列表以更新活跃规则数
      await loadRelationships()
    }
  } catch (error) {
    console.error('切换规则状态失败:', error)
    alert('切换规则状态失败: ' + (error.response?.data?.message || error.message || '未知错误'))
  } finally {
    togglingRuleStatus.value[rule.ruleId] = false
  }
}

// 删除规则
const handleDeleteRule = async (ruleId, childId) => {
  if (!confirm('确定要删除这个规则吗？')) {
    return
  }

  try {
    const res = await parentalApi.deleteRule(ruleId)
    if (res && res.success !== false) {
      alert('规则已删除')
      await loadChildRules(childId) // 重新加载规则列表
      await loadRelationships() // 刷新关系列表以更新活跃规则数
    }
  } catch (error) {
    console.error('删除规则失败:', error)
    alert('删除规则失败: ' + (error.response?.data?.message || error.message || '未知错误'))
  }
}

// 加载当前用户角色
const loadCurrentRole = () => {
  try {
    const userStr = sessionStorage.getItem('user')
    if (userStr) {
      const user = JSON.parse(userStr)
      // 检查用户角色，如果role是'parent'则设置为家长
      isParent.value = user.role === 'parent' || user.Role === 'parent'
    }
  } catch (error) {
    console.error('加载用户角色失败:', error)
  }
}

// 切换用户角色
const handleToggleRole = async () => {
  if (switchingRole.value) return

  // 如果已存在监管关系，则禁止切换角色
  // 1) 家长已有孩子：不能切回普通用户
  if (isParent.value && children.value && children.value.length > 0) {
    alert('当前账号已建立家长监管关系（存在被监管的子账户），无法切换角色。请先解除所有监管关系后再切换。')
    return
  }
  // 2) 当前账号有家长：不能切到家长角色（避免出现同时被监管又监管他人等混乱状态）
  if (!isParent.value && parentInfo.value) {
    alert('当前账号已被家长监管，无法切换为家长角色。')
    return
  }

  const newRole = !isParent.value ? 'parent' : 'user'
  const confirmMessage = newRole === 'parent' 
    ? '确定要切换为家长角色吗？切换后您将可以管理子账户。' 
    : '确定要切换为普通用户角色吗？切换后您将失去家长管理权限。'

  if (!confirm(confirmMessage)) {
    return
  }

  switchingRole.value = true
  try {
    // 调用后端 API 更新角色
    const res = await usersApi.updateRole({ role: newRole })
    if (res && res.success !== false) {
      // 更新sessionStorage中的角色
      const userStr = sessionStorage.getItem('user')
      if (userStr) {
        const user = JSON.parse(userStr)
        user.role = newRole
        user.Role = newRole
        sessionStorage.setItem('user', JSON.stringify(user))
        
        // 触发用户信息更新事件，通知其他组件
        window.dispatchEvent(new CustomEvent('userInfoUpdated', {
          detail: { user }
        }))
      }

      // 更新本地状态
      isParent.value = newRole === 'parent'

      // 清除已缓存的规则列表和展开状态，确保重新加载最新数据
      childRules.value = {}
      expandedChildren.value = {}

      // 重新加载关系列表（这会获取最新的子账户信息和规则统计）
      await loadRelationships()

      alert(`已切换为${newRole === 'parent' ? '家长' : '普通用户'}角色`)
    } else {
      throw new Error(res?.message || '更新角色失败')
    }
  } catch (error) {
    console.error('切换角色失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('切换角色失败: ' + errorMessage)
  } finally {
    switchingRole.value = false
  }
}

// 加载关系列表
const loadRelationships = async () => {
  loadingRelationships.value = true
  try {
    // 如果当前是家长角色，尝试获取子账户列表
    if (isParent.value) {
      try {
        const res = await parentalApi.getChildren()
        if (res && res.success !== false && res.data) {
          children.value = res.data.children || []
          return
        }
      } catch (error) {
        // 如果获取失败，可能是角色不匹配，重置为普通用户
        console.warn('获取子账户列表失败，可能不是家长角色:', error)
        isParent.value = false
        const userStr = sessionStorage.getItem('user')
        if (userStr) {
          const user = JSON.parse(userStr)
          user.role = 'user'
          user.Role = 'user'
          sessionStorage.setItem('user', JSON.stringify(user))
        }
      }
    }

    // 尝试获取家长信息（孩子）
    try {
      const res = await parentalApi.getParent()
      if (res && res.success !== false && res.data) {
        isParent.value = false
        parentInfo.value = res.data
        return
      }
    } catch (error) {
      // 没有建立关系
      isParent.value = false
      parentInfo.value = null
    }
  } catch (error) {
    console.error('加载关系列表失败:', error)
  } finally {
    loadingRelationships.value = false
  }
}

// 删除监管关系
const handleDeleteRelationship = async (childId) => {
  if (!confirm('确定要解除与这个子账户的监管关系吗？解除后，所有相关规则将被删除，子账户将收到通知。')) {
    return
  }

  try {
    const res = await parentalApi.deleteRelationship(childId)
    if (res && res.success !== false) {
      alert('监管关系已解除')
      await loadRelationships() // 重新加载关系列表
    }
  } catch (error) {
    console.error('解除关系失败:', error)
    alert('解除关系失败: ' + (error.response?.data?.message || error.message || '未知错误'))
  }
}

// 打开规则设置对话框（新建）
const openRuleDialog = (child) => {
  selectedChild.value = child
  editingRule.value = null
  ruleForm.value = {
    ruleType: 'playtime_daily_limit',
    ruleValue: {},
    isActive: true
  }
  updateRuleValue()
  showRuleDialog.value = true
}

// 打开编辑规则对话框
const openEditRuleDialog = (child, rule) => {
  selectedChild.value = child
  editingRule.value = rule
  let ruleValue = rule.ruleValue ? JSON.parse(JSON.stringify(rule.ruleValue)) : {}
  
  // 兼容旧数据：如果存在 blockedGameIds，转换为 blockedGameNames（显示为占位符）
  if (rule.ruleType === 'game_restriction' && ruleValue.blockedGameIds && !ruleValue.blockedGameNames) {
    ruleValue.blockedGameNames = [] // 旧数据无法直接转换游戏名，清空让用户重新输入
  }
  
  ruleForm.value = {
    ruleType: rule.ruleType,
    ruleValue: ruleValue,
    isActive: rule.isActive
  }
  showRuleDialog.value = true
}

// 保存规则
const handleSaveRule = async () => {
  if (!selectedChild.value) return

  // 验证每日时长限制规则
  if (ruleForm.value.ruleType === 'playtime_daily_limit') {
    if (!ruleForm.value.ruleValue.limitMinutes || ruleForm.value.ruleValue.limitMinutes < 1) {
      alert('每日限制时长必须大于0分钟')
      return
    }
    if (ruleForm.value.ruleValue.warningMinutes && ruleForm.value.ruleValue.warningMinutes >= ruleForm.value.ruleValue.limitMinutes) {
      alert('警告时长必须小于限制时长')
      return
    }
  }

  // 处理游戏限制：确保 blockedGameNames 是数组
  let ruleValue = { ...ruleForm.value.ruleValue }
  if (ruleForm.value.ruleType === 'game_restriction') {
    if (typeof ruleValue.blockedGameNames === 'string') {
      ruleValue.blockedGameNames = ruleValue.blockedGameNames.split(',')
        .map(name => name.trim())
        .filter(name => name.length > 0)
    }
    if (!Array.isArray(ruleValue.blockedGameNames)) {
      ruleValue.blockedGameNames = []
    }
  }

  savingRule.value = true
  try {
    if (editingRule.value) {
      // 更新现有规则
      const payload = {
        ruleValue: ruleValue,
        isActive: ruleForm.value.isActive
      }
      const res = await parentalApi.updateRule(editingRule.value.ruleId, payload)
      if (res && res.success !== false) {
        alert('规则更新成功')
        showRuleDialog.value = false
        await loadChildRules(selectedChild.value.childUserId) // 重新加载规则列表
        await loadRelationships() // 刷新关系列表
      }
    } else {
      // 创建新规则
      const payload = {
        childUserId: selectedChild.value.childUserId,
        ruleType: ruleForm.value.ruleType,
        ruleValue: ruleValue,
        isActive: ruleForm.value.isActive
      }
      const res = await parentalApi.setRule(payload)
      if (res && res.success !== false) {
        alert('规则设置成功')
        showRuleDialog.value = false
        await loadChildRules(selectedChild.value.childUserId) // 重新加载规则列表
        await loadRelationships() // 刷新关系列表
      }
    }
  } catch (error) {
    console.error('保存规则失败:', error)
    alert('保存规则失败: ' + (error.response?.data?.message || error.message || '未知错误'))
  } finally {
    savingRule.value = false
  }
}

// 格式化日期
const formatDate = (dateString) => {
  if (!dateString) return ''
  const date = new Date(dateString)
  return date.toLocaleDateString('zh-CN', { 
    year: 'numeric', 
    month: 'long', 
    day: 'numeric' 
  })
}

// 根据规则类型更新规则值
const updateRuleValue = () => {
  switch (ruleForm.value.ruleType) {
    case 'playtime_daily_limit':
      ruleForm.value.ruleValue = {
        limitMinutes: 120,
        warningMinutes: 100,
        resetTime: '00:00'
      }
      break
    case 'game_restriction':
      ruleForm.value.ruleValue = {
        blockedGameNames: []
      }
      break
    case 'age_restriction':
      ruleForm.value.ruleValue = {
        maxAgeRating: 12
      }
      break
    default:
      ruleForm.value.ruleValue = {}
  }
}

// 更新游戏限制（将字符串转换为数组）
const updateGameRestriction = (value) => {
  if (typeof value === 'string') {
    ruleForm.value.ruleValue.blockedGameNames = value.split(',')
      .map(name => name.trim())
      .filter(name => name.length > 0)
  }
}

// 验证游戏时长限制
const validatePlaytimeLimit = () => {
  if (ruleForm.value.ruleValue.limitMinutes < 1) {
    ruleForm.value.ruleValue.limitMinutes = 1
  }
  // 确保警告时长不超过限制时长
  if (ruleForm.value.ruleValue.warningMinutes > ruleForm.value.ruleValue.limitMinutes) {
    ruleForm.value.ruleValue.warningMinutes = ruleForm.value.ruleValue.limitMinutes
  }
}

onMounted(() => {
  loadCurrentRole()
  loadRelationships()
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
  transition: all 0.3s;
}

.header-status.active {
  background: rgba(16, 185, 129, 0.1);
  border-color: var(--success-color);
}

.role-toggle-btn {
  cursor: pointer;
  font-family: inherit;
  font-size: 14px;
  color: var(--text-secondary);
  transition: all 0.3s;
}

.role-toggle-btn:hover:not(:disabled) {
  background: var(--bg-surface);
  border-color: var(--primary-color);
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.15);
}

.role-toggle-btn:active:not(:disabled) {
  transform: translateY(0);
}

.role-toggle-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.role-toggle-btn.active {
  background: rgba(16, 185, 129, 0.15);
  border-color: var(--success-color);
  color: var(--success-color);
}

.role-toggle-btn.active:hover:not(:disabled) {
  background: rgba(16, 185, 129, 0.25);
}

.switching-indicator {
  margin-left: var(--spacing-xs);
  font-size: 12px;
  opacity: 0.7;
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

/* 关系列表样式 */
.loading-state,
.empty-state {
  padding: var(--spacing-xl);
  text-align: center;
  color: var(--text-secondary);
}

.relationships-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.relationship-item {
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  transition: all 0.3s;
  margin-bottom: var(--spacing-md);
}

.relationship-item:hover {
  border-color: var(--primary-color);
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.1);
}

.relationship-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--spacing-md);
  cursor: pointer;
}

.expand-icon {
  margin-left: var(--spacing-sm);
  transition: transform 0.3s;
  color: var(--text-secondary);
}

.expand-icon.expanded {
  transform: rotate(180deg);
}

.relationship-info {
  flex: 1;
}

.relationship-name {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.relationship-stats {
  display: flex;
  gap: var(--spacing-md);
  flex-wrap: wrap;
  font-size: 14px;
  color: var(--text-secondary);
}

.relationship-stats span {
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--bg-surface);
  border-radius: var(--radius-sm);
}

.relationship-actions {
  display: flex;
  gap: var(--spacing-sm);
  align-items: center;
}

.relationship-note {
  font-size: 14px;
  color: var(--text-secondary);
  font-style: italic;
}

.btn-danger {
  background: var(--error-color);
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background: #dc2626;
}

.btn-warning {
  background: #f59e0b;
  color: white;
}

.btn-warning:hover:not(:disabled) {
  background: #d97706;
}

.btn-success {
  background: var(--success-color);
  color: white;
}

.btn-success:hover:not(:disabled) {
  background: #059669;
}

/* 规则设置对话框样式 */
.rule-dialog {
  max-width: 600px;
}

.form-group {
  margin-bottom: var(--spacing-md);
}

.form-label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.setting-textarea {
  width: 100%;
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
}

.setting-textarea:focus {
  outline: none;
  border-color: var(--primary-color);
}

/* 规则列表样式 */
.rules-container {
  padding: var(--spacing-md);
  border-top: 1px solid var(--border-color-light);
  background: var(--bg-surface);
}

.rules-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.rule-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color-light);
  border-radius: var(--radius-md);
}

.rule-info {
  flex: 1;
}

.rule-header {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-sm);
}

.rule-type {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.rule-status {
  padding: var(--spacing-xs) var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 12px;
  font-weight: 500;
}

.rule-status.active {
  background: rgba(16, 185, 129, 0.1);
  color: var(--success-color);
}

.rule-status.inactive {
  background: rgba(107, 114, 128, 0.1);
  color: var(--text-secondary);
}

.rule-details {
  margin-top: var(--spacing-sm);
}

.rule-value {
  font-size: 12px;
  color: var(--text-secondary);
  background: var(--bg-surface);
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  margin: var(--spacing-xs) 0;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 150px;
  overflow-y: auto;
}

.rule-statistics {
  display: flex;
  gap: var(--spacing-md);
  margin-top: var(--spacing-sm);
  font-size: 12px;
  color: var(--text-tertiary);
}

.rule-statistics span {
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--bg-surface);
  border-radius: var(--radius-sm);
}

.rule-actions {
  display: flex;
  gap: var(--spacing-sm);
  align-items: flex-start;
}

.btn-sm {
  padding: var(--spacing-xs) var(--spacing-md);
  font-size: 12px;
}

.form-hint {
  font-size: 12px;
  color: var(--text-tertiary);
  margin-top: var(--spacing-xs);
}

/* 游戏时长相关样式 */
.playtime-limit-info {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.limit-item {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  font-size: 14px;
}

.limit-label {
  color: var(--text-secondary);
  font-weight: 500;
  min-width: 80px;
}

.limit-value {
  color: var(--text-primary);
  font-weight: 600;
}

.limit-value.warning {
  color: #f59e0b;
}

.limit-value.exceeded {
  color: var(--error-color);
}

.playtime-progress {
  margin-top: var(--spacing-xs);
}

.progress-bar {
  width: 100%;
  height: 8px;
  background: var(--bg-secondary);
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--primary-color);
  transition: width 0.3s, background-color 0.3s;
  border-radius: 4px;
}

.progress-fill.warning {
  background: #f59e0b;
}

.progress-fill.exceeded {
  background: var(--error-color);
}

.playtime-summary {
  margin-top: var(--spacing-sm);
  padding: var(--spacing-sm);
  background: var(--bg-surface);
  border-radius: var(--radius-sm);
}

.playtime-summary-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-xs);
  font-size: 13px;
}

.summary-label {
  color: var(--text-secondary);
}

.summary-value {
  color: var(--text-primary);
  font-weight: 600;
}

.playtime-progress-bar {
  width: 100%;
  height: 6px;
  background: var(--bg-secondary);
  border-radius: 3px;
  overflow: hidden;
}

.progress-fill-summary {
  height: 100%;
  background: var(--primary-color);
  transition: width 0.3s, background-color 0.3s;
  border-radius: 3px;
}

.progress-fill-summary.warning {
  background: #f59e0b;
}

.progress-fill-summary.exceeded {
  background: var(--error-color);
}

.time-input-group {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
}

.input-unit {
  color: var(--text-secondary);
  font-size: 14px;
  white-space: nowrap;
}

.current-playtime-info {
  margin-top: var(--spacing-sm);
  padding: var(--spacing-sm);
  background: var(--bg-surface);
  border-radius: var(--radius-sm);
  font-size: 13px;
}

.info-label {
  color: var(--text-secondary);
  margin-right: var(--spacing-sm);
}

.info-value {
  color: var(--text-primary);
  font-weight: 600;
}

.info-value.warning {
  color: #f59e0b;
}

.info-value.exceeded {
  color: var(--error-color);
}

.game-restriction-info,
.age-restriction-info {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.blocked-games-tags {
  display: flex;
  flex-wrap: wrap;
  gap: var(--spacing-xs);
  margin-top: var(--spacing-xs);
}

.game-tag {
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: var(--text-secondary);
}

.game-tag.more {
  color: var(--text-tertiary);
  font-style: italic;
}

.btn-info {
  background: #3b82f6;
  color: white;
}

.btn-info:hover:not(:disabled) {
  background: #2563eb;
}

.rule-value-display {
  margin-top: var(--spacing-xs);
}

.rule-value-raw {
  margin-top: var(--spacing-xs);
}

/* 过去一周游玩时间统计样式 */
.weekly-playtime-section {
  margin-bottom: var(--spacing-lg);
  padding: var(--spacing-md);
  background: var(--bg-secondary);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-color-light);
}

.weekly-playtime-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--spacing-md);
}

.weekly-playtime-chart {
  width: 100%;
}

.weekly-playtime-bars {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: var(--spacing-xs);
  height: 150px;
  margin-bottom: var(--spacing-md);
  padding: var(--spacing-sm) 0;
}

.playtime-bar-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-xs);
  min-width: 0;
}

.bar-container {
  width: 100%;
  height: 120px;
  display: flex;
  align-items: flex-end;
  justify-content: center;
}

.playtime-bar {
  width: 100%;
  min-height: 5px;
  background: var(--primary-color);
  border-radius: 4px 4px 0 0;
  transition: all 0.3s;
  cursor: pointer;
}

.playtime-bar:hover {
  background: var(--primary-hover);
  opacity: 0.9;
}

.bar-label {
  text-align: center;
  font-size: 11px;
  color: var(--text-secondary);
  width: 100%;
}

.bar-date {
  font-weight: 500;
  margin-bottom: 2px;
}

.bar-time {
  font-size: 10px;
  color: var(--text-tertiary);
}

.weekly-playtime-summary {
  text-align: center;
  font-size: 14px;
  color: var(--text-primary);
  font-weight: 500;
  padding-top: var(--spacing-sm);
  border-top: 1px solid var(--border-color-light);
}
</style>

