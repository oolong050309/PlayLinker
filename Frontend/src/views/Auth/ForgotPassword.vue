<template>
  <div class="forgot-password-page">
    <!-- 背景效果 -->
    <div class="background-effect top-effect"></div>
    <div class="background-effect bottom-effect"></div>

    <!-- 忘记密码容器 -->
    <div class="forgot-password-container">
      <!-- Logo & Title -->
      <div class="forgot-password-header">
        <div class="logo-container">
          <!-- Lucide 游戏手柄图标 -->
          <Gamepad2 class="icon gamepad-icon" />
        </div>
        <h1>忘记密码</h1>
        <p>输入你的邮箱，我们将发送验证码帮助你重置密码</p>
      </div>

      <!-- 忘记密码表单卡片 -->
      <div class="forgot-password-form-card">
        <form @submit.prevent="handleFormSubmit">
          <!-- 全局错误/成功提示 -->
          <div v-if="error" class="global-error-message">{{ error }}</div>
          <div v-if="success" class="global-success-message">{{ success }}</div>

          <!-- 步骤1: 输入邮箱 -->
          <div v-if="!codeSent && !codeVerified" class="form-step step-email">
            <div class="form-group">
              <label>电子邮箱 *</label>
              <div class="input-wrapper">
                <!-- Lucide 邮件图标 -->
                <Mail class="icon input-icon" />
                <input
                  type="email"
                  v-model="email"
                  placeholder="your.email@example.com"
                  required
                  :disabled="loading"
                  @blur="validateEmail"
                >
              </div>
            </div>

            <button
              type="button"
              class="primary-btn send-code-btn"
              :disabled="loading || !email"
              @click="handleSendCode"
            >
              <span v-if="!loading">发送验证码</span>
              <span v-if="loading">
                <!-- Lucide 加载动画图标 -->
                <Loader2 class="icon loader-icon" />
                发送中...
              </span>
              <ArrowRight v-if="!loading" class="icon arrow-icon" />
            </button>
          </div>

          <!-- 步骤2: 输入验证码 -->
          <div v-if="codeSent && !codeVerified" class="form-step step-code">
            <div class="form-group">
              <label>验证码</label>
              <div class="code-info">
                我们已向 <span class="masked-email">{{ maskedEmail }}</span> 发送了验证码
              </div>
              <div class="input-wrapper">
                <!-- Lucide 验证码图标（使用Key图标替代原Code图标） -->
                <Key class="icon input-icon" />
                <input
                  type="text"
                  v-model="code"
                  placeholder="输入6位验证码"
                  required
                  :disabled="loading"
                  maxlength="6"
                >
              </div>
              <div class="code-actions">
                <button
                  type="button"
                  class="resend-code-btn"
                  :disabled="loading || resendCooldown > 0"
                  @click="resendCode"
                >
                  {{ resendCooldown > 0 ? `重新发送 (${resendCooldown}s)` : '重新发送验证码' }}
                </button>
              </div>
            </div>

            <button
              type="button"
              class="primary-btn verify-code-btn"
              :disabled="loading || !code"
              @click="handleVerifyCode"
            >
              <span v-if="!loading">验证验证码</span>
              <span v-if="loading">
                <Loader2 class="icon loader-icon" />
                验证中...
              </span>
              <ArrowRight v-if="!loading" class="icon arrow-icon" />
            </button>
          </div>

          <!-- 步骤3: 设置新密码 -->
          <div v-if="codeVerified" class="form-step step-reset">
            <div class="form-group">
              <label>新密码 *</label>
              <div class="input-wrapper">
                <!-- Lucide 锁图标 -->
                <Lock class="icon input-icon" />
                <input
                  :type="showPassword ? 'text' : 'password'"
                  v-model="newPassword"
                  placeholder="创建新密码（至少8位）"
                  required
                  :disabled="loading"
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
              
              <!-- 密码强度指示器 -->
              <div class="password-strength-indicator" v-if="newPassword">
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

            <div class="form-group">
              <label>确认新密码 *</label>
              <div class="input-wrapper">
                <!-- Lucide 锁图标 -->
                <Lock class="icon input-icon" />
                <input
                  :type="showConfirmPassword ? 'text' : 'password'"
                  v-model="confirmNewPassword"
                  placeholder="再次输入新密码"
                  required
                  :disabled="loading"
                >
                <!-- 密码显隐切换按钮 -->
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
            </div>

            <button
              type="button"
              class="primary-btn reset-password-btn"
              :disabled="loading || !newPassword || !confirmNewPassword"
              @click="handleResetPassword"
            >
              <span v-if="!loading">重置密码</span>
              <span v-if="loading">
                <Loader2 class="icon loader-icon" />
                重置中...
              </span>
              <ArrowRight v-if="!loading" class="icon arrow-icon" />
            </button>
          </div>
        </form>

        <!-- 返回登录链接 -->
        <div class="back-to-login-link">
          想起密码了？
          <a href="/login" class="link">返回登录</a>
        </div>
      </div>

      <!-- 返回首页链接 -->
      <div class="back-home-link">
        <a href="/">
          <!-- Lucide 返回箭头图标 -->
          <ArrowLeft class="icon back-icon" />
          返回首页
        </a>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
// 导入 Lucide 图标组件
import { 
  Gamepad2, 
  Mail, 
  Key, 
  Lock, 
  Eye, 
  EyeOff, 
  ArrowRight, 
  ArrowLeft, 
  Loader2 
} from 'lucide-vue-next'
import { authApi } from '@/api/auth'

const router = useRouter()

// 表单数据
const email = ref('')
const code = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const maskedEmail = ref('')

// 状态管理
const codeSent = ref(false)
const codeVerified = ref(false)
const loading = ref(false)
const error = ref('')
const success = ref('')
const resendCooldown = ref(0)
const showPassword = ref(false)
const showConfirmPassword = ref(false)

// 密码强度相关
const passwordStrength = ref(0)
const strengthText = ref('')

let cooldownTimer = null

// 监听密码确认
watch([newPassword, confirmNewPassword], () => {
  if (confirmNewPassword.value && newPassword.value !== confirmNewPassword.value) {
    error.value = '两次输入的密码不一致'
  } else if (error.value === '两次输入的密码不一致') {
    error.value = ''
  }
})

// 倒计时处理
const startCooldown = () => {
  resendCooldown.value = 60
  cooldownTimer = setInterval(() => {
    resendCooldown.value--
    if (resendCooldown.value <= 0) {
      clearInterval(cooldownTimer)
      cooldownTimer = null
    }
  }, 1000)
}

// 组件卸载时清除定时器
onUnmounted(() => {
  if (cooldownTimer) {
    clearInterval(cooldownTimer)
  }
})

// 掩码邮箱
const maskEmail = (email) => {
  if (!email) return ''
  
  const parts = email.split('@')
  if (parts.length !== 2) return email

  const localPart = parts[0]
  const domain = parts[1]

  if (localPart.length <= 2) {
    return `${localPart[0]}***@${domain}`
  }

  return `${localPart[0]}${'*'.repeat(localPart.length - 2)}${localPart[localPart.length - 1]}@${domain}`
}

// 验证邮箱格式
const validateEmail = () => {
  error.value = ''
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  
  if (email.value && !emailRegex.test(email.value)) {
    error.value = '请输入有效的电子邮箱地址'
    return false
  }
  return true
}

// 计算密码强度
const checkPasswordStrength = () => {
  const password = newPassword.value
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

// 表单提交处理（占位）
const handleFormSubmit = () => {}

// 发送验证码
const handleSendCode = async () => {
  error.value = ''
  success.value = ''
  
  // 验证邮箱
  if (!validateEmail()) {
    return
  }

  loading.value = true

  try {
    // 实际 API 调用
    const response = await authApi.forgotPassword({ email: email.value })
    
    if (response.success) {
      maskedEmail.value = maskEmail(email.value)
      codeSent.value = true
      success.value = '验证码已发送，请查收邮件'
      startCooldown()
    } else {
      error.value = response.message || '发送失败，请稍后重试'
    }
  } catch (err) {
    console.error('发送验证码错误:', err)
    // 出于安全考虑，即使邮箱不存在也返回成功
    maskedEmail.value = maskEmail(email.value)
    codeSent.value = true
    success.value = '验证码已发送，请查收邮件'
    startCooldown()
  } finally {
    loading.value = false
  }
}

// 重新发送验证码
const resendCode = async () => {
  if (resendCooldown.value > 0) return

  error.value = ''
  success.value = ''
  loading.value = true

  try {
    // 实际 API 调用
    const response = await authApi.forgotPassword({ email: email.value })
    
    if (response.success) {
      success.value = '验证码已重新发送'
      startCooldown()
    } else {
      error.value = response.message || '发送失败，请稍后重试'
    }
  } catch (err) {
    console.error('重新发送验证码错误:', err)
    success.value = '验证码已重新发送'
    startCooldown()
  } finally {
    loading.value = false
  }
}

// 验证验证码
const handleVerifyCode = async () => {
  error.value = ''
  loading.value = true

  try {
    // 实际 API 调用
    const response = await authApi.verifyResetCode({
      email: email.value,
      code: code.value
    })
    
    if (response.success) {
      codeVerified.value = true
      success.value = '验证码验证成功，请设置新密码'
      error.value = ''
    } else {
      error.value = response.message || '验证码无效或已过期'
    }
  } catch (err) {
    console.error('验证验证码错误:', err)
    const errorMessage = err.response?.data?.message || err.message || '验证码无效或已过期'
    
    if (errorMessage.includes('过期') || errorMessage.includes('EXPIRED')) {
      error.value = '验证码已过期，请重新获取'
    } else if (errorMessage.includes('无效') || errorMessage.includes('INVALID')) {
      error.value = '验证码错误，请重新输入'
    } else {
      error.value = errorMessage
    }
  } finally {
    loading.value = false
  }
}

// 重置密码
const handleResetPassword = async () => {
  error.value = ''
  success.value = ''

  // 验证密码确认
  if (newPassword.value !== confirmNewPassword.value) {
    error.value = '两次输入的密码不一致'
    return
  }

  // 验证密码长度
  if (newPassword.value.length < 8) {
    error.value = '密码长度至少为8个字符'
    return
  }

  loading.value = true

  try {
    // 实际 API 调用
    const response = await authApi.resetPasswordByCode({
      email: email.value,
      code: code.value,
      newPassword: newPassword.value
    })
    
    if (response.success) {
      success.value = '密码重置成功！正在跳转到登录页...'

      // 延迟跳转
      setTimeout(() => {
        router.push('/login')
      }, 2000)
    } else {
      error.value = response.message || '重置失败，请稍后重试'
    }
  } catch (err) {
    console.error('重置密码错误:', err)
    const errorMessage = err.response?.data?.message || err.message || '重置失败，请稍后重试'
    
    if (errorMessage.includes('密码') || errorMessage.includes('WEAK')) {
      error.value = '密码强度不足，需包含大小写字母、数字和特殊字符'
    } else if (errorMessage.includes('验证码') || errorMessage.includes('CODE')) {
      error.value = '验证码无效或已过期，请重新获取'
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
.forgot-password-page {
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

/* 忘记密码容器 */
.forgot-password-container {
  width: 100%;
  max-width: 400px;
  padding: 0 24px;
  position: relative;
  z-index: 1;
}

/* 忘记密码头部 */
.forgot-password-header {
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

/* 通用图标样式 - 修复 Lucide 组件对齐问题 */
.icon {
  color: currentColor;
  flex-shrink: 0;
  object-fit: contain;
  /* 核心：使用 flex 布局实现内部 SVG 居中 */
  display: inline-flex;
  align-items: center;
  justify-content: center;
  vertical-align: middle;
  line-height: 1;
}

/* Logo 游戏手柄图标 */
.gamepad-icon {
  width: 24px;
  height: 24px;
  color: #ffffff;
}

.forgot-password-header h1 {
  font-size: 24px;
  font-weight: bold;
  margin-bottom: 8px;
}

.forgot-password-header p {
  color: #a1a1aa;
}

/* 忘记密码表单卡片 */
.forgot-password-form-card {
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

/* 表单步骤通用样式 */
.form-step {
  margin-bottom: 16px;
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

.code-info {
  font-size: 13px;
  color: #a1a1aa;
  margin-bottom: 8px;
}

.masked-email {
  color: #818cf8;
  font-weight: 500;
}

.input-wrapper {
  position: relative;
  height: 44px;
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

/* 输入框内图标 - 修复对齐问题 */
.input-icon {
  position: absolute;
  left: 16px;
  top: 50%;
  transform: translateY(-50%);
  width: 20px;
  height: 20px;
  color: #71717a;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 验证码操作区 */
.code-actions {
  margin-top: 8px;
}

.resend-code-btn {
  background: none;
  border: none;
  color: #818cf8;
  font-size: 13px;
  cursor: pointer;
  padding: 0;
  transition: color 0.2s;
}

.resend-code-btn:disabled {
  color: #71717a;
  cursor: not-allowed;
}

.resend-code-btn:hover:not(:disabled) {
  color: #a5b4fc;
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

/* 密码切换图标 - 修复对齐 */
.toggle-icon {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
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

/* 主要按钮样式 */
.primary-btn {
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

.primary-btn:hover {
  background-color: #4f46e5;
}

.primary-btn:disabled {
  background-color: #4338ca;
  cursor: not-allowed;
  opacity: 0.8;
}

/* 按钮图标样式 - 修复对齐 */
.arrow-icon {
  width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 加载动画 - 修复对齐 */
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

/* 返回登录链接 */
.back-to-login-link {
  text-align: center;
  font-size: 14px;
  color: #a1a1aa;
  margin-top: 24px;
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

/* 返回按钮图标 - 修复对齐 */
.back-icon {
  width: 16px;
  height: 16px;
  color: #71717a;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>