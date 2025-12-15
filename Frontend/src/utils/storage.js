/**
 * 存储数据到localStorage
 * @param {string} key - 存储键名
 * @param {*} value - 存储值（自动转换为JSON）
 */
export const setItem = (key, value) => {
  try {
    const jsonValue = JSON.stringify(value)
    localStorage.setItem(key, jsonValue)
  } catch (error) {
    console.error('存储失败:', error)
  }
}

/**
 * 从localStorage获取数据
 * @param {string} key - 存储键名
 * @returns {*} 解析后的值，失败返回null
 */
export const getItem = (key) => {
  try {
    const value = localStorage.getItem(key)
    return value ? JSON.parse(value) : null
  } catch (error) {
    console.error('读取失败:', error)
    return null
  }
}

/**
 * 删除localStorage中的数据
 * @param {string} key - 存储键名
 */
export const removeItem = (key) => {
  try {
    localStorage.removeItem(key)
  } catch (error) {
    console.error('删除失败:', error)
  }
}

/**
 * 清空localStorage所有数据
 */
export const clear = () => {
  try {
    localStorage.clear()
  } catch (error) {
    console.error('清空失败:', error)
  }
}

/**
 * 检查localStorage中是否存在某个键
 * @param {string} key - 存储键名
 * @returns {boolean}
 */
export const hasItem = (key) => {
  return localStorage.getItem(key) !== null
}

/**
 * 获取localStorage中所有键名
 * @returns {string[]}
 */
export const getAllKeys = () => {
  const keys = []
  for (let i = 0; i < localStorage.length; i++) {
    keys.push(localStorage.key(i))
  }
  return keys
}

/**
 * 存储数据到sessionStorage
 * @param {string} key - 存储键名
 * @param {*} value - 存储值（自动转换为JSON）
 */
export const setSessionItem = (key, value) => {
  try {
    const jsonValue = JSON.stringify(value)
    sessionStorage.setItem(key, jsonValue)
  } catch (error) {
    console.error('存储失败:', error)
  }
}

/**
 * 从sessionStorage获取数据
 * @param {string} key - 存储键名
 * @returns {*} 解析后的值，失败返回null
 */
export const getSessionItem = (key) => {
  try {
    const value = sessionStorage.getItem(key)
    return value ? JSON.parse(value) : null
  } catch (error) {
    console.error('读取失败:', error)
    return null
  }
}

/**
 * 删除sessionStorage中的数据
 * @param {string} key - 存储键名
 */
export const removeSessionItem = (key) => {
  try {
    sessionStorage.removeItem(key)
  } catch (error) {
    console.error('删除失败:', error)
  }
}

/**
 * 清空sessionStorage所有数据
 */
export const clearSession = () => {
  try {
    sessionStorage.clear()
  } catch (error) {
    console.error('清空失败:', error)
  }
}

/**
 * 带过期时间的存储
 * @param {string} key - 存储键名
 * @param {*} value - 存储值
 * @param {number} expire - 过期时间（毫秒）
 */
export const setItemWithExpire = (key, value, expire) => {
  const data = {
    value,
    expire: Date.now() + expire
  }
  setItem(key, data)
}

/**
 * 获取带过期时间的存储数据
 * @param {string} key - 存储键名
 * @returns {*} 未过期返回值，过期或不存在返回null
 */
export const getItemWithExpire = (key) => {
  const data = getItem(key)
  if (!data) return null
  
  if (Date.now() > data.expire) {
    removeItem(key)
    return null
  }
  
  return data.value
}

export default {
  setItem,
  getItem,
  removeItem,
  clear,
  hasItem,
  getAllKeys,
  setSessionItem,
  getSessionItem,
  removeSessionItem,
  clearSession,
  setItemWithExpire,
  getItemWithExpire
}
