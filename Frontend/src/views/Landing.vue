<template>
  <div id="landing">
    <!-- 导航栏 -->
    <header class="header">
      <div class="container">
        <nav class="nav">
          <div class="logo">
            <h1>PlayLinker</h1>
            <p>统一游戏管理平台</p>
          </div>
          <ul class="nav-menu">
            <li><a href="#features">功能特性</a></li>
            <li><a href="#about">关于我们</a></li>
            <li><router-link to="/login" class="nav-link">登录</router-link></li>
            <li><router-link to="/register" class="nav-link btn-primary">注册</router-link></li>
          </ul>
        </nav>
      </div>
    </header>

    <!-- 主要内容 -->
    <main>
      <!-- 英雄区域 -->
      <section class="hero">
        <div class="container">
          <div class="hero-content">
            <h1 class="hero-title">统一管理您的所有游戏</h1>
            <p class="hero-subtitle">
              PlayLinker 是一个强大的游戏管理平台，帮助您整合、管理和追踪来自多个平台的游戏收藏
            </p>
            <div class="hero-actions">
              <router-link to="/register" class="btn btn-primary btn-large">开始使用</router-link>
              <router-link to="/login" class="btn btn-secondary btn-large">已有账户？登录</router-link>
            </div>
          </div>
          <div class="hero-image">
            <div class="hero-placeholder">
              <svg viewBox="0 0 400 300" xmlns="http://www.w3.org/2000/svg">
                <rect width="400" height="300" fill="#667eea" opacity="0.1" rx="12"/>
                <text x="200" y="150" text-anchor="middle" fill="#667eea" font-size="24" font-weight="bold">PlayLinker</text>
              </svg>
            </div>
          </div>
        </div>
      </section>

      <!-- 功能特性 -->
      <section id="features" class="features">
        <div class="container">
          <h2 class="section-title">核心功能</h2>
          <div class="features-grid">
            <div class="feature-card">
              <div class="feature-icon">🎮</div>
              <h3>多平台整合</h3>
              <p>支持 Steam、Epic、GOG、Xbox、PlayStation 等多个游戏平台，统一管理您的游戏库</p>
            </div>
            <div class="feature-card">
              <div class="feature-icon">📊</div>
              <h3>游戏统计</h3>
              <p>详细的游戏数据分析，包括游戏时间、成就进度、收藏统计等</p>
            </div>
            <div class="feature-card">
              <div class="feature-icon">🏆</div>
              <h3>成就追踪</h3>
              <p>自动同步各平台的游戏成就，追踪您的游戏进度和完成度</p>
            </div>
            <div class="feature-card">
              <div class="feature-icon">💰</div>
              <h3>价格监控</h3>
              <p>监控游戏价格变化，在最佳时机购买您心仪的游戏</p>
            </div>
            <div class="feature-card">
              <div class="feature-icon">☁️</div>
              <h3>云存档管理</h3>
              <p>统一管理各平台的云存档，随时随地继续您的游戏进度</p>
            </div>
            <div class="feature-card">
              <div class="feature-icon">🔔</div>
              <h3>智能通知</h3>
              <p>游戏更新、价格变动、新游戏发布等实时通知，不错过任何重要信息</p>
            </div>
          </div>
        </div>
      </section>

      <!-- 关于我们 -->
      <section id="about" class="about">
        <div class="container">
          <h2 class="section-title">关于 PlayLinker</h2>
          <div class="about-content">
            <p class="about-text">
              PlayLinker 致力于为游戏爱好者提供一个统一的游戏管理解决方案。无论您拥有多少游戏，
              分布在多少个平台，PlayLinker 都能帮您轻松管理和追踪。
            </p>
            <p class="about-text">
              我们理解游戏玩家的需求，不断优化功能，提供更好的用户体验。加入 PlayLinker，
              让游戏管理变得简单而有趣。
            </p>
          </div>
        </div>
      </section>
    </main>

    <!-- 页脚 -->
    <footer class="footer">
      <div class="container">
        <div class="footer-content">
          <div class="footer-section">
            <h3>PlayLinker</h3>
            <p>统一游戏管理平台</p>
          </div>
          <div class="footer-section">
            <h4>快速链接</h4>
            <ul>
              <li><router-link to="/login">登录</router-link></li>
              <li><router-link to="/register">注册</router-link></li>
            </ul>
          </div>
          <div class="footer-section">
            <h4>功能</h4>
            <ul>
              <li><a href="#features">功能特性</a></li>
              <li><a href="#about">关于我们</a></li>
            </ul>
          </div>
        </div>
        <div class="footer-bottom">
          <p>&copy; 2024 PlayLinker. All rights reserved.</p>
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup>
</script>

<style scoped>
#landing {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 20px;
}

/* 导航栏 */
.header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 20px 0;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  position: sticky;
  top: 0;
  z-index: 100;
}

.nav {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.logo h1 {
  margin: 0;
  font-size: 28px;
}

.logo p {
  margin: 0;
  font-size: 14px;
  opacity: 0.9;
}

.nav-menu {
  display: flex;
  list-style: none;
  gap: 30px;
  align-items: center;
}

.nav-menu a {
  color: white;
  text-decoration: none;
  font-size: 16px;
  transition: opacity 0.3s;
}

.nav-menu a:hover {
  opacity: 0.8;
}

.nav-link.btn-primary {
  background: rgba(255, 255, 255, 0.2);
  padding: 8px 20px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.3);
}

.nav-link.btn-primary:hover {
  background: rgba(255, 255, 255, 0.3);
}

/* 主要内容 */
main {
  flex: 1;
}

/* 英雄区域 */
.hero {
  padding: 80px 0;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.hero .container {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 60px;
  align-items: center;
}

.hero-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.hero-title {
  font-size: 48px;
  font-weight: 700;
  margin: 0;
  line-height: 1.2;
}

.hero-subtitle {
  font-size: 20px;
  opacity: 0.9;
  line-height: 1.6;
  margin: 0;
}

.hero-actions {
  display: flex;
  gap: 16px;
  margin-top: 16px;
}

.btn {
  padding: 14px 28px;
  border-radius: 8px;
  text-decoration: none;
  font-size: 16px;
  font-weight: 600;
  transition: all 0.3s;
  display: inline-block;
  text-align: center;
}

.btn-large {
  padding: 16px 32px;
  font-size: 18px;
}

.btn-primary {
  background: white;
  color: #667eea;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.3);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.3);
}

.hero-image {
  display: flex;
  justify-content: center;
  align-items: center;
}

.hero-placeholder {
  width: 100%;
  max-width: 500px;
}

/* 功能特性 */
.features {
  padding: 80px 0;
  background: #f8f9fa;
}

.section-title {
  text-align: center;
  font-size: 36px;
  font-weight: 700;
  margin: 0 0 60px 0;
  color: #333;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 30px;
}

.feature-card {
  background: white;
  padding: 40px;
  border-radius: 12px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  transition: transform 0.3s, box-shadow 0.3s;
  text-align: center;
}

.feature-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
}

.feature-icon {
  font-size: 48px;
  margin-bottom: 20px;
}

.feature-card h3 {
  font-size: 24px;
  margin: 0 0 16px 0;
  color: #333;
}

.feature-card p {
  color: #666;
  line-height: 1.6;
  margin: 0;
}

/* 关于我们 */
.about {
  padding: 80px 0;
  background: white;
}

.about-content {
  max-width: 800px;
  margin: 0 auto;
  text-align: center;
}

.about-text {
  font-size: 18px;
  line-height: 1.8;
  color: #666;
  margin: 0 0 24px 0;
}

/* 页脚 */
.footer {
  background-color: #2c3e50;
  color: white;
  padding: 40px 0 20px;
  margin-top: auto;
}

.footer-content {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 40px;
  margin-bottom: 30px;
}

.footer-section h3,
.footer-section h4 {
  margin: 0 0 16px 0;
  font-size: 18px;
}

.footer-section p {
  margin: 0;
  opacity: 0.8;
  font-size: 14px;
}

.footer-section ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.footer-section ul li {
  margin-bottom: 8px;
}

.footer-section ul li a {
  color: white;
  text-decoration: none;
  opacity: 0.8;
  transition: opacity 0.3s;
  font-size: 14px;
}

.footer-section ul li a:hover {
  opacity: 1;
}

.footer-bottom {
  text-align: center;
  padding-top: 20px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.footer-bottom p {
  margin: 0;
  opacity: 0.6;
  font-size: 14px;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .hero .container {
    grid-template-columns: 1fr;
    gap: 40px;
  }

  .hero-title {
    font-size: 36px;
  }

  .hero-subtitle {
    font-size: 18px;
  }

  .hero-actions {
    flex-direction: column;
  }

  .nav-menu {
    gap: 15px;
    font-size: 14px;
  }

  .features-grid {
    grid-template-columns: 1fr;
  }

  .section-title {
    font-size: 28px;
  }
}
</style>

