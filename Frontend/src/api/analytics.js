import request from './index'

/**
 * 获取游玩时间分析
 * @param {Object} params - 查询参数
 * @param {string} params.period - 时间周期 (如 '2024-11', 'week', 'year')
 * @param {number} params.year - 年份
 * @param {number} params.month - 月份
 * @returns {Promise}
 */
export const getPlaytimeAnalytics = (params) => {
  return request({
    url: '/analytics/playtime',
    method: 'get',
    params
  })
}

/**
 * 获取题材偏好分析
 * @returns {Promise}
 */
export const getGenrePreferences = () => {
  return request({
    url: '/analytics/genres',
    method: 'get'
  })
}

/**
 * 获取平台分布分析 (新增)
 * @returns {Promise}
 */
export const getPlatformAnalytics = () => {
  return request({
    url: '/analytics/platforms',
    method: 'get'
  })
}

/**
 * 获取成就统计分析
 * @returns {Promise}
 */
export const getAchievementStats = () => {
  return request({
    url: '/analytics/achievements',
    method: 'get'
  })
}

/**
 * 获取消费分析
 * @param {Object} params - 查询参数
 * @param {string} params.period - 时间周期
 * @param {number} params.year - 年份
 * @returns {Promise}
 */
export const getSpendingAnalytics = (params) => {
  return request({
    url: '/analytics/spending',
    method: 'get',
    params
  })
}

/**
 * 获取报表模板列表
 * @returns {Promise}
 */
export const getReportTemplates = () => {
  return request({
    url: '/reports/templates',
    method: 'get'
  })
}

/**
 * 生成报表
 * @param {Object} data - 报表参数
 * @returns {Promise}
 */
export const generateReport = (data) => {
  return request({
    url: '/reports/generate',
    method: 'post',
    data
  })
}

/**
 * 获取报表历史列表
 * @param {Object} params - 查询参数
 * @returns {Promise}
 */
export const getReportHistory = (params) => {
  return request({
    url: '/reports',
    method: 'get',
    params
  })
}

/**
 * 获取报表详情
 * @param {string} reportId - 报表ID
 * @returns {Promise}
 */
export const getReportDetail = (reportId) => {
  return request({
    url: `/reports/${reportId}`,
    method: 'get'
  })
}

/**
 * 下载报表
 * @param {string} reportId - 报表ID
 * @returns {Promise}
 */
export const downloadReport = (reportId) => {
  return request({
    url: `/reports/${reportId}/download`,
    method: 'get',
    responseType: 'blob'
  })
}

/**
 * 删除报表
 * @param {string} reportId - 报表ID
 * @returns {Promise}
 */
export const deleteReport = (reportId) => {
  return request({
    url: `/reports/${reportId}`,
    method: 'delete'
  })
}

export default {
  getPlaytimeAnalytics,
  getGenrePreferences,
  getPlatformAnalytics, // 新增导出
  getAchievementStats,
  getSpendingAnalytics,
  getReportTemplates,
  generateReport,
  getReportHistory,
  getReportDetail,
  downloadReport,
  deleteReport
}