export default [
  {
    path: '/user-report',
    name: 'UserReport',
    component: () => import('@/views/UserReport/UserReportView.vue'),
    meta: {
      title: '我的报表',
      requiresAuth: true
    }
  }
]
