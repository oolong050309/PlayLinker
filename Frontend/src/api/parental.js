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
  },

  /**
   * 获取子账户列表（家长使用）
   */
  getChildren() {
    return api.get('/parental/children')
  },

  /**
   * 获取家长信息（子账户使用）
   */
  getParent() {
    return api.get('/parental/parent')
  },

  /**
   * 删除监管关系（仅家长可以删除）
   * @param {number} childId - 子账户ID
   */
  deleteRelationship(childId) {
    return api.delete(`/parental/relationships/${childId}`)
  },

  /**
   * 设置监管规则
   * @param {{ childUserId: number, ruleType: string, ruleValue: object, isActive: boolean }} data
   */
  setRule(data) {
    return api.post('/parental/rules', data)
  },

  /**
   * 获取规则列表
   * @param {number} childId - 子账户ID
   */
  getRules(childId) {
    return api.get(`/parental/rules/${childId}`)
  },

  /**
   * 更新监管规则
   * @param {number} ruleId - 规则ID
   * @param {{ ruleValue: object, isActive: boolean }} data
   */
  updateRule(ruleId, data) {
    return api.put(`/parental/rules/${ruleId}`, data)
  },

  /**
   * 删除监管规则
   * @param {number} ruleId - 规则ID
   */
  deleteRule(ruleId) {
    return api.delete(`/parental/rules/${ruleId}`)
  },

  /**
   * 切换规则状态（启用/停用）
   * @param {number} ruleId - 规则ID
   * @param {object} ruleValue - 当前规则值（保持不变）
   * @param {boolean} isActive - 是否启用
   */
  toggleRuleStatus(ruleId, ruleValue, isActive) {
    return api.put(`/parental/rules/${ruleId}`, {
      ruleValue: ruleValue || {}, // 传递当前规则值，确保不丢失内容
      isActive: isActive
    })
  },

  /**
   * 获取子账户过去一周的游玩时间
   * @param {number} childId - 子账户ID
   */
  getChildWeeklyPlaytime(childId) {
    return api.get(`/parental/children/${childId}/weekly-playtime`)
  }
}

export default parentalApi
