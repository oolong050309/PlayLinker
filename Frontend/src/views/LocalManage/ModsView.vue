<template>
  <div class="mods-container">
    <!-- Header -->
    <div class="mods-header">
      <h1 class="page-title">Mod与存档管理</h1>
      <div class="header-actions">
        <div class="web-notice">
          <Info class="icon" />
          <span>网页版功能受限，部分操作需要客户端</span>
        </div>
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
      <!-- Storage Overview -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">本地游戏</span>
            <HardDrive class="stat-icon indigo" />
          </div>
          <div class="stat-value">{{ summary.totalGames || 0 }}</div>
          <div class="stat-desc">{{ (summary.totalSizeGB || 0).toFixed(1) }} GB 总计</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">Mod数量</span>
            <Package class="stat-icon emerald" />
          </div>
          <div class="stat-value">{{ summary.totalMods || 0 }}</div>
          <div class="stat-desc">跨多款游戏</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">本地存档</span>
            <Save class="stat-icon amber" />
          </div>
          <div class="stat-value">{{ summary.totalSaves || 0 }}</div>
          <div class="stat-desc">{{ (savesSummary.totalSizeMB || 0).toFixed(1) }} MB</div>
        </div>

        <div class="stat-card">
          <div class="stat-header">
            <span class="stat-label">云端备份</span>
            <Cloud class="stat-icon blue" />
          </div>
          <div class="stat-value">{{ cloudSummary.totalCloudSaves || 0 }}</div>
          <div class="stat-desc">{{ (cloudSummary.storageUsedMB || 0).toFixed(1) }} / {{ cloudSummary.storageLimitMB || 1024 }} MB</div>
        </div>
      </div>

      <!-- Tab Navigation -->
      <div class="tab-nav">
        <button 
          :class="['tab-btn', { active: activeTab === 'games' }]"
          @click="activeTab = 'games'"
        >本地游戏</button>
        <button 
          :class="['tab-btn', { active: activeTab === 'saves' }]"
          @click="activeTab = 'saves'"
        >本地存档</button>
        <button 
          :class="['tab-btn', { active: activeTab === 'cloud' }]"
          @click="activeTab = 'cloud'"
        >云端备份</button>
      </div>

      <!-- Local Games Section -->
      <section v-if="activeTab === 'games'" class="content-section">
        <div class="section-header">
          <h2 class="section-title">本地游戏列表</h2>
          <button class="btn-refresh" @click="loadLocalGames">
            <RefreshCw class="icon" />
            刷新
          </button>
        </div>

        <div v-if="localGames.length > 0" class="games-list">
          <div v-for="game in localGames" :key="game.installId" class="game-card">
            <div class="game-content">
              <div class="game-info">
                <div class="game-header">
                  <div>
                    <h3 class="game-name">{{ game.gameName }}</h3>
                    <div class="game-meta">
                      <span class="meta-item">
                        <HardDrive class="icon" />
                        {{ game.sizeGB?.toFixed(1) || 0 }} GB
                      </span>
                      <span class="meta-item">
                        <Package class="icon" />
                        {{ game.modsCount || 0 }} Mods
                      </span>
                      <span class="meta-item">
                        <Save class="icon" />
                        {{ game.savesCount || 0 }} 存档
                      </span>
                    </div>
                  </div>
                  <span class="game-platform">{{ game.platformName }}</span>
                </div>
                <div class="game-path">
                  <Folder class="icon" />
                  {{ game.installPath }}
                </div>
                <div class="game-actions">
                  <button class="btn-secondary" @click="viewGameMods(game)">
                    查看Mod
                  </button>
                  <button class="btn-secondary" @click="viewGameSaves(game)">
                    查看存档
                  </button>
                  <button class="btn-danger-outline" @click="handleRemoveGame(game)">
                    移除记录
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <Package class="empty-icon" />
          <p>暂无本地游戏记录</p>
          <p class="empty-hint">网页版无法自动扫描，请使用客户端添加游戏</p>
        </div>
      </section>

      <!-- Local Saves Section -->
      <section v-if="activeTab === 'saves'" class="content-section">
        <div class="section-header">
          <h2 class="section-title">本地存档</h2>
          <button class="btn-refresh" @click="loadLocalSaves">
            <RefreshCw class="icon" />
            刷新
          </button>
        </div>

        <div v-if="localSaves.length > 0" class="saves-list">
          <div v-for="save in localSaves" :key="save.saveId" class="save-item">
            <Save class="save-icon" />
            <div class="save-info">
              <h4 class="save-name">{{ save.gameName }}</h4>
              <div class="save-meta">
                <span>{{ save.fileSizeMB?.toFixed(1) || 0 }} MB</span>
                <span>•</span>
                <span>{{ formatDate(save.updatedAt) }}</span>
              </div>
              <div class="save-path">{{ save.filePath }}</div>
            </div>
            <div class="save-actions">
              <span v-if="save.isBackupLocal" class="backup-badge">
                <Check class="icon" />
                已备份
              </span>
              <button class="btn-small" @click="handleUploadSave(save)">
                <CloudUpload class="icon" />
                上传云端
              </button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <Save class="empty-icon" />
          <p>暂无本地存档记录</p>
        </div>
      </section>

      <!-- Cloud Backups Section -->
      <section v-if="activeTab === 'cloud'" class="content-section">
        <div class="section-header">
          <h2 class="section-title">云端备份</h2>
          <div class="cloud-usage">
            {{ (cloudSummary.storageUsedMB || 0).toFixed(1) }} MB / {{ cloudSummary.storageLimitMB || 1024 }} MB 已用
          </div>
        </div>

        <div v-if="cloudSaves.length > 0" class="backups-list">
          <div v-for="backup in cloudSaves" :key="backup.cloudBackupId" class="backup-card">
            <div class="backup-icon">
              <Cloud class="icon" />
            </div>
            <div class="backup-info">
              <div class="backup-header">
                <div>
                  <h3 class="backup-name">{{ backup.gameName }}</h3>
                  <div class="backup-meta">
                    <span>{{ backup.fileSizeMB?.toFixed(1) || 0 }} MB</span>
                    <span>•</span>
                    <span>上传于 {{ formatDate(backup.uploadTime) }}</span>
                  </div>
                </div>
                <span v-if="backup.expiresAt" class="backup-expires">
                  {{ formatDate(backup.expiresAt) }} 过期
                </span>
              </div>
              <div class="backup-actions">
                <button class="btn-primary" @click="handleDownloadCloud(backup)">
                  <Download class="icon" />
                  下载
                </button>
                <button class="btn-danger" @click="handleDeleteCloud(backup)">
                  删除
                </button>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <Cloud class="empty-icon" />
          <p>暂无云端备份</p>
          <p class="empty-hint">在本地存档页面上传存档到云端</p>
        </div>
      </section>
    </template>

    <!-- Game Mods Dialog -->
    <div v-if="showModsDialog" class="dialog-overlay" @click.self="showModsDialog = false">
      <div class="dialog-content dialog-large">
        <div class="dialog-header">
          <h3 class="dialog-title">{{ selectedGame?.gameName }} - Mod列表</h3>
          <button class="btn-close" @click="showModsDialog = false">
            <X class="icon" />
          </button>
        </div>
        
        <div v-if="gameMods.length > 0" class="mods-list">
          <div 
            v-for="mod in gameMods" 
            :key="mod.modId" 
            :class="['mod-item', { disabled: !mod.enabled }]"
          >
            <button 
              :class="['mod-toggle', { active: mod.enabled }]"
              @click="handleToggleMod(mod)"
            >
              <Check v-if="mod.enabled" class="icon" />
              <X v-else class="icon" />
            </button>
            <div class="mod-info">
              <h4 class="mod-name">{{ mod.modName }}</h4>
              <p class="mod-desc">{{ mod.description || '无描述' }}</p>
              <div class="mod-meta">
                <span>v{{ mod.version }}</span>
                <span>•</span>
                <span>{{ mod.sizeGB?.toFixed(2) || 0 }} GB</span>
                <span v-if="mod.author">• {{ mod.author }}</span>
              </div>
            </div>
            <div class="mod-actions">
              <button class="btn-small danger" @click="handleDeleteMod(mod)">删除</button>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <p>该游戏暂无Mod</p>
        </div>
      </div>
    </div>

    <!-- Upload Dialog -->
    <div v-if="showUploadDialog" class="dialog-overlay" @click.self="showUploadDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">上传存档到云端</h3>
        <p class="dialog-desc">{{ selectedSave?.gameName }}</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>选择存档文件</label>
            <input 
              type="file" 
              ref="fileInput"
              @change="handleFileSelect"
              class="form-file"
            />
          </div>
          <div class="form-group">
            <label>
              <input type="checkbox" v-model="uploadForm.compress" />
              压缩上传
            </label>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showUploadDialog = false">取消</button>
          <button class="btn-confirm" @click="handleConfirmUpload" :disabled="uploading || !uploadFile">
            {{ uploading ? '上传中...' : '上传' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { 
  HardDrive, Package, Save, Cloud, RefreshCw, Check, X, 
  Folder, CloudUpload, Download, Info, AlertCircle
} from 'lucide-vue-next'
import {
  getLocalGames,
  removeLocalGame,
  getLocalSaves,
  getCloudSaves,
  uploadCloudSave,
  downloadCloudSave,
  deleteCloudSave,
  getCloudStorageUsage,
  getGameMods,
  toggleMod,
  deleteMod
} from '@/api/localGame'

const activeTab = ref('games')
const loading = ref(true)
const error = ref(null)

// Data
const localGames = ref([])
const localSaves = ref([])
const cloudSaves = ref([])
const gameMods = ref([])

// Summary data
const summary = ref({
  totalGames: 0,
  totalSizeGB: 0,
  totalSaves: 0,
  totalMods: 0
})

const savesSummary = ref({
  totalSaves: 0,
  totalSizeMB: 0
})

const cloudSummary = ref({
  totalCloudSaves: 0,
  storageUsedMB: 0,
  storageLimitMB: 1024
})

// Dialogs
const showModsDialog = ref(false)
const showUploadDialog = ref(false)
const selectedGame = ref(null)
const selectedSave = ref(null)

// Upload
const uploadFile = ref(null)
const uploadForm = ref({ compress: false })
const uploading = ref(false)
const fileInput = ref(null)

// Methods
const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN') + ' ' + date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

// Load all data
const loadData = async () => {
  loading.value = true
  error.value = null
  
  try {
    await Promise.all([
      loadLocalGames(),
      loadLocalSaves(),
      loadCloudSaves(),
      loadCloudUsage()
    ])
  } catch (err) {
    console.error('加载数据失败:', err)
    error.value = '加载数据失败，请稍后重试'
  } finally {
    loading.value = false
  }
}

const loadLocalGames = async () => {
  try {
    const res = await getLocalGames({ page: 1, page_size: 50 })
    if (res.data) {
      localGames.value = res.data.items || []
      if (res.data.summary) {
        summary.value = res.data.summary
      }
    }
  } catch (err) {
    console.error('加载本地游戏失败:', err)
  }
}

const loadLocalSaves = async () => {
  try {
    const res = await getLocalSaves({ page: 1, page_size: 50 })
    if (res.data) {
      localSaves.value = res.data.items || []
      if (res.data.summary) {
        savesSummary.value = res.data.summary
      }
    }
  } catch (err) {
    console.error('加载本地存档失败:', err)
  }
}

const loadCloudSaves = async () => {
  try {
    const res = await getCloudSaves({ page: 1, page_size: 50 })
    if (res.data) {
      cloudSaves.value = res.data.items || []
      if (res.data.summary) {
        cloudSummary.value = { ...cloudSummary.value, ...res.data.summary }
      }
    }
  } catch (err) {
    console.error('加载云存档失败:', err)
  }
}

const loadCloudUsage = async () => {
  try {
    const res = await getCloudStorageUsage()
    if (res.data) {
      cloudSummary.value = {
        ...cloudSummary.value,
        storageUsedMB: res.data.storageUsedMB,
        storageLimitMB: res.data.storageLimitMB,
        totalCloudSaves: res.data.totalFiles
      }
    }
  } catch (err) {
    console.error('加载云存储使用情况失败:', err)
  }
}

const viewGameMods = async (game) => {
  selectedGame.value = game
  showModsDialog.value = true
  
  try {
    const res = await getGameMods(game.gameId, { install_id: game.installId })
    if (res.data?.mods) {
      gameMods.value = res.data.mods
    } else {
      gameMods.value = []
    }
  } catch (err) {
    console.error('加载Mod列表失败:', err)
    gameMods.value = []
  }
}

const viewGameSaves = (game) => {
  activeTab.value = 'saves'
  // 可以添加筛选逻辑
}

const handleRemoveGame = async (game) => {
  if (!confirm(`确定要移除 ${game.gameName} 的记录吗？\n注意：这只会移除数据库记录，不会删除本地文件。`)) return
  
  try {
    await removeLocalGame(game.installId)
    localGames.value = localGames.value.filter(g => g.installId !== game.installId)
    alert('游戏记录已移除')
  } catch (err) {
    console.error('移除游戏失败:', err)
    alert('移除失败: ' + (err.message || '未知错误'))
  }
}

const handleToggleMod = async (mod) => {
  try {
    await toggleMod(mod.modId, !mod.enabled)
    mod.enabled = !mod.enabled
  } catch (err) {
    console.error('切换Mod状态失败:', err)
    alert('操作失败: ' + (err.message || '未知错误'))
  }
}

const handleDeleteMod = async (mod) => {
  if (!confirm(`确定要删除 ${mod.modName} 吗？\n注意：网页版只会删除记录，不会删除本地文件。`)) return
  
  try {
    await deleteMod(mod.modId)
    gameMods.value = gameMods.value.filter(m => m.modId !== mod.modId)
  } catch (err) {
    console.error('删除Mod失败:', err)
    alert('删除失败: ' + (err.message || '未知错误'))
  }
}

const handleUploadSave = (save) => {
  selectedSave.value = save
  uploadFile.value = null
  uploadForm.value = { compress: false }
  showUploadDialog.value = true
}

const handleFileSelect = (e) => {
  const file = e.target.files[0]
  if (file) {
    uploadFile.value = file
  }
}

const handleConfirmUpload = async () => {
  if (!uploadFile.value || !selectedSave.value) return
  
  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', uploadFile.value)
    formData.append('saveId', selectedSave.value.saveId)
    formData.append('compress', uploadForm.value.compress)
    
    await uploadCloudSave(formData)
    
    showUploadDialog.value = false
    alert('上传成功！')
    loadCloudSaves()
    loadCloudUsage()
  } catch (err) {
    console.error('上传失败:', err)
    alert('上传失败: ' + (err.message || '未知错误'))
  } finally {
    uploading.value = false
  }
}

const handleDownloadCloud = async (backup) => {
  try {
    const response = await downloadCloudSave(backup.cloudBackupId)
    const url = URL.createObjectURL(new Blob([response]))
    const link = document.createElement('a')
    link.href = url
    link.download = `${backup.cloudBackupId}.dat`
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    console.error('下载失败:', err)
    alert('下载失败: ' + (err.message || '未知错误'))
  }
}

const handleDeleteCloud = async (backup) => {
  if (!confirm('确定要删除这个云端备份吗？')) return
  
  try {
    await deleteCloudSave(backup.cloudBackupId)
    cloudSaves.value = cloudSaves.value.filter(c => c.cloudBackupId !== backup.cloudBackupId)
    loadCloudUsage()
  } catch (err) {
    console.error('删除失败:', err)
    alert('删除失败: ' + (err.message || '未知错误'))
  }
}

onMounted(() => {
  loadData()
})
</script>


<style scoped>
.mods-container {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.mods-header {
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

.web-notice {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: rgba(245, 158, 11, 0.1);
  border: 1px solid rgba(245, 158, 11, 0.3);
  border-radius: 8px;
  color: #fbbf24;
  font-size: 13px;
}

.web-notice .icon {
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
  padding: 60px 20px;
  text-align: center;
  color: var(--text-secondary);
}

.empty-icon {
  width: 48px;
  height: 48px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.empty-hint {
  font-size: 13px;
  margin-top: 8px;
  opacity: 0.7;
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
  margin-bottom: 8px;
}

.stat-label {
  font-size: 14px;
  color: var(--text-secondary);
}

.stat-icon {
  width: 20px;
  height: 20px;
}

.stat-icon.indigo { color: #818cf8; }
.stat-icon.emerald { color: #34d399; }
.stat-icon.amber { color: #fbbf24; }
.stat-icon.blue { color: #60a5fa; }

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-primary);
}

.stat-desc {
  font-size: 12px;
  color: var(--text-secondary);
}

/* Tab Navigation */
.tab-nav {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
}

.tab-btn {
  padding: 10px 20px;
  border-radius: 8px;
  font-size: 14px;
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

.tab-btn:hover:not(.active) {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

/* Section */
.content-section {
  margin-bottom: 32px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
}

/* Buttons */
.btn-primary {
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
  transition: background 0.2s;
}

.btn-primary:hover {
  background: var(--primary-hover);
}

.btn-secondary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-secondary);
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

.btn-danger {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-danger:hover {
  background: rgba(239, 68, 68, 0.3);
}

.btn-danger-outline {
  padding: 10px 20px;
  background: transparent;
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-danger-outline:hover {
  background: rgba(239, 68, 68, 0.1);
}

.btn-refresh {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: transparent;
  color: var(--primary-color);
  border: none;
  font-size: 14px;
  cursor: pointer;
}

.btn-refresh .icon {
  width: 16px;
  height: 16px;
}

.btn-small {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-secondary);
  border: none;
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-small:hover {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

.btn-small.danger {
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
}

.btn-small .icon {
  width: 14px;
  height: 14px;
}

.icon {
  width: 16px;
  height: 16px;
}

/* Games List */
.games-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.game-card {
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 20px;
}

.game-content {
  display: flex;
  gap: 16px;
}

.game-info {
  flex: 1;
  min-width: 0;
}

.game-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 8px;
}

.game-name {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.game-meta {
  display: flex;
  gap: 16px;
  font-size: 14px;
  color: var(--text-secondary);
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.meta-item .icon {
  width: 12px;
  height: 12px;
}

.game-platform {
  padding: 4px 8px;
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.game-path {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 16px;
}

.game-path .icon {
  width: 12px;
  height: 12px;
}

.game-actions {
  display: flex;
  gap: 8px;
}

/* Saves List */
.saves-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.save-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  transition: background 0.2s;
}

.save-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.save-icon {
  width: 32px;
  height: 32px;
  color: #818cf8;
}

.save-info {
  flex: 1;
  min-width: 0;
}

.save-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.save-meta {
  display: flex;
  gap: 8px;
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.save-path {
  font-size: 12px;
  color: var(--text-secondary);
  opacity: 0.7;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.save-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.backup-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.backup-badge .icon {
  width: 12px;
  height: 12px;
}

/* Cloud Backups */
.cloud-usage {
  font-size: 14px;
  color: var(--text-secondary);
}

.backups-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.backup-card {
  display: flex;
  gap: 16px;
  padding: 20px;
  background: rgba(24, 24, 27, 0.6);
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
}

.backup-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: rgba(59, 130, 246, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.backup-icon .icon {
  width: 24px;
  height: 24px;
  color: #60a5fa;
}

.backup-info {
  flex: 1;
  min-width: 0;
}

.backup-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.backup-name {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.backup-meta {
  display: flex;
  gap: 8px;
  font-size: 14px;
  color: var(--text-secondary);
}

.backup-expires {
  font-size: 12px;
  color: var(--text-secondary);
}

.backup-actions {
  display: flex;
  gap: 8px;
}

/* Mods List in Dialog */
.mods-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 400px;
  overflow-y: auto;
}

.mod-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  transition: opacity 0.2s;
}

.mod-item.disabled {
  opacity: 0.6;
}

.mod-toggle {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  cursor: pointer;
  transition: all 0.2s;
}

.mod-toggle.active {
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
}

.mod-toggle:not(.active) {
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.mod-info {
  flex: 1;
  min-width: 0;
}

.mod-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.mod-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.mod-meta {
  display: flex;
  gap: 8px;
  font-size: 12px;
  color: var(--text-secondary);
}

.mod-actions {
  display: flex;
  gap: 8px;
}

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

.dialog-content.dialog-large {
  width: 600px;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.dialog-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.dialog-desc {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 20px;
}

.btn-close {
  background: transparent;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 4px;
}

.dialog-form {
  margin-bottom: 24px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.form-file {
  width: 100%;
  padding: 10px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: white;
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

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

.btn-confirm:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Responsive */
@media (max-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .mods-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }

  .tab-nav {
    flex-wrap: wrap;
  }

  .game-actions {
    flex-wrap: wrap;
  }

  .backup-card {
    flex-direction: column;
  }

  .backup-actions {
    flex-wrap: wrap;
  }
}
</style>
