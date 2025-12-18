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
    name: 'Landing',
    component: () => import('@/views/Landing.vue'),
    meta: {
      title: '首页'
    }
  },
  {
    path: '/app',
    component: () => import('@/views/Home.vue'),
    redirect: '/app/discover',
    children: [
      {
        path: 'list',
        name: 'GameList',
        component: () => import('@/views/GameList.vue'),
        meta: {
          title: '游戏列表',
          requiresAuth: true
        }
      },
      ...gameRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      ...libraryRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      ...localManageRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      ...analyticsRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      ...priceRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      ...recommendationRoutes.map(route => ({
        ...route,
        path: route.path.startsWith('/') ? route.path.replace('/', '') : route.path,
        meta: { ...route.meta, requiresAuth: true }
      })),
      {
        path: 'ranking',
        name: 'GameRanking',
        component: () => import('@/views/GameRanking.vue'),
        meta: {
          title: '排行榜',
          requiresAuth: true
        }
      },
      {
        path: 'achievements',
        name: 'Achievements',
        component: () => import('@/views/Achievements.vue'),
        meta: {
          title: '成就',
          requiresAuth: true
        }
      },
      {
        path: 'news',
        name: 'News',
        component: () => import('@/views/News.vue'),
        meta: {
          title: '新闻',
          requiresAuth: true
        }
      }
    ]
  },
  ...authRoutes,
  ...platformRoutes,
  ...notificationRoutes,
  ...parentalRoutes,
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
  
  // 检查是否需要认证（使用 sessionStorage）
  const token = sessionStorage.getItem('token')
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth)
  
  if (requiresAuth && !token) {
    // 需要登录但未登录，跳转到登录页
    next({
      path: '/login',
      query: { redirect: to.fullPath }
    })
  } else if (to.path === '/login' && token) {
    // 已登录用户访问登录页，跳转到应用首页
    next('/app/discover')
  } else if (to.path === '/register' && token) {
    // 已登录用户访问注册页，跳转到应用首页
    next('/app/discover')
  } else {
    next()
  }
})

export default router

