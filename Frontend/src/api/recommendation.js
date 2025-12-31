import request from './index'

export const recommendationApi = {
  // 获取推荐列表 (保留原有接口，可用于其他页面)
  getRecommendations(params) {
    return request.get('/recommendations', { params })
  },
  
  // 探索新游戏 (AI)
  // params: { refresh: boolean }
  exploreGames(params) {
    return request.get('/recommendations/explore', { params })
  },
  
  // 获取相似游戏
  getSimilarGames(gameId, params) {
    return request.get(`/recommendations/similar/${gameId}`, { params })
  },
  
  // 提交推荐反馈
  // id: recommendationId
  // data: { feedbackResult: 1|2, remark: string }
  submitFeedback(id, data) {
    return request.post(`/recommendations/${id}/feedback`, data)
  }
}