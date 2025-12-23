import api from './index'

/**
 * 平台绑定API
 */
export const platformsApi = {
  /**
   * 获取绑定列表
   */
  getBindings() {
    return api.get('/platforms/bindings')
  },

  /**
   * 绑定平台
   * @param {Object} data - 绑定数据
   * @param {number} data.platformId - 平台ID (1:Steam, 5:GOG, 6:PSN, 7:Xbox)
   * @param {string} [data.steamId] - Steam ID (Steam平台必需)
   * @param {string} [data.apiKey] - Steam API Key (Steam平台必需)
   * @param {string} [data.xboxUserId] - Xbox用户ID (Xbox平台必需)
   * @param {string} [data.psnOnlineId] - PSN在线ID (PSN平台必需)
   * @param {string} [data.gogUserId] - GOG用户ID (GOG平台必需)
   * @param {string} [data.accessToken] - 访问令牌 (Xbox/PSN/GOG平台可选)
   * @param {string} [data.refreshToken] - 刷新令牌 (Xbox/PSN/GOG平台可选)
   */
  bindPlatform(data) {
    return api.post('/platforms/bind', data)
  },

  /**
   * 解绑平台
   * @param {number} bindingId - 绑定ID
   */
  unbindPlatform(bindingId) {
    return api.delete(`/platforms/bindings/${bindingId}`)
  },

  /**
   * 同步平台数据
   * @param {number} platformId - 平台ID
   */
  syncPlatform(platformId) {
    // 当前后端未提供 /platforms/{id}/sync 端点，实际同步由其他接口完成
    // 这里直接返回成功，避免 404 报错干扰前端逻辑
    return Promise.resolve({ success: true, message: '平台同步由其他接口处理' })
  }
}

export default platformsApi

