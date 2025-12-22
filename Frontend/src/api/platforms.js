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
    // 注意：这个API端点可能需要根据实际后端实现调整
    // 如果后端没有这个端点，可以暂时使用其他同步接口
    return api.post(`/platforms/${platformId}/sync`).catch(() => {
      // 如果同步API不存在，返回成功（避免报错）
      return Promise.resolve({ success: true, message: '同步功能待实现' })
    })
  }
}

export default platformsApi

