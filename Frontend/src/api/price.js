import request from './index'

export const priceApi = {
  // 获取单个游戏的价格历史数据
  getPriceHistory(gameId, params) {
    return request.get(`/prices/history/${gameId}`, { params })
  },
  // 批量查询当前价格
  getCurrentPrices(params) {
    return request.get('/prices/current', { params })
  },
  // 启动价格跟踪
  trackPrice(data) {
    return request.post('/prices/track', data)
  },
  // 获取AI价格预测
  getPricePredictions(gameId, params) {
    return request.get(`/prices/predictions/${gameId}`, { params })
  },
  // 获取订阅的提醒列表
  getSubscriptions(params) {
    return request.get('/prices/subscriptions', { params })
  },
  // 新增价格提醒订阅
  subscribeAlert(data) {
    return request.post('/prices/track', data)
  },
  // 取消订阅
  unsubscribeAlert(id) {
    return request.delete(`/prices/subscriptions/${id}`)
  },
  // 获取价格监控状态和统计信息
  getMonitoringStatus() {
    return request.get('/prices/monitoring-status')
  },
  // 手动触发价格更新
  triggerPriceUpdate() {
    return request.post('/prices/update-now')
  }
}