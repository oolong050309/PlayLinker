import api from './index'

// 家长监管 API
export const parentalApi = {
  /**
   * 家长创建邀请（通过子账户用户名）
   * @param {{ childUsername: string, message?: string }} data
   */
  createInvitation(data) {
    return api.post('/parental/invitations', data)
  },

  /**
   * 子账户响应邀请
   * @param {{ token: string, accept: boolean }} data
   */
  respondInvitation(data) {
    return api.post('/parental/invitations/respond', data)
  }
}

export default parentalApi
