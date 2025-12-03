import { createRouter, createWebHistory } from 'vue-router'
import GameList from '../views/GameList.vue'
import GameDetail from '../views/GameDetail.vue'
import GameRanking from '../views/GameRanking.vue'
import Library from '../views/Library.vue'
import Achievements from '../views/Achievements.vue'
import News from '../views/News.vue'

const routes = [
  {
    path: '/',
    name: 'GameList',
    component: GameList
  },
  {
    path: '/games/:id',
    name: 'GameDetail',
    component: GameDetail
  },
  {
    path: '/ranking',
    name: 'GameRanking',
    component: GameRanking
  },
  {
    path: '/library',
    name: 'Library',
    component: Library
  },
  {
    path: '/achievements',
    name: 'Achievements',
    component: Achievements
  },
  {
    path: '/news',
    name: 'News',
    component: News
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router

