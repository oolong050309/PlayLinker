import { createRouter, createWebHistory } from 'vue-router'

// 导入路由模块
import authRoutes from './modules/auth'
import platformRoutes from './modules/platform'
import notificationRoutes from './modules/notification'
import parentalRoutes from './modules/parental'
import gameRoutes from './modules/game'
import libraryRoutes from './modules/library'
import localManageRoutes from './modules/localManage'
import analyticsRoutes from './modules/analytics'
import priceRoutes from './modules/price'
import recommendationRoutes from './modules/recommendation'

const routes = [
  {
    path: '/',
    redirect: '/discover'
  },
  ...authRoutes,
  ...platformRoutes,
  ...notificationRoutes,
  ...parentalRoutes,
  ...gameRoutes,
  ...libraryRoutes,
  ...localManageRoutes,
  ...analyticsRoutes,
  ...priceRoutes,
  ...recommendationRoutes,
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/Error/NotFoundView.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) {
      return savedPosition
    } else {
      return { top: 0 }
    }
  }
})

// 全局路由守卫
router.beforeEach((to, from, next) => {
  // 动态设置页面标题
  document.title = to.meta.title ? `${to.meta.title} - PlayLinker` : 'PlayLinker'
  
  // 检查是否需要认证
  const token = localStorage.getItem('token')
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth)
  
  if (requiresAuth && !token) {
    // 需要登录但未登录，跳转到登录页
    next({
      path: '/login',
      query: { redirect: to.fullPath }
    })
  } else if (to.path === '/login' && token) {
    // 已登录用户访问登录页，跳转到发现页
    next('/discover')
  } else {
    next()
  }
})

export default router

