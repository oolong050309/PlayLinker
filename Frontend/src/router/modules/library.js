export default [
  {
    path: '/library',
    name: 'Library',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: '我的游戏库',
      requiresAuth: true
    }
  }
]
