import request from './index'

export const platformApi = {
  // 获取已绑定和可绑定的平台列表
  getBindings() {
    return request.get('/platforms/bindings')
  },
  // 绑定新平台（通常涉及OAuth跳转，此处为提交绑定信息）
  bindPlatform(data) {
    return request.post('/platforms/bind', data)
  },
  // 解除绑定
  unbindPlatform(id) {
    return request.delete(`/platforms/bindings/${id}`)
  },
  // 手动触发平台数据同步
  syncPlatform(id) {
    return request.post(`/platforms/bindings/${id}/sync`)
  },
  // 更新同步设置（如自动同步开关）
  updateSyncSettings(data) {
    return request.patch('/platforms/settings', data)
  }
}