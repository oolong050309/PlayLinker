<template>
  <div class="settings-container">
    <div class="settings-header">
      <h1 class="settings-title">设置</h1>
      <p class="settings-subtitle">管理您的账户和偏好设置</p>
    </div>

    <div v-if="loading" class="loading-overlay">
      <div class="loading-spinner">加载中...</div>
    </div>

    <div class="settings-content">
      <!-- 账户设置 -->
      <section class="settings-section">
        <h2 class="section-title">账户设置</h2>
        <div class="settings-card">
          <!-- 头像上传 -->
          <div class="avatar-section">
            <div class="avatar-wrapper">
              <img 
                :src="getAvatarUrl()" 
                alt="Avatar" 
                class="avatar-image"
                @error="handleAvatarError"
              />
              <button class="avatar-upload-btn" @click="triggerAvatarUpload">
                <Camera class="icon" />
              </button>
              <input 
                ref="avatarInput"
                type="file" 
                accept="image/*"
                @change="handleAvatarChange"
                style="display: none;"
              />
            </div>
            <div class="avatar-info">
              <h3 class="setting-label">头像</h3>
              <p class="setting-desc">上传您的个人头像</p>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">用户名</h3>
              <p class="setting-desc">您的显示名称（不可修改）</p>
            </div>
            <div class="setting-action">
              <input 
                v-model="settings.username" 
                type="text" 
                class="setting-input"
                placeholder="请输入用户名"
                disabled
                style="opacity: 0.6; cursor: not-allowed;"
              />
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">邮箱地址</h3>
              <p class="setting-desc">用于接收通知和重置密码</p>
            </div>
            <div class="setting-action">
              <input 
                v-model="settings.email" 
                type="email" 
                class="setting-input"
                placeholder="请输入邮箱"
              />
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">手机号</h3>
              <p class="setting-desc">用于账户验证和安全保护</p>
            </div>
            <div class="setting-action">
              <input 
                v-model="settings.phone" 
                type="tel" 
                class="setting-input"
                placeholder="请输入手机号"
              />
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">性别</h3>
              <p class="setting-desc">选择您的性别</p>
            </div>
            <div class="setting-action">
              <select v-model="settings.gender" class="setting-select">
                <option value="">请选择</option>
                <option value="male">男</option>
                <option value="female">女</option>
                <option value="other">其他</option>
              </select>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">修改密码</h3>
              <p class="setting-desc">定期更新密码以保护账户安全</p>
            </div>
            <div class="setting-action">
              <button class="btn btn-secondary" @click="showChangePassword = true">
                修改密码
              </button>
            </div>
          </div>
        </div>
      </section>

      <!-- 游戏偏好 -->
      <section class="settings-section">
        <h2 class="section-title">游戏偏好</h2>
        <div class="settings-card">
          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">喜欢的游戏类型</h3>
              <p class="setting-desc">选择您喜欢的游戏类型以获得更好的推荐</p>
            </div>
            <div class="setting-action">
              <div class="genre-tags">
                <button
                  v-for="genre in gameGenres"
                  :key="genre.value"
                  class="genre-tag"
                  :class="{ active: settings.favoriteGenres.includes(genre.value) }"
                  @click="toggleGenre(genre.value)"
                >
                  {{ genre.label }}
                </button>
              </div>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">游戏时长偏好</h3>
              <p class="setting-desc">选择您通常的游戏时长范围</p>
            </div>
            <div class="setting-action">
              <select v-model="settings.playtimePreference" class="setting-select">
                <option value="less-than-1">少于1小时/天</option>
                <option value="1-3">1-3小时/天</option>
                <option value="3-5">3-5小时/天</option>
                <option value="5-plus">5小时以上/天</option>
                <option value="weekends">休闲（仅周末）</option>
              </select>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">价格敏感度</h3>
              <p class="setting-desc">您对游戏价格的敏感程度</p>
            </div>
            <div class="setting-action">
              <div class="slider-container">
                <input 
                  type="range" 
                  min="1" 
                  max="3" 
                  v-model.number="settings.priceSensitivity"
                  class="price-slider"
                />
                <span class="sensitivity-label" :class="getSensitivityClass()">
                  {{ getSensitivityLabel() }}
                </span>
              </div>
              <div class="slider-labels">
                <span>高（注重性价比）</span>
                <span>中</span>
                <span>低（高端游戏）</span>
              </div>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">内容过滤</h3>
              <p class="setting-desc">隐藏成人内容（18+）</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.hideMatureContent"
                />
                <span class="toggle-slider"></span>
                <span class="toggle-label">隐藏成人内容</span>
              </label>
            </div>
          </div>
        </div>
      </section>

      <!-- 偏好设置 -->
      <section class="settings-section">
        <h2 class="section-title">偏好设置</h2>
        <div class="settings-card">
          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">主题模式</h3>
              <p class="setting-desc">选择您喜欢的界面主题</p>
            </div>
            <div class="setting-action">
              <select v-model="settings.theme" class="setting-select">
                <option value="dark">深色模式</option>
                <option value="light">浅色模式</option>
                <option value="auto">跟随系统</option>
              </select>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">语言</h3>
              <p class="setting-desc">选择界面显示语言</p>
            </div>
            <div class="setting-action">
              <select v-model="settings.language" class="setting-select">
                <option value="zh-CN">简体中文</option>
                <option value="zh-TW">繁体中文</option>
                <option value="en-US">English</option>
              </select>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">自动同步</h3>
              <p class="setting-desc">自动同步游戏库数据</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.autoSync"
                />
                <span class="toggle-slider"></span>
                <span class="toggle-label">启用自动同步</span>
              </label>
            </div>
          </div>
        </div>
      </section>

      <!-- 通知设置 -->
      <section class="settings-section">
        <h2 class="section-title">通知设置</h2>
        <div class="settings-card">
          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">价格下降提醒</h3>
              <p class="setting-desc">当愿望单中的游戏降价时通知您</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.priceDrop"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">游戏更新</h3>
              <p class="setting-desc">游戏补丁和DLC发布通知</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.gameUpdates"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">成就解锁</h3>
              <p class="setting-desc">当您解锁新成就时通知</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.achievements"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">游戏推荐</h3>
              <p class="setting-desc">基于您的喜好提供AI游戏推荐</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.recommendations"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">家长监管提醒</h3>
              <p class="setting-desc">家长监管违规通知</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.parentalControl"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">邮件通知</h3>
              <p class="setting-desc">通过邮件接收通知</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.notifications.email"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>
        </div>
      </section>

      <!-- 隐私与安全 -->
      <section class="settings-section">
        <h2 class="section-title">隐私与安全</h2>
        <div class="settings-card">
          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">双因素认证</h3>
              <p class="setting-desc">为您的账户添加额外的安全保护</p>
            </div>
            <div class="setting-action">
              <button 
                class="btn btn-secondary"
                @click="handleEnable2FA"
              >
                {{ settings.twoFactorEnabled ? '已启用' : '启用 2FA' }}
              </button>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">资料可见性</h3>
              <p class="setting-desc">让其他用户可以看到您的游戏资料</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.privacy.publicProfile"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>

          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">分享游戏时间统计</h3>
              <p class="setting-desc">允许他人查看您的游戏统计数据</p>
            </div>
            <div class="setting-action">
              <label class="toggle-switch">
                <input 
                  type="checkbox" 
                  v-model="settings.privacy.showStats"
                />
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>
        </div>
      </section>

      <!-- 数据与存储 -->
      <section class="settings-section">
        <h2 class="section-title">数据与存储</h2>
        <div class="settings-card">
          <div class="storage-stats">
            <div class="stat-item">
              <div class="stat-label">云存储使用</div>
              <div class="stat-value">{{ storageData.cloudUsed }} GB</div>
              <div class="stat-desc">共 {{ storageData.cloudTotal }} GB</div>
            </div>
            <div class="stat-item">
              <div class="stat-label">存档文件</div>
              <div class="stat-value">{{ storageData.saveFiles }}</div>
              <div class="stat-desc">来自 {{ storageData.gamesCount }} 款游戏</div>
            </div>
            <div class="stat-item">
              <div class="stat-label">最后备份</div>
              <div class="stat-value">{{ storageData.lastBackup }}</div>
              <div class="stat-desc">自动同步已启用</div>
            </div>
          </div>

          <div class="storage-actions">
            <button class="btn btn-secondary full-width" @click="handleBackupSaves">
              <CloudUpload class="icon" />
              备份所有存档
            </button>
            <button class="btn btn-secondary full-width" @click="handleExportData">
              <Download class="icon" />
              导出数据
            </button>
            <button class="btn btn-danger full-width" @click="handleClearCache">
              <Trash2 class="icon" />
              清除缓存
            </button>
          </div>
        </div>
      </section>

      <!-- 危险操作 -->
      <section class="settings-section">
        <h2 class="section-title danger-title">危险操作</h2>
        <div class="settings-card danger-card">
          <div class="setting-item">
            <div class="setting-info">
              <h3 class="setting-label">删除账户</h3>
              <p class="setting-desc">永久删除您的账户和所有数据，此操作不可恢复</p>
            </div>
            <div class="setting-action">
              <button class="btn btn-danger" @click="handleDeleteAccount">
                删除账户
              </button>
            </div>
          </div>
        </div>
      </section>

      <!-- 保存按钮 -->
      <div class="settings-actions">
        <button class="btn btn-primary" @click="handleSave" :disabled="saving">
          {{ saving ? '保存中...' : '保存设置' }}
        </button>
        <button class="btn btn-secondary" @click="handleReset">
          重置
        </button>
      </div>
    </div>

    <!-- 修改密码对话框 -->
    <div v-if="showChangePassword" class="modal-overlay" @click="showChangePassword = false">
      <div class="modal-content" @click.stop>
        <h3 class="modal-title">修改密码</h3>
        <div class="modal-body">
          <div class="form-group">
            <label>当前密码</label>
            <input 
              v-model="passwordForm.currentPassword" 
              type="password" 
              class="form-input"
              placeholder="请输入当前密码"
            />
          </div>
          <div class="form-group">
            <label>新密码</label>
            <input 
              v-model="passwordForm.newPassword" 
              type="password" 
              class="form-input"
              placeholder="请输入新密码"
            />
          </div>
          <div class="form-group">
            <label>确认新密码</label>
            <input 
              v-model="passwordForm.confirmPassword" 
              type="password" 
              class="form-input"
              placeholder="请再次输入新密码"
            />
          </div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-secondary" @click="showChangePassword = false">取消</button>
          <button class="btn btn-primary" @click="handleChangePassword">确认</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Camera, CloudUpload, Download, Trash2 } from 'lucide-vue-next'
import { usersApi } from '@/api/users'

const router = useRouter()
const avatarInput = ref(null)
const defaultAvatar = 'https://picsum.photos/200/200?random=1'
const loading = ref(false)

const settings = ref({
  avatar: '',
  username: '',
  email: '',
  phone: '',
  gender: '',
  theme: 'dark',
  language: 'zh-CN',
  favoriteGenres: [],
  playtimePreference: '1-3',
  priceSensitivity: 2,
  hideMatureContent: false,
  notifications: {
    email: true,
    priceDrop: true,
    gameUpdates: true,
    achievements: true,
    recommendations: true,
    parentalControl: false
  },
  autoSync: true,
  twoFactorEnabled: false,
  privacy: {
    publicProfile: false,
    showStats: true
  }
})

const storageData = ref({
  cloudUsed: 2.4,
  cloudTotal: 10,
  saveFiles: 47,
  gamesCount: 12,
  lastBackup: '2小时前'
})

const gameGenres = [
  { value: 'rpg', label: 'RPG' },
  { value: 'action', label: '动作' },
  { value: 'fps', label: 'FPS' },
  { value: 'strategy', label: '策略' },
  { value: 'adventure', label: '冒险' },
  { value: 'simulation', label: '模拟' },
  { value: 'sports', label: '体育' },
  { value: 'racing', label: '竞速' },
  { value: 'indie', label: '独立' },
  { value: 'horror', label: '恐怖' }
]

const saving = ref(false)
const showChangePassword = ref(false)
const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

// 加载用户信息
const loadUserProfile = async () => {
  loading.value = true
  try {
    const response = await usersApi.getProfile()
    if (response.success && response.data) {
      const profile = response.data
      settings.value.username = profile.username || ''
      settings.value.email = profile.email || ''
      settings.value.phone = profile.phone || ''
      // 确保头像 URL 正确设置
      settings.value.avatar = profile.avatarUrl || null
      console.log('加载用户头像:', profile.avatarUrl)
      // 性别映射：后端 0/1/2 对应 前端 ''/'male'/'female'
      if (profile.gender === 1) {
        settings.value.gender = 'male'
      } else if (profile.gender === 2) {
        settings.value.gender = 'female'
      } else {
        settings.value.gender = ''
      }
      
      // 更新 sessionStorage 中的用户信息
      try {
        const userStr = sessionStorage.getItem('user')
        if (userStr) {
          const user = JSON.parse(userStr)
          user.username = profile.username
          user.email = profile.email
          user.phone = profile.phone
          user.avatar = profile.avatarUrl
          user.avatarUrl = profile.avatarUrl // 同时保存 avatarUrl 字段
          sessionStorage.setItem('user', JSON.stringify(user))
        }
      } catch (e) {
        console.warn('更新 sessionStorage 失败:', e)
      }
    }
  } catch (error) {
    console.error('加载用户信息失败:', error)
    // 如果 API 失败，尝试从 sessionStorage 加载
    try {
      const userStr = sessionStorage.getItem('user')
      if (userStr) {
        const user = JSON.parse(userStr)
        settings.value.username = user.username || ''
        settings.value.email = user.email || ''
        settings.value.phone = user.phone || ''
        // 优先使用 avatarUrl，如果没有则使用 avatar
        settings.value.avatar = user.avatarUrl || user.avatar || ''
      }
    } catch (e) {
      console.warn('从 sessionStorage 加载用户信息失败:', e)
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadUserProfile()
})

const triggerAvatarUpload = () => {
  avatarInput.value?.click()
}

const handleAvatarChange = async (event) => {
  const file = event.target.files?.[0]
  if (!file) return

  // 验证文件类型
  if (!file.type.startsWith('image/')) {
    alert('请选择图片文件')
    return
  }

  // 验证文件大小（限制为 5MB）
  if (file.size > 5 * 1024 * 1024) {
    alert('图片大小不能超过 5MB')
    return
  }

  try {
    // 先显示预览
    const reader = new FileReader()
    reader.onload = (e) => {
      settings.value.avatar = e.target.result
    }
    reader.readAsDataURL(file)

    // 上传到服务器
    const response = await usersApi.uploadAvatar(file)
    if (response.success && response.data) {
      // 使用服务器返回的头像 URL
      const avatarUrl = response.data.avatarUrl
      settings.value.avatar = avatarUrl
      
      // 更新 sessionStorage
      try {
        const userStr = sessionStorage.getItem('user')
        if (userStr) {
          const user = JSON.parse(userStr)
          user.avatar = avatarUrl
          user.avatarUrl = avatarUrl // 同时保存 avatarUrl 字段
          sessionStorage.setItem('user', JSON.stringify(user))
        }
      } catch (e) {
        console.warn('更新 sessionStorage 失败:', e)
      }
    }
  } catch (error) {
    console.error('上传头像失败:', error)
    alert('上传头像失败: ' + (error.message || '未知错误'))
    // 恢复默认头像
    settings.value.avatar = defaultAvatar
  }
}

const getAvatarUrl = () => {
  const avatar = settings.value.avatar
  // 如果有头像 URL 且是有效的 URL（http/https 或 data URI），则使用它
  if (avatar && typeof avatar === 'string' && avatar.trim() !== '') {
    const trimmedAvatar = avatar.trim()
    if (trimmedAvatar.startsWith('http://') || trimmedAvatar.startsWith('https://') || trimmedAvatar.startsWith('data:')) {
      return trimmedAvatar
    }
  }
  // 否则使用默认头像
  return defaultAvatar
}

const handleAvatarError = (e) => {
  // 如果当前不是默认头像，则切换到默认头像
  if (e.target.src !== defaultAvatar) {
    e.target.src = defaultAvatar
    settings.value.avatar = ''
  }
}

const toggleGenre = (genreValue) => {
  const index = settings.value.favoriteGenres.indexOf(genreValue)
  if (index > -1) {
    settings.value.favoriteGenres.splice(index, 1)
  } else {
    settings.value.favoriteGenres.push(genreValue)
  }
}

const getSensitivityLabel = () => {
  const labels = { 1: '高', 2: '中', 3: '低' }
  return labels[settings.value.priceSensitivity] || '中'
}

const getSensitivityClass = () => {
  return `sensitivity-${settings.value.priceSensitivity}`
}

const handleEnable2FA = () => {
  // TODO: 实现双因素认证
  alert('双因素认证功能待实现')
}

const handleBackupSaves = async () => {
  // TODO: 实现备份存档
  alert('备份存档功能待实现')
}

const handleExportData = async () => {
  // TODO: 实现导出数据
  alert('导出数据功能待实现')
}

const handleClearCache = () => {
  if (confirm('确定要清除缓存吗？这将删除临时数据以释放空间。')) {
    // TODO: 实现清除缓存
    alert('清除缓存功能待实现')
  }
}

const handleSave = async () => {
  saving.value = true
  try {
    // 准备更新数据
    const updateData = {}
    
    // 只更新有变化的字段
    if (settings.value.email) {
      updateData.email = settings.value.email
    }
    if (settings.value.phone) {
      updateData.phone = settings.value.phone
    }
    // 性别映射：前端 'male'/'female'/'other' 对应后端 1/2/0
    if (settings.value.gender) {
      if (settings.value.gender === 'male') {
        updateData.gender = 1
      } else if (settings.value.gender === 'female') {
        updateData.gender = 2
      } else {
        updateData.gender = 0
      }
    }
    if (settings.value.avatar && settings.value.avatar.startsWith('http')) {
      updateData.avatarUrl = settings.value.avatar
    }

    // 调用 API 更新个人信息
    const response = await usersApi.updateProfile(updateData)
    if (response.success) {
      // 更新 sessionStorage
      try {
        const userStr = sessionStorage.getItem('user')
        if (userStr) {
          const user = JSON.parse(userStr)
          if (updateData.email) user.email = updateData.email
          if (updateData.phone) user.phone = updateData.phone
          if (updateData.avatarUrl) user.avatar = updateData.avatarUrl
          sessionStorage.setItem('user', JSON.stringify(user))
        }
      } catch (e) {
        console.warn('更新 sessionStorage 失败:', e)
      }
      
      alert('设置已保存')
    }
  } catch (error) {
    console.error('保存设置失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('保存失败: ' + errorMessage)
  } finally {
    saving.value = false
  }
}

const handleReset = () => {
  if (confirm('确定要重置所有设置吗？未保存的更改将丢失。')) {
    // 重新加载用户信息
    loadUserProfile()
    
    // 重置其他设置为默认值
    settings.value.theme = 'dark'
    settings.value.language = 'zh-CN'
    settings.value.favoriteGenres = []
    settings.value.playtimePreference = '1-3'
    settings.value.priceSensitivity = 2
    settings.value.hideMatureContent = false
    settings.value.notifications = {
      email: true,
      priceDrop: true,
      gameUpdates: true,
      achievements: true,
      recommendations: true,
      parentalControl: false
    }
    settings.value.autoSync = true
    settings.value.twoFactorEnabled = false
    settings.value.privacy = {
      publicProfile: false,
      showStats: true
    }
  }
}

const handleChangePassword = async () => {
  if (!passwordForm.value.currentPassword) {
    alert('请输入当前密码')
    return
  }

  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    alert('两次输入的密码不一致')
    return
  }
  
  if (passwordForm.value.newPassword.length < 8) {
    alert('密码长度至少8位')
    return
  }

  // 验证密码强度
  const hasUpper = /[A-Z]/.test(passwordForm.value.newPassword)
  const hasLower = /[a-z]/.test(passwordForm.value.newPassword)
  const hasDigit = /[0-9]/.test(passwordForm.value.newPassword)
  const hasSpecial = /[^A-Za-z0-9]/.test(passwordForm.value.newPassword)

  if (!hasUpper || !hasLower || !hasDigit || !hasSpecial) {
    alert('密码必须包含大小写字母、数字和特殊字符')
    return
  }

  try {
    const response = await usersApi.changePassword({
      oldPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword
    })

    if (response.success) {
      alert('密码修改成功，请重新登录')
      showChangePassword.value = false
      passwordForm.value = {
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
      }
      
      // 清除登录状态并跳转到登录页
      sessionStorage.removeItem('token')
      sessionStorage.removeItem('refreshToken')
      sessionStorage.removeItem('user')
      router.push('/login')
    }
  } catch (error) {
    console.error('修改密码失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('密码修改失败: ' + errorMessage)
  }
}

const handleDeleteAccount = () => {
  if (confirm('确定要删除账户吗？此操作不可恢复！')) {
    // TODO: 调用 API 删除账户
    alert('账户删除功能待实现')
  }
}
</script>

<style scoped>
.settings-container {
  max-width: 900px;
  margin: 0 auto;
  padding: var(--spacing-lg);
}

.settings-header {
  margin-bottom: var(--spacing-xl);
}

.settings-title {
  font-size: 32px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: var(--spacing-sm);
}

.settings-subtitle {
  font-size: 16px;
  color: var(--text-secondary);
}

.settings-content {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xl);
}

.settings-section {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
}

.danger-title {
  color: var(--error-color);
}

.settings-card {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color-strong);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  box-shadow: var(--shadow-md);
}

.danger-card {
  border-color: rgba(239, 68, 68, 0.3);
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: var(--spacing-md) 0;
  border-bottom: 1px solid var(--border-color-light);
  gap: var(--spacing-lg);
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
  align-items: flex-start;
  gap: var(--spacing-md);
  flex-shrink: 0;
  min-width: 0;
}

.setting-input,
.setting-select {
  padding: var(--spacing-sm) var(--spacing-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  font-size: 14px;
  min-width: 200px;
}

.setting-input:focus,
.setting-select:focus {
  outline: none;
  border-color: var(--primary-color);
}

.toggle-switch {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
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

.toggle-label {
  font-size: 14px;
  color: var(--text-primary);
}

.settings-actions {
  display: flex;
  gap: var(--spacing-md);
  justify-content: flex-end;
  margin-top: var(--spacing-lg);
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

.btn-danger {
  background: var(--error-color);
  color: white;
}

.btn-danger:hover {
  background: #dc2626;
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

.form-group {
  margin-bottom: var(--spacing-md);
}

.form-group label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
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

.modal-actions {
  display: flex;
  gap: var(--spacing-md);
  justify-content: flex-end;
}

/* 头像上传 */
.avatar-section {
  display: flex;
  align-items: center;
  gap: var(--spacing-lg);
  padding: var(--spacing-md) 0;
  border-bottom: 1px solid var(--border-color-light);
  margin-bottom: var(--spacing-md);
}

.avatar-wrapper {
  position: relative;
  width: 96px;
  height: 96px;
  flex-shrink: 0;
}

.avatar-image {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid var(--border-color);
}

.avatar-upload-btn {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 32px;
  height: 32px;
  background: var(--primary-color);
  border: 2px solid var(--bg-surface);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.3s;
}

.avatar-upload-btn:hover {
  background: var(--primary-hover);
}

.avatar-upload-btn .icon {
  width: 16px;
  height: 16px;
  color: white;
}

.avatar-info {
  flex: 1;
}

/* 游戏类型标签 */
.genre-tags {
  display: flex;
  flex-wrap: wrap;
  gap: var(--spacing-sm);
  max-width: 500px;
  justify-content: flex-end;
}

.genre-tag {
  padding: var(--spacing-xs) var(--spacing-md);
  border-radius: 20px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.3s;
}

.genre-tag:hover {
  background: var(--bg-surface);
  border-color: var(--border-color-strong);
}

.genre-tag.active {
  background: var(--primary-color);
  border-color: var(--primary-color);
  color: white;
}

/* 价格敏感度滑块 */
.slider-container {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  min-width: 300px;
}

.price-slider {
  flex: 1;
  height: 6px;
  border-radius: 3px;
  background: var(--bg-secondary);
  outline: none;
  -webkit-appearance: none;
  appearance: none;
}

.price-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--primary-color);
  cursor: pointer;
}

.price-slider::-moz-range-thumb {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--primary-color);
  cursor: pointer;
  border: none;
}

.sensitivity-label {
  padding: var(--spacing-xs) var(--spacing-md);
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 500;
  min-width: 60px;
  text-align: center;
}

.sensitivity-1 {
  background: rgba(16, 185, 129, 0.2);
  color: var(--success-color);
}

.sensitivity-2 {
  background: rgba(99, 102, 241, 0.2);
  color: var(--primary-color);
}

.sensitivity-3 {
  background: rgba(139, 92, 246, 0.2);
  color: #8b5cf6;
}

.slider-labels {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: var(--text-tertiary);
  margin-top: var(--spacing-xs);
  width: 100%;
}

/* 存储统计 */
.storage-stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-lg);
}

.stat-item {
  padding: var(--spacing-md);
  background: var(--bg-secondary);
  border-radius: var(--radius-md);
}

.stat-label {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: var(--spacing-xs);
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: var(--spacing-xs);
}

.stat-desc {
  font-size: 12px;
  color: var(--text-tertiary);
}

/* 存储操作 */
.storage-actions {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.btn.full-width {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--spacing-sm);
}

.btn .icon {
  width: 16px;
  height: 16px;
}

/* 加载状态 */
.loading-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.loading-spinner {
  background: var(--bg-surface);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color-strong);
  border-radius: var(--radius-lg);
  padding: var(--spacing-xl);
  color: var(--text-primary);
  font-size: 16px;
}
</style>

