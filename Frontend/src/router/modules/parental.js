export default [
  {
    path: '/parental',
    name: 'Parental',
    component: () => import('@/views/Parental/ParentalView.vue'),
    meta: {
      title: '家长监管',
      requiresAuth: true
    }
  }
]
