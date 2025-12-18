<template>
  <div class="forgot-password-container">
    <div class="forgot-password-card">
      <div class="forgot-password-header">
        <h1>忘记密码</h1>
        <p v-if="!codeSent && !codeVerified">请输入您的邮箱地址，我们将发送验证码</p>
        <p v-else-if="codeSent && !codeVerified">验证码已发送，请查收邮件</p>
        <p v-else>请设置您的新密码</p>
      </div>

      <!-- 第一步：发送验证码 -->
      <form v-if="!codeSent" @submit.prevent="handleSendCode" class="forgot-password-form">
        <div class="form-group">
          <label for="email">邮箱地址</label>
          <input
            id="email"
            v-model="email"
            type="email"
            placeholder="请输入注册时使用的邮箱"
            required
            :disabled="loading"
          />
        </div>

        <div v-if="error" class="error-message">{{ error }}</div>
        <div v-if="success" class="success-message">{{ success }}</div>

        <button type="submit" class="submit-btn" :disabled="loading">
          <span v-if="loading">发送中...</span>
          <span v-else>发送验证码</span>
        </button>

        <div class="form-footer">
          <router-link to="/login">返回登录</router-link>
        </div>
      </form>

      <!-- 第二步：验证码验证 -->
      <form v-else-if="codeSent && !codeVerified" @submit.prevent="handleVerifyCode" class="forgot-password-form">
        <div class="form-group">
          <label for="code">验证码</label>
          <input
            id="code"
            v-model="code"
            type="text"
            placeholder="请输入6位验证码"
            required
            maxlength="6"
            :disabled="loading"
            class="code-input"
          />
          <small class="form-hint">验证码已发送至 {{ maskedEmail }}，有效期30分钟</small>
        </div>

        <div v-if="error" class="error-message">{{ error }}</div>

        <button type="submit" class="submit-btn" :disabled="loading">
          <span v-if="loading">验证中...</span>
          <span v-else>验证</span>
        </button>

        <div class="form-footer">
          <button type="button" @click="resendCode" class="resend-btn" :disabled="resendCooldown > 0">
            <span v-if="resendCooldown > 0">重新发送 ({{ resendCooldown }}s)</span>
            <span v-else>重新发送验证码</span>
          </button>
          <router-link to="/login">返回登录</router-link>
        </div>
      </form>

      <!-- 第三步：重置密码 -->
      <form v-else @submit.prevent="handleResetPassword" class="forgot-password-form">
        <div class="form-group">
          <label for="newPassword">新密码</label>
          <input
            id="newPassword"
            v-model="newPassword"
            type="password"
            placeholder="请输入新密码（至少8个字符）"
            required
            minlength="8"
            :disabled="loading"
          />
          <small class="form-hint">密码需包含大小写字母、数字和特殊字符</small>
        </div>

        <div class="form-group">
          <label for="confirmNewPassword">确认新密码</label>
          <input
            id="confirmNewPassword"
            v-model="confirmNewPassword"
            type="password"
            placeholder="请再次输入新密码"
            required
            :disabled="loading"
          />
        </div>

        <div v-if="error" class="error-message">{{ error }}</div>
        <div v-if="success" class="success-message">{{ success }}</div>

        <button type="submit" class="submit-btn" :disabled="loading">
          <span v-if="loading">重置中...</span>
          <span v-else>重置密码</span>
        </button>

        <div class="form-footer">
          <router-link to="/login">返回登录</router-link>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { authApi } from '@/api/auth'

const router = useRouter()

const email = ref('')
const code = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const maskedEmail = ref('')

const codeSent = ref(false)
const codeVerified = ref(false)
const loading = ref(false)
const error = ref('')
const success = ref('')
const resendCooldown = ref(0)
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

onUnmounted(() => {
  if (cooldownTimer) {
    clearInterval(cooldownTimer)
  }
})

// 掩码邮箱
const maskEmail = (email) => {
  const parts = email.split('@')
  if (parts.length !== 2) return email

  const localPart = parts[0]
  const domain = parts[1]

  if (localPart.length <= 2) {
    return `${localPart[0]}***@${domain}`
  }

  return `${localPart[0]}${'*'.repeat(localPart.length - 2)}${localPart[localPart.length - 1]}@${domain}`
}

// 发送验证码
const handleSendCode = async () => {
  error.value = ''
  success.value = ''
  loading.value = true

  try {
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
    // 出于安全考虑，即使邮箱不存在也返回成功，所以这里不显示具体错误
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
    const response = await authApi.verifyResetCode({
      email: email.value,
      code: code.value
    })

    if (response.success) {
      codeVerified.value = true
      success.value = '验证码验证成功，请设置新密码'
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
.forgot-password-container {
  min-height: calc(100vh - 200px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.forgot-password-card {
  background: white;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
  padding: 40px;
  width: 100%;
  max-width: 480px;
}

.forgot-password-header {
  text-align: center;
  margin-bottom: 30px;
}

.forgot-password-header h1 {
  font-size: 28px;
  color: #333;
  margin: 0 0 8px 0;
}

.forgot-password-header p {
  color: #666;
  font-size: 14px;
  margin: 0;
}

.forgot-password-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 14px;
  font-weight: 500;
  color: #333;
}

.form-group input {
  padding: 12px 16px;
  border: 1px solid #ddd;
  border-radius: 8px;
  font-size: 16px;
  color: #333;
  background-color: #fff;
  transition: border-color 0.3s;
}

.form-group input::placeholder {
  color: #999;
  opacity: 1;
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
}

.form-group input:disabled {
  background-color: #f5f5f5;
  color: #666;
  cursor: not-allowed;
}

.code-input {
  text-align: center;
  font-size: 24px;
  letter-spacing: 8px;
  font-weight: 600;
  color: #333;
}

.form-hint {
  font-size: 12px;
  color: #999;
  margin-top: -4px;
}

.error-message {
  padding: 12px;
  background-color: #fee;
  color: #c33;
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
}

.success-message {
  padding: 12px;
  background-color: #efe;
  color: #3c3;
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
}

.submit-btn {
  padding: 14px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.3s, transform 0.2s;
}

.submit-btn:hover:not(:disabled) {
  opacity: 0.9;
  transform: translateY(-1px);
}

.submit-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.form-footer {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  margin-top: 20px;
  font-size: 14px;
}

.form-footer a {
  color: #667eea;
  text-decoration: none;
  font-weight: 500;
}

.form-footer a:hover {
  text-decoration: underline;
}

.resend-btn {
  background: none;
  border: none;
  color: #667eea;
  font-size: 14px;
  cursor: pointer;
  padding: 0;
  text-decoration: underline;
  transition: opacity 0.3s;
}

.resend-btn:hover:not(:disabled) {
  opacity: 0.8;
}

.resend-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  text-decoration: none;
}
</style>

