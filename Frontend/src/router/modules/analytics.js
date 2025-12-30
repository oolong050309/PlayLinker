export default [
  {
    path: '/analytics',
    name: 'Analytics',
    component: () => import('@/views/Analytics/AnalyticsView.vue'),
    meta: {
      title: '数据分析',
      requiresAuth: true
    }
  }
]
