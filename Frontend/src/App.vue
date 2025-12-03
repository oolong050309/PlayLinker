<template>
  <div id="app">
    <header class="header">
      <div class="container">
        <nav class="nav">
          <div class="logo">
            <h1>PlayLinker</h1>
            <p>统一游戏管理平台</p>
          </div>
          <ul class="nav-menu">
            <li><router-link to="/">游戏列表</router-link></li>
            <li><router-link to="/ranking">排行榜</router-link></li>
            <li><router-link to="/library">我的游戏库</router-link></li>
            <li><router-link to="/achievements">成就</router-link></li>
            <li><router-link to="/news">新闻</router-link></li>
          </ul>
        </nav>
      </div>
    </header>

    <main>
      <router-view />
    </main>

    <footer class="footer">
      <div class="container">
        <p>&copy; 2024 PlayLinker. All rights reserved.</p>
      </div>
    </footer>
  </div>
</template>

<script setup>
</script>

<style scoped>
.header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 20px 0;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
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

.nav-menu a.router-link-active {
  font-weight: bold;
  border-bottom: 2px solid white;
}

main {
  min-height: calc(100vh - 200px);
}

.footer {
  background-color: #2c3e50;
  color: white;
  text-align: center;
  padding: 20px 0;
  margin-top: 40px;
}
</style>

