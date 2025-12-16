import request from './index'

export const wishlistApi = {
  // 获取愿望单列表
  getWishlist(params) {
    return request.get('/wishlist', { params })
  },
  // 添加游戏到愿望单
  addToWishlist(data) {
    return request.post('/wishlist', data)
  },
  // 从愿望单移除
  removeFromWishlist(id) {
    return request.delete(`/wishlist/${id}`)
  },
  // 更新愿望单设置（如目标价格）
  updateWishlist(id, data) {
    return request.patch(`/wishlist/${id}`, data)
  }
}