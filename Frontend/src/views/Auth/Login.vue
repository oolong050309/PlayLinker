<template>
  <div class="login-page">
    <!-- 背景效果 -->
    <div class="background-effect top-effect"></div>
    <div class="background-effect bottom-effect"></div>

    <!-- 登录容器 -->
    <div class="login-container">
      <!-- Logo & Title -->
      <div class="login-header">
        <div class="logo-container">
          <Gamepad2 class="icon gamepad-icon" />
        </div>
        <h1>欢迎来到 PlayLinker</h1>
        <p>登录以访问你的统一游戏库</p>
      </div>

      <!-- 登录表单 -->
      <div class="login-form-card">
        <form @submit.prevent="handleLogin">
          <!-- 错误提示 -->
          <div v-if="error" class="error-message">{{ error }}</div>

          <!-- 用户名/邮箱 -->
          <div class="form-group">
            <label>用户名或邮箱</label>
            <div class="input-wrapper">
              <User class="icon user-icon" />
              <input
                type="text"
                v-model="form.username"
                placeholder="请输入用户名或邮箱"
                required
                :disabled="loading"
              >
            </div>
          </div>

          <!-- 密码 -->
          <div class="form-group">
            <label>密码</label>
            <div class="input-wrapper">
              <Lock class="icon lock-icon" />
              <input
                :type="showPassword ? 'text' : 'password'"
                v-model="form.password"
                placeholder="请输入密码"
                required
                :disabled="loading"
              >
              <button
                type="button"
                class="toggle-password-btn"
                @click="showPassword = !showPassword"
                :disabled="loading"
              >
                <span class="icon eye-icon">
                  <EyeOff v-if="showPassword" />
                  <Eye v-else />
                </span>
              </button>
            </div>
          </div>

          <!-- 记住我 & 忘记密码 -->
          <div class="form-row">
            <label class="checkbox-label">
              <input
                type="checkbox"
                v-model="rememberMe"
                :disabled="loading"
              >
              <span>记住我</span>
            </label>
            <router-link to="/forgot-password" class="forgot-password-link">忘记密码？</router-link>
          </div>

          <!-- 登录按钮 -->
          <button
            type="submit"
            class="login-btn"
            :disabled="loading"
          >
            <span v-if="!loading">登录</span>
            <span v-if="loading">登录中...</span>
            <ArrowRight class="icon arrow-icon" />
          </button>
        </form>

        <!-- 注册链接 -->
        <div class="signup-link">
          还没有账号？
          <router-link to="/register" class="link">立即注册</router-link>
        </div>
      </div>

      <!-- 返回首页 -->
      <div class="back-home-link">
        <a href="#">
          <ArrowLeft class="icon arrow-left-icon" />
            <router-link to="/" class="link">回到首页</router-link>
        </a>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
// 引入 Lucide 图标组件（替代 emoji）
import { 
  Gamepad2, User, Lock, Eye, EyeOff, ArrowRight, ArrowLeft 
} from 'lucide-vue-next'

// 引入 authApi
import { authApi } from '@/api/auth'

const router = useRouter()

// 表单数据
const form = ref({
  username: '',
  password: ''
})

// 记住我
const rememberMe = ref(false)
// 加载状态
const loading = ref(false)
// 错误信息
const error = ref('')
// 密码可见性
const showPassword = ref(false)

// 登录处理函数
const handleLogin = async () => {
  error.value = ''
  loading.value = true

  try {
    const response = await authApi.login({
      username: form.value.username,
      password: form.value.password
    })

    if (response.success && response.data) {
      // 保存 token（使用 sessionStorage，关闭浏览器后自动退出）
      sessionStorage.setItem('token', response.data.token)
      if (response.data.refreshToken) {
        sessionStorage.setItem('refreshToken', response.data.refreshToken)
      }

      // 保存用户信息
      if (response.data.user) {
        sessionStorage.setItem('user', JSON.stringify(response.data.user))
      }

      // 跳转到应用首页或之前要访问的页面
      const redirect = router.currentRoute.value.query.redirect || '/app/discover'
      router.push(redirect)
    } else {
      error.value = response.message || '登录失败，请检查用户名和密码'
    }
  } catch (err) {
    console.error('登录错误:', err)
    error.value = err.response?.data?.message || err.message || '登录失败，请稍后重试'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
/* 全局页面样式 */
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #09090b;
  color: #ffffff;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
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

/* 登录容器 */
.login-container {
  width: 100%;
  max-width: 400px;
  padding: 0 24px;
  position: relative;
  z-index: 1;
}

/* 登录头部 */
.login-header {
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

/* Lucide 图标基础样式（核心：解决倾斜问题） */
.icon {
  stroke: currentColor;
  stroke-width: 1.5;
  fill: none;
  stroke-linecap: round;
  stroke-linejoin: round;
  flex-shrink: 0;
}

.gamepad-icon {
  width: 32px;
  height: 32px;
  color: #ffffff;
}

.login-header h1 {
  font-size: 24px;
  font-weight: bold;
  margin-bottom: 8px;
}

.login-header p {
  color: #a1a1aa;
}

/* 登录卡片 */
.login-form-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 20px;
  padding: 32px;
}

/* 错误提示 */
.error-message {
  background-color: rgba(239, 68, 68, 0.2);
  color: #f87171;
  padding: 12px;
  border-radius: 12px;
  margin-bottom: 20px;
  font-size: 14px;
}

/* 表单样式 */
.form-group {
  margin-bottom: 24px;
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
  line-height: 42px;
  box-sizing: border-box;
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

/* 输入框内图标样式 */
.input-wrapper .icon {
  position: absolute;
  left: 16px;
  top: 50%;
  transform: translateY(-50%);
  width: 20px;
  height: 20px;
  color: #71717a;
}

/* 密码切换按钮 */
.toggle-password-btn {
  position: absolute;
  right: 32px;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #71717a;
  cursor: pointer;
  padding: 0;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.2s;
}

.toggle-password-btn .eye-icon {
  width: 0px;
  height: 22px;
}

.toggle-password-btn:hover {
  color: #ffffff;
}

.toggle-password-btn:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

/* 表单行 */
.form-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.checkbox-label input {
  width: 16px;
  height: 16px;
  border-radius: 4px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background-color: rgba(255, 255, 255, 0.05);
  color: #6366f1;
}

.checkbox-label span {
  font-size: 14px;
  color: #a1a1aa;
}

.forgot-password-link {
  font-size: 14px;
  color: #818cf8;
  text-decoration: none;
  transition: color 0.2s;
}

.forgot-password-link:hover {
  color: #a5b4fc;
}

/* 登录按钮 */
.login-btn {
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

.login-btn .arrow-icon {
  width: 18px;
  height: 18px;
  color: #ffffff;
}

.login-btn:hover {
  background-color: #4f46e5;
}

.login-btn:disabled {
  background-color: #4338ca;
  cursor: not-allowed;
  opacity: 0.8;
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

.social-btn img {
  width: 20px;
  height: 20px;
}

/* 注册链接 */
.signup-link {
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

.arrow-left-icon {
  width: 16px;
  height: 16px;
  color: #71717a;
}
</style>