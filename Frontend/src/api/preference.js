import request from './index'

export const preferenceApi = {
  // 获取用户偏好详情
  getPreferences() {
    return request.get('/preferences')
  },
  // 更新用户偏好
  updatePreferences(data) {
    return request.patch('/preferences', data)
  },
  // 基于历史行为分析生成偏好
  analyzePreferences(data) {
    return request.post('/preferences/analyze', data)
  }
}