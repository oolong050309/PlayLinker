export default [
  {
    path: '/mods',
    name: 'Mods',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: 'Mod管理',
      requiresAuth: true
    }
  },
  {
    path: '/mods/:id',
    name: 'ModDetail',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: 'Mod详情',
      requiresAuth: true
    }
  }
]
