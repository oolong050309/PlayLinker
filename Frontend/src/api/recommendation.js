import request from './index'

export const recommendationApi = {
  // 获取推荐列表 (支持类型: game, discount, similar, trending)
  getRecommendations(params) {
    return request.get('/recommendations', { params })
  },
  // 探索新游戏
  exploreGames(params) {
    return request.get('/recommendations/explore', { params })
  },
  // 获取相似游戏
  getSimilarGames(gameId, params) {
    return request.get(`/recommendations/similar/${gameId}`, { params })
  },
  // 提交推荐反馈 (喜欢/不喜欢)
  submitFeedback(id, data) {
    return request.post(`/recommendations/${id}/feedback`, data)
  }
}