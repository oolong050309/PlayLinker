import api from '@/api/index'

/**
 * 搜索游戏（模糊搜索）
 * @param {string} query 搜索关键词
 * @param {number} page 页码
 * @param {number} pageSize 每页数量
 * @returns {Promise}
 */
export const searchGames = (query, page = 1, pageSize = 10) => {
  return api.get('/games/search', { 
    params: { 
      query, 
      page, 
      page_size: pageSize 
    } 
  })
}

/**
 * 获取游戏列表
 * @param {Object} params 查询参数
 * @returns {Promise}
 */
export const getGames = (params) => {
  return api.get('/games', { params })
}

/**
 * 获取游戏详情
 * @param {number} gameId 游戏ID
 * @returns {Promise}
 */
export const getGameDetail = (gameId) => {
  return api.get(`/games/${gameId}`)
}

export default {
  searchGames,
  getGames,
  getGameDetail
}
