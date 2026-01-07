<template>
  <div class="app-container">
    <!-- 导航栏 -->
    <nav class="navbar">
      <div class="navbar-container">
        <div class="navbar-logo">
          <div class="logo-icon">
            <Gamepad2 class="lucide-icon" />
          </div>
          <span class="logo-text">PlayLinker</span>
        </div>
        <div class="navbar-links">
          <a href="#about" class="nav-link">项目介绍</a>
          <a href="#stats" class="nav-link">数据分析</a>
          <a href="#features" class="nav-link">功能模块</a>
          <a href="#personas" class="nav-link">用户画像</a>
        </div>
        <router-link to="/login" class="sign-in-btn">登录</router-link>
      </div>
    </nav>

    <!-- 英雄区 -->
    <header class="hero-section">
      <div class="hero-bg-blur"></div>
      <div class="hero-container">
        <div class="hero-badge">
          <span class="badge-dot"></span>
          2025 项目展示
        </div>
        <h1 class="hero-title">
          你的全部游戏库，<br />
          <span class="title-highlight">一个统一的管理中心。</span>
        </h1>
        <p class="hero-desc">
          告别频繁切换游戏启动器的烦恼。将 Steam、Epic、GOG 和 Xbox 整合到一个功能强大的
          管理面板中，提供高级数据分析、价格追踪和社区分享功能。
        </p>
        <div class="hero-buttons">
          <router-link to="/login" class="primary-btn">
            立即开始
            <ArrowRight class="lucide-icon" />
          </router-link>
          <a href="#about" class="secondary-btn">
            查看文档
          </a>
        </div>
      </div>
    </header>

    <!-- 问题与解决方案 -->
    <section id="about" class="about-section">
      <div class="about-container">
        <div class="problem-section">
          <h3 class="section-subtitle">面临的挑战</h3>
          <h2 class="section-title">碎片化是最大的敌人</h2>
          <p class="section-desc">
            如今的游戏玩家深受启动器疲劳的困扰。成就、存档和好友列表分散在
            Steam、Epic、GOG 和 Xbox 等多个平台。
          </p>
          <ul class="problem-list">
            <li class="problem-item">
              <span class="item-dot red"></span>
              多个平台账号管理成本高
            </li>
            <li class="problem-item">
              <span class="item-dot red"></span>
              游戏收藏分享和展示困难
            </li>
            <li class="problem-item">
              <span class="item-dot red"></span>
              缺乏面向媒体和社区管理者的统一工具
            </li>
          </ul>
        </div>
        
        <div class="solution-card glass-panel">
          <div class="card-bg-blur"></div>
          <h3 class="section-subtitle">我们的解决方案</h3>
          <h2 class="section-title">统一且安全</h2>
          <p class="section-desc">
            深度集成的聚合平台，优先使用官方 API 确保安全性，
            同时为封闭系统提供灵活的手动导入功能。
          </p>
          <div class="solution-grid">
            <div class="solution-item">
              <Globe class="lucide-icon" />
              <div class="item-title">多平台支持</div>
              <div class="item-subtitle">Steam、Epic 等全兼容</div>
            </div>
            <div class="solution-item">
              <ShieldCheck class="lucide-icon" />
              <div class="item-title">官方 API</div>
              <div class="item-subtitle">100% 合规安全</div>
            </div>
            <div class="solution-item">
              <BarChart3 class="lucide-icon" />
              <div class="item-title">可视化统计</div>
              <div class="item-subtitle">深度数据分析</div>
            </div>
            <div class="solution-item">
              <Zap class="lucide-icon" />
              <div class="item-title">实时监控</div>
              <div class="item-subtitle">价格提醒功能</div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 数据可视化区域 -->
    <section id="stats" class="stats-section">
      <div class="stats-container">
        <div class="stats-header">
          <h2 class="section-title gradient-text">数据可视化展示</h2>
          <p class="section-desc">
            通过数据面板直观展示你的游戏习惯。一站式查看游戏时长、平台分布和成就进度。
          </p>
        </div>

        <div class="stats-grid">
          <!-- 左侧：统计卡片 & 饼图 -->
          <div class="stats-left">
            <div class="stats-cards">
              <div class="stat-card glass-panel">
                <div class="card-label">游戏总数</div>
                <div class="card-value">695</div>
                <div class="card-change positive">本月新增 12 款</div>
              </div>
              <div class="stat-card glass-panel">
                <div class="card-label">游戏总时长</div>
                <div class="card-value">4,208小时</div>
                <div class="card-change positive">玩家前 1% 水平</div>
              </div>
            </div>
            
            <div class="chart-card glass-panel">
              <h4 class="chart-title">游戏库平台分布</h4>
              <div class="chart-container">
                <canvas ref="platformChart"></canvas>
              </div>
              <div class="chart-legend">
                <div class="legend-item">
                  <span class="legend-dot blue"></span> Steam
                </div>
                <div class="legend-item">
                  <span class="legend-dot black"></span> Epic
                </div>
                <div class="legend-item">
                  <span class="legend-dot green"></span> Xbox
                </div>
              </div>
            </div>
          </div>

          <!-- 右侧：主图表 -->
          <div class="stats-right glass-panel">
            <div class="chart-header">
              <h4 class="chart-title">每周游戏时长趋势</h4>
              <button class="export-btn">导出 PDF</button>
            </div>
            <div class="chart-container large">
              <canvas ref="activityChart"></canvas>
            </div>
            <div class="alert-box">
              <Bell class="lucide-icon" />
              <div class="alert-content">
                <span class="alert-title">每周目标提醒</span>
                你已完成本周"解锁 2 项成就"目标的 50%。继续加油！
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 功能模块区域 -->
    <section id="features" class="features-section">
      <div class="features-container">
        <div class="features-header">
          <h3 class="section-subtitle">核心能力</h3>
          <h2 class="section-title gradient-text">强大的功能模块</h2>
          <p class="section-desc">
            专为游戏玩家打造的全方位功能体系，满足从游戏管理到社交分享的所有需求
          </p>
        </div>

        <div class="features-grid">
          <!-- 功能卡片 1：游戏库管理 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon blue">
              <Library class="lucide-icon" />
            </div>
            <h3 class="feature-title">游戏库管理</h3>
            <p class="feature-desc">
              集中管理所有平台游戏，支持批量导入、分类整理、标签自定义，让游戏收藏井井有条
            </p>
            <ul class="feature-list">
              <li>自动同步各平台游戏库</li>
              <li>查看游戏分类与标签</li>
              <li>游戏排行与评分</li>
              <li>游戏详情与mod快速查看</li>
            </ul>
          </div>

          <!-- 功能卡片 2：价格追踪 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon green">
              <DollarSign class="lucide-icon" />
            </div>
            <h3 class="feature-title">价格追踪</h3>
            <p class="feature-desc">
              实时监控游戏价格变动，设置折扣阈值自动提醒，再也不错过心仪游戏的促销
            </p>
            <ul class="feature-list">
            
              <li>自定义折扣提醒阈值</li>
              <li>历史价格走势分析</li>
              <li>愿望单智能管理</li>
            </ul>
          </div>

          <!-- 功能卡片 3：成就系统 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon purple">
              <Trophy class="lucide-icon" />
            </div>
            <h3 class="feature-title">成就管理</h3>
            <p class="feature-desc">
              聚合所有平台游戏成就，智能分析完成进度，制定个性化成就解锁计划
            </p>
            <ul class="feature-list">
              <li>跨平台成就统一展示</li>
              <li>成就完成率分析</li>
              <li>难度评级与解锁攻略</li>
              <li>成就分享到社交平台</li>
            </ul>
          </div>

          <!-- 功能卡片 4：存档 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon pink">
              <Archive class="lucide-icon" />
            </div>
            <h3 class="feature-title">存档</h3>
            <p class="feature-desc">
              轻松整理与管理游戏存档，支持备份与迁移，确保关键进度安全无忧
            </p>
            <ul class="feature-list">
              <li>本地与云端存档备份</li>
              <li>跨设备存档迁移</li>
              <li>存档版本管理与恢复</li>
              <li>存档占用与状态概览</li>
            </ul>
          </div>

          <!-- 功能卡片 5：家长控制 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon orange">
              <Shield class="lucide-icon" />
            </div>
            <h3 class="feature-title">家长控制</h3>
            <p class="feature-desc">
              为青少年玩家提供健康的游戏管理方案，平衡游戏娱乐与学习生活
            </p>
            <ul class="feature-list">
              <li>每日/每周游戏时长限制</li>
              <li>游戏内容分级筛选</li>
              <li>游戏活动周报推送</li>
            </ul>
          </div>

          <!-- 功能卡片 6：数据导出 -->
          <div class="feature-card glass-panel">
            <div class="feature-icon teal">
              <Download class="lucide-icon" />
            </div>
            <h3 class="feature-title">数据导出</h3>
            <p class="feature-desc">
              支持多种格式导出游戏数据，满足媒体创作、数据分析等专业需求
            </p>
            <ul class="feature-list">
              <li>PDF/html 格式导出</li>
              <li>自定义导出数据维度</li>
              <li>可视化图表导出</li>
            </ul>
          </div>
        </div>

      </div>
    </section>

    <!-- 用户画像区域 -->
    <section id="personas" class="personas-section">
      <div class="personas-bg-blur"></div>
      <div class="personas-container">
        <div class="personas-header">
          <h2 class="section-title gradient-text">为各类用户量身打造</h2>
          <p class="section-desc">根据不同用户需求提供定制化体验。</p>
        </div>

        <div class="tab-buttons">
          <button 
            class="tab-btn" 
            :class="{ active: activeTab === 'gamer' }"
            @click="switchTab('gamer')"
          >
            普通游戏玩家
          </button>
          <button 
            class="tab-btn" 
            :class="{ active: activeTab === 'media' }"
            @click="switchTab('media')"
          >
            媒体与社区运营
          </button>
          <button 
            class="tab-btn" 
            :class="{ active: activeTab === 'parent' }"
            @click="switchTab('parent')"
          >
            家长与监护人
          </button>
        </div>

        <div class="tab-content glass-panel">
          <!-- 普通玩家 -->
          <div v-if="activeTab === 'gamer'" class="tab-panel fade-in">
            <div class="panel-header">
              <div class="panel-icon indigo">
                <Gamepad2 class="lucide-icon" />
              </div>
              <div class="panel-info">
                <h3 class="panel-title">核心游戏玩家</h3>
                <p class="panel-subtitle">需要游戏管理和优惠信息</p>
              </div>
            </div>
            <ul class="panel-list">
              <li class="panel-item">
                <span class="item-emoji">🎯</span>
                <div class="item-content">
                  <h4 class="item-title">统一视图</h4>
                  <p class="item-desc">"我希望能同时查看 Steam 和 Epic 游戏库，方便选择接下来要玩的游戏。"</p>
                </div>
              </li>
              <li class="panel-item">
                <span class="item-emoji">💰</span>
                <div class="item-content">
                  <h4 class="item-title">价格提醒</h4>
                  <p class="item-desc">"为《赛博朋克 2077》设置 30% 折扣阈值，并在手机上收到提醒。"</p>
                </div>
              </li>
            </ul>
          </div>

          <!-- 媒体创作者 -->
          <div v-if="activeTab === 'media'" class="tab-panel fade-in">
            <div class="panel-header">
              <div class="panel-icon emerald">
                <Share2 class="lucide-icon" />
              </div>
              <div class="panel-info">
                <h3 class="panel-title">媒体创作者</h3>
                <p class="panel-subtitle">需要数据统计和内容创作支持</p>
              </div>
            </div>
            <ul class="panel-list">
              <li class="panel-item">
                <span class="item-emoji">📊</span>
                <div class="item-content">
                  <h4 class="item-title">年度报告</h4>
                  <p class="item-desc">"我需要导出年度游戏时长趋势 PDF，用于撰写'年度游戏回顾'文章。"</p>
                </div>
              </li>
              <li class="panel-item">
                <span class="item-emoji">🤝</span>
                <div class="item-content">
                  <h4 class="item-title">社区数据</h4>
                  <p class="item-desc">"轻松分享游戏库统计数据，与我的 Discord 社区互动。"</p>
                </div>
              </li>
            </ul>
          </div>

          <!-- 家长监护 -->
          <div v-if="activeTab === 'parent'" class="tab-panel fade-in">
            <div class="panel-header">
              <div class="panel-icon rose">
                <ShieldAlert class="lucide-icon" />
              </div>
              <div class="panel-info">
                <h3 class="panel-title">家长与监护人</h3>
                <p class="panel-subtitle">需要游戏监管和时间管理</p>
              </div>
            </div>
            <ul class="panel-list">
              <li class="panel-item">
                <span class="item-emoji">⏰</span>
                <div class="item-content">
                  <h4 class="item-title">游戏时长限制</h4>
                  <p class="item-desc">"设置每日游戏时长限制（如工作日 1 小时），确保游戏不影响功课。"</p>
                </div>
              </li>
              <li class="panel-item">
                <span class="item-emoji">📩</span>
                <div class="item-content">
                  <h4 class="item-title">孩子活动摘要</h4>
                  <p class="item-desc">"接收电子邮件摘要，了解孩子玩了哪些游戏以及时长。"</p>
                </div>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </section>

    <!-- 页脚 -->
    <footer class="footer">
      <div class="footer-container">
        <div class="footer-logo">
          <Gamepad2 class="lucide-icon" />
          <span class="logo-text">PlayLinker</span>
        </div>
        <div class="copyright">
          © 2025 Playlinker项目 版权所有
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import Chart from 'chart.js/auto';
// 引入需要的 Lucide 图标组件
import { 
  Gamepad2, ArrowRight, Globe, ShieldCheck, BarChart3, Zap,
  Library, DollarSign, Trophy, Archive, Shield, Download, 
  Bell, Share2, ShieldAlert
} from 'lucide-vue-next';

// 响应式数据
const activeTab = ref('gamer');
const platformChart = ref(null);
const activityChart = ref(null);

// 切换标签页方法
const switchTab = (tabName) => {
  activeTab.value = tabName;
};

// 初始化图表
onMounted(() => {
  // 初始化滚动平滑
  document.documentElement.classList.add('scroll-smooth');

  // 移除原来的 lucide 初始化代码（使用组件形式无需此步骤）

  // 配置 Chart.js 全局样式
  Chart.defaults.color = '#71717a';
  Chart.defaults.borderColor = '#27272a';

  // 初始化平台分布饼图
  if (platformChart.value) {
    new Chart(platformChart.value, {
      type: 'doughnut',
      data: {
        labels: ['Steam', 'Epic', 'Xbox', 'GOG'],
        datasets: [{
          data: [450, 120, 80, 45],
          backgroundColor: ['#3b82f6', '#333333', '#107c10', '#8c429e'],
          borderWidth: 0,
          hoverOffset: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '70%',
        plugins: { legend: { display: false } }
      }
    });
  }

  // 初始化周活动柱状图
  if (activityChart.value) {
    new Chart(activityChart.value, {
      type: 'bar',
      data: {
        labels: ['周一', '周二', '周三', '周四', '周五', '周六', '周日'],
        datasets: [{
          label: '游戏时长(小时)',
          data: [2.5, 1.0, 4.0, 0.5, 5.5, 8.0, 6.5],
          backgroundColor: '#6366f1',
          borderRadius: 4,
          barThickness: 30,
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          y: {
            beginAtZero: true,
            grid: { display: true, drawBorder: false },
            ticks: { 
              callback: function(value) { 
                return value + '小时'; 
              } 
            }
          },
          x: { grid: { display: false, drawBorder: false } }
        },
        plugins: { legend: { display: false } }
      }
    });
  }
});
</script>


<style scoped>
/* 全局基础样式 */
.app-container {
  background-color: #09090b;
  color: #ffffff;
  font-family: 'Inter', system-ui, sans-serif;
  min-height: 100vh;
}

/* 滚动条样式 */
::-webkit-scrollbar { width: 8px; }
::-webkit-scrollbar-track { background: #09090b; }
::-webkit-scrollbar-thumb { background: #27272a; border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: #3f3f46; }

/* 通用样式 */
.glass-panel {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 24px;
}

.gradient-text {
  background: linear-gradient(to bottom, #ffffff, #71717a);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.fade-in {
  animation: fadeIn 0.5s ease-out forwards;
}

@keyframes fadeIn {
  0% { opacity: 0; transform: translateY(10px); }
  100% { opacity: 1; transform: translateY(0); }
}

/* 导航栏样式 */
.navbar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 50;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(9, 9, 11, 0.8);
  backdrop-filter: blur(12px);
  height: 64px;
  display: flex;
  align-items: center;
}

.navbar-container {
  max-width: 1152px;
  margin: 0 auto;
  padding: 0 24px;
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.navbar-logo {
  display: flex;
  align-items: center;
  gap: 8px;
}

.logo-icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background-color: #4f46e5;
  display: flex;
  align-items: center;
  justify-content: center;
}

.logo-text {
  font-weight: bold;
  font-size: 18px;
  letter-spacing: -0.02em;
}

.navbar-links {
  display: none;
  align-items: center;
  gap: 32px;
  font-size: 14px;
  font-weight: 500;
  color: #a1a1aa;
}

.nav-link {
  color: #a1a1aa;
  text-decoration: none;
  transition: color 0.2s;
}

.nav-link:hover {
  color: #ffffff;
}

.sign-in-btn {
  background-color: #ffffff;
  color: #000000;
  padding: 8px 16px;
  border-radius: 9999px;
  font-size: 14px;
  font-weight: 600;
  text-decoration: none;
  transition: background-color 0.2s;
}

.sign-in-btn:hover {
  background-color: #e4e4e7;
}

/* Hero 区域样式 */
.hero-section {
  padding: 128px 24px 96px;
  position: relative;
  overflow: hidden;
  text-align: center;
}

.hero-bg-blur {
  position: absolute;
  top: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 1000px;
  height: 500px;
  background: rgba(99, 102, 241, 0.2);
  border-radius: 50%;
  filter: blur(120px);
  z-index: -1;
  opacity: 0.5;
}

.hero-container {
  max-width: 800px;
  margin: 0 auto;
}

.hero-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px;
  border-radius: 9999px;
  border: 1px solid rgba(99, 102, 241, 0.3);
  background: rgba(99, 102, 241, 0.1);
  color: #a5b4fc;
  font-size: 12px;
  font-weight: 500;
  margin-bottom: 32px;
}

.badge-dot {
  position: relative;
  width: 8px;
  height: 8px;
}

.badge-dot::before {
  content: '';
  position: absolute;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background-color: #818cf8;
  opacity: 0.75;
  animation: ping 1s cubic-bezier(0, 0, 0.2, 1) infinite;
}

.badge-dot::after {
  content: '';
  position: absolute;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background-color: #6366f1;
}

@keyframes ping {
  75%, 100% {
    transform: scale(2);
    opacity: 0;
  }
}

.hero-title {
  font-size: 48px;
  font-weight: bold;
  letter-spacing: -0.02em;
  margin-bottom: 32px;
  line-height: 1.2;
}

.title-highlight {
  color: #6366f1;
  -webkit-text-fill-color: #6366f1;
}

.hero-desc {
  font-size: 18px;
  color: #a1a1aa;
  max-width: 640px;
  margin: 0 auto 40px;
  line-height: 1.5;
}

.hero-buttons {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
}

.primary-btn {
  height: 48px;
  padding: 0 32px;
  border-radius: 9999px;
  background-color: #4f46e5;
  color: #ffffff;
  font-weight: 500;
  text-decoration: none;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: background-color 0.2s;
}

.primary-btn:hover {
  background-color: #6366f1;
}

.secondary-btn {
  height: 48px;
  padding: 0 32px;
  border-radius: 9999px;
  background-color: #18181b;
  border: 1px solid #3f3f46;
  color: #a1a1aa;
  font-weight: 500;
  text-decoration: none;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.secondary-btn:hover {
  background-color: #27272a;
  color: #e4e4e7;
}

/* About 区域样式 */
.about-section {
  padding: 96px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(10, 10, 11, 0.5);
}

.about-container {
  max-width: 1152px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 64px;
}

.section-subtitle {
  color: #a5b4fc;
  font-weight: 600;
  margin-bottom: 8px;
}

.section-title {
  font-size: 32px;
  font-weight: bold;
  margin-bottom: 24px;
  color: #ffffff;
}

.section-desc {
  color: #a1a1aa;
  line-height: 1.6;
  margin-bottom: 24px;
}

.problem-list {
  margin-top: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.problem-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  color: #a1a1aa;
}

.item-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
  margin-top: 8px;
}

.item-dot.red {
  background-color: #ef4444;
}

.solution-card {
  padding: 32px;
  position: relative;
  overflow: hidden;
}

.card-bg-blur {
  position: absolute;
  top: 0;
  right: 0;
  width: 256px;
  height: 256px;
  background: rgba(99, 102, 241, 0.1);
  border-radius: 50%;
  filter: blur(72px);
  z-index: -1;
}

.solution-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.solution-item {
  background: rgba(24, 24, 27, 0.5);
  padding: 16px;
  border-radius: 16px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.solution-item .lucide-icon {
  width: 24px;
  height: 24px;
  color: #ffffff;
  margin-bottom: 8px;
}

.item-title {
  font-size: 14px;
  font-weight: 600;
  color: #ffffff;
  margin-bottom: 4px;
}

.item-subtitle {
  font-size: 12px;
  color: #71717a;
}

/* Stats 区域样式 */
.stats-section {
  padding: 96px 24px;
  position: relative;
}

.stats-container {
  max-width: 1152px;
  margin: 0 auto;
}

.stats-header {
  text-align: center;
  margin-bottom: 48px;
}

.stats-grid {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-left {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-cards {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.stat-card {
  padding: 24px;
  border-radius: 16px;
}

.card-label {
  font-size: 14px;
  color: #71717a;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 8px;
}

.card-value {
  font-size: 24px;
  font-weight: bold;
  color: #ffffff;
  margin-bottom: 4px;
}

.card-change {
  font-size: 12px;
  font-family: monospace;
}

.card-change.positive {
  color: #4ade80;
}

.chart-card {
  padding: 24px;
  border-radius: 16px;
  height: 340px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.chart-title {
  font-size: 18px;
  font-weight: 600;
  color: #e4e4e7;
  align-self: flex-start;
  margin-bottom: 16px;
}

.chart-container {
  position: relative;
  width: 192px;
  height: 192px;
}

.chart-container.large {
  width: 100%;
  min-height: 300px;
}

.chart-legend {
  display: flex;
  justify-content: center;
  gap: 16px;
  font-size: 12px;
  color: #71717a;
  margin-top: 24px;
  width: 100%;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.legend-dot.blue {
  background-color: #3b82f6;
}

.legend-dot.black {
  background-color: #333333;
}

.legend-dot.green {
  background-color: #107c10;
}

.stats-right {
  padding: 24px;
  border-radius: 16px;
  display: flex;
  flex-direction: column;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.export-btn {
  font-size: 12px;
  background-color: #27272a;
  color: #ffffff;
  padding: 6px 12px;
  border-radius: 8px;
  border: none;
  cursor: pointer;
  transition: background-color 0.2s;
}

.export-btn:hover {
  background-color: #3f3f46;
}

.alert-box {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  border-radius: 16px;
  background: rgba(79, 70, 229, 0.2);
  border: 1px solid rgba(99, 102, 241, 0.2);
  color: #c7d2fe;
  font-size: 14px;
  margin-top: 16px;
}

.alert-box .lucide-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.alert-title {
  font-weight: 600;
  display: block;
  margin-bottom: 4px;
}

/* 功能模块区域样式 */
.features-section {
  padding: 96px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  background: rgba(10, 10, 11, 0.5);
  position: relative;
}

.features-container {
  max-width: 1152px;
  margin: 0 auto;
}

.features-header {
  text-align: center;
  margin-bottom: 64px;
}

.features-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 24px;
  margin-bottom: 64px;
}

.feature-card {
  padding: 32px;
  transition: transform 0.3s ease, box-shadow 0.3s ease;
}

.feature-card:hover {
  transform: translateY(-8px);
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.3), 0 8px 10px -6px rgba(0, 0, 0, 0.2);
}

.feature-icon {
  width: 64px;
  height: 64px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 24px;
}

.feature-icon.blue {
  background: rgba(59, 130, 246, 0.2);
  color: #93c5fd;
}

.feature-icon.green {
  background: rgba(34, 197, 94, 0.2);
  color: #86efac;
}

.feature-icon.purple {
  background: rgba(168, 85, 247, 0.2);
  color: #c4b5fd;
}

.feature-icon.pink {
  background: rgba(236, 72, 153, 0.2);
  color: #f9a8d4;
}

.feature-icon.orange {
  background: rgba(249, 115, 22, 0.2);
  color: #fdba74;
}

.feature-icon.teal {
  background: rgba(20, 184, 166, 0.2);
  color: #5eead4;
}

.feature-icon .lucide-icon {
  width: 32px;
  height: 32px;
}

.feature-title {
  font-size: 20px;
  font-weight: bold;
  color: #ffffff;
  margin-bottom: 16px;
}

.feature-desc {
  color: #a1a1aa;
  line-height: 1.6;
  margin-bottom: 24px;
}

.feature-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.feature-list li {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  color: #71717a;
  font-size: 14px;
}

.feature-list li::before {
  content: "✓";
  color: #4ade80;
  font-weight: bold;
  flex-shrink: 0;
  margin-top: 2px;
}

.features-highlight {
  padding: 48px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  position: relative;
  overflow: hidden;
}

.highlight-content {
  max-width: 640px;
  margin-bottom: 32px;
}

.highlight-title {
  font-size: 24px;
  font-weight: bold;
  color: #ffffff;
  margin-bottom: 16px;
}

.highlight-desc {
  color: #a1a1aa;
  line-height: 1.6;
  margin-bottom: 24px;
}

.highlight-link {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #818cf8;
  font-weight: 500;
  text-decoration: none;
  transition: color 0.2s;
}

.highlight-link:hover {
  color: #a5b4fc;
}

.highlight-image {
  width: 128px;
  height: 128px;
  position: relative;
}

.image-bg-blur {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(99, 102, 241, 0.2);
  border-radius: 50%;
  filter: blur(24px);
  z-index: 0;
}

.highlight-image .lucide-icon {
  position: relative;
  z-index: 1;
  width: 64px;
  height: 64px;
  color: #ffffff;
  margin: 0 auto;
  display: block;
}

/* Personas 区域样式 */
.personas-section {
  padding: 96px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  position: relative;
  overflow: hidden;
}

.personas-bg-blur {
  position: absolute;
  top: 50%;
  left: 0;
  width: 384px;
  height: 384px;
  background: rgba(16, 185, 129, 0.1);
  border-radius: 50%;
  filter: blur(100px);
  z-index: -1;
}

.personas-container {
  max-width: 768px;
  margin: 0 auto;
}

.personas-header {
  text-align: center;
  margin-bottom: 48px;
}

.tab-buttons {
  display: inline-flex;
  background-color: #18181b;
  padding: 4px;
  border-radius: 9999px;
  border: 1px solid #27272a;
  margin-bottom: 48px;
  justify-content: center;
}

.tab-btn {
  padding: 8px 24px;
  border-radius: 9999px;
  font-size: 14px;
  font-weight: 500;
  border: none;
  background: transparent;
  color: #a1a1aa;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-btn.active {
  background-color: #4f46e5;
  color: #ffffff;
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.4);
}

.tab-btn:hover:not(.active) {
  color: #ffffff;
}

.tab-content {
  padding: 32px;
  min-height: 300px;
  position: relative;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
}

.panel-icon {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.panel-icon.indigo {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.panel-icon.emerald {
  background: rgba(16, 185, 129, 0.2);
  color: #6ee7b7;
}

.panel-icon.rose {
  background: rgba(244, 63, 94, 0.2);
  color: #fda4af;
}

.panel-icon .lucide-icon {
  width: 24px;
  height: 24px;
}

.panel-title {
  font-size: 24px;
  font-weight: bold;
  color: #ffffff;
}

.panel-subtitle {
  font-size: 14px;
  color: #71717a;
}

.panel-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel-item {
  display: flex;
  gap: 16px;
  padding: 16px;
  border-radius: 16px;
  background: rgba(24, 24, 27, 0.5);
  border: 1px solid #27272a;
}

.item-emoji {
  font-size: 24px;
  line-height: 1;
}

.item-title {
  font-weight: 600;
  color: #e4e4e7;
  margin-bottom: 4px;
}

.item-desc {
  font-size: 14px;
  color: #a1a1aa;
}

/* 页脚样式 */
.footer {
  background-color: #000000;
  padding: 48px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  text-align: center;
  color: #71717a;
  font-size: 14px;
}

.footer-container {
  max-width: 1152px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.footer-logo {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 24px;
  opacity: 0.5;
}

.footer-logo .lucide-icon {
  width: 20px;
  height: 20px;
}

.footer-desc {
  max-width: 384px;
  margin: 0 auto 32px;
  line-height: 1.5;
}

.copyright {
  font-size: 12px;
  color: #3f3f46;
  margin-top: 32px;
}

/* 响应式适配 */
@media (min-width: 768px) {
  .navbar-links {
    display: flex;
  }
  
  .hero-section {
    padding: 192px 24px 128px;
  }
  
  .hero-title {
    font-size: 64px;
  }
  
  .hero-buttons {
    flex-direction: row;
    justify-content: center;
  }
  
  .about-container {
    flex-direction: row;
    gap: 64px;
    align-items: center;
  }
  
  .problem-section, .solution-card {
    flex: 1;
  }
  
  .stats-grid {
    flex-direction: row;
  }
  
  .stats-left {
    flex: 1;
  }
  
  .stats-right {
    flex: 2;
  }
  
  .features-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 32px;
  }
  
  .features-highlight {
    flex-direction: row;
    text-align: left;
    gap: 48px;
  }
  
  .highlight-content {
    margin-bottom: 0;
    flex: 2;
  }
  
  .highlight-image {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
  }
}

@media (min-width: 1024px) {
  .features-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>