import api from '@/api'

/**
 * 通用GET请求
 * @param {string} url - 请求URL
 * @param {Object} params - 查询参数
 * @returns {Promise}
 */
export const get = (url, params = {}) => {
  return api.get(url, { params })
}

/**
 * 通用POST请求
 * @param {string} url - 请求URL
 * @param {Object} data - 请求数据
 * @returns {Promise}
 */
export const post = (url, data = {}) => {
  return api.post(url, data)
}

/**
 * 通用PUT请求
 * @param {string} url - 请求URL
 * @param {Object} data - 请求数据
 * @returns {Promise}
 */
export const put = (url, data = {}) => {
  return api.put(url, data)
}

/**
 * 通用DELETE请求
 * @param {string} url - 请求URL
 * @returns {Promise}
 */
export const del = (url) => {
  return api.delete(url)
}

/**
 * 通用PATCH请求
 * @param {string} url - 请求URL
 * @param {Object} data - 请求数据
 * @returns {Promise}
 */
export const patch = (url, data = {}) => {
  return api.patch(url, data)
}

/**
 * 文件上传
 * @param {string} url - 上传URL
 * @param {FormData} formData - 表单数据
 * @param {Function} onProgress - 上传进度回调
 * @returns {Promise}
 */
export const upload = (url, formData, onProgress) => {
  return api.post(url, formData, {
    headers: {
      'Content-Type': 'multipart/form-data'
    },
    onUploadProgress: (progressEvent) => {
      if (onProgress && progressEvent.total) {
        const percentCompleted = Math.round((progressEvent.loaded * 100) / progressEvent.total)
        onProgress(percentCompleted)
      }
    }
  })
}

/**
 * 文件下载
 * @param {string} url - 下载URL
 * @param {string} filename - 文件名
 * @returns {Promise}
 */
export const download = async (url, filename) => {
  try {
    const response = await api.get(url, {
      responseType: 'blob'
    })
    
    const blob = new Blob([response])
    const link = document.createElement('a')
    link.href = window.URL.createObjectURL(blob)
    link.download = filename
    link.click()
    window.URL.revokeObjectURL(link.href)
    
    return true
  } catch (error) {
    console.error('下载失败:', error)
    return false
  }
}

export default {
  get,
  post,
  put,
  del,
  patch,
  upload,
  download
}
