<template>
  <div class="analytics-container">
    <div class="analytics-header">
      <div class="header-left">
        <h1 class="page-title">数据指挥舱</h1>
        <p class="page-subtitle">
          <span class="pulse-dot"></span>
          数据更新于 {{ lastUpdateTime }}
        </p>
      </div>
      <div class="header-right">
        <div class="period-selector">
          <button 
            v-for="p in periods" 
            :key="p.value"
            :class="['period-btn', { active: selectedPeriod === p.value }]"
            @click="changePeriod(p.value)"
          >
            {{ p.label }}
          </button>
        </div>
        <button class="btn-primary" @click="handleExportReport">
          <Download class="icon" />
          <span>导出报告</span>
        </button>
      </div>
    </div>

    <div v-if="loading" class="loading-wrapper">
      <div class="cyber-spinner">
        <div class="inner"></div>
      </div>
      <p>正在分析神经连接数据...</p>
    </div>

    <div v-else-if="error" class="error-wrapper">
      <AlertTriangle class="icon-lg" />
      <h3>数据同步中断</h3>
      <p>{{ error }}</p>
      <button class="btn-outline" @click="loadData">重新连接</button>
    </div>

    <div v-else class="dashboard-grid fade-in">
      
      <section class="kpi-section">
        <div class="kpi-card glow-purple">
          <div class="kpi-icon">
            <Clock class="icon" />
          </div>
          <div class="kpi-content">
            <span class="kpi-label">总游玩时长</span>
            <div class="kpi-value">
              {{ formatHours(playtimeData.totalMinutes) }}
              <span class="unit">小时</span>
            </div>
            <div class="kpi-trend">
              日均 {{ formatHours(playtimeData.dailyAverage) }} 小时
            </div>
          </div>
          <div class="kpi-bg-chart">
            <svg viewBox="0 0 100 30" class="mini-sparkline">
              <path d="M0,30 Q20,10 40,25 T100,5" fill="none" stroke="rgba(139, 92, 246, 0.3)" stroke-width="2" />
            </svg>
          </div>
        </div>

        <div class="kpi-card glow-blue">
          <div class="kpi-icon">
            <Gamepad2 class="icon" />
          </div>
          <div class="kpi-content">
            <span class="kpi-label">游戏库存</span>
            <div class="kpi-value">
              {{ totalGamesOwned }}
              <span class="unit">款</span>
            </div>
            <div class="kpi-trend">
              跨越 {{ platformData.totalPlatforms || 0 }} 个平台
            </div>
          </div>
        </div>

        <div class="kpi-card glow-amber">
          <div class="kpi-icon">
            <Trophy class="icon" />
          </div>
          <div class="kpi-content">
            <span class="kpi-label">成就解锁</span>
            <div class="kpi-value">
              {{ achievementData.unlockedAchievements || 0 }}
              <span class="unit">个</span>
            </div>
            <div class="kpi-trend positive" v-if="achievementData.recentTrend">
              <Zap class="icon-xs" />
              本周 +{{ achievementData.recentTrend.last7Days }}
            </div>
          </div>
        </div>

        <div class="kpi-card glow-rose">
          <div class="kpi-icon">
            <Target class="icon" />
          </div>
          <div class="kpi-content">
            <span class="kpi-label">完美通关</span>
            <div class="kpi-value">
              {{ achievementData.perfectGames || 0 }}
              <span class="unit">款</span>
            </div>
            <div class="kpi-trend">
              全成就率 {{ Math.round((achievementData.unlockRate || 0) * 100) }}%
            </div>
          </div>
        </div>
      </section>

      <section class="chart-section full-width">
        <div class="panel">
          <div class="panel-header">
            <h3><Activity class="icon" /> 游玩热力图</h3>
            <div class="legend">
              <span>少</span>
              <span class="dot l1"></span>
              <span class="dot l2"></span>
              <span class="dot l3"></span>
              <span class="dot l4"></span>
              <span>多</span>
            </div>
          </div>
          <div class="heatmap-container">
            <div class="heatmap-grid">
              <div 
                v-for="(day, index) in heatmapDays" 
                :key="index"
                class="heatmap-cell"
                :class="getHeatmapLevel(day.minutes)"
                :title="`${day.date}: ${formatMinutes(day.minutes)}`"
              ></div>
            </div>
          </div>
        </div>
      </section>

      <div class="split-section">
        <div class="panel chart-panel">
          <div class="panel-header">
            <h3><TrendingUp class="icon" /> 时长趋势分析</h3>
            <div class="chart-toggles">
              <button 
                :class="{ active: trendView === 'daily' }" 
                @click="trendView = 'daily'"
              >每日</button>
              <button 
                :class="{ active: trendView === 'weekly' }" 
                @click="trendView = 'weekly'"
              >每周</button>
            </div>
          </div>
          <div class="canvas-wrapper">
            <canvas ref="playtimeChartRef"></canvas>
          </div>
        </div>

        <div class="panel list-panel">
          <div class="panel-header">
            <h3><Star class="icon" /> 核心投入游戏</h3>
          </div>
          <div class="games-list-scroll">
            <div 
              v-for="(game, index) in playtimeData.gameBreakdown" 
              :key="game.gameId"
              class="game-row"
            >
              <div class="rank-badge" :class="`rank-${index + 1}`">{{ index + 1 }}</div>
              <div class="game-cover">
                <img :src="game.headerImage || '/placeholder-game.png'" @error="handleImgError" alt="cover">
              </div>
              <div class="game-info-cell">
                <div class="game-title">{{ game.name }}</div>
                <div class="game-progress-bar">
                  <div class="progress-fill" :style="{ width: `${game.percentage}%` }"></div>
                </div>
              </div>
              <div class="game-stat-cell">
                <div class="stat-main">{{ formatHours(game.minutes) }}h</div>
                <div class="stat-sub">{{ game.percentage }}%</div>
              </div>
            </div>
            <div v-if="!playtimeData.gameBreakdown?.length" class="empty-placeholder">
              暂无游玩记录
            </div>
          </div>
        </div>
      </div>

      <div class="tri-section">
        <div class="panel">
          <div class="panel-header">
            <h3><PieChart class="icon" /> 题材偏好</h3>
          </div>
          <div class="canvas-wrapper-square">
            <canvas ref="genreChartRef"></canvas>
          </div>
        </div>

        <div class="panel">
          <div class="panel-header">
            <h3><Monitor class="icon" /> 平台分布</h3>
          </div>
          <div class="platform-list">
            <div v-for="plat in platformData.platformDistribution" :key="plat.platformId" class="platform-row">
              <div class="plat-icon">
                <div class="dot" :class="getPlatformColorClass(plat.platformName)"></div>
              </div>
              <div class="plat-name">{{ plat.platformName }}</div>
              <div class="plat-bar-container">
                <div class="plat-bar" :style="{ width: `${plat.percentage}%` }"></div>
              </div>
              <div class="plat-value">{{ plat.gamesCount }}款</div>
            </div>
          </div>
        </div>

        <div class="panel">
          <div class="panel-header">
            <h3><Award class="icon" /> 成就概览</h3>
          </div>
          <div class="achievement-dashboard">
            <div class="circle-chart-wrapper">
              <svg viewBox="0 0 36 36" class="circular-chart">
                <path class="circle-bg" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                <path class="circle" :stroke-dasharray="`${(achievementData.unlockRate || 0) * 100}, 100`" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
              </svg>
              <div class="circle-content">
                <span class="percentage">{{ Math.round((achievementData.unlockRate || 0) * 100) }}%</span>
                <span class="label">完成率</span>
              </div>
            </div>
            <div class="achieve-stats">
              <div class="stat-box">
                <span class="val">{{ achievementData.totalAchievements }}</span>
                <span class="lbl">总奖杯</span>
              </div>
              <div class="stat-box highlight">
                <span class="val">{{ achievementData.unlockedAchievements }}</span>
                <span class="lbl">已获得</span>
              </div>
            </div>
          </div>
        </div>
      </div>

    </div>

    <Transition name="fade">
      <div v-if="showExportModal" class="modal-backdrop" @click.self="showExportModal = false">
        <div class="modal-card">
          <div class="modal-header">
            <h3>导出数据报表</h3>
            <button class="close-btn" @click="showExportModal = false"><X class="icon" /></button>
          </div>
          <div class="modal-body">
            <div class="template-selector">
              <div 
                v-for="t in reportTemplates" 
                :key="t.templateId"
                class="template-option"
                :class="{ selected: selectedTemplate?.templateId === t.templateId }"
                @click="selectedTemplate = t"
              >
                <div class="radio-circle"></div>
                <div class="t-info">
                  <span class="t-name">{{ t.templateName }}</span>
                  <span class="t-desc">{{ t.description }}</span>
                </div>
              </div>
            </div>
            <div class="format-selector" v-if="selectedTemplate">
              <label>导出格式</label>
              <div class="formats">
                <button 
                  v-for="fmt in selectedTemplate.supportedFormats" 
                  :key="fmt"
                  :class="{ active: exportFormat === fmt }"
                  @click="exportFormat = fmt"
                >
                  {{ fmt.toUpperCase() }}
                </button>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-text" @click="showExportModal = false">取消</button>
            <button class="btn-primary" :disabled="exporting || !selectedTemplate" @click="confirmExport">
              {{ exporting ? '生成中...' : '立即生成' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'
import { 
  Clock, Gamepad2, Trophy, Zap, Activity, Target,
  Download, AlertTriangle, TrendingUp, PieChart,
  Monitor, Award, Star, X
} from 'lucide-vue-next'
import Chart from 'chart.js/auto'
// [重要] 使用命名导入，解决之前的 SyntaxError
import { 
  getPlaytimeAnalytics,
  getGenrePreferences,
  getPlatformAnalytics,
  getAchievementStats,
  getReportTemplates,
  generateReport
} from '@/api/analytics'

// --- State ---
const loading = ref(true)
const error = ref(null)
const selectedPeriod = ref('month')
const trendView = ref('daily')
const lastUpdateTime = ref(new Date().toLocaleTimeString())

// Data Containers
const playtimeData = ref({ totalMinutes: 0, dailyAverage: 0, distribution: [], gameBreakdown: [] })
const genreData = ref([])
const platformData = ref({ platformDistribution: [], totalPlatforms: 0 })
const achievementData = ref({ totalAchievements: 0, unlockedAchievements: 0, unlockRate: 0, perfectGames: 0 })
const reportTemplates = ref([])

// Charts Refs
const playtimeChartRef = ref(null)
const genreChartRef = ref(null)
let charts = { playtime: null, genre: null }

// Export Modal
const showExportModal = ref(false)
const selectedTemplate = ref(null)
const exportFormat = ref('pdf')
const exporting = ref(false)

const periods = [
  { label: '本周', value: 'week' },
  { label: '本月', value: 'month' },
  { label: '今年', value: 'year' }
]

// --- Computed ---
const totalGamesOwned = computed(() => {
  if (platformData.value.platformDistribution?.length) {
    return platformData.value.platformDistribution.reduce((sum, p) => sum + p.gamesCount, 0)
  }
  return 0
})

const heatmapDays = computed(() => {
  // 生成热力图数据（填充最近90天）
  // 真实数据
  const dist = playtimeData.value.distribution || []
  // 如果后端数据不足90天，前端补齐空天数以保持网格美观（此处简化为直接使用）
  return dist.slice(-90) 
})

// --- Methods ---
const formatHours = (minutes) => {
  if (!minutes) return '0.0'
  return (minutes / 60).toFixed(1)
}

const formatMinutes = (minutes) => {
  if (!minutes) return '0m'
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}

const handleImgError = (e) => {
  e.target.src = '/placeholder-game.png' // 请确保 public 目录下有默认图片
}

const getHeatmapLevel = (minutes) => {
  if (!minutes || minutes <= 0) return 'l0'
  if (minutes < 30) return 'l1'
  if (minutes < 60) return 'l2'
  if (minutes < 120) return 'l3'
  return 'l4'
}

const getPlatformColorClass = (name) => {
  const n = (name || '').toLowerCase()
  if (n.includes('steam')) return 'steam'
  if (n.includes('xbox')) return 'xbox'
  if (n.includes('playstation') || n.includes('psn')) return 'psn'
  if (n.includes('epic')) return 'epic'
  return 'other'
}

const changePeriod = (p) => {
  selectedPeriod.value = p
  loadData()
}

// --- API Loading ---
const loadData = async () => {
  loading.value = true
  error.value = null
  
  try {
    const params = { period: selectedPeriod.value }
    
    // 并行调用所有接口
    const [playtimeRes, genreRes, platformRes, achievementRes, templatesRes] = await Promise.all([
      getPlaytimeAnalytics(params).catch(() => ({ data: null })),
      getGenrePreferences().catch(() => ({ data: null })),
      getPlatformAnalytics().catch(() => ({ data: null })),
      getAchievementStats().catch(() => ({ data: null })),
      getReportTemplates().catch(() => ({ data: null }))
    ])

    if (playtimeRes.data) playtimeData.value = playtimeRes.data
    if (genreRes.data?.genrePreferences) genreData.value = genreRes.data.genrePreferences
    if (platformRes.data) platformData.value = platformRes.data
    if (achievementRes.data) achievementData.value = achievementRes.data
    if (templatesRes.data?.templates) {
      reportTemplates.value = templatesRes.data.templates
      if (reportTemplates.value.length) selectedTemplate.value = reportTemplates.value[0]
    }

    lastUpdateTime.value = new Date().toLocaleTimeString()
    
    // 等待 DOM 更新后渲染图表
    setTimeout(() => {
      initPlaytimeChart()
      initGenreChart()
    }, 100)

  } catch (err) {
    console.error(err)
    error.value = '无法连接到数据核心'
  } finally {
    loading.value = false
  }
}

// --- Charts Initialization ---
const initPlaytimeChart = () => {
  if (!playtimeChartRef.value) return
  if (charts.playtime) charts.playtime.destroy()

  const ctx = playtimeChartRef.value.getContext('2d')
  
  // 创建紫色渐变背景
  const gradient = ctx.createLinearGradient(0, 0, 0, 400)
  gradient.addColorStop(0, 'rgba(139, 92, 246, 0.5)') // Purple
  gradient.addColorStop(1, 'rgba(139, 92, 246, 0.0)')

  const rawData = playtimeData.value.distribution || []
  let labels = [], data = []

  if (trendView.value === 'weekly') {
    // 简单的按周聚合
    for (let i = 0; i < rawData.length; i += 7) {
      const chunk = rawData.slice(i, i + 7)
      const sum = chunk.reduce((acc, c) => acc + c.minutes, 0)
      if (chunk.length) {
        labels.push(new Date(chunk[0].date).toLocaleDateString('zh-CN', {month:'numeric', day:'numeric'}))
        data.push((sum / 60).toFixed(1))
      }
    }
  } else {
    // 每日
    labels = rawData.map(d => new Date(d.date).toLocaleDateString('zh-CN', {month:'numeric', day:'numeric'}))
    data = rawData.map(d => (d.minutes / 60).toFixed(1))
  }

  charts.playtime = new Chart(ctx, {
    type: 'line',
    data: {
      labels,
      datasets: [{
        label: '游戏时长 (h)',
        data,
        borderColor: '#8b5cf6',
        backgroundColor: gradient,
        borderWidth: 2,
        tension: 0.4,
        fill: true,
        pointBackgroundColor: '#1f2937',
        pointBorderColor: '#8b5cf6',
        pointRadius: 3,
        pointHoverRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          mode: 'index',
          intersect: false,
          backgroundColor: 'rgba(17, 24, 39, 0.9)',
          titleColor: '#fff',
          bodyColor: '#cbd5e1',
          borderColor: 'rgba(255,255,255,0.1)',
          borderWidth: 1
        }
      },
      scales: {
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(255,255,255,0.05)' },
          ticks: { color: '#64748b' }
        },
        x: {
          grid: { display: false },
          ticks: { color: '#64748b' }
        }
      }
    }
  })
}

const initGenreChart = () => {
  if (!genreChartRef.value || !genreData.value.length) return
  if (charts.genre) charts.genre.destroy()

  // 仅取前6个题材
  const topGenres = genreData.value.slice(0, 6)
  const labels = topGenres.map(g => g.genreName)
  const data = topGenres.map(g => g.preferenceScore * 100)

  charts.genre = new Chart(genreChartRef.value, {
    type: 'radar',
    data: {
      labels,
      datasets: [{
        label: '偏好指数',
        data,
        backgroundColor: 'rgba(59, 130, 246, 0.2)',
        borderColor: '#3b82f6',
        pointBackgroundColor: '#3b82f6',
        pointBorderColor: '#fff',
        pointHoverBackgroundColor: '#fff',
        pointHoverBorderColor: '#3b82f6'
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        r: {
          angleLines: { color: 'rgba(255,255,255,0.1)' },
          grid: { color: 'rgba(255,255,255,0.1)' },
          pointLabels: { color: '#94a3b8', font: { size: 12 } },
          ticks: { display: false, backdropColor: 'transparent' }
        }
      },
      plugins: { legend: { display: false } }
    }
  })
}

// --- Export Logic ---
const handleExportReport = () => {
  showExportModal.value = true
}

const confirmExport = async () => {
  if (!selectedTemplate.value) return
  exporting.value = true
  try {
    const now = new Date()
    // 调用生成报表 API
    await generateReport({
      templateId: selectedTemplate.value.templateId,
      format: exportFormat.value,
      reportType: selectedTemplate.value.category,
      parameters: {
        startDate: new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0],
        endDate: now.toISOString().split('T')[0]
      }
    })
    showExportModal.value = false
    alert('报表生成任务已提交，请留意通知中心')
  } catch(e) {
    alert('生成失败: ' + e.message)
  } finally {
    exporting.value = false
  }
}

// Watchers & Lifecycle
watch(trendView, initPlaytimeChart)
onMounted(loadData)
onUnmounted(() => {
  Object.values(charts).forEach(c => c?.destroy())
})
</script>

<style scoped>
/* --- 全局容器与重置 --- */
.analytics-container {
  padding: 32px;
  max-width: 1600px;
  margin: 0 auto;
  color: #f8fafc;
  background-color: #0f172a; /* 深色背景 */
  min-height: 100vh;
  font-family: 'Inter', sans-serif;
}

/* --- 头部 --- */
.analytics-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 40px;
  border-bottom: 1px solid rgba(255,255,255,0.05);
  padding-bottom: 20px;
}

.page-title {
  font-size: 36px;
  font-weight: 800;
  background: linear-gradient(135deg, #fff 0%, #94a3b8 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  margin-bottom: 8px;
}

.page-subtitle {
  color: #64748b;
  font-size: 14px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.pulse-dot {
  width: 8px;
  height: 8px;
  background-color: #10b981;
  border-radius: 50%;
  box-shadow: 0 0 8px #10b981;
  animation: pulse 2s infinite;
}

@keyframes pulse { 0% { opacity: 1; } 50% { opacity: 0.5; } 100% { opacity: 1; } }

.header-right {
  display: flex;
  gap: 16px;
  align-items: center;
}

.period-selector {
  background: rgba(255,255,255,0.05);
  padding: 4px;
  border-radius: 12px;
  display: flex;
}

.period-btn {
  background: transparent;
  border: none;
  color: #94a3b8;
  padding: 8px 16px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.3s;
}

.period-btn.active {
  background: #1e293b;
  color: #fff;
  box-shadow: 0 2px 4px rgba(0,0,0,0.2);
}

.btn-primary {
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  border: none;
  color: white;
  padding: 10px 20px;
  border-radius: 12px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: transform 0.2s;
}
.btn-primary:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(139, 92, 246, 0.4); }

/* --- 核心指标 (KPI) --- */
.kpi-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 24px;
  margin-bottom: 32px;
}

.kpi-card {
  background: rgba(30, 41, 59, 0.7);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255,255,255,0.05);
  border-radius: 20px;
  padding: 24px;
  position: relative;
  overflow: hidden;
  transition: transform 0.3s;
}
.kpi-card:hover { transform: translateY(-4px); border-color: rgba(255,255,255,0.1); }

/* 光晕效果 */
.glow-purple::before { content:''; position:absolute; top:-50%; left:-50%; width:200%; height:200%; background: radial-gradient(circle, rgba(139,92,246,0.1) 0%, transparent 70%); pointer-events: none; }
.glow-blue::before { content:''; position:absolute; top:-50%; left:-50%; width:200%; height:200%; background: radial-gradient(circle, rgba(59,130,246,0.1) 0%, transparent 70%); pointer-events: none; }
.glow-amber::before { content:''; position:absolute; top:-50%; left:-50%; width:200%; height:200%; background: radial-gradient(circle, rgba(245,158,11,0.1) 0%, transparent 70%); pointer-events: none; }
.glow-rose::before { content:''; position:absolute; top:-50%; left:-50%; width:200%; height:200%; background: radial-gradient(circle, rgba(244,63,94,0.1) 0%, transparent 70%); pointer-events: none; }

.kpi-icon {
  width: 48px; height: 48px; border-radius: 12px;
  background: rgba(255,255,255,0.05);
  display: flex; align-items: center; justify-content: center;
  margin-bottom: 16px;
  color: #e2e8f0;
}

.kpi-label { font-size: 14px; color: #94a3b8; display: block; margin-bottom: 4px; }
.kpi-value { font-size: 32px; font-weight: 700; color: #f1f5f9; letter-spacing: -1px; }
.kpi-value .unit { font-size: 14px; color: #64748b; font-weight: 400; margin-left: 4px; }
.kpi-trend { font-size: 12px; color: #64748b; margin-top: 8px; display: flex; align-items: center; gap: 4px; }
.kpi-trend.positive { color: #10b981; }

.kpi-bg-chart {
  position: absolute; bottom: 0; right: 0; width: 120px; height: 40px; opacity: 0.5;
}

/* --- 热力图 --- */
.chart-section { margin-bottom: 32px; }
.panel {
  background: rgba(30, 41, 59, 0.7);
  border: 1px solid rgba(255,255,255,0.05);
  border-radius: 20px;
  padding: 24px;
  display: flex; flex-direction: column;
}

.panel-header {
  display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px;
}
.panel-header h3 { font-size: 18px; font-weight: 600; display: flex; align-items: center; gap: 8px; margin: 0; color: #e2e8f0; }

.heatmap-container { overflow-x: auto; padding-bottom: 8px; }
.heatmap-grid { display: grid; grid-template-columns: repeat(90, 1fr); gap: 4px; height: 20px; min-width: 800px; }
.heatmap-cell {
  background: #1e293b; border-radius: 2px; height: 100%;
  transition: all 0.2s;
}
.heatmap-cell:hover { transform: scale(1.2); border: 1px solid #fff; }
.heatmap-cell.l0 { background: #1e293b; }
.heatmap-cell.l1 { background: #064e3b; }
.heatmap-cell.l2 { background: #059669; }
.heatmap-cell.l3 { background: #10b981; }
.heatmap-cell.l4 { background: #34d399; }

.legend { display: flex; align-items: center; gap: 6px; font-size: 12px; color: #64748b; }
.dot { width: 10px; height: 10px; border-radius: 2px; }
.dot.l1 { background: #064e3b; } .dot.l2 { background: #059669; }
.dot.l3 { background: #10b981; } .dot.l4 { background: #34d399; }

/* --- 分栏布局 --- */
.split-section { display: grid; grid-template-columns: 2fr 1fr; gap: 24px; margin-bottom: 32px; }
.chart-panel { min-height: 400px; }
.canvas-wrapper { flex: 1; position: relative; min-height: 300px; }

/* --- 游戏列表 --- */
.games-list-scroll { max-height: 350px; overflow-y: auto; padding-right: 8px; }
/* 自定义滚动条 */
.games-list-scroll::-webkit-scrollbar { width: 6px; }
.games-list-scroll::-webkit-scrollbar-track { background: #1e293b; border-radius: 3px; }
.games-list-scroll::-webkit-scrollbar-thumb { background: #475569; border-radius: 3px; }

.game-row {
  display: flex; align-items: center; gap: 16px;
  padding: 12px; border-radius: 12px;
  transition: background 0.2s;
  border-bottom: 1px solid rgba(255,255,255,0.02);
}
.game-row:hover { background: rgba(255,255,255,0.05); }

.rank-badge {
  width: 24px; height: 24px; border-radius: 6px;
  display: flex; align-items: center; justify-content: center;
  font-weight: 700; font-size: 12px; color: #94a3b8;
  background: rgba(255,255,255,0.05);
}
.rank-1 { background: #fbbf24; color: #000; }
.rank-2 { background: #9ca3af; color: #000; }
.rank-3 { background: #d97706; color: #fff; }

.game-cover {
  width: 48px; height: 64px; border-radius: 8px; overflow: hidden; flex-shrink: 0;
}
.game-cover img { width: 100%; height: 100%; object-fit: cover; }

.game-info-cell { flex: 1; min-width: 0; }
.game-title { font-weight: 600; color: #f1f5f9; margin-bottom: 6px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.game-progress-bar { height: 6px; background: #334155; border-radius: 3px; overflow: hidden; }
.progress-fill { height: 100%; background: #8b5cf6; border-radius: 3px; }

.game-stat-cell { text-align: right; min-width: 60px; }
.stat-main { font-weight: 700; color: #f1f5f9; }
.stat-sub { font-size: 12px; color: #64748b; }

/* --- 底部三栏 --- */
.tri-section { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }
.canvas-wrapper-square { height: 200px; position: relative; }

/* 平台列表 */
.platform-list { display: flex; flex-direction: column; gap: 12px; }
.platform-row { display: flex; align-items: center; gap: 10px; font-size: 14px; }
.plat-icon .dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.steam { background: #1b2838; box-shadow: 0 0 4px #1b2838; border:1px solid #fff; }
.dot.xbox { background: #107c10; }
.dot.psn { background: #003791; }
.dot.other { background: #64748b; }

.plat-name { width: 80px; color: #cbd5e1; }
.plat-bar-container { flex: 1; height: 6px; background: #334155; border-radius: 3px; }
.plat-bar { height: 100%; background: #3b82f6; border-radius: 3px; }
.plat-value { width: 40px; text-align: right; color: #94a3b8; }

/* 成就仪表盘 */
.achievement-dashboard { display: flex; align-items: center; gap: 24px; justify-content: center; height: 100%; }
.circle-chart-wrapper { width: 100px; height: 100px; position: relative; }
.circular-chart { display: block; margin: 0 auto; max-width: 100%; max-height: 100%; }
.circle-bg { fill: none; stroke: #334155; stroke-width: 2.5; }
.circle { fill: none; stroke-width: 2.5; stroke-linecap: round; stroke: #f43f5e; transition: stroke-dasharray 1s ease; }
.circle-content { position: absolute; top:50%; left:50%; transform:translate(-50%, -50%); text-align: center; }
.circle-content .percentage { font-size: 20px; font-weight: 800; display: block; }
.circle-content .label { font-size: 10px; color: #64748b; }

.achieve-stats { display: flex; flex-direction: column; gap: 12px; }
.stat-box { background: rgba(255,255,255,0.05); padding: 8px 16px; border-radius: 8px; text-align: center; }
.stat-box .val { font-size: 18px; font-weight: 700; display: block; color: #cbd5e1; }
.stat-box .lbl { font-size: 10px; color: #64748b; }
.stat-box.highlight .val { color: #f43f5e; }

/* --- 弹窗 --- */
.modal-backdrop {
  position: fixed; inset: 0; background: rgba(0,0,0,0.8); backdrop-filter: blur(4px);
  display: flex; align-items: center; justify-content: center; z-index: 999;
}
.modal-card {
  background: #1e293b; width: 500px; border-radius: 20px; padding: 24px;
  border: 1px solid rgba(255,255,255,0.1); box-shadow: 0 20px 50px rgba(0,0,0,0.5);
}
.modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.modal-header h3 { font-size: 20px; margin: 0; }
.close-btn { background: transparent; border: none; color: #64748b; cursor: pointer; }

.template-selector { display: flex; flex-direction: column; gap: 12px; margin-bottom: 24px; max-height: 300px; overflow-y: auto; }
.template-option {
  display: flex; align-items: center; gap: 16px; padding: 16px;
  background: rgba(255,255,255,0.03); border-radius: 12px; cursor: pointer; border: 2px solid transparent;
}
.template-option:hover { background: rgba(255,255,255,0.06); }
.template-option.selected { border-color: #6366f1; background: rgba(99, 102, 241, 0.1); }
.radio-circle { width: 16px; height: 16px; border-radius: 50%; border: 2px solid #64748b; }
.template-option.selected .radio-circle { border-color: #6366f1; background: #6366f1; }

.formats { display: flex; gap: 8px; }
.formats button {
  background: #334155; border: none; color: #cbd5e1; padding: 8px 16px; border-radius: 6px; cursor: pointer;
}
.formats button.active { background: #6366f1; color: white; }

.modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
.btn-text { background: transparent; border: none; color: #94a3b8; cursor: pointer; }

/* 动画 */
.fade-in { animation: fadeIn 0.5s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }

/* 加载动画 */
.loading-wrapper { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 400px; color: #64748b; }
.cyber-spinner {
  width: 40px; height: 40px; border: 2px solid #1e293b; border-top-color: #8b5cf6; border-radius: 50%; animation: spin 1s linear infinite; margin-bottom: 16px;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* 响应式 */
@media (max-width: 1200px) {
  .split-section { grid-template-columns: 1fr; }
  .tri-section { grid-template-columns: 1fr; }
}
</style>