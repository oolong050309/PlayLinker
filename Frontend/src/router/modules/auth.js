export default [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: '登录'
    }
  },
  {
    path: '/register',
    name: 'Register',
    component: () => import('@/views/ComingSoon.vue'),
    meta: {
      title: '注册'
    }
  }
]
