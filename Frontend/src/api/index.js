import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 请求拦截器
api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
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
    if (res.success === false) {
      console.error('API错误:', res.message)
      // 可以在这里添加全局错误提示
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
          localStorage.removeItem('token')
          window.location.href = '/login'
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
  }
}

// 成就API
export const achievementApi = {
  // 获取游戏成就列表
  getGameAchievements(gameId) {
    return api.get(`/games/${gameId}/achievements`)
  },
  // 获取用户成就总览
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
  }
}

// Steam API
export const steamApi = {
  // 导入Steam数据
  importData(data) {
    return api.post('/steam/import', data)
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

export default api

