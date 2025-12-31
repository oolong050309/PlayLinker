import request from './index'

// 获取API基础URL
const getBaseUrl = () => {
  return import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1'
}

/**
 * 获取用户报表概览
 * @returns {Promise}
 */
export const getUserReportOverview = () => {
  return request({
    url: '/user-report/overview',
    method: 'get',
    timeout: 30000 // 30秒超时
  })
}

/**
 * 获取游戏库统计
 * @returns {Promise}
 */
export const getGameLibraryStats = () => {
  return request({
    url: '/user-report/game-library',
    method: 'get',
    timeout: 30000
  })
}

/**
 * 获取成就统计
 * @returns {Promise}
 */
export const getAchievementStats = () => {
  return request({
    url: '/user-report/achievements',
    method: 'get',
    timeout: 30000
  })
}

/**
 * 获取最近游玩记录
 * @param {number} count - 返回数量
 * @returns {Promise}
 */
export const getRecentPlayed = (count = 10) => {
  return request({
    url: '/user-report/recent-played',
    method: 'get',
    params: { count },
    timeout: 30000
  })
}

/**
 * 获取愿望单
 * @returns {Promise}
 */
export const getWishlist = () => {
  return request({
    url: '/user-report/wishlist',
    method: 'get',
    timeout: 30000
  })
}

/**
 * 同步Steam数据
 * @returns {Promise}
 */
export const syncFromSteam = () => {
  return request({
    url: '/user-report/sync',
    method: 'post',
    timeout: 120000 // 2分钟超时，同步需要更长时间
  })
}

// ============ 报告生成 API ============

/**
 * 获取月度报告下载URL
 * @param {string} format - 格式: html, csv, pdf
 * @param {number} year - 年份
 * @param {number} month - 月份
 * @returns {string}
 */
export const getMonthlyReportUrl = (format, year, month) => {
  const baseUrl = getBaseUrl()
  return `${baseUrl}/user-report/reports/monthly/${format}?year=${year}&month=${month}`
}

/**
 * 获取年度报告下载URL
 * @param {string} format - 格式: html, pdf
 * @param {number} year - 年份
 * @returns {string}
 */
export const getYearlyReportUrl = (format, year) => {
  const baseUrl = getBaseUrl()
  return `${baseUrl}/user-report/reports/yearly/${format}?year=${year}`
}

/**
 * 获取库存报告下载URL
 * @param {string} format - 格式: html, csv, pdf
 * @returns {string}
 */
export const getInventoryReportUrl = (format) => {
  const baseUrl = getBaseUrl()
  return `${baseUrl}/user-report/reports/inventory/${format}`
}

/**
 * 下载报告（带认证）
 * @param {string} url - 报告URL
 * @param {string} filename - 文件名
 */
export const downloadReport = async (url, filename) => {
  const token = localStorage.getItem('token')
  
  const response = await fetch(url, {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  })
  
  if (!response.ok) {
    throw new Error('下载失败')
  }
  
  const blob = await response.blob()
  const downloadUrl = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = downloadUrl
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(downloadUrl)
}

/**
 * 在新窗口打开HTML报告
 * @param {string} url - 报告URL
 */
export const openHtmlReport = async (url) => {
  const token = localStorage.getItem('token')
  
  const response = await fetch(url, {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  })
  
  if (!response.ok) {
    throw new Error('获取报告失败')
  }
  
  const html = await response.text()
  const newWindow = window.open('', '_blank')
  newWindow.document.write(html)
  newWindow.document.close()
}

export default {
  getUserReportOverview,
  getGameLibraryStats,
  getAchievementStats,
  getRecentPlayed,
  getWishlist,
  syncFromSteam,
  getMonthlyReportUrl,
  getYearlyReportUrl,
  getInventoryReportUrl,
  downloadReport,
  openHtmlReport
}
