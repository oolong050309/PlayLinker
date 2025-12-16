export default [
  {
    path: '/price-monitor',
    name: 'PriceMonitor',
    component: () => import('@/views/Price/PriceMonitorView.vue'),
    meta: {
      title: '价格监控',
      requiresAuth: true
    }
  }
]