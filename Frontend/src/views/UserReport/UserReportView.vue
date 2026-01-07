<template>
  <div class="user-report-container">
    <!-- Header -->
    <div class="report-header">
      <div class="header-left">
        <h1 class="page-title">我的游戏报表</h1>
        <p class="page-desc">
          Steam 个人数据概览
          <span v-if="refreshing" class="refreshing-hint">
            <RefreshCw class="icon spinning" /> 更新中...
          </span>
          <span v-else-if="lastUpdateTime" class="last-update">
            上次更新: {{ lastUpdateTime }}
          </span>
        </p>
      </div>
      <div class="header-right">
        <button class="btn-export" @click="showExportDialog = true">
          <Download class="icon" />
          导出报表
        </button>
      </div>
    </div>

    <!-- Loading State (only show if no cached data) -->
    <div v-if="loading && !hasCachedData" class="loading-container">
      <div class="loading-spinner"></div>
      <p>加载数据中...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error && !hasCachedData" class="error-container">
      <AlertCircle class="error-icon" />
      <p>{{ error }}</p>
      <button class="btn-retry" @click="loadData(true)">重试</button>
    </div>

    <!-- Content (show with cached data or fresh data) -->
    <template v-else>
      <!-- Stats Overview -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">游戏库</span>
            <div class="stat-icon indigo">
              <Gamepad2 class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ gameLibrary.totalGames }}</div>
          <div class="stat-desc">已玩 {{ gameLibrary.playedGames }} 款</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">总游戏时长</span>
            <div class="stat-icon emerald">
              <Clock class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ gameLibrary.totalPlaytimeFormatted }}</div>
          <div class="stat-desc">日均 {{ formatMinutes(gameLibrary.dailyAverageMinutes) }}</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">本周时长</span>
            <div class="stat-icon cyan">
              <TrendingUp class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ formatMinutes(gameLibrary.thisWeekPlaytimeMinutes) }}</div>
          <div class="stat-desc">本月 {{ formatMinutes(gameLibrary.thisMonthPlaytimeMinutes) }}</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">绑定平台</span>
            <div class="stat-icon blue">
              <Layers class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ gameLibrary.boundPlatformCount }} 个</div>
          <div class="stat-desc">跨平台游戏 {{ gameLibrary.crossPlatformGames }} 款</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">成就解锁</span>
            <div class="stat-icon amber">
              <Trophy class="icon" />
            </div>
          </div>
          <div class="stat-value">{{ achievements.unlockedAchievements }}</div>
          <div class="stat-desc">完成率 {{ achievements.completionRate }}%</div>
        </div>
      </div>

      <!-- Playtime Trend Chart -->
      <div v-if="gameLibrary.dailyPlaytimeTrend?.length" class="trend-section">
        <h3 class="section-title">📈 游戏时长趋势（最近14天）</h3>
        <div class="trend-chart-container">
          <canvas ref="trendChartRef"></canvas>
        </div>
        <!-- 趋势分析摘要 -->
        <div class="trend-summary">
          <div class="trend-stat">
            <span class="trend-stat-label">日均时长</span>
            <span class="trend-stat-value">{{ dailyAverageFormatted }}</span>
          </div>
          <div class="trend-stat">
            <span class="trend-stat-label">最高单日</span>
            <span class="trend-stat-value">{{ maxDayPlaytimeFormatted }}</span>
          </div>
          <div class="trend-stat">
            <span class="trend-stat-label">活跃天数</span>
            <span class="trend-stat-value">{{ activeDaysCount }} 天</span>
          </div>
          <div class="trend-stat" :class="trendDirection">
            <span class="trend-stat-label">趋势</span>
            <span class="trend-stat-value">{{ trendDirectionText }}</span>
          </div>
        </div>
      </div>

      <!-- Weekly Comparison Chart -->
      <div v-if="gameLibrary.dailyPlaytimeTrend?.length >= 7" class="weekly-section">
        <h3 class="section-title">📊 周游戏时长对比</h3>
        <div class="weekly-chart-container">
          <canvas ref="weeklyChartRef"></canvas>
        </div>
      </div>

      <!-- Recent Played Analysis -->
      <div v-if="filteredRecentPlayed.length" class="recent-analysis-section">
        <h3 class="section-title">🎮 最近游玩详细分析</h3>
        <div class="recent-analysis-container">
          <!-- 总览统计 -->
          <div class="recent-summary">
            <div class="summary-item">
              <span class="summary-value">{{ filteredRecentPlayed.length }}</span>
              <span class="summary-label">游戏数</span>
            </div>
            <div class="summary-item">
              <span class="summary-value">{{ formatMinutes(recentTotalPlaytime) }}</span>
              <span class="summary-label">总时长(2周)</span>
            </div>
            <div class="summary-item">
              <span class="summary-value">{{ formatMinutes(recentDailyAverage) }}</span>
              <span class="summary-label">日均时长</span>
            </div>
            <div class="summary-item">
              <span class="summary-value">{{ mostPlayedRecent?.gameName?.substring(0, 8) || '-' }}</span>
              <span class="summary-label">最常玩</span>
            </div>
          </div>
          
          <!-- 游戏详细列表 -->
          <div class="recent-games-detail">
            <div 
              v-for="(game, index) in filteredRecentPlayed" 
              :key="game.gameId" 
              class="recent-game-card clickable"
              @click="goToGameDetail(game.gameId)"
            >
              <div class="game-rank-badge" :class="getRankClass(index)">{{ index + 1 }}</div>
              <img :src="game.headerImage || noCoverImage" class="game-cover" @error="handleImageError" />
              <div class="game-details">
                <h4 class="game-title">{{ game.gameName }}</h4>
                <div class="game-stats">
                  <div class="stat-row">
                    <span class="stat-icon">⏱️</span>
                    <span class="stat-text">近2周: {{ formatMinutes(game.recentPlaytimeMinutes) }}</span>
                  </div>
                  <div class="stat-row">
                    <span class="stat-icon">📊</span>
                    <span class="stat-text">总时长: {{ formatMinutes(game.playtimeMinutes) }}</span>
                  </div>
                  <div class="stat-row" v-if="game.lastPlayed">
                    <span class="stat-icon">📅</span>
                    <span class="stat-text">最后游玩: {{ formatLastPlayed(game.lastPlayed) }}</span>
                  </div>
                </div>
                <!-- 时长占比条 -->
                <div class="playtime-bar-container">
                  <div 
                    class="playtime-bar" 
                    :style="{ width: getRecentPlaytimePercent(game.recentPlaytimeMinutes) + '%' }"
                  ></div>
                  <span class="playtime-percent">{{ getRecentPlaytimePercent(game.recentPlaytimeMinutes).toFixed(1) }}%</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Recent Games Playtime Chart -->
      <div v-if="recentGamesHistory.games?.length" class="recent-chart-section">
        <h3 class="section-title">📈 最近14天各游戏时长趋势</h3>
        <div class="recent-chart-container">
          <div class="chart-wrapper">
            <canvas ref="recentGamesChartRef"></canvas>
          </div>
          <div class="chart-legend">
            <div 
              v-for="(game, index) in recentGamesHistory.games.slice(0, 6)" 
              :key="game.gameId"
              class="legend-item clickable"
              :class="{ 'legend-hidden': hiddenGames.has(game.gameId) }"
              @click="toggleGameVisibility(game.gameId)"
            >
              <span class="legend-color" :style="{ background: gameChartColors[index] }"></span>
              <span class="legend-name">{{ game.gameName }}</span>
              <span class="legend-time">{{ formatMinutes(game.totalPlaytime) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Platform Stats -->
      <div v-if="gameLibrary.platformStats?.length" class="platform-stats-section">
        <h3 class="section-title">📊 平台统计</h3>
        <div class="platform-stats-grid">
          <div v-for="platform in gameLibrary.platformStats" :key="platform.platformId" class="platform-stat-card">
            <div class="platform-header">
              <span class="platform-icon">{{ getPlatformIcon(platform.platformName) }}</span>
              <span class="platform-name">{{ platform.platformName }}</span>
            </div>
            <div class="platform-stats">
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.gameCount }}</span>
                <span class="platform-stat-label">游戏</span>
              </div>
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.playtimeFormatted }}</span>
                <span class="platform-stat-label">时长</span>
              </div>
              <div class="platform-stat">
                <span class="platform-stat-value">{{ platform.percentage }}%</span>
                <span class="platform-stat-label">占比</span>
              </div>
            </div>
            <div class="platform-bar">
              <div class="platform-bar-fill" :style="{ width: platform.percentage + '%' }"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="content-grid">
        <!-- Left Column -->
        <div class="main-column">
          <!-- Playtime by Genre (Pie Chart) -->
          <div class="chart-card">
            <h3 class="chart-title">游戏时长分布</h3>
            <div class="chart-container">
              <canvas ref="genreChartRef"></canvas>
            </div>
            <div class="genre-legend" v-if="gameLibrary.playtimeByGenre?.length">
              <div v-for="(genre, index) in gameLibrary.playtimeByGenre.slice(0, 6)" :key="genre.genre" class="legend-item">
                <span class="legend-color" :style="{ background: chartColors[index] }"></span>
                <span class="legend-name">{{ genre.genre }}</span>
                <span class="legend-value">{{ genre.percentage }}%</span>
              </div>
            </div>
          </div>

          <!-- Top Played Games -->
          <div class="chart-card">
            <h3 class="chart-title">最常玩的游戏</h3>
            <div v-if="gameLibrary.topPlayedGames?.length" class="games-list">
              <div 
                v-for="(game, index) in gameLibrary.topPlayedGames.slice(0, 10)" 
                :key="game.gameId" 
                class="game-item clickable" 
                @click="goToGameDetail(game.gameId)"
              >
                <div class="game-rank" :class="getRankClass(index)">{{ index + 1 }}</div>
                <img :src="game.headerImage || noCoverImage" class="game-image" @error="handleImageError" />
                <div class="game-info">
                  <h4 class="game-name">{{ game.gameName }}</h4>
                </div>
                <div class="game-playtime">
                  <span class="playtime-value">{{ game.playtimeFormatted }}</span>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无游戏数据</div>
          </div>

          <!-- Activity Heatmap -->
          <div v-if="gameLibrary.dailyPlaytimeTrend?.length" class="chart-card">
            <h3 class="chart-title">🔥 游戏活跃度</h3>
            <div class="heatmap-container-inline">
              <div class="heatmap-weekdays-inline">
                <span>一</span><span>二</span><span>三</span><span>四</span><span>五</span><span>六</span><span>日</span>
              </div>
              <div class="heatmap-grid-inline">
                <div 
                  v-for="(day, index) in heatmapData" 
                  :key="index" 
                  class="heatmap-cell-inline"
                  :class="getHeatmapClass(day.playtimeMinutes)"
                  :title="day.isEmpty ? '' : `${day.date}: ${formatMinutes(day.playtimeMinutes)}`"
                >
                  <span v-if="!day.isEmpty" class="heatmap-date">{{ day.dayOfMonth }}</span>
                </div>
              </div>
              <div class="heatmap-legend-inline">
                <span class="legend-label">少</span>
                <div class="legend-cell level-0"></div>
                <div class="legend-cell level-1"></div>
                <div class="legend-cell level-2"></div>
                <div class="legend-cell level-3"></div>
                <div class="legend-cell level-4"></div>
                <span class="legend-label">多</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Right Column -->
        <div class="side-column">
          <!-- Achievement Progress -->
          <div class="chart-card">
            <h3 class="chart-title">成就进度</h3>
            <div class="achievement-ring">
              <svg class="progress-ring" viewBox="0 0 128 128">
                <circle class="ring-bg" cx="64" cy="64" r="56" stroke-width="8" fill="none" />
                <circle 
                  class="ring-progress" 
                  cx="64" cy="64" r="56" 
                  stroke-width="8" 
                  fill="none"
                  :stroke-dasharray="351.86"
                  :stroke-dashoffset="351.86 * (1 - achievements.completionRate / 100)"
                />
              </svg>
              <div class="ring-center">
                <span class="ring-value">{{ achievements.completionRate }}%</span>
                <span class="ring-label">完成</span>
              </div>
            </div>
            <div class="achievement-stats">
              <p>已解锁 {{ achievements.unlockedAchievements }} / {{ achievements.totalAchievements }}</p>
              <p class="perfect-games">🏆 完美游戏 {{ achievements.perfectGames }} 款</p>
            </div>
          </div>

          <!-- Genre Stats -->
          <div class="chart-card">
            <h3 class="chart-title">🎮 游戏类型统计</h3>
            <div v-if="genreStats.length" class="genre-stats-list">
              <div v-for="(genre, index) in genreStats.slice(0, 6)" :key="genre.genre" class="genre-stat-item">
                <div class="genre-stat-header">
                  <span class="genre-stat-color" :style="{ background: chartColors[index] }"></span>
                  <span class="genre-stat-name">{{ genre.genre }}</span>
                  <span class="genre-stat-count">{{ genre.gameCount }} 款</span>
                </div>
                <div class="genre-stat-bar-bg">
                  <div class="genre-stat-bar-fill" :style="{ width: genre.percentage + '%', background: chartColors[index] }"></div>
                </div>
                <div class="genre-stat-details">
                  <span>{{ formatMinutes(genre.playtimeMinutes) }}</span>
                  <span>{{ genre.percentage }}%</span>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无类型数据</div>
          </div>

          <!-- Recent Played -->
          <div class="chart-card">
            <h3 class="chart-title">最近游玩</h3>
            <div v-if="recentPlayed.length" class="recent-list">
              <div 
                v-for="game in recentPlayed.slice(0, 10)" 
                :key="game.gameId" 
                class="recent-item clickable" 
                @click="goToGameDetail(game.gameId)"
              >
                <img :src="game.headerImage || noCoverImage" class="recent-image" @error="handleImageError" />
                <div class="recent-info">
                  <h4 class="recent-name">{{ game.gameName }}</h4>
                  <p class="recent-time">{{ formatMinutes(game.recentPlaytimeMinutes) }} (2周内)</p>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无最近游玩记录</div>
          </div>

          <!-- Game Achievement Progress -->
          <div class="chart-card">
            <h3 class="chart-title">各游戏成就进度</h3>
            <div v-if="achievements.gameProgress?.length" class="progress-list">
              <div v-for="game in achievements.gameProgress.slice(0, 5)" :key="game.gameId" class="progress-item">
                <div class="progress-header">
                  <span class="progress-name">{{ game.gameName }}</span>
                  <span class="progress-percent">{{ game.completionRate }}%</span>
                </div>
                <div class="progress-bar-bg">
                  <div class="progress-bar-fill" :style="{ width: game.completionRate + '%' }"></div>
                </div>
                <p class="progress-detail">{{ game.unlockedAchievements }}/{{ game.totalAchievements }}</p>
              </div>
            </div>
            <div v-else class="empty-state">暂无成就数据</div>
          </div>
        </div>
      </div>

      <!-- Reports Section -->
      <div class="reports-section">
        <div class="section-header">
          <h2 class="section-title">📊 报告生成</h2>
        </div>
        <div class="reports-grid">
          <!-- Monthly Report -->
          <div class="report-card">
            <div class="report-icon monthly">
              <Calendar class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">月度游戏报告</h3>
              <p class="report-desc">游戏时长、成就、消费等统计</p>
              <div class="report-options">
                <!-- 月份选择器 -->
                <div class="month-picker" @click="showMonthPicker = !showMonthPicker">
                  <Calendar class="picker-icon" />
                  <span class="picker-value">{{ monthlyYear }}年{{ monthlyMonth }}月</span>
                  <ChevronDown class="picker-arrow" :class="{ 'rotate': showMonthPicker }" />
                </div>
                <!-- 月份选择弹窗 -->
                <div v-if="showMonthPicker" class="month-picker-dropdown" @click.stop>
                  <div class="picker-header">
                    <button class="picker-nav" @click="pickerYear--" :disabled="pickerYear <= currentYear - 4">
                      <ChevronLeft class="nav-icon" />
                    </button>
                    <span class="picker-year">{{ pickerYear }}年</span>
                    <button class="picker-nav" @click="pickerYear++" :disabled="pickerYear >= currentYear">
                      <ChevronRight class="nav-icon" />
                    </button>
                  </div>
                  <div class="picker-months">
                    <button 
                      v-for="m in 12" 
                      :key="m" 
                      class="picker-month"
                      :class="{ 
                        'selected': monthlyYear === pickerYear && monthlyMonth === m,
                        'disabled': isMonthDisabled(pickerYear, m)
                      }"
                      :disabled="isMonthDisabled(pickerYear, m)"
                      @click="selectMonth(pickerYear, m)"
                    >
                      {{ m }}月
                    </button>
                  </div>
                </div>
              </div>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateMonthlyReport('pdf')" :disabled="generating.monthly">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report csv" @click="generateMonthlyReport('csv')" :disabled="generating.monthly">
                <FileSpreadsheet class="btn-icon" /> CSV
              </button>
              <button class="btn-report html" @click="generateMonthlyReport('html')" :disabled="generating.monthly">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>

          <!-- Yearly Report -->
          <div class="report-card">
            <div class="report-icon yearly">
              <Award class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">年度总结报告</h3>
              <p class="report-desc">年度游戏数据全面分析</p>
              <div class="report-options">
                <!-- 年份选择器 -->
                <div class="year-picker" @click="showYearPicker = !showYearPicker">
                  <Calendar class="picker-icon" />
                  <span class="picker-value">{{ yearlyYear }}年</span>
                  <ChevronDown class="picker-arrow" :class="{ 'rotate': showYearPicker }" />
                </div>
                <!-- 年份选择弹窗 -->
                <div v-if="showYearPicker" class="year-picker-dropdown" @click.stop>
                  <div class="picker-years">
                    <button 
                      v-for="y in yearlyYearOptions" 
                      :key="y" 
                      class="picker-year-btn"
                      :class="{ 'selected': yearlyYear === y }"
                      @click="selectYear(y)"
                    >
                      {{ y }}年
                    </button>
                  </div>
                </div>
              </div>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateYearlyReport('pdf')" :disabled="generating.yearly">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report csv" @click="generateYearlyReport('csv')" :disabled="generating.yearly">
                <FileSpreadsheet class="btn-icon" /> CSV
              </button>
              <button class="btn-report html" @click="generateYearlyReport('html')" :disabled="generating.yearly">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>

          <!-- Inventory Report -->
          <div class="report-card">
            <div class="report-icon inventory">
              <Package class="icon" />
            </div>
            <div class="report-info">
              <h3 class="report-title">游戏库存报告</h3>
              <p class="report-desc">游戏收藏、安装、存档统计</p>
            </div>
            <div class="report-actions">
              <button class="btn-report pdf" @click="generateInventoryReport('pdf')" :disabled="generating.inventory">
                <FileText class="btn-icon" /> PDF
              </button>
              <button class="btn-report csv" @click="generateInventoryReport('csv')" :disabled="generating.inventory">
                <FileSpreadsheet class="btn-icon" /> CSV
              </button>
              <button class="btn-report html" @click="generateInventoryReport('html')" :disabled="generating.inventory">
                <Globe class="btn-icon" /> HTML
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Recent Reports Section -->
      <div class="recent-reports-section">
        <div class="section-header">
          <h2 class="section-title">📋 最近报表</h2>
          <button class="btn-refresh-reports" @click="loadReportHistory">
            <RefreshCw class="icon" :class="{ spinning: loadingHistory }" />
            刷新
          </button>
        </div>
        <div v-if="recentReports.length" class="reports-list">
          <div v-for="report in recentReports" :key="report.id" class="report-history-item">
            <div class="report-history-icon" :class="getReportTypeClass(report.type)">
              <component :is="getReportTypeIcon(report.type)" class="icon" />
            </div>
            <div class="report-history-info">
              <h4 class="report-history-name">{{ report.name }}</h4>
              <div class="report-history-meta">
                <span>生成于 {{ formatReportDate(report.generatedAt) }}</span>
                <span>•</span>
                <span>{{ report.fileSize }}</span>
                <span>•</span>
                <span class="report-status" :class="report.status">{{ getStatusText(report.status) }}</span>
              </div>
            </div>
            <div class="report-history-actions">
              <button v-if="report.status === 'completed'" class="btn-action download" @click="redownloadReport(report)">
                <Download class="icon" />
                下载
              </button>
              <button class="btn-action delete" @click="deleteReportHistory(report.id)">
                <Trash2 class="icon" />
                删除
              </button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">暂无报表记录</div>
      </div>
    </template>

    <!-- Export Dialog -->
    <div v-if="showExportDialog" class="dialog-overlay" @click.self="showExportDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">导出报表</h3>
        <p class="dialog-desc">选择报表类型和格式</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>报表类型</label>
            <select v-model="exportForm.type" class="form-select">
              <option value="monthly">月度游戏报告</option>
              <option value="yearly">年度总结报告</option>
              <option value="inventory">游戏库存报告</option>
            </select>
          </div>
          
          <div class="form-group" v-if="exportForm.type === 'monthly'">
            <label>选择月份</label>
            <div class="dialog-picker-wrapper">
              <div class="month-picker dialog-picker" @click="showExportMonthPicker = !showExportMonthPicker">
                <Calendar class="picker-icon" />
                <span class="picker-value">{{ exportForm.year }}年{{ exportForm.month }}月</span>
                <ChevronDown class="picker-arrow" :class="{ 'rotate': showExportMonthPicker }" />
              </div>
              <div v-if="showExportMonthPicker" class="month-picker-dropdown dialog-dropdown" @click.stop>
                <div class="picker-header">
                  <button class="picker-nav" @click="exportPickerYear--" :disabled="exportPickerYear <= currentYear - 4">
                    <ChevronLeft class="nav-icon" />
                  </button>
                  <span class="picker-year">{{ exportPickerYear }}年</span>
                  <button class="picker-nav" @click="exportPickerYear++" :disabled="exportPickerYear >= currentYear">
                    <ChevronRight class="nav-icon" />
                  </button>
                </div>
                <div class="picker-months">
                  <button 
                    v-for="m in 12" 
                    :key="m" 
                    class="picker-month"
                    :class="{ 
                      'selected': exportForm.year === exportPickerYear && exportForm.month === m,
                      'disabled': isMonthDisabled(exportPickerYear, m)
                    }"
                    :disabled="isMonthDisabled(exportPickerYear, m)"
                    @click="selectExportMonth(exportPickerYear, m)"
                  >
                    {{ m }}月
                  </button>
                </div>
              </div>
            </div>
          </div>
          
          <div class="form-group" v-if="exportForm.type === 'yearly'">
            <label>选择年份</label>
            <div class="dialog-picker-wrapper">
              <div class="year-picker dialog-picker" @click="showExportYearPicker = !showExportYearPicker">
                <Calendar class="picker-icon" />
                <span class="picker-value">{{ exportForm.year }}年</span>
                <ChevronDown class="picker-arrow" :class="{ 'rotate': showExportYearPicker }" />
              </div>
              <div v-if="showExportYearPicker" class="year-picker-dropdown dialog-dropdown" @click.stop>
                <div class="picker-years">
                  <button 
                    v-for="y in yearlyYearOptions" 
                    :key="y" 
                    class="picker-year-btn"
                    :class="{ 'selected': exportForm.year === y }"
                    @click="selectExportYear(y)"
                  >
                    {{ y }}年
                  </button>
                </div>
              </div>
            </div>
          </div>
          
          <div class="form-group">
            <label>输出格式</label>
            <div class="format-options">
              <label class="format-option" v-for="fmt in availableFormats" :key="fmt">
                <input type="radio" v-model="exportForm.format" :value="fmt" />
                <span class="format-label">{{ fmt.toUpperCase() }}</span>
              </label>
            </div>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showExportDialog = false">取消</button>
          <button class="btn-confirm" @click="handleExport" :disabled="exporting">
            {{ exporting ? '导出中...' : '导出报表' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>


<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { 
  RefreshCw, AlertCircle, User, Gamepad2, Clock, Trophy, Heart,
  Calendar, Award, Package, FileText, FileSpreadsheet, Globe, Download, Trash2,
  TrendingUp, Activity, Layers, ChevronDown, ChevronLeft, ChevronRight
} from 'lucide-vue-next'
import Chart from 'chart.js/auto'
import noCoverImage from '@/assets/no_cover.png'
import { 
  getUserReportOverview, 
  syncFromSteam,
  getMonthlyReportUrl,
  getYearlyReportUrl,
  getInventoryReportUrl,
  downloadReport,
  openHtmlReport,
  getRecentPlayedHistory
} from '@/api/userReport'
import { useRouter } from 'vue-router'

// Router
const router = useRouter()

// Cache key
const CACHE_KEY = 'user_report_cache'
const CACHE_EXPIRY = 30 * 60 * 1000 // 30 minutes

// Refs
const genreChartRef = ref(null)
const trendChartRef = ref(null)
const weeklyChartRef = ref(null)
const recentGamesChartRef = ref(null)
let genreChart = null
let trendChart = null
let weeklyChart = null
let recentGamesChart = null

const loading = ref(true)
const refreshing = ref(false)
const error = ref(null)
const syncing = ref(false)
const hasCachedData = ref(false)
const lastUpdateTime = ref('')

// Recent games history data
const recentGamesHistory = ref({ dates: [], games: [] })
const hiddenGames = ref(new Set())

// Game chart colors
const gameChartColors = [
  '#6366f1', '#8b5cf6', '#ec4899', '#f43f5e', '#f97316', '#eab308',
  '#22c55e', '#14b8a6', '#06b6d4', '#3b82f6'
]

// Report generation state
const generating = ref({
  monthly: false,
  yearly: false,
  inventory: false
})

// Export dialog
const showExportDialog = ref(false)
const exporting = ref(false)

// Report date selectors
const currentYear = new Date().getFullYear()
const currentMonth = new Date().getMonth() + 1

// 获取上个月的年份和月份
const getLastMonthYearAndMonth = () => {
  const now = new Date()
  now.setMonth(now.getMonth() - 1)
  return {
    year: now.getFullYear(),
    month: now.getMonth() + 1
  }
}

const lastMonth = getLastMonthYearAndMonth()

const monthlyYear = ref(lastMonth.year)
const monthlyMonth = ref(lastMonth.month)
const yearlyYear = ref(currentYear - 1) // 默认选择去年

// 月份/年份选择器状态
const showMonthPicker = ref(false)
const showYearPicker = ref(false)
const pickerYear = ref(lastMonth.year)

// 导出对话框的选择器状态
const showExportMonthPicker = ref(false)
const showExportYearPicker = ref(false)
const exportPickerYear = ref(lastMonth.year)

// 判断月份是否禁用（未完成的月份）
const isMonthDisabled = (year, month) => {
  if (year > currentYear) return true
  if (year === currentYear && month >= currentMonth) return true
  return false
}

// 选择月份
const selectMonth = (year, month) => {
  monthlyYear.value = year
  monthlyMonth.value = month
  exportForm.value.year = year
  exportForm.value.month = month
  showMonthPicker.value = false
}

// 导出对话框选择月份
const selectExportMonth = (year, month) => {
  exportForm.value.year = year
  exportForm.value.month = month
  showExportMonthPicker.value = false
}

// 导出对话框选择年份
const selectExportYear = (year) => {
  exportForm.value.year = year
  showExportYearPicker.value = false
}

// 选择年份
const selectYear = (year) => {
  yearlyYear.value = year
  exportForm.value.year = year
  showYearPicker.value = false
}

// 点击外部关闭选择器
const closePickersOnClickOutside = (e) => {
  if (!e.target.closest('.month-picker') && !e.target.closest('.month-picker-dropdown')) {
    showMonthPicker.value = false
    showExportMonthPicker.value = false
  }
  if (!e.target.closest('.year-picker') && !e.target.closest('.year-picker-dropdown')) {
    showYearPicker.value = false
    showExportYearPicker.value = false
  }
}

const exportForm = ref({
  type: 'monthly',
  year: lastMonth.year,
  month: lastMonth.month,
  format: 'pdf'
})

// Year options for monthly report (last 5 years)
const monthlyYearOptions = computed(() => {
  const years = []
  for (let i = currentYear; i >= currentYear - 4; i--) {
    years.push(i)
  }
  return years
})

// Month options for monthly report (only completed months)
const monthlyMonthOptions = computed(() => {
  const months = []
  const maxMonth = monthlyYear.value === currentYear ? currentMonth - 1 : 12
  for (let m = 1; m <= maxMonth; m++) {
    months.push(m)
  }
  return months
})

// Year options for yearly report (only completed years, excluding current year)
const yearlyYearOptions = computed(() => {
  const years = []
  for (let i = currentYear - 1; i >= currentYear - 5; i--) {
    years.push(i)
  }
  return years
})

// Watch for monthly year change to reset month if needed
watch(() => monthlyYear.value, (newYear) => {
  const maxMonth = newYear === currentYear ? currentMonth - 1 : 12
  if (monthlyMonth.value > maxMonth) {
    monthlyMonth.value = maxMonth
  }
  // 同步到 exportForm
  exportForm.value.year = newYear
  exportForm.value.month = monthlyMonth.value
})

// Watch for monthly month change
watch(() => monthlyMonth.value, (newMonth) => {
  exportForm.value.month = newMonth
})

// Watch for yearly year change
watch(() => yearlyYear.value, (newYear) => {
  exportForm.value.year = newYear
})

// Watch for report type change to reset year
watch(() => exportForm.value.type, (newType) => {
  if (newType === 'yearly') {
    exportForm.value.year = yearlyYear.value
  } else if (newType === 'monthly') {
    exportForm.value.year = monthlyYear.value
    exportForm.value.month = monthlyMonth.value
  }
})

// Available formats based on report type
const availableFormats = computed(() => {
  return ['pdf', 'csv', 'html']
})

// Data
const profile = ref({})
const gameLibrary = ref({
  totalGames: 0,
  totalPlaytimeMinutes: 0,
  totalPlaytimeFormatted: '0小时',
  playedGames: 0,
  neverPlayedGames: 0,
  recentPlaytimeMinutes: 0,
  thisWeekPlaytimeMinutes: 0,
  thisMonthPlaytimeMinutes: 0,
  dailyAverageMinutes: 0,
  boundPlatformCount: 0,
  crossPlatformGames: 0,
  dailyPlaytimeTrend: [],
  playtimeByGenre: [],
  topPlayedGames: [],
  platformStats: []
})
const achievements = ref({
  totalAchievements: 0,
  unlockedAchievements: 0,
  completionRate: 0,
  perfectGames: 0,
  recentUnlocks: [],
  gameProgress: []
})
const recentPlayed = ref([])
const genreStats = ref([])

// Recent reports history (stored in localStorage)
const REPORTS_HISTORY_KEY = 'user_report_history'
const recentReports = ref([])
const loadingHistory = ref(false)

// Chart colors
const chartColors = [
  '#6366f1', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', 
  '#ec4899', '#14b8a6', '#f97316', '#06b6d4', '#84cc16'
]

// 趋势分析计算属性
const dailyAverageFormatted = computed(() => {
  const trend = gameLibrary.value.dailyPlaytimeTrend
  if (!trend?.length) return '0分钟'
  const total = trend.reduce((sum, d) => sum + d.playtimeMinutes, 0)
  const avg = Math.round(total / trend.length)
  return formatMinutes(avg)
})

const maxDayPlaytimeFormatted = computed(() => {
  const trend = gameLibrary.value.dailyPlaytimeTrend
  if (!trend?.length) return '0分钟'
  const max = Math.max(...trend.map(d => d.playtimeMinutes))
  return formatMinutes(max)
})

const activeDaysCount = computed(() => {
  const trend = gameLibrary.value.dailyPlaytimeTrend
  if (!trend?.length) return 0
  return trend.filter(d => d.playtimeMinutes > 0).length
})

const trendDirection = computed(() => {
  const trend = gameLibrary.value.dailyPlaytimeTrend
  if (!trend?.length || trend.length < 7) return ''
  const firstHalf = trend.slice(0, Math.floor(trend.length / 2))
  const secondHalf = trend.slice(Math.floor(trend.length / 2))
  const firstAvg = firstHalf.reduce((s, d) => s + d.playtimeMinutes, 0) / firstHalf.length
  const secondAvg = secondHalf.reduce((s, d) => s + d.playtimeMinutes, 0) / secondHalf.length
  if (secondAvg > firstAvg * 1.1) return 'trend-up'
  if (secondAvg < firstAvg * 0.9) return 'trend-down'
  return 'trend-stable'
})

const trendDirectionText = computed(() => {
  const dir = trendDirection.value
  if (dir === 'trend-up') return '📈 上升'
  if (dir === 'trend-down') return '📉 下降'
  return '➡️ 平稳'
})

// 过滤掉最近两周时长为0的游戏
const filteredRecentPlayed = computed(() => {
  return recentPlayed.value.filter(g => g.recentPlaytimeMinutes > 0)
})

// 最近游玩分析计算属性
const recentTotalPlaytime = computed(() => {
  return filteredRecentPlayed.value.reduce((sum, g) => sum + (g.recentPlaytimeMinutes || 0), 0)
})

const recentDailyAverage = computed(() => {
  return Math.round(recentTotalPlaytime.value / 14) // 2周 = 14天
})

const mostPlayedRecent = computed(() => {
  if (!filteredRecentPlayed.value.length) return null
  return filteredRecentPlayed.value.reduce((max, g) => 
    (g.recentPlaytimeMinutes || 0) > (max.recentPlaytimeMinutes || 0) ? g : max
  , filteredRecentPlayed.value[0])
})

const getRecentPlaytimePercent = (minutes) => {
  if (!recentTotalPlaytime.value || recentTotalPlaytime.value === 0) return 0
  return (minutes / recentTotalPlaytime.value) * 100
}

const formatLastPlayed = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  const now = new Date()
  const diffDays = Math.floor((now - date) / (1000 * 60 * 60 * 24))
  if (diffDays === 0) return '今天'
  if (diffDays === 1) return '昨天'
  if (diffDays < 7) return `${diffDays}天前`
  return date.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
}

// 热力图数据
const heatmapData = computed(() => {
  const trend = gameLibrary.value.dailyPlaytimeTrend
  if (!trend?.length) return []
  
  // 获取第一天是星期几 (0=周日, 1=周一, ..., 6=周六)
  const firstDate = new Date(trend[0].date)
  // 转换为周一开始 (0=周一, 1=周二, ..., 6=周日)
  let firstDayOfWeek = firstDate.getDay()
  firstDayOfWeek = firstDayOfWeek === 0 ? 6 : firstDayOfWeek - 1
  
  // 在前面填充空白格子，使日期对齐到正确的星期
  const result = []
  for (let i = 0; i < firstDayOfWeek; i++) {
    result.push({
      date: '',
      dayOfMonth: '',
      playtimeMinutes: -1, // 用 -1 表示空白格子
      isEmpty: true
    })
  }
  
  // 添加实际数据
  trend.forEach(d => {
    result.push({
      date: new Date(d.date).toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' }),
      dayOfMonth: new Date(d.date).getDate(),
      playtimeMinutes: d.playtimeMinutes,
      isEmpty: false
    })
  })
  
  return result
})

const getHeatmapClass = (minutes) => {
  if (minutes === -1) return 'level-empty' // 空白格子
  if (minutes === 0) return 'level-0'
  if (minutes < 30) return 'level-1'
  if (minutes < 60) return 'level-2'
  if (minutes < 120) return 'level-3'
  return 'level-4'
}

// Cache functions
const saveToCache = (data) => {
  try {
    const cacheData = {
      data,
      timestamp: Date.now()
    }
    localStorage.setItem(CACHE_KEY, JSON.stringify(cacheData))
  } catch (e) {
    console.warn('Failed to save cache:', e)
  }
}

const loadFromCache = () => {
  try {
    const cached = localStorage.getItem(CACHE_KEY)
    if (cached) {
      const cacheData = JSON.parse(cached)
      // Check if cache is still valid (30 minutes)
      if (Date.now() - cacheData.timestamp < CACHE_EXPIRY) {
        return cacheData
      }
    }
  } catch (e) {
    console.warn('Failed to load cache:', e)
  }
  return null
}

const formatCacheTime = (timestamp) => {
  const date = new Date(timestamp)
  const now = new Date()
  const diff = now - date
  
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  return date.toLocaleString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// 处理图片加载错误
const handleImageError = (e) => {
  e.target.src = noCoverImage
}

// 跳转到游戏详情页面
const goToGameDetail = (gameId) => {
  if (gameId) {
    router.push({ name: 'GameDetail', params: { id: gameId } })
  }
}

const applyData = (data) => {
  profile.value = data.profile || {}
  gameLibrary.value = data.gameLibrary || gameLibrary.value
  achievements.value = data.achievements || achievements.value
  recentPlayed.value = data.recentPlayed || []
  
  // Extract genre stats from playtimeByGenre
  if (data.gameLibrary?.playtimeByGenre) {
    genreStats.value = data.gameLibrary.playtimeByGenre.map(g => ({
      genre: g.genre,
      playtimeMinutes: g.playtimeMinutes,
      percentage: g.percentage,
      gameCount: g.gameCount || Math.round(g.percentage / 10) || 1 // Estimate if not provided
    }))
  }
}

// Methods
const formatMinutes = (minutes) => {
  if (!minutes) return '0分钟'
  if (minutes < 60) return `${minutes}分钟`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours >= 100) return `${hours}小时`
  return mins > 0 ? `${hours}小时${mins}分钟` : `${hours}小时`
}

const getRankClass = (index) => {
  if (index === 0) return 'gold'
  if (index === 1) return 'silver'
  if (index === 2) return 'bronze'
  return ''
}

const getPlatformIcon = (platformName) => {
  const icons = {
    'Steam': '🎮',
    'Xbox': '🎯',
    'PlayStation': '🎲',
    'PSN': '🎲',
    'GOG': '🌟',
    'Epic': '⚡',
    'Nintendo': '🍄',
    'Origin': '🔶',
    'Ubisoft': '🔷',
    'Battle.net': '💠'
  }
  return icons[platformName] || '🎮'
}

const loadData = async (forceRefresh = false) => {
  error.value = null

  // Try to load from cache first
  if (!forceRefresh) {
    const cachedData = loadFromCache()
    if (cachedData) {
      hasCachedData.value = true
      applyData(cachedData.data)
      lastUpdateTime.value = formatCacheTime(cachedData.timestamp)
      loading.value = false
      setTimeout(() => initCharts(), 100)
      
      // Load recent games history
      loadRecentGamesHistory()
      
      // Refresh in background
      refreshing.value = true
      try {
        const res = await getUserReportOverview()
        if (res.data) {
          applyData(res.data)
          saveToCache({ data: res.data, timestamp: Date.now() })
          lastUpdateTime.value = '刚刚'
          setTimeout(() => initCharts(), 100)
        }
      } catch (err) {
        console.warn('Background refresh failed:', err)
      } finally {
        refreshing.value = false
      }
      return
    }
  }

  // No cache, load fresh
  loading.value = true
  try {
    const res = await getUserReportOverview()
    if (res.data) {
      applyData(res.data)
      saveToCache({ data: res.data, timestamp: Date.now() })
      lastUpdateTime.value = '刚刚'
      hasCachedData.value = true
    }
    setTimeout(() => initCharts(), 100)
    
    // Load recent games history
    loadRecentGamesHistory()
  } catch (err) {
    console.error('加载数据失败:', err)
    error.value = '加载数据失败，请确保已绑定Steam账号'
  } finally {
    loading.value = false
  }
}

const handleSync = async () => {
  syncing.value = true
  try {
    const res = await syncFromSteam()
    if (res.data?.success) {
      alert(`同步成功！游戏: ${res.data.gamesSync}, 成就: ${res.data.achievementsSync}`)
      // Clear cache and reload
      localStorage.removeItem(CACHE_KEY)
      loadData(true)
    } else {
      alert(res.data?.message || '同步失败')
    }
  } catch (err) {
    console.error('同步失败:', err)
    alert('同步失败: ' + (err.message || '未知错误'))
  } finally {
    syncing.value = false
  }
}

// 加载最近游玩游戏的时长历史
const loadRecentGamesHistory = async () => {
  try {
    const res = await getRecentPlayedHistory(14)
    if (res.data) {
      recentGamesHistory.value = res.data
      setTimeout(() => initRecentGamesChart(), 100)
    }
  } catch (err) {
    console.error('加载游戏时长历史失败:', err)
  }
}

// 切换游戏在图表中的显示/隐藏
const toggleGameVisibility = (gameId) => {
  if (hiddenGames.value.has(gameId)) {
    hiddenGames.value.delete(gameId)
  } else {
    hiddenGames.value.add(gameId)
  }
  hiddenGames.value = new Set(hiddenGames.value) // 触发响应式更新
  initRecentGamesChart()
}

// 初始化最近游戏时长图表
const initRecentGamesChart = () => {
  if (!recentGamesChartRef.value || !recentGamesHistory.value.games?.length) return
  
  const ctx = recentGamesChartRef.value.getContext('2d')
  
  if (recentGamesChart) {
    recentGamesChart.destroy()
  }
  
  const visibleGames = recentGamesHistory.value.games
    .slice(0, 6)
    .filter(g => !hiddenGames.value.has(g.gameId))
  
  const datasets = visibleGames.map((game, index) => ({
    label: game.gameName.length > 15 ? game.gameName.substring(0, 15) + '...' : game.gameName,
    data: game.dailyPlaytime,
    borderColor: gameChartColors[recentGamesHistory.value.games.findIndex(g => g.gameId === game.gameId)],
    backgroundColor: gameChartColors[recentGamesHistory.value.games.findIndex(g => g.gameId === game.gameId)] + '20',
    fill: true,
    tension: 0.4,
    pointRadius: 3,
    pointHoverRadius: 5
  }))
  
  recentGamesChart = new Chart(ctx, {
    type: 'line',
    data: {
      labels: recentGamesHistory.value.dates,
      datasets
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'index',
        intersect: false
      },
      plugins: {
        legend: {
          display: false // 使用自定义图例
        },
        tooltip: {
          callbacks: {
            label: (context) => {
              const minutes = context.raw
              if (minutes < 60) return `${context.dataset.label}: ${minutes}分钟`
              const hours = Math.floor(minutes / 60)
              const mins = minutes % 60
              return `${context.dataset.label}: ${hours}小时${mins > 0 ? mins + '分钟' : ''}`
            }
          }
        }
      },
      scales: {
        x: {
          grid: { color: 'rgba(255,255,255,0.05)' },
          ticks: { color: 'rgba(255,255,255,0.6)' }
        },
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(255,255,255,0.05)' },
          ticks: {
            color: 'rgba(255,255,255,0.6)',
            callback: (value) => {
              if (value < 60) return value + '分'
              return Math.floor(value / 60) + '时'
            }
          }
        }
      }
    }
  })
}

const initCharts = () => {
  // 初始化类型分布饼图
  if (genreChartRef.value && gameLibrary.value.playtimeByGenre?.length > 0) {
    const ctx = genreChartRef.value.getContext('2d')
    
    if (genreChart) genreChart.destroy()

    const data = gameLibrary.value.playtimeByGenre.slice(0, 8)
    
    genreChart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: data.map(g => g.genre),
        datasets: [{
          data: data.map(g => g.playtimeMinutes),
          backgroundColor: chartColors.slice(0, data.length),
          borderWidth: 0
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '60%',
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (context) => {
                const minutes = context.raw
                const hours = Math.floor(minutes / 60)
                return `${context.label}: ${hours}小时`
              }
            }
          }
        }
      }
    })
  }

  // 初始化时长趋势折线图
  if (trendChartRef.value && gameLibrary.value.dailyPlaytimeTrend?.length > 0) {
    const ctx = trendChartRef.value.getContext('2d')
    
    if (trendChart) trendChart.destroy()

    const trendData = gameLibrary.value.dailyPlaytimeTrend
    
    trendChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: trendData.map(d => {
          const date = new Date(d.date)
          return `${date.getMonth() + 1}/${date.getDate()}`
        }),
        datasets: [{
          label: '游戏时长',
          data: trendData.map(d => d.playtimeMinutes),
          borderColor: '#6366f1',
          backgroundColor: 'rgba(99, 102, 241, 0.1)',
          fill: true,
          tension: 0.4,
          pointRadius: 4,
          pointHoverRadius: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (context) => {
                const minutes = context.raw
                const hours = Math.floor(minutes / 60)
                const mins = minutes % 60
                const dayData = trendData[context.dataIndex]
                let label = hours > 0 ? `${hours}小时${mins}分钟` : `${mins}分钟`
                if (dayData.gamesPlayed > 0) {
                  label += ` (${dayData.gamesPlayed}款游戏)`
                }
                return label
              }
            }
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: (value) => {
                const hours = Math.floor(value / 60)
                return hours > 0 ? `${hours}h` : `${value}m`
              }
            }
          }
        }
      }
    })
  }

  // 初始化周对比柱状图
  if (weeklyChartRef.value && gameLibrary.value.dailyPlaytimeTrend?.length >= 7) {
    const ctx = weeklyChartRef.value.getContext('2d')
    
    if (weeklyChart) weeklyChart.destroy()

    const trendData = gameLibrary.value.dailyPlaytimeTrend
    const weekdays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']
    
    // 按星期几分组统计
    const weekdayStats = Array(7).fill(0).map(() => ({ total: 0, count: 0 }))
    trendData.forEach(d => {
      const dayOfWeek = new Date(d.date).getDay()
      weekdayStats[dayOfWeek].total += d.playtimeMinutes
      weekdayStats[dayOfWeek].count++
    })
    
    const avgByWeekday = weekdayStats.map(s => s.count > 0 ? Math.round(s.total / s.count) : 0)
    // 重新排序：周一到周日
    const reorderedAvg = [...avgByWeekday.slice(1), avgByWeekday[0]]
    const reorderedLabels = [...weekdays.slice(1), weekdays[0]]
    
    weeklyChart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: reorderedLabels,
        datasets: [{
          label: '平均游戏时长',
          data: reorderedAvg,
          backgroundColor: reorderedAvg.map((v, i) => {
            const max = Math.max(...reorderedAvg)
            const intensity = max > 0 ? v / max : 0
            return `rgba(99, 102, 241, ${0.3 + intensity * 0.7})`
          }),
          borderColor: '#6366f1',
          borderWidth: 1,
          borderRadius: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (context) => {
                const minutes = context.raw
                return formatMinutes(minutes)
              }
            }
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: (value) => {
                const hours = Math.floor(value / 60)
                return hours > 0 ? `${hours}h` : `${value}m`
              }
            }
          }
        }
      }
    })
  }
}

// Report generation methods
const generateMonthlyReport = async (format) => {
  generating.value.monthly = true
  try {
    const url = getMonthlyReportUrl(format, monthlyYear.value, monthlyMonth.value)
    const filename = `monthly_report_${monthlyYear.value}_${String(monthlyMonth.value).padStart(2, '0')}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = `${monthlyYear.value}年${monthlyMonth.value}月 月度报告`
    saveReportToHistory('monthly', reportName, format, format === 'pdf' ? '~2.5 MB' : '~0.5 MB')
  } catch (err) {
    console.error('生成月度报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.monthly = false
  }
}

const generateYearlyReport = async (format) => {
  generating.value.yearly = true
  try {
    const url = getYearlyReportUrl(format, yearlyYear.value)
    const filename = `yearly_report_${yearlyYear.value}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = `${yearlyYear.value}年 年度总结`
    saveReportToHistory('yearly', reportName, format, format === 'pdf' ? '~1.8 MB' : '~0.3 MB')
  } catch (err) {
    console.error('生成年度报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.yearly = false
  }
}

const generateInventoryReport = async (format) => {
  generating.value.inventory = true
  try {
    const url = getInventoryReportUrl(format)
    const filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    // Save to history
    const reportName = '游戏库存报告'
    saveReportToHistory('inventory', reportName, format, format === 'pdf' ? '~1.2 MB' : '~0.4 MB')
  } catch (err) {
    console.error('生成库存报告失败:', err)
    alert('生成报告失败: ' + (err.message || '未知错误'))
  } finally {
    generating.value.inventory = false
  }
}

// Report history functions
const loadReportHistory = () => {
  loadingHistory.value = true
  try {
    const saved = localStorage.getItem(REPORTS_HISTORY_KEY)
    if (saved) {
      recentReports.value = JSON.parse(saved)
    }
  } catch (e) {
    console.warn('Failed to load report history:', e)
  } finally {
    loadingHistory.value = false
  }
}

const saveReportToHistory = (type, name, format, fileSize) => {
  const report = {
    id: Date.now(),
    type,
    name,
    format,
    fileSize,
    generatedAt: new Date().toISOString(),
    status: 'completed',
    url: null // We don't store the actual URL for security
  }
  
  recentReports.value.unshift(report)
  // Keep only last 10 reports
  if (recentReports.value.length > 10) {
    recentReports.value = recentReports.value.slice(0, 10)
  }
  
  try {
    localStorage.setItem(REPORTS_HISTORY_KEY, JSON.stringify(recentReports.value))
  } catch (e) {
    console.warn('Failed to save report history:', e)
  }
}

const deleteReportHistory = (id) => {
  if (!confirm('确定要删除这条记录吗？')) return
  
  recentReports.value = recentReports.value.filter(r => r.id !== id)
  try {
    localStorage.setItem(REPORTS_HISTORY_KEY, JSON.stringify(recentReports.value))
  } catch (e) {
    console.warn('Failed to save report history:', e)
  }
}

const redownloadReport = async (report) => {
  // Re-generate the report based on stored info
  try {
    let url, filename
    const format = report.format
    
    // Parse the report name to get parameters
    if (report.type === 'monthly') {
      const match = report.name.match(/(\d{4})年(\d{1,2})月/)
      if (match) {
        const year = parseInt(match[1])
        const month = parseInt(match[2])
        url = getMonthlyReportUrl(format, year, month)
        filename = `monthly_report_${year}_${String(month).padStart(2, '0')}.${format}`
      }
    } else if (report.type === 'yearly') {
      const match = report.name.match(/(\d{4})年/)
      if (match) {
        const year = parseInt(match[1])
        url = getYearlyReportUrl(format, year)
        filename = `yearly_report_${year}.${format}`
      }
    } else if (report.type === 'inventory') {
      url = getInventoryReportUrl(format)
      filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
    }
    
    if (url) {
      if (format === 'html') {
        await openHtmlReport(url)
      } else {
        await downloadReport(url, filename)
      }
    }
  } catch (err) {
    console.error('重新下载失败:', err)
    alert('下载失败: ' + (err.message || '未知错误'))
  }
}

const formatReportDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

const getStatusText = (status) => {
  const map = {
    completed: '已完成',
    generating: '生成中',
    failed: '失败'
  }
  return map[status] || status
}

const getReportTypeClass = (type) => {
  const map = {
    monthly: 'monthly',
    yearly: 'yearly',
    inventory: 'inventory'
  }
  return map[type] || 'monthly'
}

const getReportTypeIcon = (type) => {
  const map = {
    monthly: Calendar,
    yearly: Award,
    inventory: Package
  }
  return map[type] || FileText
}

// Period change handler (for future backend implementation)
// Export dialog handler
const handleExport = async () => {
  exporting.value = true
  try {
    let url, filename
    const format = exportForm.value.format
    
    switch (exportForm.value.type) {
      case 'monthly':
        url = getMonthlyReportUrl(format, exportForm.value.year, exportForm.value.month)
        filename = `monthly_report_${exportForm.value.year}_${String(exportForm.value.month).padStart(2, '0')}.${format}`
        break
      case 'yearly':
        url = getYearlyReportUrl(format, exportForm.value.year)
        filename = `yearly_report_${exportForm.value.year}.${format}`
        break
      case 'inventory':
        url = getInventoryReportUrl(format)
        filename = `inventory_report_${new Date().toISOString().slice(0, 10)}.${format}`
        break
    }
    
    if (format === 'html') {
      await openHtmlReport(url)
    } else {
      await downloadReport(url, filename)
    }
    
    showExportDialog.value = false
  } catch (err) {
    console.error('导出报表失败:', err)
    alert('导出失败: ' + (err.message || '未知错误'))
  } finally {
    exporting.value = false
  }
}

onMounted(() => {
  loadData()
  loadReportHistory()
  // 添加点击外部关闭选择器的事件监听
  document.addEventListener('click', closePickersOnClickOutside)
})

onUnmounted(() => {
  if (genreChart) genreChart.destroy()
  // 移除事件监听
  document.removeEventListener('click', closePickersOnClickOutside)
})
</script>

<style scoped>
.user-report-container {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.report-header {
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

.page-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-top: 4px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.refreshing-hint {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--primary-color);
  font-size: 12px;
}

.refreshing-hint .icon {
  width: 14px;
  height: 14px;
}

.last-update {
  font-size: 12px;
  color: var(--text-secondary);
  opacity: 0.7;
}

.btn-sync {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-sync:hover:not(:disabled) {
  background: var(--primary-hover);
}

.btn-sync:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-sync .icon {
  width: 16px;
  height: 16px;
}

.btn-sync .icon.spinning {
  animation: spin 1s linear infinite;
}

.period-select {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 10px 14px;
  color: white;
  font-size: 14px;
  cursor: pointer;
  outline: none;
}

.period-select:focus {
  border-color: var(--primary-color);
}

.period-select option {
  background: #1f1f23;
  color: white;
}

.btn-export {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
  border: 1px solid rgba(16, 185, 129, 0.3);
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-export:hover {
  background: rgba(16, 185, 129, 0.3);
  border-color: rgba(16, 185, 129, 0.5);
}

.btn-export .icon {
  width: 16px;
  height: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Loading & Error */
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

/* Stats Grid */.profile-stat .stat-label {
  font-size: 12px;
  color: var(--text-secondary);
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

/* Trend Section */
.trend-section {
  margin-bottom: 24px;
}

.trend-chart-container {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 20px;
  height: 280px;
}

.trend-summary {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-top: 16px;
}

.trend-stat {
  background: rgba(24, 24, 27, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  padding: 12px 16px;
  text-align: center;
}

.trend-stat-label {
  display: block;
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.trend-stat-value {
  display: block;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.trend-stat.trend-up .trend-stat-value {
  color: #10b981;
}

.trend-stat.trend-down .trend-stat-value {
  color: #ef4444;
}

.trend-stat.trend-stable .trend-stat-value {
  color: #6366f1;
}

/* Weekly Section */
.weekly-section {
  margin-bottom: 24px;
}

.weekly-chart-container {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 20px;
  height: 220px;
}

/* Heatmap Section */
.heatmap-section {
  margin-bottom: 24px;
}

.heatmap-container {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 20px;
}

.heatmap-container-inline {
  padding: 0;
}

/* Inline Heatmap Styles (for main-column) */
.heatmap-weekdays-inline {
  display: flex;
  gap: 6px;
  margin-bottom: 10px;
  justify-content: center;
}

.heatmap-weekdays-inline span {
  width: 40px;
  text-align: center;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
}

.heatmap-grid-inline {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  justify-content: center;
  max-width: calc(7 * 40px + 6 * 6px);
  margin: 0 auto;
}

.heatmap-cell-inline {
  width: 40px;
  height: 40px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: transform 0.2s;
  font-size: 12px;
}

.heatmap-cell-inline:hover {
  transform: scale(1.1);
}

.heatmap-cell-inline .heatmap-date {
  font-size: 12px;
  color: rgba(255, 255, 255, 0.7);
}

.heatmap-cell-inline.level-empty {
  background: transparent;
  pointer-events: none;
}

.heatmap-cell-inline.level-0 {
  background: rgba(255, 255, 255, 0.05);
}

.heatmap-cell-inline.level-1 {
  background: rgba(99, 102, 241, 0.2);
}

.heatmap-cell-inline.level-2 {
  background: rgba(99, 102, 241, 0.4);
}

.heatmap-cell-inline.level-3 {
  background: rgba(99, 102, 241, 0.6);
}

.heatmap-cell-inline.level-4 {
  background: rgba(99, 102, 241, 0.9);
}

.heatmap-legend-inline {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  margin-top: 14px;
}

.heatmap-legend-inline .legend-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.heatmap-legend-inline .legend-cell {
  width: 16px;
  height: 16px;
  border-radius: 3px;
}

.heatmap-weekdays {
  display: flex;
  gap: 4px;
  margin-bottom: 8px;
}

.heatmap-weekdays span {
  width: 32px;
  text-align: center;
  font-size: 12px;
  color: var(--text-secondary);
}

.heatmap-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  max-width: calc(7 * 32px + 6 * 4px); /* 7个格子 + 6个间隙 */
}

.heatmap-cell {
  width: 32px;
  height: 32px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: transform 0.2s;
}

.heatmap-cell:hover {
  transform: scale(1.1);
}

.heatmap-date {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.6);
}

.heatmap-cell.level-empty {
  background: transparent;
  pointer-events: none;
}

.heatmap-cell.level-0 {
  background: rgba(255, 255, 255, 0.05);
}

.heatmap-cell.level-1 {
  background: rgba(99, 102, 241, 0.2);
}

.heatmap-cell.level-2 {
  background: rgba(99, 102, 241, 0.4);
}

.heatmap-cell.level-3 {
  background: rgba(99, 102, 241, 0.6);
}

.heatmap-cell.level-4 {
  background: rgba(99, 102, 241, 0.9);
}

.heatmap-legend {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  margin-top: 12px;
}

.heatmap-legend .legend-label {
  font-size: 11px;
  color: var(--text-secondary);
  margin: 0 4px;
}

.heatmap-legend .legend-cell {
  width: 14px;
  height: 14px;
  border-radius: 2px;
}

.heatmap-legend .legend-cell.level-0 {
  background: rgba(255, 255, 255, 0.05);
}

.heatmap-legend .legend-cell.level-1 {
  background: rgba(99, 102, 241, 0.2);
}

.heatmap-legend .legend-cell.level-2 {
  background: rgba(99, 102, 241, 0.4);
}

.heatmap-legend .legend-cell.level-3 {
  background: rgba(99, 102, 241, 0.6);
}

.heatmap-legend .legend-cell.level-4 {
  background: rgba(99, 102, 241, 0.9);
}

/* Recent Analysis Section */
.recent-analysis-section {
  margin-bottom: 24px;
}

.recent-analysis-container {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 20px;
}

.recent-summary {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.summary-item {
  text-align: center;
}

.summary-value {
  display: block;
  font-size: 24px;
  font-weight: 700;
  color: var(--primary-color);
  margin-bottom: 4px;
}

.summary-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.recent-games-detail {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 16px;
}

.recent-game-card {
  display: flex;
  gap: 12px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 12px;
  padding: 12px;
  transition: all 0.2s ease;
  position: relative;
}

.recent-game-card:hover {
  background: rgba(255, 255, 255, 0.06);
  border-color: rgba(99, 102, 241, 0.3);
  transform: translateY(-2px);
}

.game-rank-badge {
  position: absolute;
  top: -8px;
  left: -8px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  background: rgba(100, 100, 100, 0.8);
  color: white;
}

.game-rank-badge.rank-gold {
  background: linear-gradient(135deg, #ffd700, #ffb700);
  color: #1a1a1a;
}

.game-rank-badge.rank-silver {
  background: linear-gradient(135deg, #c0c0c0, #a0a0a0);
  color: #1a1a1a;
}

.game-rank-badge.rank-bronze {
  background: linear-gradient(135deg, #cd7f32, #b87333);
  color: white;
}

.recent-game-card .game-cover {
  width: 80px;
  height: 80px;
  border-radius: 8px;
  object-fit: cover;
  flex-shrink: 0;
}

.game-details {
  flex: 1;
  min-width: 0;
}

.game-details .game-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 8px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.game-stats {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 8px;
}

.stat-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-secondary);
}

.stat-icon {
  font-size: 12px;
}

.playtime-bar-container {
  position: relative;
  height: 6px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  overflow: hidden;
}

.playtime-bar {
  height: 100%;
  background: linear-gradient(90deg, var(--primary-color), #8b5cf6);
  border-radius: 3px;
  transition: width 0.3s ease;
}

.playtime-percent {
  position: absolute;
  right: 0;
  top: -16px;
  font-size: 10px;
  color: var(--text-secondary);
}

@media (max-width: 768px) {
  .recent-summary {
    grid-template-columns: repeat(2, 1fr);
  }
  
  .recent-games-detail {
    grid-template-columns: 1fr;
  }
}

/* Recent Games Chart Section */
.recent-chart-section {
  margin-bottom: 24px;
}

.recent-chart-container {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 20px;
}

.recent-chart-container .chart-wrapper {
  height: 300px;
  margin-bottom: 16px;
}

.recent-chart-container .chart-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
  padding-top: 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.recent-chart-container .legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 20px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.recent-chart-container .legend-item:hover {
  background: rgba(255, 255, 255, 0.08);
}

.recent-chart-container .legend-item.legend-hidden {
  opacity: 0.4;
}

.recent-chart-container .legend-item.legend-hidden .legend-color {
  background: rgba(100, 100, 100, 0.5) !important;
}

.recent-chart-container .legend-color {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.recent-chart-container .legend-name {
  font-size: 12px;
  color: var(--text-primary);
  max-width: 120px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.recent-chart-container .legend-time {
  font-size: 11px;
  color: var(--text-secondary);
}

@media (max-width: 768px) {
  .recent-chart-container .chart-wrapper {
    height: 250px;
  }
  
  .recent-chart-container .legend-item {
    padding: 4px 8px;
  }
  
  .recent-chart-container .legend-name {
    max-width: 80px;
  }
}

/* Platform Stats Section */
.platform-stats-section {
  margin-bottom: 24px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.platform-stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.platform-stat-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 16px;
}

.platform-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.platform-icon {
  font-size: 20px;
}

.platform-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.platform-stats {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.platform-stat {
  text-align: center;
}

.platform-stat-value {
  display: block;
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
}

.platform-stat-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.platform-bar {
  height: 4px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
  overflow: hidden;
}

.platform-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #818cf8, #6366f1);
  border-radius: 2px;
  transition: width 0.3s ease;
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
.stat-icon.cyan { background: rgba(6, 182, 212, 0.2); color: #22d3ee; }
.stat-icon.purple { background: rgba(139, 92, 246, 0.2); color: #a78bfa; }
.stat-icon.blue { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }

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

.stat-desc {
  font-size: 12px;
  color: var(--text-secondary);
}

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

.chart-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.chart-container {
  height: 250px;
  margin-bottom: 16px;
}

/* Genre Legend */
.genre-legend {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.legend-color {
  width: 12px;
  height: 12px;
  border-radius: 3px;
}

.legend-name {
  flex: 1;
  font-size: 12px;
  color: var(--text-secondary);
}

.legend-value {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

/* Games List */
.games-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.btn-show-more {
  width: 100%;
  padding: 10px;
  margin-top: 8px;
  background: rgba(99, 102, 241, 0.1);
  border: 1px dashed rgba(99, 102, 241, 0.3);
  border-radius: 8px;
  color: var(--primary-color);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-show-more:hover {
  background: rgba(99, 102, 241, 0.2);
  border-color: rgba(99, 102, 241, 0.5);
}

.game-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-radius: 12px;
  transition: background 0.2s;
}

.game-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.game-item.clickable {
  cursor: pointer;
}

.game-item.clickable:hover {
  background: rgba(99, 102, 241, 0.15);
}

.game-rank {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.game-rank.gold { background: linear-gradient(135deg, #fbbf24, #f59e0b); color: white; }
.game-rank.silver { background: linear-gradient(135deg, #9ca3af, #6b7280); color: white; }
.game-rank.bronze { background: linear-gradient(135deg, #d97706, #b45309); color: white; }

.game-image {
  width: 80px;
  height: 38px;
  border-radius: 4px;
  object-fit: cover;
}

.game-info {
  flex: 1;
}

.game-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.game-meta {
  font-size: 12px;
  color: var(--text-secondary);
}

.game-playtime {
  text-align: right;
}

.playtime-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--primary-color);
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

.ring-value {
  display: block;
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
}

.ring-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.achievement-stats {
  text-align: center;
  font-size: 14px;
  color: var(--text-secondary);
}

.perfect-games {
  color: #fbbf24;
  margin-top: 8px;
}

/* Recent List */
.recent-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.recent-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border-radius: 8px;
  transition: background 0.2s;
}

.recent-item.clickable {
  cursor: pointer;
}

.recent-item.clickable:hover {
  background: rgba(99, 102, 241, 0.15);
}

.recent-image {
  width: 60px;
  height: 28px;
  border-radius: 4px;
  object-fit: cover;
}

.recent-info {
  flex: 1;
}

.recent-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
}

.recent-time {
  font-size: 11px;
  color: var(--text-secondary);
}

/* Progress List */
.progress-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.progress-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.progress-header {
  display: flex;
  justify-content: space-between;
}

.progress-name {
  font-size: 13px;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 150px;
}

.progress-percent {
  font-size: 13px;
  font-weight: 600;
  color: var(--primary-color);
}

.progress-bar-bg {
  height: 6px;
  background: #27272a;
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.progress-detail {
  font-size: 11px;
  color: var(--text-secondary);
}

/* Genre Stats */
.genre-stats-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.genre-stat-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.genre-stat-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.genre-stat-color {
  width: 10px;
  height: 10px;
  border-radius: 3px;
}

.genre-stat-name {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
}

.genre-stat-count {
  font-size: 12px;
  color: var(--text-secondary);
}

.genre-stat-bar-bg {
  height: 6px;
  background: #27272a;
  border-radius: 3px;
  overflow: hidden;
}

.genre-stat-bar-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.genre-stat-details {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-secondary);
}

/* Recent Reports Section */
.recent-reports-section {
  margin-top: 32px;
}

.recent-reports-section .section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.btn-refresh-reports {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-refresh-reports:hover {
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-primary);
}

.btn-refresh-reports .icon {
  width: 14px;
  height: 14px;
}

.reports-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.report-history-item {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(24, 24, 27, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 16px;
  transition: all 0.2s;
}

.report-history-item:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.report-history-icon {
  width: 44px;
  height: 44px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.report-history-icon .icon {
  width: 22px;
  height: 22px;
}

.report-history-icon.monthly {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.report-history-icon.yearly {
  background: rgba(245, 158, 11, 0.2);
  color: #fcd34d;
}

.report-history-icon.inventory {
  background: rgba(16, 185, 129, 0.2);
  color: #6ee7b7;
}

.report-history-info {
  flex: 1;
  min-width: 0;
}

.report-history-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.report-history-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}

.report-status {
  font-weight: 500;
}

.report-status.completed {
  color: #34d399;
}

.report-status.generating {
  color: #60a5fa;
}

.report-status.failed {
  color: #f87171;
}

.report-history-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.btn-action {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-action .icon {
  width: 14px;
  height: 14px;
}

.btn-action.download {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
}

.btn-action.download:hover {
  background: rgba(99, 102, 241, 0.3);
}

.btn-action.delete {
  background: rgba(239, 68, 68, 0.15);
  color: #f87171;
}

.btn-action.delete:hover {
  background: rgba(239, 68, 68, 0.25);
}

/* Responsive */
@media (max-width: 1200px) {
  .stats-grid {
    grid-template-columns: repeat(3, 1fr);
  }
  
  .content-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  
  .trend-chart-container {
    height: 220px;
  }
}

/* Reports Section */
.reports-section {
  margin-top: 32px;
}

.reports-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
}

.report-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: all 0.2s;
}

.report-card:hover {
  border-color: rgba(99, 102, 241, 0.3);
}

.report-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.report-icon .icon {
  width: 28px;
  height: 28px;
}

.report-icon.monthly {
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.3), rgba(139, 92, 246, 0.2));
  color: #a5b4fc;
}

.report-icon.yearly {
  background: linear-gradient(135deg, rgba(245, 158, 11, 0.3), rgba(234, 88, 12, 0.2));
  color: #fcd34d;
}

.report-icon.inventory {
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.3), rgba(5, 150, 105, 0.2));
  color: #6ee7b7;
}

.report-info {
  flex: 1;
}

.report-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 6px;
}

.report-desc {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 12px;
}

.report-options {
  margin-top: 8px;
  position: relative;
}

/* 月份选择器样式 */
.month-picker,
.year-picker {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 14px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s ease;
  user-select: none;
}

.month-picker:hover,
.year-picker:hover {
  background: rgba(255, 255, 255, 0.1);
  border-color: var(--primary-color);
}

.picker-icon {
  width: 16px;
  height: 16px;
  color: var(--primary-color);
}

.picker-value {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.picker-arrow {
  width: 14px;
  height: 14px;
  color: var(--text-secondary);
  transition: transform 0.2s ease;
}

.picker-arrow.rotate {
  transform: rotate(180deg);
}

/* 月份选择弹窗 */
.month-picker-dropdown,
.year-picker-dropdown {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  z-index: 100;
  background: #1f1f23;
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  overflow: hidden;
  animation: dropdownFadeIn 0.2s ease;
}

@keyframes dropdownFadeIn {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.picker-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: rgba(255, 255, 255, 0.03);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.picker-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: transparent;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.picker-nav:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.1);
}

.picker-nav:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.nav-icon {
  width: 18px;
  height: 18px;
  color: var(--text-primary);
}

.picker-year {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.picker-months {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
  padding: 12px;
}

.picker-month {
  padding: 10px 8px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 8px;
  font-size: 13px;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.15s ease;
}

.picker-month:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.15);
}

.picker-month.selected {
  background: var(--primary-color);
  color: white;
  font-weight: 500;
}

.picker-month.disabled {
  opacity: 0.3;
  cursor: not-allowed;
  text-decoration: line-through;
}

/* 年份选择弹窗 */
.picker-years {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 6px;
  padding: 12px;
  min-width: 180px;
}

.picker-year-btn {
  padding: 12px 16px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 8px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.15s ease;
}

.picker-year-btn:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.15);
}

.picker-year-btn.selected {
  background: var(--primary-color);
  color: white;
  font-weight: 500;
}

.date-selector {
  display: flex;
  gap: 8px;
}

.date-select {
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 13px;
  cursor: pointer;
  outline: none;
}

.date-select:focus {
  border-color: var(--primary-color);
}

.date-select option {
  background: #1f1f23;
  color: var(--text-primary);
}

.report-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.btn-report {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-report:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-report .btn-icon {
  width: 16px;
  height: 16px;
}

.btn-report.pdf {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
}

.btn-report.pdf:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.3);
}

.btn-report.csv {
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
}

.btn-report.csv:hover:not(:disabled) {
  background: rgba(16, 185, 129, 0.3);
}

.btn-report.html {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
}

.btn-report.html:hover:not(:disabled) {
  background: rgba(99, 102, 241, 0.3);
}

@media (max-width: 1200px) {
  .reports-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .reports-grid {
    grid-template-columns: 1fr;
  }
}

/* Dialog Styles */
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
  z-index: 1000;
}

.dialog-content {
  background: #1f1f23;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 24px;
  width: 100%;
  max-width: 420px;
}

.dialog-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.dialog-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 24px;
}

.dialog-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-bottom: 24px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.form-select {
  padding: 10px 14px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 14px;
  cursor: pointer;
  outline: none;
}

.form-select:focus {
  border-color: var(--primary-color);
}

.form-select option {
  background: #1f1f23;
  color: var(--text-primary);
}

.date-row {
  display: flex;
  gap: 12px;
}

.date-row .form-select {
  flex: 1;
}

.dialog-picker-wrapper {
  position: relative;
}

.dialog-picker {
  width: 100%;
  justify-content: flex-start;
  padding: 10px 14px;
}

.dialog-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 4px;
  z-index: 1001;
}

.picker-years {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  padding: 12px;
}

.picker-year-btn {
  padding: 10px;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.picker-year-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.picker-year-btn.selected {
  background: var(--primary-color);
  border-color: var(--primary-color);
  color: white;
}

.format-options {
  display: flex;
  gap: 16px;
}

.format-option {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.format-option input[type="radio"] {
  width: 18px;
  height: 18px;
  accent-color: var(--primary-color);
}

.format-label {
  font-size: 14px;
  color: var(--text-primary);
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.btn-cancel {
  padding: 10px 20px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-cancel:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-confirm {
  padding: 10px 20px;
  background: var(--primary-color);
  border: none;
  border-radius: 8px;
  color: white;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-confirm:hover:not(:disabled) {
  background: var(--primary-hover);
}

.btn-confirm:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
