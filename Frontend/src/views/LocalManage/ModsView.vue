<template>
  <div class="mods-container">
    <!-- Header -->
    <div class="mods-header">
      <h1 class="page-title">Mod与存档管理</h1>
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
          <div class="header-actions">
            <button class="btn-primary" @click="showAddGameDialog = true">
              <Plus class="icon" />
              添加游戏
            </button>
            <button class="btn-refresh" @click="loadLocalGames">
              <RefreshCw class="icon" />
              刷新
            </button>
          </div>
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
                  <button class="btn-secondary" @click="handleEditGamePath(game)">
                    修改目录
                  </button>
                  <button class="btn-secondary" @click="handleAddMod(game)">
                    添加Mod
                  </button>
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
          <div class="header-actions">
            <button class="btn-primary" @click="showAddSaveDialog = true">
              <Plus class="icon" />
              添加存档
            </button>
            <button class="btn-refresh" @click="loadLocalSaves">
              <RefreshCw class="icon" />
              刷新
            </button>
          </div>
        </div>

        <div v-if="localSaves.length > 0" class="saves-list">
          <div v-for="save in localSaves" :key="save.saveId" class="save-item">
            <Save class="save-icon" />
            <div class="save-info">
              <h4 class="save-name">{{ save.gameName }}</h4>
              <div class="save-meta">
                <span>{{ formatFileSize(save.fileSize) }}</span>
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
              <button class="btn-small btn-danger-small" @click="handleDeleteSave(save)">
                <X class="icon" />
                删除
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
          <div class="cloud-usage-info">
            <div class="usage-text">
              {{ (cloudSummary.storageUsedMB || 0).toFixed(1) }} MB / {{ cloudSummary.storageLimitMB || 1024 }} MB
            </div>
            <div class="usage-bar">
              <div 
                class="usage-fill" 
                :style="{ width: ((cloudSummary.storageUsedMB / cloudSummary.storageLimitMB) * 100) + '%' }"
                :class="{ warning: (cloudSummary.storageUsedMB / cloudSummary.storageLimitMB) > 0.8 }"
              ></div>
            </div>
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
                    <span>{{ formatFileSize(backup.fileSize) }}</span>
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
        <div class="dialog-header">
          <div>
            <h3 class="dialog-title">上传存档到云端</h3>
            <p class="dialog-desc">{{ selectedSave?.gameName }}</p>
          </div>
          <button class="btn-close" @click="showUploadDialog = false">
            <X class="icon" />
          </button>
        </div>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>上传类型</label>
            <div class="radio-group">
              <label class="radio-label">
                <input type="radio" v-model="uploadType" value="file" />
                单个文件
              </label>
              <label class="radio-label">
                <input type="radio" v-model="uploadType" value="folder" />
                文件夹
              </label>
            </div>
          </div>

          <div class="form-group">
            <label>选择{{ uploadType === 'folder' ? '文件夹' : '文件' }}</label>
            <input 
              v-if="uploadType === 'file'"
              type="file" 
              ref="fileInput"
              @change="handleFileSelect"
              class="form-file"
            />
            <input 
              v-else
              type="file" 
              ref="uploadFolderInput"
              webkitdirectory
              directory
              multiple
              @change="handleFolderSelect"
              class="form-file"
            />
            <p v-if="uploadFile || uploadFiles.length > 0" class="file-info-box">
              <span class="file-name">
                {{ uploadType === 'folder' ? uploadFolderName : uploadFile?.name }}
              </span>
              <span class="file-size">{{ uploadFileSize }}</span>
            </p>
          </div>
          
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="uploadForm.compress" />
              <span>压缩上传（可减小文件大小）</span>
            </label>
          </div>

          <div v-if="uploading" class="upload-progress">
            <div class="progress-bar">
              <div class="progress-fill" :style="{ width: uploadProgress + '%' }"></div>
            </div>
            <p class="progress-text">{{ uploadStatusText }}</p>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showUploadDialog = false" :disabled="uploading">取消</button>
          <button 
            class="btn-confirm" 
            @click="handleConfirmUpload" 
            :disabled="uploading || (!uploadFile && uploadFiles.length === 0)">
            <CloudUpload class="icon" />
            {{ uploading ? '上传中...' : '上传' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Add Game Dialog -->
    <div v-if="showAddGameDialog" class="dialog-overlay" @click.self="showAddGameDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">添加本地游戏记录</h3>
        <p class="dialog-desc">从你的游戏库中选择已安装的游戏</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>选择游戏（从我的游戏库）</label>
            <input 
              v-model="gameSearchQuery"
              type="text"
              class="form-input"
              placeholder="搜索我的游戏库..."
              @input="searchUserGames"
              @focus="searchUserGames"
            />
            <div v-if="searchingGames" class="search-loading">
              <div class="loading-spinner-small"></div>
              搜索中...
            </div>
            <div v-else-if="searchedGames.length > 0" class="search-results">
              <div 
                v-for="game in searchedGames" 
                :key="game.gameId"
                class="search-result-item"
                @click="selectGame(game)"
              >
                <div class="game-result-name">{{ game.name }}</div>
                <div class="game-result-meta">
                  <span v-if="game.releaseDate">{{ game.releaseDate.substring(0, 4) }}</span>
                  <span v-if="game.developer"> • {{ game.developer }}</span>
                </div>
              </div>
            </div>
            <p v-else-if="gameSearchQuery && searchedGames.length === 0 && !searchingGames && !selectedGameForAdd" class="form-hint">
              未找到匹配的游戏 (已搜索: "{{ gameSearchQuery }}")
            </p>
            <p v-if="selectedGameForAdd" class="selected-game">
              ✓ 已选择: {{ selectedGameForAdd.name }}
            </p>
          </div>
          <div class="form-group">
            <label>游戏安装路径</label>
            <div class="path-input-group">
              <input 
                v-model="addGameForm.installPath"
                type="text"
                class="form-input"
                placeholder="点击右侧按钮选择游戏文件夹"
                readonly
              />
              <button class="btn-browse" @click="browseGameFolder" type="button">
                <Folder class="icon" />
                浏览
              </button>
            </div>
            <input 
              ref="folderInput"
              type="file"
              webkitdirectory
              directory
              style="display: none"
              @change="handleGameFolderSelect"
            />
            <p v-if="addGameForm.sizeGB > 0" class="file-info">
              游戏大小: {{ addGameForm.sizeGB.toFixed(2) }} GB
            </p>
          </div>

          <!-- 存档选项 -->
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="addGameForm.includeSave" />
              同时添加存档位置（可选）
            </label>
          </div>

          <template v-if="addGameForm.includeSave">
            <div class="form-group">
              <label>存档类型</label>
              <div class="radio-group">
                <label class="radio-label">
                  <input type="radio" v-model="addGameForm.saveType" value="file" />
                  单个文件
                </label>
                <label class="radio-label">
                  <input type="radio" v-model="addGameForm.saveType" value="folder" />
                  文件夹
                </label>
              </div>
            </div>

            <div class="form-group">
              <label>存档位置</label>
              <div class="path-input-group">
                <input 
                  v-model="addGameForm.savePath"
                  type="text"
                  class="form-input"
                  placeholder="点击右侧按钮选择存档位置"
                  readonly
                />
                <button class="btn-browse" @click="browseSaveFolder" type="button">
                  <Folder class="icon" />
                  浏览
                </button>
              </div>
              <input 
                ref="saveFileInput"
                type="file"
                :webkitdirectory="addGameForm.saveType === 'folder'"
                :directory="addGameForm.saveType === 'folder'"
                :multiple="addGameForm.saveType === 'folder'"
                style="display: none"
                @change="handleSaveFolderSelect"
              />
              <p v-if="addGameForm.saveSize > 0" class="file-info">
                存档大小: {{ addGameForm.saveSize.toFixed(2) }} MB
              </p>
            </div>
          </template>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="closeAddGameDialog">取消</button>
          <button 
            class="btn-confirm" 
            @click="handleConfirmAddGame" 
            :disabled="addingGame || !selectedGameForAdd || !addGameForm.installPath"
          >
            {{ addingGame ? '添加中...' : '添加' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Edit Game Path Dialog -->
    <div v-if="showEditPathDialog" class="dialog-overlay" @click.self="showEditPathDialog = false">
      <div class="dialog-content">
        <div class="dialog-header">
          <div>
            <h3 class="dialog-title">修改游戏目录</h3>
            <p class="dialog-desc">{{ editingGame?.gameName }}</p>
          </div>
          <button class="btn-close" @click="showEditPathDialog = false">
            <X class="icon" />
          </button>
        </div>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>当前目录</label>
            <div class="path-display">{{ editingGame?.installPath }}</div>
          </div>
          
          <div class="form-group">
            <label>选择新目录</label>
            <input 
              type="file" 
              ref="editPathInput"
              webkitdirectory
              directory
              @change="handleEditPathSelect"
              class="form-file"
            />
            <p v-if="newGamePath" class="file-info-box">
              <span class="file-name">{{ newGamePath }}</span>
              <span class="file-size">{{ (newGameSize / 1024 / 1024 / 1024).toFixed(2) }} GB</span>
            </p>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showEditPathDialog = false">取消</button>
          <button 
            class="btn-confirm" 
            @click="handleConfirmEditPath" 
            :disabled="!newGamePath || savingPath">
            {{ savingPath ? '保存中...' : '保存' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Add Mod Dialog -->
    <div v-if="showAddModDialog" class="dialog-overlay" @click.self="showAddModDialog = false">
      <div class="dialog-content">
        <div class="dialog-header">
          <div>
            <h3 class="dialog-title">添加 Mod 记录</h3>
            <p class="dialog-desc">{{ modTargetGame?.gameName }}</p>
          </div>
          <button class="btn-close" @click="showAddModDialog = false">
            <X class="icon" />
          </button>
        </div>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>Mod 名称</label>
            <input type="text" v-model="addModForm.modName" class="form-input" placeholder="输入 Mod 名称" />
          </div>
          
          <div class="form-group">
            <label>版本</label>
            <input type="text" v-model="addModForm.version" class="form-input" placeholder="如: 1.0.0" />
          </div>
          
          <div class="form-group">
            <label>选择 Mod 文件/文件夹</label>
            <input 
              type="file" 
              ref="modFileInput"
              webkitdirectory
              directory
              multiple
              @change="handleModFileSelect"
              class="form-file"
            />
            <p v-if="addModForm.filePath" class="file-info-box">
              <span class="file-name">{{ addModForm.filePath }}</span>
            </p>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="showAddModDialog = false">取消</button>
          <button 
            class="btn-confirm" 
            @click="handleConfirmAddMod" 
            :disabled="!addModForm.modName || !addModForm.filePath || addingMod">
            {{ addingMod ? '添加中...' : '添加' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Add Save Dialog -->
    <div v-if="showAddSaveDialog" class="dialog-overlay" @click.self="showAddSaveDialog = false">
      <div class="dialog-content">
        <h3 class="dialog-title">添加本地存档记录</h3>
        <p class="dialog-desc">选择存档文件，系统将记录文件信息到数据库</p>
        
        <div class="dialog-form">
          <div class="form-group">
            <label>选择游戏</label>
            <select v-model="addSaveForm.installId" class="form-select">
              <option value="">请选择游戏</option>
              <option v-for="game in localGames" :key="game.installId" :value="game.installId">
                {{ game.gameName }} ({{ game.platformName }})
              </option>
            </select>
            <p v-if="localGames.length === 0" class="form-hint">
              暂无本地游戏记录，请先在"本地游戏"标签页添加游戏
            </p>
          </div>
          <div class="form-group">
            <label>存档类型</label>
            <div class="radio-group">
              <label class="radio-label">
                <input type="radio" v-model="addSaveForm.saveType" value="file" />
                单个文件
              </label>
              <label class="radio-label">
                <input type="radio" v-model="addSaveForm.saveType" value="folder" />
                文件夹
              </label>
            </div>
          </div>
          <div class="form-group">
            <label>选择存档{{ addSaveForm.saveType === 'folder' ? '文件夹' : '文件' }}</label>
            <input 
              v-if="addSaveForm.saveType === 'file'"
              type="file" 
              ref="addSaveFileInput"
              @change="handleAddSaveFileSelect"
              class="form-file"
            />
            <input 
              v-else
              type="file" 
              ref="addSaveFileInput"
              @change="handleAddSaveFileSelect"
              webkitdirectory
              directory
              multiple
              class="form-file"
            />
            <p v-if="addSaveFile" class="file-info">
              {{ addSaveForm.saveType === 'folder' ? '文件夹' : '文件名' }}: {{ addSaveFileName }}<br>
              大小: {{ addSaveFileSize }}<br>
              {{ addSaveForm.saveType === 'folder' ? '文件数量: ' + addSaveFileCount + '<br>' : '' }}
              修改时间: {{ addSaveFileTime }}
            </p>
          </div>
        </div>

        <div class="dialog-actions">
          <button class="btn-cancel" @click="closeAddSaveDialog">取消</button>
          <button 
            class="btn-confirm" 
            @click="handleConfirmAddSave" 
            :disabled="addingSave || !addSaveForm.installId || !addSaveFile"
          >
            {{ addingSave ? '添加中...' : '添加' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { 
  HardDrive, Package, Save, Cloud, RefreshCw, Check, X, 
  Folder, CloudUpload, Download, AlertCircle, Plus
} from 'lucide-vue-next'
import JSZip from 'jszip'
import {
  getLocalGames,
  addLocalGame,
  removeLocalGame,
  updateLocalGamePath,
  getLocalSaves,
  addLocalSave,
  deleteLocalSave,
  getCloudSaves,
  uploadCloudSave,
  downloadCloudSave,
  deleteCloudSave,
  getCloudStorageUsage,
  getGameMods,
  getLocalGameDetail,
  addMod,
  toggleMod,
  deleteMod
} from '@/api/localGame'
import { libraryApi } from '@/api/index'

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
const showAddSaveDialog = ref(false)
const showAddGameDialog = ref(false)
const showEditPathDialog = ref(false)
const showAddModDialog = ref(false)
const selectedGame = ref(null)
const selectedSave = ref(null)

// Add Game
const folderInput = ref(null)
const saveFileInput = ref(null) // 存档文件选择器
const gameSearchQuery = ref('')
const searchedGames = ref([])
const searchingGames = ref(false)
const selectedGameForAdd = ref(null)
const addGameForm = ref({
  installPath: '',
  sizeGB: 0,
  // 存档相关
  includeSave: false,
  savePath: '',
  saveType: 'folder', // 'file' 或 'folder'
  saveSize: 0
})
const addingGame = ref(false)

// Upload
const uploadType = ref('file')
const uploadFile = ref(null)
const uploadFiles = ref([])
const uploadForm = ref({ compress: false })
const uploading = ref(false)
const uploadProgress = ref(0)
const uploadStatusText = ref('')
const fileInput = ref(null)
const uploadFolderInput = ref(null)

const uploadFolderName = computed(() => {
  if (uploadFiles.value.length === 0) return ''
  const firstFile = uploadFiles.value[0]
  const pathParts = firstFile.webkitRelativePath.split('/')
  return pathParts[0] || '未知文件夹'
})

const uploadFileSize = computed(() => {
  if (uploadType.value === 'folder' && uploadFiles.value.length > 0) {
    const totalSize = uploadFiles.value.reduce((sum, file) => sum + file.size, 0)
    return `${(totalSize / 1024 / 1024).toFixed(2)} MB (${uploadFiles.value.length} 个文件)`
  } else if (uploadFile.value) {
    return `${(uploadFile.value.size / 1024 / 1024).toFixed(2)} MB`
  }
  return '0 MB'
})

// Add Save
const addSaveFile = ref(null)
const addSaveFiles = ref([]) // 用于文件夹
const addSaveForm = ref({ 
  installId: '',
  saveType: 'file' // 'file' 或 'folder'
})
const addingSave = ref(false)
const addSaveFileInput = ref(null)

// 计算属性
const addSaveFileName = computed(() => {
  if (!addSaveFile.value) return ''
  if (addSaveForm.value.saveType === 'folder' && addSaveFiles.value.length > 0) {
    // 获取文件夹名称（从第一个文件的路径中提取）
    const firstFile = addSaveFiles.value[0]
    const pathParts = firstFile.webkitRelativePath.split('/')
    return pathParts[0] || firstFile.name
  }
  return addSaveFile.value.name
})

const addSaveFileSize = computed(() => {
  if (!addSaveFile.value) return '0 MB'
  if (addSaveForm.value.saveType === 'folder' && addSaveFiles.value.length > 0) {
    const totalSize = addSaveFiles.value.reduce((sum, file) => sum + file.size, 0)
    return (totalSize / 1024 / 1024).toFixed(2) + ' MB'
  }
  return (addSaveFile.value.size / 1024 / 1024).toFixed(2) + ' MB'
})

const addSaveFileCount = computed(() => {
  return addSaveFiles.value.length
})

const addSaveFileTime = computed(() => {
  if (!addSaveFile.value) return ''
  return new Date(addSaveFile.value.lastModified).toLocaleString('zh-CN')
})

// Methods
const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN') + ' ' + date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

// 格式化文件大小（字节转换为合适的单位）
const formatFileSize = (bytes) => {
  if (!bytes || bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let unitIndex = 0
  let size = bytes
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  return size.toFixed(unitIndex === 0 ? 0 : 2) + ' ' + units[unitIndex]
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
    // 使用 getLocalGameDetail 获取游戏详情（包含 Mods）
    const res = await getLocalGameDetail(game.installId)
    if (res.data?.mods) {
      gameMods.value = res.data.mods.map(mod => ({
        modId: mod.modId,
        modName: mod.modName,
        version: mod.version,
        enabled: mod.enabled,
        filePath: mod.filePath,
        sizeGB: mod.sizeGB || 0
      }))
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

// 编辑游戏目录
const editingGame = ref(null)
const newGamePath = ref('')
const newGameSize = ref(0)
const savingPath = ref(false)
const editPathInput = ref(null)

const handleEditGamePath = (game) => {
  editingGame.value = game
  newGamePath.value = ''
  newGameSize.value = 0
  showEditPathDialog.value = true
}

const handleEditPathSelect = (e) => {
  const files = e.target.files
  if (files && files.length > 0) {
    const firstFile = files[0]
    const pathParts = firstFile.webkitRelativePath.split('/')
    newGamePath.value = pathParts[0] || ''
    
    // 计算文件夹总大小
    let totalSize = 0
    for (let i = 0; i < files.length; i++) {
      totalSize += files[i].size
    }
    newGameSize.value = totalSize
  }
}

const handleConfirmEditPath = async () => {
  if (!newGamePath.value || !editingGame.value) return
  
  savingPath.value = true
  try {
    // 计算 GB 大小
    const sizeGB = newGameSize.value / 1024 / 1024 / 1024
    
    await updateLocalGamePath(editingGame.value.installId, newGamePath.value, sizeGB)
    
    // 更新本地数据
    const game = localGames.value.find(g => g.installId === editingGame.value.installId)
    if (game) {
      game.installPath = newGamePath.value
      game.sizeGB = sizeGB
    }
    
    showEditPathDialog.value = false
    alert('目录已更新')
  } catch (err) {
    console.error('更新目录失败:', err)
    alert('更新失败: ' + (err.response?.data?.message || err.message || '未知错误'))
  } finally {
    savingPath.value = false
  }
}

// 删除本地存档
const handleDeleteSave = async (save) => {
  if (!confirm(`确定要删除 ${save.gameName} 的存档记录吗？\n注意：这只会删除数据库记录，不会删除本地文件。`)) return
  
  try {
    await deleteLocalSave(save.saveId)
    localSaves.value = localSaves.value.filter(s => s.saveId !== save.saveId)
    alert('存档记录已删除')
  } catch (err) {
    console.error('删除存档失败:', err)
    alert('删除失败: ' + (err.response?.data?.message || err.message || '未知错误'))
  }
}

// 添加 Mod
const modTargetGame = ref(null)
const modFileInput = ref(null)
const addModForm = ref({
  modName: '',
  version: '',
  filePath: ''
})
const addingMod = ref(false)

const handleAddMod = (game) => {
  modTargetGame.value = game
  addModForm.value = { modName: '', version: '', filePath: '' }
  showAddModDialog.value = true
}

const handleModFileSelect = (e) => {
  const files = e.target.files
  if (files && files.length > 0) {
    const firstFile = files[0]
    const pathParts = firstFile.webkitRelativePath.split('/')
    addModForm.value.filePath = pathParts[0] || firstFile.name
  }
}

const handleConfirmAddMod = async () => {
  if (!addModForm.value.modName || !addModForm.value.filePath || !modTargetGame.value) return
  
  addingMod.value = true
  try {
    // 解析版本号，提取主版本号作为整数
    let versionInt = 1
    const versionStr = addModForm.value.version || '1.0'
    const versionMatch = versionStr.match(/^(\d+)/)
    if (versionMatch) {
      versionInt = parseInt(versionMatch[1], 10)
    }
    
    await addMod({
      installId: modTargetGame.value.installId,
      modName: addModForm.value.modName,
      version: versionInt,
      filePath: addModForm.value.filePath,
      description: addModForm.value.description || '',
      author: addModForm.value.author || '',
      autoEnable: true
    })
    
    showAddModDialog.value = false
    alert('Mod 记录添加成功！')
    loadData()
  } catch (err) {
    console.error('添加 Mod 失败:', err)
    alert('添加失败: ' + (err.response?.data?.message || err.message || '未知错误'))
  } finally {
    addingMod.value = false
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
  uploadType.value = 'file'
  uploadFile.value = null
  uploadFiles.value = []
  uploadForm.value = { compress: false }
  showUploadDialog.value = true
}

const handleFileSelect = (e) => {
  const file = e.target.files[0]
  if (file) {
    uploadFile.value = file
  }
}

const handleFolderSelect = (e) => {
  const files = e.target.files
  if (files && files.length > 0) {
    uploadFiles.value = Array.from(files)
  }
}

const handleConfirmUpload = async () => {
  if (!selectedSave.value) return
  if (uploadType.value === 'file' && !uploadFile.value) {
    alert('请选择文件')
    return
  }
  if (uploadType.value === 'folder' && uploadFiles.value.length === 0) {
    alert('请选择文件夹')
    return
  }
  
  uploading.value = true
  uploadProgress.value = 0
  uploadStatusText.value = '准备上传...'
  
  try {
    let fileToUpload = uploadFile.value
    
    // 如果是文件夹，打包成 zip
    if (uploadType.value === 'folder' && uploadFiles.value.length > 0) {
      uploadStatusText.value = '正在打包文件夹...'
      uploadProgress.value = 10
      
      const zip = new JSZip()
      
      // 添加所有文件到 zip
      for (let i = 0; i < uploadFiles.value.length; i++) {
        const file = uploadFiles.value[i]
        const relativePath = file.webkitRelativePath
        zip.file(relativePath, file)
        uploadProgress.value = 10 + Math.floor((i / uploadFiles.value.length) * 30)
      }
      
      uploadStatusText.value = '正在生成压缩包...'
      uploadProgress.value = 40
      
      // 生成 zip blob
      const zipBlob = await zip.generateAsync({ 
        type: 'blob',
        compression: 'DEFLATE',
        compressionOptions: { level: 6 }
      }, (metadata) => {
        uploadProgress.value = 40 + Math.floor(metadata.percent / 100 * 10)
      })
      
      const folderName = uploadFolderName.value
      fileToUpload = new File([zipBlob], `${folderName}.zip`, { type: 'application/zip' })
      uploadProgress.value = 50
    }
    
    uploadStatusText.value = '正在上传...'
    
    const formData = new FormData()
    formData.append('file', fileToUpload)
    formData.append('saveId', selectedSave.value.saveId)
    formData.append('compress', uploadForm.value.compress)
    
    // 模拟上传进度
    const progressInterval = setInterval(() => {
      if (uploadProgress.value < 90) {
        uploadProgress.value += 5
      }
    }, 200)
    
    await uploadCloudSave(formData)
    
    clearInterval(progressInterval)
    uploadProgress.value = 100
    uploadStatusText.value = '上传完成！'
    
    setTimeout(() => {
      showUploadDialog.value = false
      uploadFile.value = null
      uploadFiles.value = []
      uploadProgress.value = 0
      uploadStatusText.value = ''
      alert('上传成功！')
      loadCloudSaves()
      loadCloudUsage()
    }, 500)
  } catch (err) {
    console.error('上传失败:', err)
    alert('上传失败: ' + (err.response?.data?.message || err.message || '未知错误'))
    uploadProgress.value = 0
    uploadStatusText.value = ''
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
    // 文件名格式: 游戏名_云存档_日期.zip
    const dateStr = new Date(backup.uploadTime).toISOString().slice(0, 10)
    const safeName = (backup.gameName || 'save').replace(/[<>:"/\\|?*]/g, '_')
    link.download = `${safeName}_云存档_${dateStr}.zip`
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

// Add Game handlers
let searchTimeout = null

const searchUserGames = async () => {
  // 清除之前的定时器
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  
  // 如果输入为空，清空结果
  if (gameSearchQuery.value.length === 0) {
    searchedGames.value = []
    return
  }
  
  // 防抖：延迟300ms执行搜索
  searchTimeout = setTimeout(async () => {
    searchingGames.value = true
    try {
      // 从用户游戏库中搜索
      const response = await libraryApi.getGames({
        search: gameSearchQuery.value,
        page: 1,
        pageSize: 10
      })
      
      if (response.data?.items) {
        // 转换字段名以匹配现有逻辑 (后端返回 name, 前端需要 name)
        searchedGames.value = response.data.items.map(item => ({
          gameId: item.gameId,
          name: item.name,
          headerImage: item.headerImage
        }))
      } else {
        searchedGames.value = []
      }
    } catch (err) {
      console.error('搜索游戏失败:', err)
      searchedGames.value = []
    } finally {
      searchingGames.value = false
    }
  }, 300) // 300ms防抖
}

const selectGame = (game) => {
  selectedGameForAdd.value = game
  gameSearchQuery.value = game.name
  searchedGames.value = [] // 选择后清空下拉列表
}

const browseGameFolder = () => {
  if (folderInput.value) {
    folderInput.value.click()
  }
}

const browseSaveFolder = () => {
  if (saveFileInput.value) {
    saveFileInput.value.click()
  }
}

const handleGameFolderSelect = (e) => {
  const files = e.target.files
  if (!files || files.length === 0) return
  
  // 获取文件夹路径（从第一个文件的路径中提取）
  const firstFile = files[0]
  if (firstFile.webkitRelativePath) {
    const pathParts = firstFile.webkitRelativePath.split('/')
    const folderName = pathParts[0]
    addGameForm.value.installPath = folderName
    
    // 计算文件夹总大小
    const totalSize = Array.from(files).reduce((sum, file) => sum + file.size, 0)
    addGameForm.value.sizeGB = parseFloat((totalSize / 1024 / 1024 / 1024).toFixed(2))
  }
}

const handleSaveFolderSelect = (e) => {
  const files = e.target.files
  if (!files || files.length === 0) return
  
  if (addGameForm.value.saveType === 'folder') {
    // 文件夹模式
    const firstFile = files[0]
    if (firstFile.webkitRelativePath) {
      const pathParts = firstFile.webkitRelativePath.split('/')
      addGameForm.value.savePath = pathParts[0]
      
      // 计算总大小
      const totalSize = Array.from(files).reduce((sum, file) => sum + file.size, 0)
      addGameForm.value.saveSize = parseFloat((totalSize / 1024 / 1024).toFixed(2))
    }
  } else {
    // 文件模式
    const file = files[0]
    addGameForm.value.savePath = file.name
    addGameForm.value.saveSize = parseFloat((file.size / 1024 / 1024).toFixed(2))
  }
}

const closeAddGameDialog = () => {
  showAddGameDialog.value = false
  gameSearchQuery.value = ''
  searchedGames.value = []
  selectedGameForAdd.value = null
  addGameForm.value = {
    installPath: '',
    sizeGB: 0,
    includeSave: false,
    savePath: '',
    saveType: 'folder',
    saveSize: 0
  }
}

const handleConfirmAddGame = async () => {
  if (!selectedGameForAdd.value || !addGameForm.value.installPath) return
  
  addingGame.value = true
  try {
    // 1. 添加游戏
    const gameData = {
      gameId: selectedGameForAdd.value.gameId,
      platformId: null,
      installPath: addGameForm.value.installPath,
      version: 'Unknown',
      sizeGB: addGameForm.value.sizeGB || 0
    }
    
    const gameResult = await addLocalGame(gameData)
    
    // 2. 如果勾选了添加存档，则添加存档
    if (addGameForm.value.includeSave && addGameForm.value.savePath && gameResult.data) {
      try {
        const saveData = {
          installId: gameResult.data.installId,
          filePath: addGameForm.value.savePath,
          fileSize: Math.round(addGameForm.value.saveSize * 1024), // 转换为KB
          updatedAt: new Date().toISOString()
        }
        
        await addLocalSave(saveData)
        alert('游戏和存档记录添加成功！')
      } catch (saveErr) {
        console.error('添加存档失败:', saveErr)
        alert('游戏添加成功，但存档添加失败: ' + (saveErr.response?.data?.message || saveErr.message))
      }
    } else {
      alert('游戏记录添加成功！')
    }
    
    closeAddGameDialog()
    loadLocalGames()
    if (addGameForm.value.includeSave) {
      loadLocalSaves()
    }
  } catch (err) {
    console.error('添加游戏失败:', err)
    alert('添加失败: ' + (err.response?.data?.message || err.message || '未知错误'))
  } finally {
    addingGame.value = false
  }
}

// Add Save handlers
const handleAddSaveFileSelect = (e) => {
  const files = e.target.files
  if (!files || files.length === 0) return
  
  if (addSaveForm.value.saveType === 'folder') {
    // 文件夹模式：保存所有文件
    addSaveFiles.value = Array.from(files)
    addSaveFile.value = files[0] // 用第一个文件作为代表
  } else {
    // 文件模式：只保存单个文件
    addSaveFile.value = files[0]
    addSaveFiles.value = []
  }
}

const closeAddSaveDialog = () => {
  showAddSaveDialog.value = false
  addSaveFile.value = null
  addSaveFiles.value = []
  addSaveForm.value = { 
    installId: '',
    saveType: 'file'
  }
}

const handleConfirmAddSave = async () => {
  if (!addSaveFile.value || !addSaveForm.value.installId) return
  
  addingSave.value = true
  try {
    let filePath = ''
    let fileSize = 0
    
    if (addSaveForm.value.saveType === 'folder') {
      // 文件夹模式：使用文件夹名称和总大小
      const firstFile = addSaveFiles.value[0]
      const pathParts = firstFile.webkitRelativePath.split('/')
      filePath = pathParts[0] || firstFile.name
      fileSize = addSaveFiles.value.reduce((sum, file) => sum + file.size, 0)
    } else {
      // 文件模式：使用文件名和大小
      filePath = addSaveFile.value.name
      fileSize = addSaveFile.value.size
    }
    
    const saveData = {
      installId: parseInt(addSaveForm.value.installId),
      filePath: filePath,
      fileSize: fileSize,
      updatedAt: new Date(addSaveFile.value.lastModified).toISOString()
    }
    
    await addLocalSave(saveData)
    
    closeAddSaveDialog()
    alert('存档记录添加成功！')
    loadLocalSaves()
  } catch (err) {
    console.error('添加存档失败:', err)
    alert('添加失败: ' + (err.response?.data?.message || err.message || '未知错误'))
  } finally {
    addingSave.value = false
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

.btn-danger-small {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
  border: none;
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-danger-small:hover {
  background: rgba(239, 68, 68, 0.2);
}

.btn-danger-small .icon {
  width: 14px;
  height: 14px;
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
.cloud-usage-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 200px;
}

.usage-text {
  font-size: 13px;
  color: var(--text-secondary);
  text-align: right;
}

.usage-bar {
  width: 200px;
  height: 6px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  overflow: hidden;
}

.usage-fill {
  height: 100%;
  background: linear-gradient(90deg, #34d399, #10b981);
  border-radius: 3px;
  transition: width 0.3s ease, background 0.3s ease;
}

.usage-fill.warning {
  background: linear-gradient(90deg, #fbbf24, #f59e0b);
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
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.form-file:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.2);
}

.form-file::file-selector-button {
  padding: 6px 16px;
  background: var(--primary-color);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  margin-right: 12px;
  transition: background 0.2s;
}

.form-file::file-selector-button:hover {
  background: var(--primary-hover);
}

.form-select {
  width: 100%;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: white;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
}

.form-select:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.2);
}

.form-select:focus {
  outline: none;
  border-color: var(--primary-color);
  background: rgba(255, 255, 255, 0.08);
}

.form-select option {
  background: #2a2a2e;
  color: white;
  padding: 8px;
}

.form-select option:hover {
  background: rgba(99, 102, 241, 0.2);
}

.form-select option:checked {
  background: rgba(99, 102, 241, 0.3);
  color: white;
}

.radio-group {
  display: flex;
  gap: 20px;
  padding: 8px 0;
}

.radio-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  padding: 6px 12px;
  border-radius: 6px;
  transition: all 0.2s;
  user-select: none;
}

.radio-label:hover {
  background: rgba(255, 255, 255, 0.05);
  color: white;
}

.radio-label input[type="radio"] {
  cursor: pointer;
  width: 16px;
  height: 16px;
  accent-color: var(--primary-color);
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  font-weight: 500;
  padding: 8px 0;
  user-select: none;
}

.checkbox-label:hover {
  color: white;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 18px;
  height: 18px;
  accent-color: var(--primary-color);
}

.file-info {
  margin-top: 8px;
  padding: 10px 12px;
  background: rgba(99, 102, 241, 0.1);
  border-left: 3px solid var(--primary-color);
  border-radius: 6px;
  font-size: 13px;
  color: #a5b4fc;
  line-height: 1.6;
}

.form-hint {
  margin-top: 8px;
  font-size: 13px;
  color: #fbbf24;
  padding: 8px 12px;
  background: rgba(251, 191, 36, 0.1);
  border-radius: 6px;
  border-left: 3px solid #fbbf24;
}

.form-input {
  width: 100%;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: white;
  font-size: 14px;
}

.form-input:focus {
  outline: none;
  border-color: var(--primary-color);
}

.search-results {
  max-height: 200px;
  overflow-y: auto;
  margin-top: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
}

.search-loading {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}

.loading-spinner-small {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-top-color: var(--primary-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.search-result-item {
  padding: 10px 12px;
  cursor: pointer;
  transition: background 0.2s;
  font-size: 14px;
  color: var(--text-primary);
}

.search-result-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.selected-game {
  margin-top: 8px;
  padding: 8px;
  background: rgba(99, 102, 241, 0.2);
  border-radius: 6px;
  font-size: 13px;
  color: #818cf8;
}

.path-input-group {
  display: flex;
  gap: 8px;
}

.path-input-group .form-input {
  flex: 1;
  cursor: not-allowed;
}

.btn-browse {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-primary);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
}

.btn-browse:hover {
  background: rgba(255, 255, 255, 0.15);
  border-color: var(--primary-color);
}

.btn-browse .icon {
  width: 16px;
  height: 16px;
}

.game-result-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: 2px;
}

.game-result-meta {
  font-size: 12px;
  color: var(--text-secondary);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
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

/* Warning Box */
.warning-box {
  display: flex;
  gap: 12px;
  padding: 16px;
  background: rgba(251, 191, 36, 0.1);
  border: 1px solid rgba(251, 191, 36, 0.3);
  border-radius: 8px;
  margin-bottom: 16px;
}

.warning-box .icon {
  width: 20px;
  height: 20px;
  color: #fbbf24;
  flex-shrink: 0;
  margin-top: 2px;
}

.warning-title {
  font-size: 14px;
  font-weight: 600;
  color: #fbbf24;
  margin: 0 0 4px 0;
}

.warning-text {
  font-size: 13px;
  color: var(--text-secondary);
  margin: 0;
  line-height: 1.5;
}

/* Path Display */
.path-display {
  padding: 12px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 13px;
  color: var(--text-secondary);
  word-break: break-all;
  margin-bottom: 8px;
}

.hint-text {
  font-size: 12px;
  color: var(--text-secondary);
  margin: 0;
  opacity: 0.7;
}

/* File Info Box */
.file-info-box {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  background: rgba(139, 92, 246, 0.1);
  border: 1px solid rgba(139, 92, 246, 0.3);
  border-radius: 6px;
  margin-top: 8px;
}

.file-name {
  font-size: 13px;
  color: var(--text-primary);
  font-weight: 500;
}

.file-size {
  font-size: 12px;
  color: var(--text-secondary);
  background: rgba(255, 255, 255, 0.05);
  padding: 4px 8px;
  border-radius: 4px;
}

/* Upload Progress */
.upload-progress {
  margin-top: 20px;
  padding: 16px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 8px;
}

.progress-bar {
  width: 100%;
  height: 8px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 8px;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--primary-color), #818cf8);
  border-radius: 4px;
  transition: width 0.3s ease;
}

.progress-text {
  font-size: 13px;
  color: var(--text-secondary);
  text-align: center;
  margin: 0;
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
