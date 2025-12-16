<template>
  <div class="flex-1 overflow-y-auto p-8">
    
    <div class="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
      <div class="glass-panel p-5 rounded-2xl flex items-center gap-4">
        <div class="w-12 h-12 rounded-xl bg-indigo-500/20 flex items-center justify-center text-indigo-400">
          <i data-lucide="link" class="w-6 h-6"></i>
        </div>
        <div>
          <div class="text-sm text-zinc-400">已连接平台</div>
          <div class="text-2xl font-bold">{{ connectedCount }} / {{ platforms.length }}</div>
        </div>
      </div>
    </div>

    <div class="mb-8">
      <h2 class="text-xl font-bold mb-4 flex items-center gap-2">
        <i data-lucide="gamepad-2" class="w-5 h-5 text-white"></i> 平台管理
      </h2>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div 
          v-for="platform in platforms" 
          :key="platform.id"
          class="glass-panel rounded-2xl overflow-hidden border transition-all hover:-translate-y-1"
          :class="platform.isConnected ? 'border-emerald-500/30' : 'border-white/5'"
        >
          <div :class="['p-6']">
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-3">
                <div class="w-12 h-12 bg-white rounded-xl flex items-center justify-center overflow-hidden p-1">
                  <img :src="platform.icon" class="w-full h-full object-contain">
                </div>
                <div>
                  <h3 class="font-bold text-lg">{{ platform.name }}</h3>
                  <p class="text-sm text-white/60">{{ platform.isConnected ? platform.username : '未连接' }}</p>
                </div>
              </div>
              <span v-if="platform.isConnected" class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs font-bold border border-emerald-500/30">已连接</span>
            </div>
            
            <div v-if="platform.isConnected" class="flex gap-4 text-sm text-white/70">
              <span class="flex items-center gap-1"><i data-lucide="gamepad-2" class="w-3 h-3"></i> {{ platform.gamesCount }} 游戏</span>
              <span class="flex items-center gap-1"><i data-lucide="trophy" class="w-3 h-3"></i> {{ platform.achievementsCount }} 成就</span>
            </div>
            <p v-else class="text-sm text-white/70">{{ platform.description }}</p>
            
            <div class="mt-4 flex gap-2">
              <button 
                v-if="platform.isConnected"
                @click="handleSync(platform.id)"
                class="flex-1 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-medium"
              >
                <i data-lucide="refresh-cw" class="w-4 h-4 mr-1"></i> 同步数据
              </button>
              <button 
                v-if="platform.isConnected"
                @click="handleDisconnect(platform.id)"
                class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg text-sm font-medium"
              >
                <i data-lucide="x" class="w-4 h-4"></i>
              </button>
              <button 
                v-else
                @click="handleConnect(platform.id)"
                class="flex-1 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium"
              >
                <i data-lucide="plus" class="w-4 h-4 mr-1"></i> 连接
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div>
      <h2 class="text-xl font-bold mb-4 flex items-center gap-2">
        <i data-lucide="settings" class="w-5 h-5 text-white"></i> 同步设置
      </h2>
      <div class="glass-panel p-6 rounded-2xl">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h3 class="text-lg font-medium mb-1">自动同步</h3>
            <p class="text-sm text-zinc-400">定期自动同步平台数据</p>
          </div>
          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="syncSettings.autoSync" class="sr-only peer">
            <div class="w-11 h-6 bg-zinc-700 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-indigo-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-indigo-600"></div>
          </label>
        </div>
        <div class="flex items-center justify-between mb-4">
          <div>
            <h3 class="text-lg font-medium mb-1">同步周期</h3>
            <p class="text-sm text-zinc-400">选择自动同步的频率</p>
          </div>
          <select v-model="syncSettings.syncInterval" class="bg-zinc-800 border border-zinc-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:ring-2 focus:ring-indigo-500">
            <option value="daily">每日</option>
            <option value="weekly">每周</option>
            <option value="monthly">每月</option>
          </select>
        </div>
        <div class="flex items-center justify-between">
          <div>
            <h3 class="text-lg font-medium mb-1">同步内容</h3>
            <p class="text-sm text-zinc-400">选择要同步的数据类型</p>
          </div>
          <div class="flex gap-2">
            <button class="px-3 py-1 bg-indigo-600 text-white rounded-full text-xs">游戏库</button>
            <button class="px-3 py-1 bg-indigo-600 text-white rounded-full text-xs">成就</button>
            <button class="px-3 py-1 bg-zinc-700 text-white rounded-full text-xs">好友</button>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick } from 'vue'
import { platformApi } from '@/api/platform'
import { createIcons, icons } from 'lucide'

const platforms = ref([
  {
    id: 1,
    name: 'Steam',
    icon: 'https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png',
    isConnected: true,
    username: 'User123',
    gamesCount: 156,
    achievementsCount: 420,
    description: '连接Steam账号以同步游戏库和成就'
  },
  {
    id: 2,
    name: 'Epic Games',
    icon: 'https://upload.wikimedia.org/wikipedia/commons/thumb/3/31/Epic_Games_logo.svg/2048px-Epic_Games_logo.svg.png',
    isConnected: false,
    username: '',
    gamesCount: 0,
    achievementsCount: 0,
    description: '连接Epic Games账号以同步游戏库和成就'
  },
  {
    id: 3,
    name: 'Xbox',
    icon: 'https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/Xbox_one_logo.svg/2048px-Xbox_one_logo.svg.png',
    isConnected: false,
    username: '',
    gamesCount: 0,
    achievementsCount: 0,
    description: '连接Xbox账号以同步游戏库和成就'
  },
  {
    id: 4,
    name: 'PlayStation',
    icon: 'https://upload.wikimedia.org/wikipedia/commons/thumb/6/65/PlayStation_logo.svg/2048px-PlayStation_logo.svg.png',
    isConnected: true,
    username: 'GamerPS5',
    gamesCount: 89,
    achievementsCount: 275,
    description: '连接PlayStation账号以同步游戏库和成就'
  }
])

const syncSettings = ref({
  autoSync: true,
  syncInterval: 'daily'
})

const connectedCount = computed(() => platforms.value.filter(p => p.isConnected).length)

const handleConnect = (platformId) => {
  console.log('Connect platform:', platformId)
}

const handleDisconnect = (platformId) => {
  console.log('Disconnect platform:', platformId)
}

const handleSync = (platformId) => {
  console.log('Sync platform:', platformId)
}

onMounted(() => {
  nextTick(() => createIcons({ icons }))
})
</script>

<style scoped>
.glass-panel {
  background: rgba(255,255,255,0.03);
  border: 1px solid rgba(255,255,255,0.08);
  backdrop-filter: blur(10px);
}
</style>