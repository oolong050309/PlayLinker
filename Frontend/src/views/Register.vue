<template>
  <div class="register-container">
    <div class="register-card">
      <div class="register-header">
        <h1>创建账户</h1>
        <p>加入 PlayLinker，开始您的游戏之旅</p>
      </div>

      <form @submit.prevent="handleRegister" class="register-form">
        <div class="form-group">
          <label for="username">用户名</label>
          <input
            id="username"
            v-model="form.username"
            type="text"
            placeholder="请输入用户名（3-128个字符）"
            required
            minlength="3"
            maxlength="128"
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="email">邮箱</label>
          <input
            id="email"
            v-model="form.email"
            type="email"
            placeholder="请输入邮箱地址"
            required
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="phone">手机号（可选）</label>
          <input
            id="phone"
            v-model="form.phone"
            type="tel"
            placeholder="请输入手机号"
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="password">密码</label>
          <input
            id="password"
            v-model="form.password"
            type="password"
            placeholder="请输入密码（至少8个字符）"
            required
            minlength="8"
            :disabled="loading"
          />
          <small class="form-hint">密码需包含大小写字母、数字和特殊字符</small>
        </div>

        <div class="form-group">
          <label for="confirmPassword">确认密码</label>
          <input
            id="confirmPassword"
            v-model="confirmPassword"
            type="password"
            placeholder="请再次输入密码"
            required
            :disabled="loading"
          />
        </div>

        <div v-if="error" class="error-message">{{ error }}</div>
        <div v-if="success" class="success-message">{{ success }}</div>

        <button type="submit" class="submit-btn" :disabled="loading">
          <span v-if="loading">注册中...</span>
          <span v-else>注册</span>
        </button>

        <div class="form-footer">
          <p>已有账户？ <router-link to="/login">立即登录</router-link></p>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { authApi } from '@/api/auth'

const router = useRouter()

const form = ref({
  username: '',
  email: '',
  phone: '',
  password: ''
})

const confirmPassword = ref('')
const loading = ref(false)
const error = ref('')
const success = ref('')

// 监听密码确认
watch([() => form.value.password, confirmPassword], () => {
  if (confirmPassword.value && form.value.password !== confirmPassword.value) {
    error.value = '两次输入的密码不一致'
  } else if (error.value === '两次输入的密码不一致') {
    error.value = ''
  }
})

const handleRegister = async () => {
  error.value = ''
  success.value = ''

  // 验证密码确认
  if (form.value.password !== confirmPassword.value) {
    error.value = '两次输入的密码不一致'
    return
  }

  // 验证密码长度
  if (form.value.password.length < 8) {
    error.value = '密码长度至少为8个字符'
    return
  }

  loading.value = true

  try {
    const registerData = {
      username: form.value.username,
      email: form.value.email,
      password: form.value.password
    }

    if (form.value.phone) {
      registerData.phone = form.value.phone
    }

    const response = await authApi.register(registerData)

    if (response.success && response.data) {
      // 保存 token（使用 sessionStorage，关闭浏览器后自动退出）
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
.register-container {
  min-height: calc(100vh - 200px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.register-card {
  background: white;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
  padding: 40px;
  width: 100%;
  max-width: 480px;
}

.register-header {
  text-align: center;
  margin-bottom: 30px;
}

.register-header h1 {
  font-size: 28px;
  color: #333;
  margin: 0 0 8px 0;
}

.register-header p {
  color: #666;
  font-size: 14px;
  margin: 0;
}

.register-form {
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
  text-align: center;
  margin-top: 20px;
  font-size: 14px;
  color: #666;
}

.form-footer a {
  color: #667eea;
  text-decoration: none;
  font-weight: 500;
}

.form-footer a:hover {
  text-decoration: underline;
}
</style>

