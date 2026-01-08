export default [
  {
    path: '/library',
    name: 'Library',
    component: () => import('@/views/GameLibrary/Library.vue'),
    meta: {
      title: '我的游戏库',
      requiresAuth: true
    }
  }
]
