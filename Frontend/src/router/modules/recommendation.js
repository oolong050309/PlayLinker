export default [
  {
    path: '/discover',
    name: 'Discover',
    component: () => import('@/views/Game/DiscoverView.vue'),
    meta: {
      title: '发现游戏',
      requiresAuth: true
    }
  }
]