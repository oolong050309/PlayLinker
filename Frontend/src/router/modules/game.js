export default [
  {
    path: '/game/:id',
    name: 'GameDetail',
    component: () => import('@/views/GameDetail/GameDetail.vue'),
    meta: {
      title: '游戏详情',
      requiresAuth: true
    }
  },
  {
    path: '/store/:id',
    name: 'StoreDetail',
    component: () => import('@/views/GameDetail/StoreDetail.vue'),
    meta: {
      title: '商店详情',
      requiresAuth: true
    }
  }
]
