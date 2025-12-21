<template>
  <!-- 模板部分完全不变，省略以节省篇幅 -->
  <div class="register-page">
    <!-- 背景效果 -->
    <div class="background-effect top-effect"></div>
    <div class="background-effect bottom-effect"></div>

    <!-- 注册容器 -->
    <div class="register-container">
      <!-- Logo & Title -->
      <div class="register-header">
        <div class="logo-container">
          <!-- Lucide 游戏手柄图标 -->
          <Gamepad2 class="icon gamepad-icon" />
        </div>
        <h1>创建账号</h1>
        <p>加入 GameVerse，统一管理你的游戏库</p>
      </div>

      <!-- 注册表单 -->
      <div class="register-form-card">
        <form @submit.prevent="handleRegister">
          <!-- 全局错误提示 -->
          <div v-if="error" class="global-error-message">{{ error }}</div>
          
          <!-- 全局成功提示 -->
          <div v-if="success" class="global-success-message">{{ success }}</div>

          <!-- 用户名 -->
          <div class="form-group">
            <label>用户名 *</label>
            <div class="input-wrapper">
              <!-- Lucide 用户图标 -->
              <User class="icon input-icon" />
              <input
                type="text"
                v-model="form.username"
                placeholder="选择一个用户名"
                required
                :disabled="loading"
                :class="{ 'input-error': errors.username }"
                @blur="validateUsername"
              >
            </div>
            <p v-if="errors.username" class="field-error-message">{{ errors.username }}</p>
          </div>

          <!-- 邮箱 -->
          <div class="form-group">
            <label>电子邮箱 *</label>
            <div class="input-wrapper">
              <!-- Lucide 邮件图标 -->
              <Mail class="icon input-icon" />
              <input
                type="email"
                v-model="form.email"
                placeholder="your.email@example.com"
                required
                :disabled="loading"
                :class="{ 'input-error': errors.email }"
                @blur="validateEmail"
              >
            </div>
            <p v-if="errors.email" class="field-error-message">{{ errors.email }}</p>
          </div>

          <!-- 密码 -->
          <div class="form-group">
            <label>密码 *</label>
            <div class="input-wrapper">
              <!-- Lucide 锁图标 -->
              <Lock class="icon input-icon" />
              <input
                :type="showPassword ? 'text' : 'password'"
                v-model="form.password"
                placeholder="创建一个安全的密码"
                required
                :disabled="loading"
                :class="{ 'input-error': errors.password }"
                @blur="validatePassword"
                @input="checkPasswordStrength"
              >
              <!-- 密码显隐切换按钮 -->
              <button
                type="button"
                class="toggle-password-btn"
                @click="showPassword = !showPassword"
                :disabled="loading"
              >
                <Eye v-if="!showPassword" class="icon toggle-icon" />
                <EyeOff v-if="showPassword" class="icon toggle-icon" />
              </button>
            </div>
            <p v-if="errors.password" class="field-error-message">{{ errors.password }}</p>
            
            <!-- 密码强度指示器 -->
            <div class="password-strength-indicator" v-if="form.password">
              <div class="strength-bars">
                <div 
                  class="strength-bar" 
                  :style="{ backgroundColor: getStrengthColor(1) }"
                ></div>
                <div 
                  class="strength-bar" 
                  :style="{ backgroundColor: getStrengthColor(2) }"
                ></div>
                <div 
                  class="strength-bar" 
                  :style="{ backgroundColor: getStrengthColor(3) }"
                ></div>
                <div 
                  class="strength-bar" 
                  :style="{ backgroundColor: getStrengthColor(4) }"
                ></div>
              </div>
              <p class="strength-text" :style="{ color: getStrengthTextColor() }">
                {{ strengthText }}
              </p>
            </div>
          </div>

          <!-- 确认密码 -->
          <div class="form-group">
            <label>确认密码 *</label>
            <div class="input-wrapper">
              <!-- Lucide 锁图标 -->
              <Lock class="icon input-icon" />
              <input
                :type="showConfirmPassword ? 'text' : 'password'"
                v-model="confirmPassword"
                placeholder="确认你的密码"
                required
                :disabled="loading"
                :class="{ 'input-error': errors.confirmPassword }"
                @blur="validateConfirmPassword"
              >
              <!-- 确认密码显隐切换按钮 -->
              <button
                type="button"
                class="toggle-password-btn"
                @click="showConfirmPassword = !showConfirmPassword"
                :disabled="loading"
              >
                <Eye v-if="!showConfirmPassword" class="icon toggle-icon" />
                <EyeOff v-if="showConfirmPassword" class="icon toggle-icon" />
              </button>
            </div>
            <p v-if="errors.confirmPassword" class="field-error-message">{{ errors.confirmPassword }}</p>
          </div>

          <!-- 条款同意 -->
          <div class="form-group terms-group">
            <label class="checkbox-label">
              <input
                type="checkbox"
                v-model="agreeTerms"
                :disabled="loading"
              >
              <span>
                我同意 <a href="#" class="link">服务条款</a> 和 <a href="#" class="link">隐私政策</a>
              </span>
            </label>
            <p v-if="errors.agreeTerms" class="field-error-message">{{ errors.agreeTerms }}</p>
          </div>

          <!-- 注册按钮 -->
          <button
            type="submit"
            class="register-btn"
            :disabled="loading"
          >
            <span v-if="!loading">创建账号</span>
            <span v-if="loading">
              <!-- Lucide 加载动画图标 -->
              <Loader2 class="icon loader-icon" />
              创建账号中...
            </span>
            <ArrowRight v-if="!loading" class="icon arrow-icon" />
          </button>

          <!-- 分隔线 -->
          <div class="divider">
            <span>或通过以下方式注册</span>
          </div>

          <!-- 社交注册 -->
          <div class="social-login-container">
            <button type="button" class="social-btn" :disabled="loading">
              <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/1024px-Steam_icon_logo.svg.png" alt="Steam" class="social-icon">
            </button>
            <button type="button" class="social-btn" :disabled="loading">
              <img src="https://upload.wikimedia.org/wikipedia/commons/a/a7/Epic_Games_logo.png" alt="Epic" class="social-icon">
            </button>
            <button type="button" class="social-btn" :disabled="loading">
              <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/f/f9/Xbox_one_logo.svg/2048px-Xbox_one_logo.svg.png" alt="Xbox" class="social-icon">
            </button>
          </div>
        </form>

        <!-- 登录链接 -->
        <div class="signin-link">
          已有账号？
          <router-link to="/login" class="link">立即登录</router-link>
        </div>
      </div>

      <!-- 返回首页链接 -->
      <div class="back-home-link">
        <router-link to="/" class="link">
          <!-- Lucide 返回箭头图标 -->
          <ArrowLeft class="icon back-icon" />
          返回首页
        </router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
// script 部分完全不变，省略以节省篇幅
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
// 导入 Lucide 图标组件
import { 
  Gamepad2, 
  User, 
  Mail, 
  Lock, 
  Eye, 
  EyeOff, 
  ArrowRight, 
  Loader2, 
  ArrowLeft 
} from 'lucide-vue-next'

// 引入 authApi
import { authApi } from '@/api/auth'

const router = useRouter()

// 表单数据
const form = ref({
  username: '',
  email: '',
  password: ''
})

// 确认密码
const confirmPassword = ref('')
// 加载状态
const loading = ref(false)
// 全局错误/成功提示
const error = ref('')
const success = ref('')
// 字段级错误
const errors = ref({
  username: '',
  email: '',
  password: '',
  confirmPassword: '',
  agreeTerms: ''
})
// 密码显隐状态
const showPassword = ref(false)
const showConfirmPassword = ref(false)
// 条款同意
const agreeTerms = ref(false)
// 密码强度相关
const passwordStrength = ref(0)
const strengthText = ref('')

// 监听密码和确认密码变化，校验一致性
watch([() => form.value.password, confirmPassword], () => {
  if (confirmPassword.value && form.value.password !== confirmPassword.value) {
    errors.value.confirmPassword = '两次输入的密码不一致'
  } else if (errors.value.confirmPassword === '两次输入的密码不一致') {
    errors.value.confirmPassword = ''
  }
})

// 计算密码强度
const checkPasswordStrength = () => {
  const password = form.value.password
  let strength = 0
  
  // 基础长度校验
  if (password.length >= 8) strength++
  if (password.length >= 12) strength++
  // 包含大小写字母
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++
  // 包含数字
  if (/\d/.test(password)) strength++
  // 包含特殊字符
  if (/[^a-zA-Z0-9]/.test(password)) strength++
  
  // 限制最大强度为4
  passwordStrength.value = Math.min(strength, 4)
  
  // 设置强度文本
  const strengthTexts = ['', '弱', '一般', '良好', '强']
  strengthText.value = password ? `密码强度: ${strengthTexts[passwordStrength.value]}` : ''
}

// 获取强度颜色
const getStrengthColor = (level) => {
  const colors = [
    'rgba(255, 255, 255, 0.1)', // 默认
    '#ef4444', // 弱
    '#f59e0b', // 一般
    '#eab308', // 良好
    '#22c55e'  // 强
  ]
  
  return passwordStrength.value >= level ? colors[passwordStrength.value] : colors[0]
}

// 获取强度文本颜色
const getStrengthTextColor = () => {
  const colors = ['#ef4444', '#f59e0b', '#eab308', '#22c55e']
  return passwordStrength.value > 0 ? colors[passwordStrength.value - 1] : '#71717a'
}

// 字段验证函数
const validateUsername = () => {
  const username = form.value.username.trim()
  errors.value.username = ''
  
  if (!username) {
    errors.value.username = '用户名不能为空'
    return false
  }
  if (username.length < 3) {
    errors.value.username = '用户名至少需要3个字符'
    return false
  }
  if (!/^[a-zA-Z0-9_]+$/.test(username)) {
    errors.value.username = '用户名只能包含字母、数字和下划线'
    return false
  }
  return true
}

const validateEmail = () => {
  const email = form.value.email.trim()
  errors.value.email = ''
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  
  if (!email) {
    errors.value.email = '电子邮箱不能为空'
    return false
  }
  if (!emailRegex.test(email)) {
    errors.value.email = '请输入有效的电子邮箱地址'
    return false
  }
  return true
}

const validatePassword = () => {
  const password = form.value.password
  errors.value.password = ''
  
  if (!password) {
    errors.value.password = '密码不能为空'
    return false
  }
  if (password.length < 8) {
    errors.value.password = '密码至少需要8个字符'
    return false
  }
  if (!/(?=.*[a-z])(?=.*[A-Z])/.test(password)) {
    errors.value.password = '密码必须包含大小写字母'
    return false
  }
  if (!/\d/.test(password)) {
    errors.value.password = '密码必须包含至少一个数字'
    return false
  }
  return true
}

const validateConfirmPassword = () => {
  errors.value.confirmPassword = ''
  
  if (!confirmPassword.value) {
    errors.value.confirmPassword = '请确认你的密码'
    return false
  }
  if (form.value.password !== confirmPassword.value) {
    errors.value.confirmPassword = '两次输入的密码不一致'
    return false
  }
  return true
}

const validateAgreeTerms = () => {
  errors.value.agreeTerms = ''
  
  if (!agreeTerms.value) {
    errors.value.agreeTerms = '请同意服务条款和隐私政策'
    return false
  }
  return true
}

// 验证所有字段
const validateAllFields = () => {
  const isUsernameValid = validateUsername()
  const isEmailValid = validateEmail()
  const isPasswordValid = validatePassword()
  const isConfirmPasswordValid = validateConfirmPassword()
  const isAgreeTermsValid = validateAgreeTerms()
  
  return isUsernameValid && isEmailValid && isPasswordValid && isConfirmPasswordValid && isAgreeTermsValid
}

// 注册处理函数
const handleRegister = async () => {
  error.value = ''
  success.value = ''
  
  // 验证所有字段
  if (!validateAllFields()) {
    return
  }

  loading.value = true

  try {
    const registerData = {
      username: form.value.username.trim(),
      email: form.value.email.trim(),
      password: form.value.password
    }

    const response = await authApi.register(registerData)

    if (response.success && response.data) {
      // 保存 token
      sessionStorage.setItem('token', response.data.token)
      if (response.data.refreshToken) {
        sessionStorage.setItem('refreshToken', response.data.refreshToken)
      }

      success.value = '注册成功！正在跳转...'

      // 延迟跳转，让用户看到成功消息
      setTimeout(() => {
        router.push('/')
      }, 1500)
    } else {
      error.value = response.message || '注册失败，请稍后重试'
    }
  } catch (err) {
    console.error('注册错误:', err)
    const errorMessage = err.response?.data?.message || err.message || '注册失败，请稍后重试'
    
    if (errorMessage.includes('已存在') || errorMessage.includes('EXISTS')) {
      error.value = '用户名或邮箱已被使用'
    } else if (errorMessage.includes('密码') || errorMessage.includes('WEAK')) {
      error.value = '密码强度不足，需包含大小写字母、数字和特殊字符'
    } else {
      error.value = errorMessage
    }
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
/* 全局页面样式 */
.register-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #09090b;
  color: #ffffff;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
  padding: 32px 0;
}

/* 背景效果 */
.background-effect {
  position: absolute;
  border-radius: 50%;
  filter: blur(150px);
  z-index: 0;
}

.top-effect {
  top: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 800px;
  height: 800px;
  background-color: rgba(99, 102, 241, 0.2);
}

.bottom-effect {
  bottom: 0;
  right: 0;
  width: 600px;
  height: 600px;
  background-color: rgba(168, 85, 247, 0.1);
}

/* 注册容器 */
.register-container {
  width: 100%;
  max-width: 400px;
  padding: 0 24px;
  position: relative;
  z-index: 1;
}

/* 注册头部 */
.register-header {
  text-align: center;
  margin-bottom: 32px;
}

.logo-container {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 64px;
  height: 64px;
  border-radius: 20px;
  background-color: #6366f1;
  margin-bottom: 16px;
}

/* 核心样式：Lucide 图标基础样式 */
.icon {
  color: currentColor;
  flex-shrink: 0;
  object-fit: contain;
  /* 修复 Lucide 组件的内置样式影响 */
  display: inline-block;
}

/* Logo 游戏手柄图标 */
.gamepad-icon {
  width: 24px;
  height: 24px;
  color: #ffffff;
}

.register-header h1 {
  font-size: 24px;
  font-weight: bold;
  margin-bottom: 8px;
}

.register-header p {
  color: #a1a1aa;
}

/* 注册卡片 */
.register-form-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 20px;
  padding: 32px;
}

/* 全局错误/成功提示 */
.global-error-message {
  background-color: rgba(239, 68, 68, 0.2);
  color: #f87171;
  padding: 12px;
  border-radius: 12px;
  margin-bottom: 20px;
  font-size: 14px;
  text-align: center;
}

.global-success-message {
  background-color: rgba(34, 197, 94, 0.2);
  color: #4ade80;
  padding: 12px;
  border-radius: 12px;
  margin-bottom: 20px;
  font-size: 14px;
  text-align: center;
}

/* 表单样式 */
.form-group {
  margin-bottom: 20px;
}

.form-group label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: #d4d4d8;
  margin-bottom: 8px;
}

.input-wrapper {
  position: relative;
  height: 44px; /* 固定输入框高度 */
}

.input-wrapper input {
  width: 100%;
  height: 100%;
  background-color: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 0 16px 0 48px;
  color: #ffffff;
  font-size: 16px;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
  line-height: 42px;
}

/* 错误输入框样式 */
.input-wrapper input.input-error {
  border-color: #ef4444 !important;
}

.input-wrapper input::placeholder {
  color: #71717a;
}

.input-wrapper input:focus {
  border-color: #818cf8;
}

.input-wrapper input:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

/* 输入框内图标 - 修复对齐问题的关键样式 */
.input-icon {
  position: absolute;
  left: 16px;
  top: 50%;
  /* 修复 Lucide 组件的垂直偏移 */
  transform: translateY(-50%) translateX(0);
  width: 20px;
  height: 20px;
  color: #71717a;
  /* 确保图标容器尺寸正确 */
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 修复 Lucide 组件内部 SVG 的对齐 */
.input-icon > svg {
  width: 100%;
  height: 100%;
}

/* 密码切换按钮 */
.toggle-password-btn {
  position: absolute;
  right: 16px;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #71717a;
  cursor: pointer;
  padding: 0;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.2s;
}

.toggle-password-btn:hover {
  color: #ffffff;
}

.toggle-password-btn:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

/* 密码切换图标 - 固定小尺寸 */
.toggle-icon {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 字段错误提示 */
.field-error-message {
  color: #ef4444;
  font-size: 14px;
  margin-top: 8px;
}

/* 密码强度指示器 */
.password-strength-indicator {
  margin-top: 8px;
}

.strength-bars {
  display: flex;
  gap: 4px;
  margin-bottom: 4px;
}

.strength-bar {
  height: 4px;
  flex: 1;
  border-radius: 2px;
  background-color: rgba(255, 255, 255, 0.1);
  transition: background-color 0.2s;
}

.strength-text {
  font-size: 12px;
  color: #71717a;
}

/* 条款同意组 */
.terms-group {
  margin-bottom: 24px;
}

.checkbox-label {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  cursor: pointer;
}

.checkbox-label input {
  width: 16px;
  height: 16px;
  border-radius: 4px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background-color: rgba(255, 255, 255, 0.05);
  color: #6366f1;
  margin-top: 2px;
}

.checkbox-label span {
  font-size: 14px;
  color: #a1a1aa;
  line-height: 1.4;
}

.link {
  color: #818cf8;
  font-weight: 500;
  text-decoration: none;
  transition: color 0.2s;
}

.link:hover {
  color: #a5b4fc;
}

/* 注册按钮 */
.register-btn {
  width: 100%;
  height: 44px;
  background-color: #6366f1;
  color: #ffffff;
  font-weight: 500;
  border: none;
  border-radius: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: background-color 0.2s;
  font-size: 16px;
  box-sizing: border-box;
}

.register-btn:hover {
  background-color: #4f46e5;
}

.register-btn:disabled {
  background-color: #4338ca;
  cursor: not-allowed;
  opacity: 0.8;
}

/* 按钮箭头图标 - 固定小尺寸 */
.arrow-icon {
  width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 加载动画 - 固定小尺寸 */
.loader-icon {
  width: 16px;
  height: 16px;
  margin-right: 8px;
  animation: spin 1s linear infinite;
  display: flex;
  align-items: center;
  justify-content: center;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* 分隔线 */
.divider {
  position: relative;
  margin: 24px 0;
  text-align: center;
}

.divider::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 1px;
  background-color: rgba(255, 255, 255, 0.1);
  z-index: 0;
}

.divider span {
  background-color: #18181b;
  padding: 0 16px;
  color: #71717a;
  font-size: 14px;
  position: relative;
  z-index: 1;
}

/* 社交登录 */
.social-login-container {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-bottom: 24px;
}

.social-btn {
  padding: 12px;
  background-color: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.social-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
}

.social-btn:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

/* 社交图标 - 固定小尺寸 */
.social-icon {
  width: 20px;
  height: 20px;
  object-fit: contain;
}

/* 登录链接 */
.signin-link {
  text-align: center;
  font-size: 14px;
  color: #a1a1aa;
}

/* 返回首页链接 */
.back-home-link {
  margin-top: 24px;
  text-align: center;
}

.back-home-link a {
  font-size: 14px;
  color: #71717a;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  transition: color 0.2s;
}

.back-home-link a:hover {
  color: #a1a1aa;
}

/* 返回按钮图标 - 固定小尺寸 */
.back-icon {
  width: 16px;
  height: 16px;
  color: #71717a;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>