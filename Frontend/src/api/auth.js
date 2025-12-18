import api from './index'

/**
 * 认证相关 API
 */
export const authApi = {
  /**
   * 用户注册
   * @param {Object} data - 注册数据 { username, password, email, phone? }
   * @returns {Promise}
   */
  register(data) {
    return api.post('/auth/register', data)
  },

  /**
   * 用户登录
   * @param {Object} data - 登录数据 { username, password }
   * @returns {Promise}
   */
  login(data) {
    return api.post('/auth/login', data)
  },

  /**
   * 忘记密码 - 发送验证码
   * @param {Object} data - { email }
   * @returns {Promise}
   */
  forgotPassword(data) {
    return api.post('/auth/forgot-password', data)
  },

  /**
   * 验证重置码
   * @param {Object} data - { email, code }
   * @returns {Promise}
   */
  verifyResetCode(data) {
    return api.post('/auth/verify-reset-code', data)
  },

  /**
   * 使用验证码重置密码
   * @param {Object} data - { email, code, newPassword }
   * @returns {Promise}
   */
  resetPasswordByCode(data) {
    return api.post('/auth/reset-password-by-code', data)
  },

  /**
   * 刷新 Token
   * @param {Object} data - { refreshToken }
   * @returns {Promise}
   */
  refreshToken(data) {
    return api.post('/auth/refresh', data)
  },

  /**
   * 退出登录
   * @param {Object} data - { allDevices?: boolean }
   * @returns {Promise}
   */
  logout(data = {}) {
    return api.post('/auth/logout', data)
  }
}

export default authApi

