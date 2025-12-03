import axios from 'axios'

const api = axios.create({
  baseURL: '/api/v1',
  timeout: 10000
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
    return Promise.reject(error)
  }
)

// 响应拦截器
api.interceptors.response.use(
  response => {
    return response.data
  },
  error => {
    console.error('API Error:', error)
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

