import api from './index'

// 通知中心 API
export const notificationsApi = {
  /**
   * 获取通知列表
   * @param {Object} params - 查询参数，如 { isRead, type, page, pageSize }
   */
  getNotifications(params) {
    return api.get('/notifications', { params })
  },

  /**
   * 标记单条通知为已读
   * @param {number} id - 通知ID
   */
  markAsRead(id) {
    return api.patch(`/notifications/${id}/read`)
  },

  /**
   * 删除单条通知
   * @param {number} id - 通知ID
   */
  delete(id) {
    return api.delete(`/notifications/${id}`)
  }
}

export default notificationsApi
