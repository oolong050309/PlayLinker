<template>
  <div class="analytics-container">
    <!-- Header -->
    <div class="analytics-header">
      <div class="header-left">
        <h1 class="page-title">数据分析</h1>
      </div>
      <div class="header-right">
        <select v-model="selectedPeriod" class="period-select" @change="loadData">
          <option value="week">最近7天</option>
          <option value="month">最近30天</option>
          <option value="quarter">最近3个月</option>
          <option value="year">今年</option>
        </select>
        <button class="btn-export" @click="handleExportReport">
          <Download class="icon" />
          导出报表
        </button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="loading-container">
      <div class="loading-spinner"></div>
      <p>加载数据中...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="error-container">
      <AlertCircle class="error-icon" />
      <p>{{ error }}</p>
      <button class="btn-retry" @click="loadData">重试</button>
    </div>

    <!-- Content -->
    <template v-else>
      <!-- Stats Overview -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">总游戏时长</span>
            <div class="stat-icon indigo">
              <Clock class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ formatMinutes(playtimeData.totalMinutes) }}</div>
          <div class="stat-desc">日均 {{ formatMinutes(playtimeData.dailyAverage) }}</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">游戏数量</span>
            <div class="stat-icon emerald">
              <Gamepad2 class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ genreData.length > 0 ? genreData.reduce((sum, g) => sum + g.gamesOwned, 0) : 0 }}</div>
          <div class="stat-desc">已游玩 {{ genreData.length > 0 ? genreData.reduce((sum, g) => sum + g.gamesPlayed, 0) : 0 }} 款</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">成就数量</span>
            <div class="stat-icon amber">
              <Trophy class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ achievementData.unlockedAchievements || 0 }}</div>
          <div class="stat-change positive" v-if="achievementData.recentTrend">
            <Zap class="icon" />
            本周解锁 {{ achievementData.recentTrend.last7Days }} 个
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">成就完成率</span>
            <div class="stat-icon rose">
              <Activity class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ Math.round((achievementData.unlockRate || 0) * 100) }}%</div>
          <div class="stat-desc">完美通关 {{ achievementData.perfectGames || 0 }} 款</div>
        </div>
      </div>

      <!-- Main Content Grid -->
      <div class="content-grid">
        <!-- Left Column: Charts -->
        <div class="main-column">
          <!-- Playtime Trend Chart -->
          <div class="chart-card">
            <div class="chart-header">
              <div>
                <h2 class="chart-title">游戏时长趋势</h2>
                <p class="chart-desc">{{ getPeriodDescription() }}</p>
              </div>
              <div class="chart-tabs">
                <button 
                  :class="['tab-btn', { active: trendView === 'daily' }]"
                  @click="trendView = 'daily'"
                >每日</button>
                <button 
                  :class="['tab-btn', { active: trendView === 'weekly' }]"
                  @click="trendView = 'weekly'"
                >每周</button>
              </div>
            </div>
            <div class="chart-container">
              <canvas ref="playtimeChartRef"></canvas>
            </div>
          </div>

          <!-- Most Played Games -->
          <div class="chart-card">
            <h2 class="chart-title">最常玩的游戏</h2>
            <div v-if="playtimeData.gameBreakdown && playtimeData.gameBreakdown.length > 0" class="games-list">
              <div 
                v-for="(game, index) in playtimeData.gameBreakdown.slice(0, 5)" 
                :key="game.gameId"
                class="game-item"
              >
                <div class="game-rank" :class="getRankClass(index)">{{ index + 1 }}</div>
                <div class="game-info">
                  <h3 class="game-name">{{ game.name }}</h3>
                  <p class="game-sessions">{{ game.sessions }} 次游戏</p>
                </div>
                <div class="game-playtime">
                  <div class="playtime-value">{{ formatMinutes(game.minutes) }}</div>
                  <div class="playtime-percent">{{ game.percentage.toFixed(1) }}%</div>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">
              <p>暂无游戏数据</p>
            </div>
          </div>
        </div>

        <!-- Right Column: Side Stats -->
        <div class="side-column">
          <!-- Genre Preferences -->
          <div class="chart-card">
            <h3 class="chart-title">游戏类型偏好</h3>
            <div v-if="genreData.length > 0" class="genre-bars">
              <div v-for="genre in genreData.slice(0, 5)" :key="genre.genreId" class="genre-item">
                <div class="genre-header">
                  <span class="genre-name">{{ genre.genreName }}</span>
                  <span class="genre-percent">{{ Math.round(genre.preferenceScore * 100) }}%</span>
                </div>
                <div class="genre-bar-bg">
                  <div 
                    class="genre-bar-fill" 
                    :style="{ width: (genre.preferenceScore * 100) + '%' }"
                  ></div>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">
              <p>暂无类型数据</p>
            </div>
          </div>

          <!-- Achievement Progress -->
          <div class="chart-card">
            <h3 class="chart-title">成就进度</h3>
            <div class="achievement-ring">
              <svg class="progress-ring" viewBox="0 0 128 128">
                <circle 
                  class="ring-bg" 
                  cx="64" cy="64" r="56" 
                  stroke-width="8" 
                  fill="none"
                />
                <circle 
                  class="ring-progress" 
                  cx="64" cy="64" r="56" 
                  stroke-width="8" 
                  fill="none"
                  :stroke-dasharray="351.86"
                  :stroke-dashoffset="351.86 * (1 - (achievementData.unlockRate || 0))"
                />
              </svg>
              <div class="ring-center">
                <span class="ring-value">{{ Math.round((achievementData.unlockRate || 0) * 100) }}%</span>
                <span class="ring-label">完成</span>
              </div>
            </div>
            <div class="achievement-stats">
              <p>已解锁 {{ achievementData.unlockedAchievements || 0 }} / {{ achievementData.totalAchievements || 0 }} 个成就</p>
              <p class="remaining">剩余 {{ (achievementData.totalAchievements || 0) - (achievementData.unlockedAchievements || 0) }} 个</p>
            </div>
          </div>

          <!-- Time Slot Distribution -->
          <div class="chart-card">
            <h3 class="chart-title">游戏时段分布</h3>
            <div v-if="playtimeData.timeSlotDistribution" class="time-slots">
              <div v-for="slot in playtimeData.timeSlotDistribution" :key="slot.slot" class="time-slot-item">
                <span class="slot-name">{{ slot.slot }}</span>
                <div class="slot-bar-bg">
                  <div 
                    class="slot-bar-fill" 
                    :style="{ width: getSlotPercent(slot.minutes) + '%' }"
                  ></div>
                </div>
                <span class="slot-time">{{ formatMinutes(slot.minutes) }}</span>
              </div>
            </div>
            <div v-else class="empty-state">
              <p>暂无时段数据</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Report Templates -->
      <div class="reports-section">
        <h2 class="section-title">报表模板</h2>
        <div v-if="reportTemplates.length > 0" class="templates-grid">
          <div 
            v-for="template in reportTemplates" 
            :key="template.templateId"
            class="template-card"
            @click="openGenerateDialog(template)"
          >
            <div class="template-icon" :class="getTemplateIconClass(template.category)">
              <component :is="getTemplateIcon(template.category)" class="icon" />
            </div>
            <h3 class="template-name">{{ template.templateName }}</h3>
            <p class="template-desc">{{ template.description }}</p>
            <div class="template-footer">
              <span class="template-formats">{{ template.supportedFormats.join(', ').toUpperCase() }}</span>
              <button class="btn-generate">生成</button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <p>暂无报表模板</p>
        </div>
      </div>

      <!-- Recent Reports -->
      <div class="recent-reports">
        <div class="section-header">
          <h2 class="section-title">最近报表</h2>
          <button class="btn-refresh" @click="loadReportHistory">
            <RefreshCw class="icon" />
            刷新
          </button>
        </div>
        <div v-if="recentReports.length > 0" class="reports-list">
          <div 
            v-for="report in recentReports" 
            :key="report.reportId"
            class="report-item"
          >
            <div class="report-icon" :class="getStatusClass(report.status)">
              <FileText class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-name">{{ report.templateName }}</h3>
              <div class="report-meta">
                <span>生成于 {{ formatDate(report.generatedAt) }}</span>
                <span>•</span>
                <span>{{ report.fileSizeMB ? report.fileSizeMB.toFixed(1) + ' MB' : '-' }}</span>
                <span>•</span>
                <span :class="['report-status', report.status]">{{ getStatusText(report.status) }}</span>
              </div>
            </div>
            <div class="report-actions">
              <button 
                v-if="report.status === 'completed'" 
                class="btn-action" 
                @click="handleDownloadReport(report.reportId)"
              >
                <Download class="icon" />
                下载
              </button>
              <button class="btn-action danger" @click="handleDeleteReport(report.reportId)">
                <Trash2 class="icon" />
                删除
              </button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <p>暂无报表记录</p>
        </div>
      </div>
    </template>

    <!-- Generate Report Dialog -->
    <div v-if="showGenerateDialog" class="dialog-overlay" @click.self="showGenerateDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">生成报表</h3>
        <p class="dialog-desc">{{ selectedTemplate?.templateName }}</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>输出格式</label>
            <select v-model="generateForm.format" class="form-select">
              <option v-for="fmt in selectedTemplate?.supportedFormats" :key="fmt" :value="fmt">
                {{ fmt.toUpperCase() }}
              </option>
            </select>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showGenerateDialog = false">取消</button>
          <button class="btn-confirm" @click="handleGenerateReport" :disabled="generating">
            {{ generating ? '生成中...' : '生成报表' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { 
  Clock, Gamepad2, Trophy, Zap, Activity, 
  Download, FileText, RefreshCw, Trash2, AlertCircle,
  Calendar, TrendingUp, DollarSign, Users
} from 'lucide-vue-next'
import Chart from 'chart.js/auto'
import {
  getPlaytimeAnalytics,
  getGenrePreferences,
  getAchievementStats,
  getReportTemplates,
  getReportHistory,
  generateReport,
  downloadReport,
  deleteReport
} from '@/api/analytics'

// Refs
const playtimeChartRef = ref(null)
let playtimeChart = null

const selectedPeriod = ref('month')
const trendView = ref('daily')
const loading = ref(true)
const error = ref(null)

// Data
const playtimeData = ref({
  totalMinutes: 0,
  dailyAverage: 0,
  distribution: [],
  gameBreakdown: [],
  timeSlotDistribution: [],
  weekdayDistribution: []
})

const genreData = ref([])
const achievementData = ref({
  totalAchievements: 0,
  unlockedAchievements: 0,
  unlockRate: 0,
  perfectGames: 0,
  recentTrend: null
})

const reportTemplates = ref([])
const recentReports = ref([])

// Dialog
const showGenerateDialog = ref(false)
const selectedTemplate = ref(null)
const generateForm = ref({ format: 'pdf' })
const generating = ref(false)

// Methods
const formatMinutes = (minutes) => {
  if (!minutes) return '0h'
  const hours = Math.floor(minutes / 60)
  if (hours >= 1000) {
    return (hours / 1000).toFixed(1) + 'k h'
  }
  return hours + 'h ' + (minutes % 60) + 'm'
}

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

const getRankClass = (index) => {
  if (index === 0) return 'gold'
  if (index === 1) return 'silver'
  if (index === 2) return 'bronze'
  return ''
}

const getStatusText = (status) => {
  const statusMap = {
    completed: '已完成',
    generating: '生成中',
    failed: '失败',
    expired: '已过期'
  }
  return statusMap[status] || status
}

const getStatusClass = (status) => {
  const classMap = {
    completed: 'indigo',
    generating: 'blue',
    failed: 'red',
    expired: 'gray'
  }
  return classMap[status] || 'gray'
}

const getPeriodDescription = () => {
  const map = {
    week: '过去7天的每日游戏时长',
    month: '过去30天的每日游戏时长',
    quarter: '过去3个月的游戏时长',
    year: '今年的游戏时长'
  }
  return map[selectedPeriod.value]
}

const getSlotPercent = (minutes) => {
  if (!playtimeData.value.timeSlotDistribution) return 0
  const max = Math.max(...playtimeData.value.timeSlotDistribution.map(s => s.minutes))
  return max > 0 ? (minutes / max) * 100 : 0
}

const getTemplateIcon = (category) => {
  const iconMap = {
    gaming: Calendar,
    achievement: Trophy,
    spending: DollarSign,
    parental: Users
  }
  return iconMap[category] || TrendingUp
}

const getTemplateIconClass = (category) => {
  const classMap = {
    gaming: 'indigo',
    achievement: 'amber',
    spending: 'purple',
    parental: 'rose'
  }
  return classMap[category] || 'emerald'
}

// API calls
const loadData = async () => {
  loading.value = true
  error.value = null
  
  try {
    // 计算时间参数
    const now = new Date()
    const year = now.getFullYear()
    const month = now.getMonth() + 1
    
    // 并行加载数据
    const [playtimeRes, genreRes, achievementRes, templatesRes, reportsRes] = await Promise.all([
      getPlaytimeAnalytics({ year, month }).catch(() => ({ data: null })),
      getGenrePreferences().catch(() => ({ data: null })),
      getAchievementStats().catch(() => ({ data: null })),
      getReportTemplates().catch(() => ({ data: null })),
      getReportHistory({ page: 1, page_size: 10 }).catch(() => ({ data: null }))
    ])

    // 处理游玩时间数据
    if (playtimeRes.data) {
      playtimeData.value = playtimeRes.data
    }

    // 处理类型偏好数据
    if (genreRes.data?.genrePreferences) {
      genreData.value = genreRes.data.genrePreferences
    }

    // 处理成就数据
    if (achievementRes.data) {
      achievementData.value = achievementRes.data
    }

    // 处理报表模板
    if (templatesRes.data?.templates) {
      reportTemplates.value = templatesRes.data.templates
    }

    // 处理报表历史
    if (reportsRes.data?.items) {
      recentReports.value = reportsRes.data.items
    }

    // 初始化图表
    setTimeout(() => initCharts(), 100)
  } catch (err) {
    console.error('加载数据失败:', err)
    error.value = '加载数据失败，请稍后重试'
  } finally {
    loading.value = false
  }
}

const loadReportHistory = async () => {
  try {
    const res = await getReportHistory({ page: 1, page_size: 10 })
    if (res.data?.items) {
      recentReports.value = res.data.items
    }
  } catch (err) {
    console.error('加载报表历史失败:', err)
  }
}

const openGenerateDialog = (template) => {
  selectedTemplate.value = template
  generateForm.value.format = template.supportedFormats[0] || 'pdf'
  showGenerateDialog.value = true
}

const handleGenerateReport = async () => {
  if (!selectedTemplate.value) return
  
  generating.value = true
  try {
    const now = new Date()
    await generateReport({
      templateId: selectedTemplate.value.templateId,
      reportType: selectedTemplate.value.category,
      parameters: {
        startDate: new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0],
        endDate: now.toISOString().split('T')[0]
      },
      format: generateForm.value.format
    })
    
    showGenerateDialog.value = false
    alert('报表生成任务已创建，请稍后刷新查看')
    loadReportHistory()
  } catch (err) {
    console.error('生成报表失败:', err)
    alert('生成报表失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value = false
  }
}

const handleDownloadReport = async (reportId) => {
  try {
    const response = await downloadReport(reportId)
    const url = URL.createObjectURL(new Blob([response]))
    const link = document.createElement('a')
    link.href = url
    link.download = `report_${reportId}.pdf`
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    console.error('下载报表失败:', err)
    alert('下载报表失败')
  }
}

const handleDeleteReport = async (reportId) => {
  if (!confirm('确定要删除这个报表吗？')) return
  
  try {
    await deleteReport(reportId)
    recentReports.value = recentReports.value.filter(r => r.reportId !== reportId)
  } catch (err) {
    console.error('删除报表失败:', err)
    alert('删除报表失败')
  }
}

const handleExportReport = () => {
  if (reportTemplates.value.length > 0) {
    openGenerateDialog(reportTemplates.value[0])
  } else {
    alert('暂无可用的报表模板')
  }
}

// Initialize charts
const initCharts = () => {
  if (playtimeChartRef.value && playtimeData.value.distribution?.length > 0) {
    const ctx = playtimeChartRef.value.getContext('2d')
    
    if (playtimeChart) {
      playtimeChart.destroy()
    }

    const labels = playtimeData.value.distribution.map(d => {
      const date = new Date(d.date)
      return `${date.getMonth() + 1}/${date.getDate()}`
    })
    const data = playtimeData.value.distribution.map(d => Math.round(d.minutes / 60 * 10) / 10)

    playtimeChart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: '游戏时长 (小时)',
          data,
          backgroundColor: '#6366f1',
          borderRadius: 4,
          barThickness: 20
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false }
        },
        scales: {
          y: {
            beginAtZero: true,
            grid: { color: '#27272a' },
            ticks: { 
              color: '#71717a',
              callback: (value) => value + 'h'
            }
          },
          x: {
            grid: { display: false },
            ticks: { color: '#71717a' }
          }
        }
      }
    })
  }
}

// Watch for trend view changes
watch(trendView, () => {
  initCharts()
})

onMounted(() => {
  loadData()
})

onUnmounted(() => {
  if (playtimeChart) playtimeChart.destroy()
})
</script>


<style scoped>
.analytics-container {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.analytics-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.period-select {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 8px 12px;
  color: white;
  font-size: 14px;
  cursor: pointer;
}

.period-select:focus {
  outline: none;
  border-color: var(--primary-color);
}

.btn-export {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-export:hover {
  background: var(--primary-hover);
}

.btn-export .icon {
  width: 16px;
  height: 16px;
}

/* Loading & Error States */
.loading-container, .error-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 20px;
  color: var(--text-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid rgba(255, 255, 255, 0.1);
  border-top-color: var(--primary-color);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error-icon {
  width: 48px;
  height: 48px;
  color: #f87171;
  margin-bottom: 16px;
}

.btn-retry {
  margin-top: 16px;
  padding: 8px 24px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
}

.empty-state {
  padding: 40px 20px;
  text-align: center;
  color: var(--text-secondary);
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
}

.stat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.stat-label {
  font-size: 14px;
  color: var(--text-secondary);
}

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-icon.indigo { background: rgba(99, 102, 241, 0.2); color: #818cf8; }
.stat-icon.emerald { background: rgba(16, 185, 129, 0.2); color: #34d399; }
.stat-icon.amber { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
.stat-icon.rose { background: rgba(244, 63, 94, 0.2); color: #fb7185; }

.stat-icon .icon {
  width: 20px;
  height: 20px;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.stat-change {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
}

.stat-change.positive { color: #34d399; }
.stat-change .icon { width: 12px; height: 12px; }
.stat-desc { font-size: 12px; color: var(--text-secondary); }

/* Content Grid */
.content-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 24px;
  margin-bottom: 32px;
}

.main-column, .side-column {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* Chart Card */
.chart-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
}

.chart-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.chart-desc {
  font-size: 14px;
  color: var(--text-secondary);
}

.chart-tabs {
  display: flex;
  gap: 8px;
}

.tab-btn {
  padding: 6px 12px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 500;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-secondary);
  border: none;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-btn.active {
  background: var(--primary-color);
  color: white;
}

.chart-container {
  height: 300px;
}

/* Games List */
.games-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.game-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px;
  border-radius: 12px;
  transition: background 0.2s;
}

.game-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.game-rank {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 700;
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.game-rank.gold { background: linear-gradient(135deg, #fbbf24, #f59e0b); color: white; }
.game-rank.silver { background: linear-gradient(135deg, #9ca3af, #6b7280); color: white; }
.game-rank.bronze { background: linear-gradient(135deg, #d97706, #b45309); color: white; }

.game-info { flex: 1; }
.game-name { font-size: 14px; font-weight: 600; color: var(--text-primary); margin-bottom: 2px; }
.game-sessions { font-size: 12px; color: var(--text-secondary); }
.game-playtime { text-align: right; }
.playtime-value { font-size: 16px; font-weight: 700; color: var(--text-primary); }
.playtime-percent { font-size: 12px; color: var(--text-secondary); }

/* Genre Bars */
.genre-bars {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.genre-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.genre-header {
  display: flex;
  justify-content: space-between;
}

.genre-name { font-size: 14px; color: var(--text-primary); }
.genre-percent { font-size: 14px; font-weight: 600; color: var(--text-primary); }

.genre-bar-bg {
  height: 8px;
  background: rgba(39, 39, 42, 1);
  border-radius: 4px;
  overflow: hidden;
}

.genre-bar-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 4px;
  transition: width 0.3s ease;
}

/* Achievement Ring */
.achievement-ring {
  position: relative;
  width: 128px;
  height: 128px;
  margin: 0 auto 16px;
}

.progress-ring {
  transform: rotate(-90deg);
  width: 100%;
  height: 100%;
}

.ring-bg { stroke: #27272a; }
.ring-progress {
  stroke: #6366f1;
  stroke-linecap: round;
  transition: stroke-dashoffset 0.5s ease;
}

.ring-center {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  text-align: center;
}

.ring-value { display: block; font-size: 24px; font-weight: 700; color: var(--text-primary); }
.ring-label { font-size: 12px; color: var(--text-secondary); }

.achievement-stats {
  text-align: center;
  font-size: 14px;
  color: var(--text-secondary);
}

.achievement-stats .remaining {
  color: #818cf8;
  font-size: 12px;
  margin-top: 4px;
}

/* Time Slots */
.time-slots {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.time-slot-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.slot-name {
  width: 80px;
  font-size: 12px;
  color: var(--text-secondary);
}

.slot-bar-bg {
  flex: 1;
  height: 6px;
  background: rgba(39, 39, 42, 1);
  border-radius: 3px;
  overflow: hidden;
}

.slot-bar-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 3px;
}

.slot-time {
  width: 60px;
  text-align: right;
  font-size: 12px;
  color: var(--text-primary);
}

/* Reports Section */
.reports-section {
  margin-bottom: 32px;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.templates-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

.template-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;
}

.template-card:hover {
  border-color: rgba(99, 102, 241, 0.5);
}

.template-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
}

.template-icon.indigo { background: rgba(99, 102, 241, 0.2); color: #818cf8; }
.template-icon.emerald { background: rgba(16, 185, 129, 0.2); color: #34d399; }
.template-icon.amber { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
.template-icon.purple { background: rgba(168, 85, 247, 0.2); color: #c084fc; }
.template-icon.rose { background: rgba(244, 63, 94, 0.2); color: #fb7185; }

.template-icon .icon { width: 24px; height: 24px; }
.template-name { font-size: 16px; font-weight: 600; color: var(--text-primary); margin-bottom: 8px; }
.template-desc { font-size: 14px; color: var(--text-secondary); margin-bottom: 16px; line-height: 1.5; }

.template-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.template-formats { font-size: 12px; color: var(--text-secondary); }

.btn-generate {
  padding: 8px 16px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}

/* Recent Reports */
.recent-reports { margin-bottom: 32px; }

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.btn-refresh {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: transparent;
  color: var(--primary-color);
  border: none;
  font-size: 14px;
  cursor: pointer;
}

.btn-refresh .icon { width: 16px; height: 16px; }

.reports-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.report-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
}

.report-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.report-icon.indigo { background: rgba(99, 102, 241, 0.2); color: #818cf8; }
.report-icon.blue { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
.report-icon.red { background: rgba(239, 68, 68, 0.2); color: #f87171; }
.report-icon.gray { background: rgba(113, 113, 122, 0.2); color: #a1a1aa; }

.report-icon .icon { width: 24px; height: 24px; }
.report-info { flex: 1; min-width: 0; }
.report-name { font-size: 14px; font-weight: 600; color: var(--text-primary); margin-bottom: 4px; }

.report-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text-secondary);
}

.report-status {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.report-status.completed { background: rgba(16, 185, 129, 0.2); color: #34d399; }
.report-status.generating { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
.report-status.failed { background: rgba(239, 68, 68, 0.2); color: #f87171; }

.report-actions { display: flex; gap: 8px; }

.btn-action {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-secondary);
  border: none;
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-action:hover { background: rgba(255, 255, 255, 0.1); color: white; }
.btn-action.danger { background: rgba(239, 68, 68, 0.1); color: #f87171; }
.btn-action.danger:hover { background: rgba(239, 68, 68, 0.2); }
.btn-action .icon { width: 16px; height: 16px; }

/* Dialog */
.dialog-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.dialog-content {
  background: #18181b;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 24px;
  width: 400px;
  max-width: 90%;
}

.dialog-title { font-size: 18px; font-weight: 600; color: var(--text-primary); margin-bottom: 8px; }
.dialog-desc { font-size: 14px; color: var(--text-secondary); margin-bottom: 20px; }
.dialog-form { margin-bottom: 24px; }
.form-group { margin-bottom: 16px; }
.form-group label { display: block; font-size: 14px; color: var(--text-secondary); margin-bottom: 8px; }

.form-select {
  width: 100%;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: white;
  font-size: 14px;
}

.dialog-actions { display: flex; justify-content: flex-end; gap: 12px; }

.btn-cancel {
  padding: 10px 20px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-secondary);
  border: none;
  border-radius: 8px;
  cursor: pointer;
}

.btn-confirm {
  padding: 10px 20px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
}

.btn-confirm:disabled { opacity: 0.6; cursor: not-allowed; }

/* Responsive */
@media (max-width: 1200px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
  .content-grid { grid-template-columns: 1fr; }
  .templates-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .analytics-header { flex-direction: column; align-items: flex-start; gap: 16px; }
  .stats-grid { grid-template-columns: 1fr; }
  .templates-grid { grid-template-columns: 1fr; }
  .report-item { flex-direction: column; align-items: flex-start; }
  .report-actions { width: 100%; }
  .btn-action { flex: 1; justify-content: center; }
}
</style>
