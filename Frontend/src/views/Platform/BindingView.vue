<template>
  <div class="app-container">
    <!-- 头部区域 -->
    <header class="header">
      <div class="header-content">
        <div class="header-title">
          <h1>平台绑定</h1>
          <p>连接你的游戏账号以同步游戏库</p>
        </div>
        <div class="header-status">
          <div class="status-badge">
            <i data-lucide="check-circle" class="icon icon-success"></i>
            <span><span class="text-success font-bold">3</span> 已连接</span>
          </div>
        </div>
      </div>
    </header>

    <!-- 主要内容 -->
    <main class="main-content">
      <!-- 统计概览 -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon bg-indigo">
            <i data-lucide="link" class="icon"></i>
          </div>
          <div class="stat-info">
            <div class="stat-label">已连接平台</div>
            <div class="stat-value">3 / 7</div>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon bg-emerald">
            <i data-lucide="gamepad-2" class="icon"></i>
          </div>
          <div class="stat-info">
            <div class="stat-label">已同步游戏总数</div>
            <div class="stat-value">127</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-amber">
            <i data-lucide="trophy" class="icon"></i>
          </div>
          <div class="stat-info">
            <div class="stat-label">已同步成就数</div>
            <div class="stat-value">892</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-rose">
            <i data-lucide="clock" class="icon"></i>
          </div>
          <div class="stat-info">
            <div class="stat-label">最后同步时间</div>
            <div class="stat-value">2分钟前</div>
          </div>
        </div>
      </div>

      <!-- 已连接平台 -->
      <div class="section">
        <h2 class="section-title">
          <i data-lucide="check-circle" class="icon icon-success"></i>
          已连接平台
        </h2>
        <div class="platform-grid">
          <!-- Steam -->
          <div class="platform-card connected">
            <div class="platform-header steam-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/1024px-Steam_icon_logo.svg.png" alt="Steam">
                </div>
                <div class="platform-details">
                  <h3>Steam</h3>
                  <p>@gamer_pro_2024</p>
                </div>
              </div>
              <span class="badge success">已连接</span>
              <div class="platform-stats">
                <div class="stat-item">
                  <i data-lucide="gamepad-2" class="icon"></i>
                  <span>85 款游戏</span>
                </div>
                <div class="stat-item">
                  <i data-lucide="trophy" class="icon"></i>
                  <span>542 项成就</span>
                </div>
              </div>
            </div>
            <div class="platform-actions">
              <span class="last-synced">最后同步：2分钟前</span>
              <div class="action-buttons">
                <button class="btn btn-secondary">
                  <i data-lucide="refresh-cw" class="icon"></i> 同步
                </button>
                <button class="btn btn-danger">断开连接</button>
              </div>
            </div>
          </div>

          <!-- Epic Games -->
          <div class="platform-card connected">
            <div class="platform-header epic-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://upload.wikimedia.org/wikipedia/commons/a/a7/Epic_Games_logo.png" alt="Epic Games">
                </div>
                <div class="platform-details">
                  <h3>Epic Games</h3>
                  <p>@epic_gamer</p>
                </div>
              </div>
              <span class="badge success">已连接</span>
              <div class="platform-stats">
                <div class="stat-item">
                  <i data-lucide="gamepad-2" class="icon"></i>
                  <span>32 款游戏</span>
                </div>
                <div class="stat-item">
                  <i data-lucide="trophy" class="icon"></i>
                  <span>128 项成就</span>
                </div>
              </div>
            </div>
            <div class="platform-actions">
              <span class="last-synced">最后同步：15分钟前</span>
              <div class="action-buttons">
                <button class="btn btn-secondary">
                  <i data-lucide="refresh-cw" class="icon"></i> 同步
                </button>
                <button class="btn btn-danger">断开连接</button>
              </div>
            </div>
          </div>

          <!-- Xbox -->
          <div class="platform-card connected">
            <div class="platform-header xbox-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/f/f9/Xbox_one_logo.svg/2048px-Xbox_one_logo.svg.png" alt="Xbox">
                </div>
                <div class="platform-details">
                  <h3>Xbox</h3>
                  <p>@XboxPlayer123</p>
                </div>
              </div>
              <span class="badge success">已连接</span>
              <div class="platform-stats">
                <div class="stat-item">
                  <i data-lucide="gamepad-2" class="icon"></i>
                  <span>10 款游戏</span>
                </div>
                <div class="stat-item">
                  <i data-lucide="trophy" class="icon"></i>
                  <span>222 项成就</span>
                </div>
              </div>
            </div>
            <div class="platform-actions">
              <span class="last-synced">最后同步：1小时前</span>
              <div class="action-buttons">
                <button class="btn btn-secondary">
                  <i data-lucide="refresh-cw" class="icon"></i> 同步
                </button>
                <button class="btn btn-danger">断开连接</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 可用平台 -->
      <div class="section">
        <h2 class="section-title">
          <i data-lucide="plus-circle" class="icon icon-muted"></i>
          可用平台
        </h2>
        <div class="platform-grid">
          <!-- PlayStation -->
          <div class="platform-card">
            <div class="platform-header playstation-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://images.seeklogo.com/logo-png/49/1/playstation-logo-png_seeklogo-494440.png" alt="PlayStation">
                </div>
                <div class="platform-details">
                  <h3>PlayStation</h3>
                  <p>未连接</p>
                </div>
              </div>
              <p class="platform-desc">同步你的PlayStation奖杯和游戏库</p>
            </div>
            <div class="platform-actions">
              <button class="btn btn-primary w-full">
                <i data-lucide="link" class="icon"></i> 连接账号
              </button>
            </div>
          </div>

          <!-- Nintendo -->
          <div class="platform-card">
            <div class="platform-header nintendo-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://images.seeklogo.com/logo-png/31/1/nintendo-switch-logo-png_seeklogo-315901.png" alt="Nintendo Switch">
                </div>
                <div class="platform-details">
                  <h3>Nintendo Switch</h3>
                  <p>未连接</p>
                </div>
              </div>
              <p class="platform-desc">连接你的任天堂账号以同步Switch游戏</p>
            </div>
            <div class="platform-actions">
              <button class="btn btn-primary w-full">
                <i data-lucide="link" class="icon"></i> 连接账号
              </button>
            </div>
          </div>

          <!-- GOG -->
          <div class="platform-card">
            <div class="platform-header gog-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://w7.pngwing.com/pngs/403/46/png-transparent-gog-galaxy-alt-macos-bigsur-icon-thumbnail.png" alt="GOG">
                </div>
                <div class="platform-details">
                  <h3>GOG Galaxy</h3>
                  <p>未连接</p>
                </div>
              </div>
              <p class="platform-desc">无DRM保护的游戏和经典游戏作品</p>
            </div>
            <div class="platform-actions">
              <button class="btn btn-primary w-full">
                <i data-lucide="link" class="icon"></i> 连接账号
              </button>
            </div>
          </div>

          <!-- Battle.net -->
          <div class="platform-card">
            <div class="platform-header battlenet-gradient">
              <div class="platform-info">
                <div class="platform-logo">
                  <img src="https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTmi7pxirQ-yuPN673zi5amugvUn8bCsPSiTw&s" alt="Battle.net">
                </div>
                <div class="platform-details">
                  <h3>Battle.net</h3>
                  <p>未连接</p>
                </div>
              </div>
              <p class="platform-desc">暴雪游戏，包括魔兽世界、暗黑破坏神、守望先锋等</p>
            </div>
            <div class="platform-actions">
              <button class="btn btn-primary w-full">
                <i data-lucide="link" class="icon"></i> 连接账号
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- 同步设置 -->
      <div class="section">
        <div class="settings-card">
          <h2 class="section-title">
            <i data-lucide="settings" class="icon"></i>
            同步设置
          </h2>
          <div class="settings-list">
            <div class="setting-item">
              <div class="setting-info">
                <h3>自动同步游戏库</h3>
                <p>每小时自动同步你的游戏库</p>
              </div>
              <div 
                class="toggle-switch active" 
                @click="toggleSwitch('autoSync')"
              >
                <span class="toggle-thumb"></span>
              </div>
            </div>
            
            <div class="setting-item">
              <div class="setting-info">
                <h3>同步成就</h3>
                <p>从已连接平台导入成就和奖杯</p>
              </div>
              <div 
                class="toggle-switch active" 
                @click="toggleSwitch('achievements')"
              >
                <span class="toggle-thumb"></span>
              </div>
            </div>
            
            <div class="setting-item">
              <div class="setting-info">
                <h3>同步游玩时长</h3>
                <p>追踪并汇总所有平台的游玩时长</p>
              </div>
              <div 
                class="toggle-switch active" 
                @click="toggleSwitch('playtime')"
              >
                <span class="toggle-thumb"></span>
              </div>
            </div>
            
            <div class="setting-item">
              <div class="setting-info">
                <h3>同步通知</h3>
                <p>同步完成或失败时收到通知</p>
              </div>
              <div 
                class="toggle-switch" 
                @click="toggleSwitch('notify')"
              >
                <span class="toggle-thumb"></span>
              </div>
            </div>
          </div>
          
          <div class="settings-actions">
            <button class="btn btn-primary">
              <i data-lucide="refresh-cw" class="icon"></i> 立即同步全部
            </button>
            <button class="btn btn-tertiary">
              查看同步历史
            </button>
          </div>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup>
import { onMounted } from 'vue';
import { createIcons, CheckCircle, Link, Gamepad2, Trophy, Clock, PlusCircle, Settings, RefreshCw } from 'lucide';

// 注册lucide图标
onMounted(() => {
  createIcons({
    icons: {
      CheckCircle,
      Link,
      Gamepad2,
      Trophy,
      Clock,
      PlusCircle,
      Settings,
      RefreshCw
    }
  });
});

// 开关状态管理
const toggleSwitch = (type) => {
  const toggleElements = document.querySelectorAll('.toggle-switch');
  let index;
  
  switch(type) {
    case 'autoSync': index = 0; break;
    case 'achievements': index = 1; break;
    case 'playtime': index = 2; break;
    case 'notify': index = 3; break;
  }
  
  const toggle = toggleElements[index];
  toggle.classList.toggle('active');
};
</script>

<style scoped>
/* 基础样式 */
.app-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #000000 0%, #000000 50%, #0f0f0f 100%);
  color: #ffffff;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部样式 */
.header {
  position: sticky;
  top: 0;
  z-index: 50;
  background: rgba(10, 10, 10, 0.8);
  backdrop-filter: blur(24px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.header-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-title h1 {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0 0 0.25rem 0;
}

.header-title p {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin: 0;
}

.status-badge {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1rem;
  font-size: 0.75rem;
}

/* 主要内容 */
.main-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem;
}

/* 统计网格 */
.stats-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

@media (min-width: 768px) {
  .stats-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
}

.stat-icon {
  width: 3rem;
  height: 3rem;
  border-radius: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.bg-indigo {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.bg-emerald {
  background: rgba(16, 185, 129, 0.2);
  color: #6ee7b7;
}

.bg-amber {
  background: rgba(245, 158, 11, 0.2);
  color: #fcd34d;
}

.bg-rose {
  background: rgba(244, 63, 94, 0.2);
  color: #fda4af;
}

.stat-info {
  flex: 1;
}

.stat-label {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin-bottom: 0.25rem;
}

.stat-value {
  font-size: 1.25rem;
  font-weight: 700;
}

/* 通用样式 */
.section {
  margin-bottom: 2rem;
}

.section-title {
  font-size: 1.125rem;
  font-weight: 700;
  margin: 0 0 1rem 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

/* 平台网格 */
.platform-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.5rem;
}

@media (min-width: 768px) {
  .platform-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .platform-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

.platform-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
  overflow: hidden;
  transition: all 0.3s ease;
}

.platform-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
}

.platform-card.connected {
  border-color: rgba(34, 197, 94, 0.3);
}

.platform-header {
  padding: 1.5rem;
}

.platform-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.platform-info > div:first-child {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.platform-logo {
  width: 3rem;
  height: 3rem;
  background: #ffffff;
  border-radius: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.platform-logo img {
  width: 2rem;
  height: 2rem;
  object-fit: contain;
}

.platform-details h3 {
  font-size: 1rem;
  font-weight: 700;
  margin: 0 0 0.25rem 0;
}

.platform-details p {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.6);
  margin: 0;
}

.badge {
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.7rem;
  font-weight: 700;
}

.badge.success {
  background: rgba(34, 197, 94, 0.2);
  color: #4ade80;
  border: 1px solid rgba(34, 197, 94, 0.3);
}

.platform-stats {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.platform-desc {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
  margin: 0;
}

.platform-actions {
  padding: 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.last-synced {
  font-size: 0.7rem;
  color: #71717a;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

/* 渐变样式 */
.steam-gradient {
  background: linear-gradient(135deg, #1b2838 0%, #2a475e 100%);
}

.epic-gradient {
  background: linear-gradient(135deg, #2a2a2a 0%, #1a1a1a 100%);
}

.xbox-gradient {
  background: linear-gradient(135deg, #107c10 0%, #0e6b0e 100%);
}

.playstation-gradient {
  background: linear-gradient(135deg, #003791 0%, #00246d 100%);
}

.nintendo-gradient {
  background: linear-gradient(135deg, #e60012 0%, #c4000f 100%);
}

.gog-gradient {
  background: linear-gradient(135deg, #86328a 0%, #5c2260 100%);
}

.battlenet-gradient {
  background: linear-gradient(135deg, #00aeff 0%, #0078d4 100%);
}

/* 按钮样式 */
.btn {
  padding: 0.5rem 0.75rem;
  border-radius: 0.75rem;
  font-size: 0.75rem;
  font-weight: 500;
  border: none;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #4f46e5;
  color: #ffffff;
}

.btn-primary:hover {
  background: #4338ca;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.05);
  color: #ffffff;
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-tertiary {
  background: rgba(255, 255, 255, 0.05);
  color: #ffffff;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-tertiary:hover {
  background: rgba(255, 255, 255, 0.1);
}

.btn-danger {
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
}

.btn-danger:hover {
  background: rgba(239, 68, 68, 0.2);
}

.w-full {
  width: 100%;
}

/* 设置卡片 */
.settings-card {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(10px);
  border-radius: 1.25rem;
  padding: 1.5rem;
}

.settings-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 1rem;
}

.setting-info h3 {
  font-size: 0.875rem;
  font-weight: 500;
  margin: 0 0 0.25rem 0;
}

.setting-info p {
  font-size: 0.75rem;
  color: #a1a1aa;
  margin: 0;
}

/* 开关样式 */
.toggle-switch {
  width: 3rem;
  height: 1.75rem;
  border-radius: 9999px;
  background: #3f3f46;
  position: relative;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toggle-switch.active {
  background: #4f46e5;
}

.toggle-thumb {
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 50%;
  background: #ffffff;
  position: absolute;
  top: 0.25rem;
  left: 0.25rem;
  transition: all 0.2s ease;
}

.toggle-switch.active .toggle-thumb {
  left: calc(100% - 1.25rem - 0.25rem);
}

.settings-actions {
  display: flex;
  gap: 1rem;
  margin-top: 1.5rem;
}

/* 图标样式 */
.icon {
  width: 1.25rem;
  height: 1.25rem;
}

.icon-success {
  color: #4ade80;
}

.icon-muted {
  color: #a1a1aa;
}

.text-success {
  color: #4ade80;
}

.font-bold {
  font-weight: 700;
}
</style>