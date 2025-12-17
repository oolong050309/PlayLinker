export default [
  {
    path: '/notifications',
    name: 'Notifications',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: '通知中心',
      requiresAuth: true
    }
  }
]
