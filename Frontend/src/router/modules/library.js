export default [
  {
    path: '/library',
    name: 'Library',
    component: () => import('@/views/Library.vue'),
    meta: {
      title: '我的游戏库',
      requiresAuth: true
    }
  }
]
