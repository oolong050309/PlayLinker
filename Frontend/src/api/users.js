import api from './index'

/**
 * 用户相关 API
 */
export const usersApi = {
  /**
   * 获取个人信息
   * @returns {Promise}
   */
  getProfile() {
    return api.get('/users/profile')
  },

  /**
   * 更新个人信息
   * @param {Object} data - 更新数据 { email?, phone?, gender?, avatarUrl? }
   * @returns {Promise}
   */
  updateProfile(data) {
    return api.patch('/users/profile', data)
  },

  /**
   * 修改密码
   * @param {Object} data - { oldPassword, newPassword }
   * @returns {Promise}
   */
  changePassword(data) {
    return api.post('/users/change-password', data)
  },

  /**
   * 上传头像
   * @param {FormData} formData - 包含文件的 FormData
   * @returns {Promise}
   */
  uploadAvatar(file) {
    const formData = new FormData()
    formData.append('file', file)
    return api.post('/users/avatar', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  },

  /**
   * 更新用户角色
   * @param {Object} data - { role: 'user' | 'parent' | 'admin' }
   * @returns {Promise}
   */
  updateRole(data) {
    return api.patch('/users/role', data)
  },

  /**
   * 删除账户（将状态设为 inactive）
   * @returns {Promise}
   */
  deleteAccount() {
    return api.delete('/users/account')
  }
}

export default usersApi

