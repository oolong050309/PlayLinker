export default [
  {
    path: '/binding',
    name: 'PlatformBinding',
    component: () => import('@/views/Platform/BindingView.vue'),
    meta: {
      title: '平台绑定',
      requiresAuth: true
    }
  }
]