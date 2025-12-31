import request from './index'

// ==================== 本地游戏管理 ====================

/**
 * 获取本地游戏列表
 * @param {Object} params - 查询参数
 * @param {number} params.page - 页码
 * @param {number} params.page_size - 每页数量
 * @param {string} params.sort_by - 排序字段
 * @returns {Promise}
 */
export const getLocalGames = (params) => {
  return request({
    url: '/local/games',
    method: 'get',
    params
  })
}

/**
 * 添加本地游戏记录
 * @param {Object} data - 游戏信息
 * @param {number} data.gameId - 游戏ID
 * @param {number} data.platformId - 平台ID（可选）
 * @param {string} data.installPath - 安装路径
 * @param {string} data.version - 版本（可选）
 * @param {number} data.sizeGB - 大小（GB）
 * @param {string} data.executablePath - 可执行文件路径（可选）
 * @param {string} data.configPath - 配置路径（可选）
 * @returns {Promise}
 */
export const addLocalGame = (data) => {
  return request({
    url: '/local/games',
    method: 'post',
    data
  })
}

/**
 * 获取本地游戏详情
 * @param {number} installId - 安装ID
 * @returns {Promise}
 */
export const getLocalGameDetail = (installId) => {
  return request({
    url: `/local/games/${installId}`,
    method: 'get'
  })
}

/**
 * 移除本地游戏记录
 * @param {number} installId - 安装ID
 * @returns {Promise}
 */
export const removeLocalGame = (installId) => {
  return request({
    url: `/local/games/${installId}`,
    method: 'delete',
    data: { deleteFiles: false }
  })
}

/**
 * 更新本地游戏安装路径
 * @param {number} installId - 安装ID
 * @param {string} installPath - 新的安装路径
 * @param {number} sizeGB - 游戏大小（GB）
 * @returns {Promise}
 */
export const updateLocalGamePath = (installId, installPath, sizeGB = 0) => {
  return request({
    url: `/local/games/${installId}/path`,
    method: 'patch',
    data: { newPath: installPath, sizeGB }
  })
}

// ==================== 存档管理 ====================

/**
 * 获取本地存档列表
 * @param {Object} params - 查询参数
 * @param {number} params.game_id - 游戏ID
 * @param {number} params.page - 页码
 * @param {number} params.page_size - 每页数量
 * @returns {Promise}
 */
export const getLocalSaves = (params) => {
  return request({
    url: '/saves/local',
    method: 'get',
    params
  })
}

/**
 * 添加本地存档记录
 * @param {Object} data - 存档信息
 * @param {number} data.installId - 游戏安装ID
 * @param {string} data.filePath - 存档文件路径
 * @param {number} data.fileSize - 文件大小（字节）
 * @param {string} data.updatedAt - 更新时间（可选）
 * @returns {Promise}
 */
export const addLocalSave = (data) => {
  return request({
    url: '/saves/local',
    method: 'post',
    data
  })
}

/**
 * 删除存档记录
 * @param {number} saveId - 存档ID
 * @returns {Promise}
 */
export const deleteSave = (saveId) => {
  return request({
    url: `/saves/${saveId}`,
    method: 'delete',
    data: { deleteFile: false, deleteBackups: false }
  })
}

/**
 * 删除本地存档记录
 * @param {number} saveId - 存档ID
 * @returns {Promise}
 */
export const deleteLocalSave = (saveId) => {
  return request({
    url: `/saves/${saveId}`,
    method: 'delete',
    data: { deleteFile: false, deleteBackups: false }
  })
}

// ==================== 云存档管理 ====================

/**
 * 获取云存档列表
 * @param {Object} params - 查询参数
 * @param {number} params.game_id - 游戏ID
 * @param {number} params.page - 页码
 * @param {number} params.page_size - 每页数量
 * @returns {Promise}
 */
export const getCloudSaves = (params) => {
  return request({
    url: '/cloud/saves',
    method: 'get',
    params
  })
}

/**
 * 上传存档到云端
 * @param {FormData} formData - 包含文件和参数的FormData
 * @returns {Promise}
 */
export const uploadCloudSave = (formData) => {
  return request({
    url: '/cloud/upload',
    method: 'post',
    data: formData,
    headers: {
      'Content-Type': 'multipart/form-data'
    }
  })
}

/**
 * 下载云存档
 * @param {string} cloudBackupId - 云备份ID
 * @returns {Promise}
 */
export const downloadCloudSave = (cloudBackupId) => {
  return request({
    url: `/cloud/download/${cloudBackupId}`,
    method: 'get',
    responseType: 'blob'
  })
}

/**
 * 删除云存档
 * @param {string} cloudBackupId - 云备份ID
 * @returns {Promise}
 */
export const deleteCloudSave = (cloudBackupId) => {
  return request({
    url: `/cloud/saves/${cloudBackupId}`,
    method: 'delete'
  })
}

/**
 * 获取云存储使用情况
 * @returns {Promise}
 */
export const getCloudStorageUsage = () => {
  return request({
    url: '/cloud/storage/usage',
    method: 'get'
  })
}

// ==================== Mod管理 ====================

/**
 * 获取游戏Mod列表
 * @param {number} gameId - 游戏ID
 * @param {Object} params - 查询参数
 * @param {number} params.install_id - 安装ID
 * @param {boolean} params.enabled - 是否启用
 * @param {number} params.page - 页码
 * @param {number} params.page_size - 每页数量
 * @returns {Promise}
 */
export const getGameMods = (gameId, params) => {
  return request({
    url: `/games/${gameId}/mods`,
    method: 'get',
    params
  })
}

/**
 * 添加Mod记录（网页版仅记录信息）
 * @param {Object} data - Mod信息
 * @param {number} data.installId - 游戏安装ID
 * @param {string} data.modName - Mod名称
 * @param {number} data.version - Mod版本
 * @param {string} data.filePath - Mod文件路径
 * @param {string} data.description - Mod描述
 * @param {string} data.author - Mod作者
 * @param {boolean} data.autoEnable - 是否自动启用
 * @returns {Promise}
 */
export const addMod = (data) => {
  return request({
    url: '/mods/install',
    method: 'post',
    data
  })
}

/**
 * 确认手动安装完成
 * @param {number} modId - Mod ID
 * @returns {Promise}
 */
export const confirmModInstall = (modId) => {
  return request({
    url: `/mods/${modId}/confirm-install`,
    method: 'post'
  })
}

/**
 * 启用/禁用Mod
 * @param {number} modId - Mod ID
 * @param {boolean} enabled - 是否启用
 * @returns {Promise}
 */
export const toggleMod = (modId, enabled) => {
  return request({
    url: `/mods/${modId}/toggle`,
    method: 'patch',
    data: { enabled }
  })
}

/**
 * 删除Mod记录
 * @param {number} modId - Mod ID
 * @returns {Promise}
 */
export const deleteMod = (modId) => {
  return request({
    url: `/mods/${modId}`,
    method: 'delete',
    data: { deleteFiles: false }
  })
}

/**
 * 检测Mod冲突
 * @param {number} installId - 安装ID
 * @returns {Promise}
 */
export const checkModConflicts = (installId) => {
  return request({
    url: '/mods/conflicts',
    method: 'get',
    params: { install_id: installId }
  })
}

/**
 * 搜索游戏（全局游戏数据库）
 * @param {Object} params - 查询参数
 * @param {string} params.query - 搜索关键词
 * @param {number} params.page - 页码
 * @param {number} params.page_size - 每页数量
 * @returns {Promise}
 */
export const searchGames = (params) => {
  return request({
    url: '/games/search',
    method: 'get',
    params
  })
}

export default {
  // 本地游戏
  getLocalGames,
  addLocalGame,
  getLocalGameDetail,
  removeLocalGame,
  updateLocalGamePath,
  // 存档
  getLocalSaves,
  addLocalSave,
  deleteSave,
  deleteLocalSave,
  // 云存档
  getCloudSaves,
  uploadCloudSave,
  downloadCloudSave,
  deleteCloudSave,
  getCloudStorageUsage,
  // Mod
  getGameMods,
  addMod,
  confirmModInstall,
  toggleMod,
  deleteMod,
  checkModConflicts,
  // 游戏搜索
  searchGames
}
