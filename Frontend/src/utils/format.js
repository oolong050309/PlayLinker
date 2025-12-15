/**
 * 格式化日期
 * @param {Date|string|number} date - 日期对象、日期字符串或时间戳
 * @param {string} format - 格式化模板，默认 'YYYY-MM-DD HH:mm:ss'
 * @returns {string} 格式化后的日期字符串
 */
export const formatDate = (date, format = 'YYYY-MM-DD HH:mm:ss') => {
  if (!date) return ''
  
  const d = new Date(date)
  if (isNaN(d.getTime())) return ''
  
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const hour = String(d.getHours()).padStart(2, '0')
  const minute = String(d.getMinutes()).padStart(2, '0')
  const second = String(d.getSeconds()).padStart(2, '0')
  
  return format
    .replace('YYYY', year)
    .replace('MM', month)
    .replace('DD', day)
    .replace('HH', hour)
    .replace('mm', minute)
    .replace('ss', second)
}

/**
 * 格式化相对时间（多久之前）
 * @param {Date|string|number} date - 日期
 * @returns {string} 相对时间描述
 */
export const formatRelativeTime = (date) => {
  if (!date) return ''
  
  const now = new Date()
  const past = new Date(date)
  const diff = now - past
  
  const seconds = Math.floor(diff / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)
  const months = Math.floor(days / 30)
  const years = Math.floor(days / 365)
  
  if (years > 0) return `${years}年前`
  if (months > 0) return `${months}个月前`
  if (days > 0) return `${days}天前`
  if (hours > 0) return `${hours}小时前`
  if (minutes > 0) return `${minutes}分钟前`
  if (seconds > 0) return `${seconds}秒前`
  return '刚刚'
}

/**
 * 格式化价格
 * @param {number} price - 价格
 * @param {string} currency - 货币符号，默认 '$'
 * @param {number} decimals - 小数位数，默认 2
 * @returns {string} 格式化后的价格
 */
export const formatPrice = (price, currency = '$', decimals = 2) => {
  if (price === null || price === undefined) return ''
  const num = Number(price)
  if (isNaN(num)) return ''
  return `${currency}${num.toFixed(decimals)}`
}

/**
 * 格式化文件大小
 * @param {number} bytes - 字节数
 * @param {number} decimals - 小数位数，默认 2
 * @returns {string} 格式化后的文件大小
 */
export const formatFileSize = (bytes, decimals = 2) => {
  if (bytes === 0) return '0 B'
  if (!bytes) return ''
  
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  
  return `${(bytes / Math.pow(k, i)).toFixed(decimals)} ${sizes[i]}`
}

/**
 * 格式化游戏时长
 * @param {number} hours - 小时数
 * @returns {string} 格式化后的时长
 */
export const formatPlaytime = (hours) => {
  if (!hours) return '0分钟'
  
  if (hours < 1) {
    const minutes = Math.round(hours * 60)
    return `${minutes}分钟`
  }
  
  if (hours < 24) {
    return `${Math.round(hours)}小时`
  }
  
  const days = Math.floor(hours / 24)
  const remainingHours = Math.round(hours % 24)
  
  if (remainingHours === 0) {
    return `${days}天`
  }
  
  return `${days}天${remainingHours}小时`
}

/**
 * 格式化数字（添加千位分隔符）
 * @param {number} num - 数字
 * @returns {string} 格式化后的数字
 */
export const formatNumber = (num) => {
  if (num === null || num === undefined) return ''
  return Number(num).toLocaleString()
}

/**
 * 格式化百分比
 * @param {number} value - 数值（0-1 或 0-100）
 * @param {boolean} isDecimal - 是否为小数形式（0-1），默认 true
 * @param {number} decimals - 小数位数，默认 0
 * @returns {string} 格式化后的百分比
 */
export const formatPercent = (value, isDecimal = true, decimals = 0) => {
  if (value === null || value === undefined) return ''
  const percent = isDecimal ? value * 100 : value
  return `${percent.toFixed(decimals)}%`
}

/**
 * 截断文本
 * @param {string} text - 文本
 * @param {number} maxLength - 最大长度
 * @param {string} suffix - 后缀，默认 '...'
 * @returns {string} 截断后的文本
 */
export const truncate = (text, maxLength, suffix = '...') => {
  if (!text) return ''
  if (text.length <= maxLength) return text
  return text.substring(0, maxLength) + suffix
}

/**
 * 格式化手机号（隐藏中间4位）
 * @param {string} phone - 手机号
 * @returns {string} 格式化后的手机号
 */
export const formatPhone = (phone) => {
  if (!phone) return ''
  const str = String(phone)
  if (str.length !== 11) return str
  return str.replace(/(\d{3})\d{4}(\d{4})/, '$1****$2')
}

/**
 * 格式化邮箱（隐藏部分字符）
 * @param {string} email - 邮箱
 * @returns {string} 格式化后的邮箱
 */
export const formatEmail = (email) => {
  if (!email) return ''
  const [name, domain] = email.split('@')
  if (!name || !domain) return email
  
  const visibleLength = Math.min(3, Math.floor(name.length / 2))
  const hiddenPart = '*'.repeat(name.length - visibleLength)
  return `${name.substring(0, visibleLength)}${hiddenPart}@${domain}`
}

/**
 * 格式化评分（星级）
 * @param {number} rating - 评分（0-5）
 * @returns {string} 星级字符串
 */
export const formatRating = (rating) => {
  if (rating === null || rating === undefined) return ''
  const fullStars = Math.floor(rating)
  const hasHalfStar = rating % 1 >= 0.5
  const emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0)
  
  return '★'.repeat(fullStars) + 
         (hasHalfStar ? '☆' : '') + 
         '☆'.repeat(emptyStars)
}

/**
 * 格式化游戏平台名称
 * @param {string} platform - 平台代码
 * @returns {string} 平台全称
 */
export const formatPlatform = (platform) => {
  const platformMap = {
    'steam': 'Steam',
    'epic': 'Epic Games',
    'gog': 'GOG',
    'origin': 'Origin',
    'uplay': 'Ubisoft Connect',
    'xbox': 'Xbox',
    'psn': 'PlayStation Network',
    'switch': 'Nintendo Switch'
  }
  return platformMap[platform?.toLowerCase()] || platform
}

export default {
  formatDate,
  formatRelativeTime,
  formatPrice,
  formatFileSize,
  formatPlaytime,
  formatNumber,
  formatPercent,
  truncate,
  formatPhone,
  formatEmail,
  formatRating,
  formatPlatform
}
