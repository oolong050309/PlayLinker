export default [
  {
    path: 'price-monitor',
    name: 'PriceMonitor',
    component: () => import('@/views/Price/PriceMonitorView.vue'),
    meta: {
      title: '愿望单',
      requiresAuth: true
    }
  }
]