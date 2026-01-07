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
      <section class="settings-section">
        <h2 class="section-title">账户设置</h2>
        <div class="settings-card">
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
              <p class="setting-desc">请输入您的手机号</p>
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
          
          <div class="setting-actions">
            <button class="btn btn-primary save-button" @click="handleSave" :disabled="saving">
              <span v-if="saving" class="loading-spinner"></span>
              {{ saving ? '保存中...' : '保存所有设置' }}
            </button>
          </div>
        </div>
      </section>


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
    </div>

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
import { Camera } from 'lucide-vue-next'
import { usersApi } from '@/api/users'
import { preferenceApi } from '@/api/preference' // [修复] 引入偏好 API

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



// [修复] 前端字符串与后端数据库ID的映射表
// 注意：这里假设数据库中的 genres 表 ID 顺序如下。
// 如果后端 ID 不同，请根据实际数据库 genres 表进行调整。
const genreIdMap = {
  'rpg': 1,
  'action': 2,
  'fps': 3,
  'strategy': 4,
  'adventure': 5,
  'simulation': 6,
  'sports': 7,
  'racing': 8,
  'indie': 9,
  'horror': 10
}

// 反向映射：ID 转 字符串
const genreStrMap = Object.entries(genreIdMap).reduce((acc, [key, val]) => {
  acc[val] = key
  return acc
}, {})

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
  try {
    const response = await usersApi.getProfile()
    if (response.success && response.data) {
      const profile = response.data
      settings.value.username = profile.username || ''
      settings.value.email = profile.email || ''
      settings.value.phone = profile.phone || ''
      settings.value.avatar = profile.avatarUrl || null
      
      if (profile.gender === 1) settings.value.gender = 'male'
      else if (profile.gender === 2) settings.value.gender = 'female'
      else settings.value.gender = ''
      
      updateSessionStorage(profile)
    }
  } catch (error) {
    console.error('加载用户信息失败:', error)
  }
}

// [修复] 加载用户偏好
const loadUserPreferences = async () => {
  try {
    const response = await preferenceApi.getPreferences()
    if (response.success && response.data) {
      const pref = response.data
      // 映射时长
      settings.value.playtimePreference = pref.playtimeRange || '1-3'
      // 映射价格敏感度
      settings.value.priceSensitivity = pref.priceSensitivity || 2
      // 映射喜好类型 (后端返回 List<PreferenceGenreDto>，我们需要转回前端的字符串数组)
      if (pref.favoriteGenres && pref.favoriteGenres.length > 0) {
        settings.value.favoriteGenres = pref.favoriteGenres.map(g => {
          // 优先尝试用 ID 映射，如果失败尝试用 Name 匹配（虽然不太可靠）
          return genreStrMap[g.genreId] || g.name.toLowerCase()
        }).filter(g => g) // 过滤掉未知的
      }
    }
  } catch (error) {
    console.error('加载偏好失败:', error)
  }
}

const updateSessionStorage = (userData) => {
  try {
    const userStr = sessionStorage.getItem('user')
    if (userStr) {
      const user = JSON.parse(userStr)
      Object.assign(user, userData)
      // 特殊处理 avatarUrl
      if (userData.avatarUrl) {
        user.avatar = userData.avatarUrl
        user.avatarUrl = userData.avatarUrl
      }
      sessionStorage.setItem('user', JSON.stringify(user))
      window.dispatchEvent(new CustomEvent('userInfoUpdated', { detail: { user } }))
    }
  } catch (e) { console.warn('Session update failed', e) }
}

onMounted(async () => {
  loading.value = true
  await Promise.all([loadUserProfile(), loadUserPreferences()])
  loading.value = false
})

const triggerAvatarUpload = () => {
  avatarInput.value?.click()
}

const handleAvatarChange = async (event) => {
  const file = event.target.files?.[0]
  if (!file) return

  if (!file.type.startsWith('image/')) {
    alert('请选择图片文件')
    return
  }
  if (file.size > 5 * 1024 * 1024) {
    alert('图片大小不能超过 5MB')
    return
  }

  try {
    const reader = new FileReader()
    reader.onload = (e) => { settings.value.avatar = e.target.result }
    reader.readAsDataURL(file)

    const response = await usersApi.uploadAvatar(file)
    if (response.success && response.data) {
      const avatarUrl = response.data.avatarUrl
      settings.value.avatar = avatarUrl
      updateSessionStorage({ avatarUrl })
    }
  } catch (error) {
    console.error('上传头像失败:', error)
    alert('上传头像失败')
    settings.value.avatar = defaultAvatar
  }
}

const getAvatarUrl = () => {
  const avatar = settings.value.avatar
  if (avatar && typeof avatar === 'string' && avatar.trim() !== '') {
    const trimmedAvatar = avatar.trim()
    if (trimmedAvatar.startsWith('http') || trimmedAvatar.startsWith('data:')) {
      return trimmedAvatar
    }
  }
  return defaultAvatar
}

const handleAvatarError = (e) => {
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

// 统一保存处理
const handleSave = async () => {
  saving.value = true
  try {
    // 1. 准备个人信息更新数据
    const updateProfileData = {}
    if (settings.value.email) updateProfileData.email = settings.value.email
    if (settings.value.phone) updateProfileData.phone = settings.value.phone
    
    if (settings.value.gender === 'male') updateProfileData.gender = 1
    else if (settings.value.gender === 'female') updateProfileData.gender = 2
    else updateProfileData.gender = 0

    if (settings.value.avatar && settings.value.avatar.startsWith('http')) {
      updateProfileData.avatarUrl = settings.value.avatar
    }

    // 2. 准备偏好设置更新数据
    // 将前端字符串类型转换为后端 ID
    const favoriteGenreIds = settings.value.favoriteGenres
      .map(g => genreIdMap[g])
      .filter(id => id !== undefined)

    const updatePrefData = {
      playtimeRange: settings.value.playtimePreference,
      priceSensitivity: settings.value.priceSensitivity,
      favoriteGenres: favoriteGenreIds // 后端期望是 int[]
    }

    // 3. 并行调用 API
    const promises = [
      usersApi.updateProfile(updateProfileData),
      preferenceApi.updatePreferences(updatePrefData)
    ]

    const results = await Promise.all(promises)
    
    // 检查结果
    if (results[0].success && results[1].success) {
      updateSessionStorage(updateProfileData)
      alert('所有设置已保存成功')
    } else {
      console.warn('部分设置保存可能失败', results)
      alert('设置已保存，但部分可能未生效，请刷新查看')
    }

  } catch (error) {
    console.error('保存设置失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '未知错误'
    alert('保存失败: ' + errorMessage)
  } finally {
    saving.value = false
  }
}

// 删除账户：调用后端接口置为 inactive，并注销跳转
const handleDeleteAccount = async () => {
  const confirmed = confirm('确定要删除账户吗？此操作不可恢复！')
  if (!confirmed) return

  try {
    const resp = await usersApi.deleteAccount()
    if (resp?.success) {
      alert('账户已删除，状态已设为 inactive')
      sessionStorage.clear()
      router.push('/login')
    } else {
      const msg = resp?.message || '删除失败，请稍后重试'
      alert(msg)
    }
  } catch (error) {
    const msg = error?.response?.data?.message || error.message || '未知错误'
    alert('删除失败：' + msg)
  }
}

const handleChangePassword = async () => {
  // ... 保持原有逻辑 ...
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
  // 简单密码校验
  if (!/[A-Z]/.test(passwordForm.value.newPassword) || !/[0-9]/.test(passwordForm.value.newPassword)) {
    alert('密码需包含大写字母和数字')
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
      sessionStorage.clear()
      router.push('/login')
    }
  } catch (error) {
    alert('密码修改失败')
  }
}
</script>

<style scoped>
/* 样式保持不变 */
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

/* 保存按钮样式 */
.setting-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: var(--spacing-lg);
  padding-top: var(--spacing-lg);
  border-top: 1px solid var(--border-color);
}

.save-button {
  position: relative;
  min-width: 120px;
  padding: var(--spacing-sm) var(--spacing-lg);
  border-radius: var(--radius-md);
  font-weight: 500;
  transition: all 0.3s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.save-button:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.save-button:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.save-button .loading-spinner {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 16px;
  height: 16px;
  background: transparent;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top: 2px solid white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  padding: 0;
}

@keyframes spin {
  0% { transform: translate(-50%, -50%) rotate(0deg); }
  100% { transform: translate(-50%, -50%) rotate(360deg); }
}
</style>