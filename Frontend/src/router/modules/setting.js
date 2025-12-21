export default [
  {
    path: '/settings',
    name: 'Settings',
    component: () => import('@/views/Settings/SettingsView.vue'),
    meta: {
      title: '设置',
      requiresAuth: true
    }
  }
]

