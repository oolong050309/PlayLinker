export default [
  {
    path: '/notifications',
    name: 'Notifications',
    component: () => import('@/views/Notifications/NotificationsView.vue'),
    meta: {
      title: '消息中心',
      requiresAuth: true
    }
  }
]
