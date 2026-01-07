import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

/**
 * 解析 JWT 并检查是否过期
 * @param {string} token - JWT token
 * @returns {boolean} true 表示有效，false 表示过期或无效
 */
function isTokenValid(token) {
  if (!token) return false
  try {
    const parts = token.split('.')
    if (parts.length !== 3) return false
    const payload = JSON.parse(atob(parts[1]))
    // exp 是秒级时间戳
    if (!payload.exp) return true // 没有过期时间则认为有效
    return payload.exp * 1000 > Date.now()
  } catch (e) {
    return false
  }
}

/**
 * 清除登录状态并跳转登录页
 */
function clearAuthAndRedirect() {
  sessionStorage.removeItem('token')
  sessionStorage.removeItem('refreshToken')
  sessionStorage.removeItem('user')
  // 避免重复跳转
  if (!window.location.pathname.includes('/login')) {
    window.location.href = '/login'
  }
}

// 请求拦截器
api.interceptors.request.use(
  config => {
    const token = sessionStorage.getItem('token')
    if (token) {
      // 检查 token 是否过期
      if (!isTokenValid(token)) {
        clearAuthAndRedirect()
        return Promise.reject(new Error('Token已过期，请重新登录'))
      }
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  error => {
    console.error('请求错误:', error)
    return Promise.reject(error)
  }
)

// 响应拦截器
api.interceptors.response.use(
  response => {
    const res = response.data
    
    // 统一处理响应格式
    // 注意：部分“认证流程”接口会返回 res.success=true，但 res.data.success=false（表示需要用户继续操作）
    // 这里仅在HTTP层面的 success=false 时才当作全局错误处理
    if (res.success === false) {
      console.error('API错误:', res.message)
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    
    return res
  },
  error => {
    console.error('响应错误:', error)
    
    // 处理HTTP错误状态码
    if (error.response) {
      const { status, data } = error.response
      
      switch (status) {
        case 401:
          console.error('未授权，请重新登录')
          clearAuthAndRedirect()
          break
        case 403:
          console.error('没有权限访问')
          break
        case 404:
          console.error('请求的资源不存在')
          break
        case 500:
          console.error('服务器错误')
          break
        default:
          console.error(data?.message || '请求失败')
      }
    } else if (error.request) {
      console.error('网络错误，请检查网络连接')
    } else {
      console.error('请求配置错误:', error.message)
    }
    
    return Promise.reject(error)
  }
)

// 游戏API
export const gameApi = {
  // 获取游戏列表
  getGames(params) {
    return api.get('/games', { params })
  },
  // 获取游戏详情
  getGame(id) {
    return api.get(`/games/${id}`)
  },
  // 搜索游戏
  searchGames(params) {
    return api.get('/games/search', { params })
  },
  // 获取游戏排行榜
  getRanking(params) {
    return api.get('/games/ranking', { params })
  }
}

// 元数据API
export const metadataApi = {
  // 获取题材列表
  getGenres() {
    return api.get('/genres')
  },
  // 获取分类列表
  getCategories() {
    return api.get('/categories')
  },
  // 获取开发商列表
  getDevelopers(params) {
    return api.get('/developers', { params })
  },
  // 获取发行商列表
  getPublishers(params) {
    return api.get('/publishers', { params })
  }
}

// 游戏库API
export const libraryApi = {
  // 获取游戏库概览
  getOverview() {
    return api.get('/library/overview')
  },
  // 获取用户游戏列表
  getGames(params) {
    return api.get('/library/games', { params })
  },
  // 同步平台数据
  syncPlatform(data) {
    return api.post('/library/sync', data)
  },
  // 获取游戏统计
  getStats() {
    return api.get('/library/stats')
  },
  // 获取游戏时长历史数据（7天内）
  getGamePlaytimeHistory(gameId) {
    return api.get(`/library/games/${gameId}/playtime-history`)
  }
}

// 成就API
export const achievementApi = {
  // 获取游戏成就列表
  getGameAchievements(gameId) {
    return api.get(`/games/${gameId}/achievements`)
  },
  // 获取用户成就总览（不需要传递userId，后端从JWT token获取）
  getUserAchievements() {
    return api.get('/library/achievements')
  },
  // 获取用户游戏成就
  getUserGameAchievements(gameId) {
    return api.get(`/library/games/${gameId}/achievements`)
  },
  // 同步成就
  syncAchievements(data) {
    return api.post('/library/achievements/sync', data)
  }
}

// 新闻API
export const newsApi = {
  // 获取新闻列表
  getNews(params) {
    return api.get('/news', { params })
  },
  // 获取新闻详情
  getNewsDetail(id) {
    return api.get(`/news/${id}`)
  },
  // 获取游戏新闻
  getGameNews(gameId, params) {
    return api.get(`/games/${gameId}/news`, { params })
  },
  // 同步指定游戏的Steam新闻
  syncSteamNews(gameId, count = 20) {
    return api.post('/news/steam/sync', {
      gameId: gameId,
      count: count
    })
  }
}

// Steam API
export const steamApi = {
  // 导入Steam数据（单独提高超时时间，避免导入过程过长导致超时）
  importData(data) {
    return api.post('/steam/import', data, { timeout: 120000 }) // 2分钟超时
  },
  // 获取Steam用户信息
  getUser(steamId) {
    return api.get(`/steam/user/${steamId}`)
  },
  // 获取Steam游戏信息
  getGame(appId) {
    return api.get(`/steam/games/${appId}`)
  }
}

// Xbox API
export const xboxApi = {
  // 导入Xbox数据
  importData(data) {
    return api.post('/xbox/import', data, { timeout: 120000 }) // 2分钟超时
  },
  // 获取Xbox用户信息
  getUser(xuid) {
    return api.get(`/xbox/user/${xuid}`)
  },
  // 获取Xbox游戏信息
  getGame(titleId) {
    return api.get(`/xbox/games/${titleId}`)
  },
  // 获取Xbox用户成就
  getUserAchievements(xuid) {
    return api.get(`/xbox/user/${xuid}/achievements`)
  },
  // 检查Xbox令牌状态
  checkTokenStatus() {
    return api.get('/xbox/token-status', { timeout: 30000 }) // 30秒超时
  },
  // Xbox认证（增加超时时间，因为可能需要打开浏览器）
  authenticate(data) {
    return api.post('/xbox/authenticate', data, { timeout: 300000 }) // 5分钟超时
  }
}

// PSN API
export const psnApi = {
  // 导入PSN数据
  importData(data) {
    return api.post('/psn/import', data, { timeout: 120000 }) // 2分钟超时
  },
  // 获取PSN用户信息
  getUser(onlineId) {
    return api.get(`/psn/user/${onlineId}`)
  },
  // 获取PSN游戏信息
  getGame(titleId) {
    return api.get(`/psn/games/${titleId}`)
  },
  // 获取PSN用户奖杯
  getUserTrophies(onlineId) {
    return api.get(`/psn/user/${onlineId}/trophies`)
  },
  // 检查PSN令牌状态
  checkTokenStatus() {
    return api.get('/psn/token-status', { timeout: 30000 }) // 30秒超时
  },
  // PSN认证（增加超时时间）
  authenticate(data) {
    return api.post('/psn/authenticate', data, { timeout: 300000 }) // 5分钟超时
  }
}

// GOG API
export const gogApi = {
  // 导入GOG数据
  importData(data) {
    return api.post('/gog/import', data, { timeout: 120000 }) // 2分钟超时
  },
  // 获取GOG用户信息
  getUser(gogUserId) {
    return api.get(`/gog/user/${gogUserId}`)
  },
  // 获取GOG游戏信息
  getGame(gogGameId) {
    return api.get(`/gog/games/${gogGameId}`)
  },
  // 检查GOG令牌状态
  checkTokenStatus() {
    return api.get('/gog/token-status', { timeout: 30000 }) // 30秒超时
  },
  // GOG认证（增加超时时间，因为可能需要浏览器认证）
  authenticate(data) {
    return api.post('/gog/authenticate', data, { timeout: 300000 }) // 5分钟超时
  }
}

// Epic Games API
export const epicApi = {
  // 导入Epic Games数据
  importData(data) {
    return api.post('/epic/import', data, { timeout: 120000 }) // 2分钟超时
  },
  // 获取Epic Games用户信息
  getUser(epicAccountId) {
    return api.get(`/epic/user/${epicAccountId}`)
  },
  // 获取Epic Games游戏信息
  getGame(gameId) {
    return api.get(`/epic/games/${gameId}`)
  },
  // 检查Epic Games令牌状态
  checkTokenStatus() {
    return api.get('/epic/token-status', { timeout: 30000 }) // 30秒超时
  },
  // Epic Games认证（增加超时时间）
  authenticate(data) {
    return api.post('/epic/authenticate', data, { timeout: 300000 }) // 5分钟超时
  }
}

export default api

