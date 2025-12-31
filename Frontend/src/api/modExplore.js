import api from '@/api/index'

/**
 * 获取游戏支持的 Mod 来源
 * @param {number} gameId 游戏ID
 * @returns {Promise}
 */
export const getGameModSources = (gameId) => {
  return api.get(`/mod-explore/sources/${gameId}`)
}

/**
 * 获取 Mod 列表
 * @param {Object} params 查询参数
 * @param {number} params.gameId 游戏ID
 * @param {string} params.source Mod来源: NexusMods, 3DM, GameBanana
 * @param {number} params.page 页码
 * @param {number} params.pageSize 每页数量
 * @param {string} params.sortBy 排序: downloads, updated, endorsements
 * @returns {Promise}
 */
export const getModList = (params) => {
  return api.get('/mod-explore/list', { params })
}

/**
 * 获取 Mod 详情
 * @param {string} source Mod来源
 * @param {string} modId Mod ID
 * @param {string} domain 域名（NexusMods 需要）
 * @returns {Promise}
 */
export const getModDetail = (source, modId, domain = null) => {
  return api.get('/mod-explore/detail', { params: { source, modId, domain } })
}

/**
 * 搜索 Mod
 * @param {string} source Mod来源
 * @param {string} query 搜索关键词
 * @param {string} domain 域名（NexusMods 需要）
 * @param {number} page 页码
 * @returns {Promise}
 */
export const searchMods = (source, query, domain = null, page = 1) => {
  return api.get('/mod-explore/search', { params: { source, query, domain, page } })
}

export default {
  getGameModSources,
  getModList,
  getModDetail,
  searchMods
}
