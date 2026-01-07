<template>
  <div v-if="totalPages > 1" class="pagination-container">
    <button
      @click="handlePageChange(currentPage - 1)"
      :disabled="currentPage === 1"
      class="pagination-btn prev-btn"
    >
      上一页
    </button>

    <div class="page-numbers">
      <button
        v-for="pageNum in visiblePages"
        :key="pageNum"
        @click="handlePageChange(pageNum)"
        :class="['page-number', { active: pageNum === currentPage, ellipsis: pageNum === '...' }]"
        :disabled="pageNum === '...'"
      >
        {{ pageNum }}
      </button>
    </div>

    <button
      @click="handlePageChange(currentPage + 1)"
      :disabled="currentPage >= totalPages"
      class="pagination-btn next-btn"
    >
      下一页
    </button>

    <div class="page-jump">
      <span>转到</span>
      <input
        v-model.number="jumpPage"
        type="number"
        :min="1"
        :max="totalPages"
        class="jump-input"
        @keyup.enter="handleJump"
      />
      <span>页</span>
      <button @click="handleJump" class="jump-btn">确定</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'

const props = defineProps({
  currentPage: {
    type: Number,
    required: true,
    default: 1
  },
  totalPages: {
    type: Number,
    required: true,
    default: 1
  }
})

const emit = defineEmits(['update:currentPage', 'page-change'])

const jumpPage = ref(1)

// 计算可见的页码按钮
const visiblePages = computed(() => {
  const pages = []
  const total = props.totalPages
  const current = props.currentPage

  if (total <= 7) {
    // 如果总页数少于等于7，显示所有页码
    for (let i = 1; i <= total; i++) {
      pages.push(i)
    }
  } else {
    // 总页数大于7，显示部分页码
    if (current <= 4) {
      // 当前页在前4页，显示 1 2 3 4 5 ... total
      for (let i = 1; i <= 5; i++) {
        pages.push(i)
      }
      pages.push('...')
      pages.push(total)
    } else if (current >= total - 3) {
      // 当前页在后4页，显示 1 ... total-4 total-3 total-2 total-1 total
      pages.push(1)
      pages.push('...')
      for (let i = total - 4; i <= total; i++) {
        pages.push(i)
      }
    } else {
      // 当前页在中间，显示 1 ... current-1 current current+1 ... total
      pages.push(1)
      pages.push('...')
      for (let i = current - 1; i <= current + 1; i++) {
        pages.push(i)
      }
      pages.push('...')
      pages.push(total)
    }
  }

  return pages
})

const handlePageChange = (page) => {
  if (page < 1 || page > props.totalPages || page === props.currentPage) {
    return
  }
  emit('update:currentPage', page)
  emit('page-change', page)
}

const handleJump = () => {
  const page = parseInt(jumpPage.value)
  if (page >= 1 && page <= props.totalPages) {
    handlePageChange(page)
    jumpPage.value = page
  } else {
    jumpPage.value = props.currentPage
  }
}

// 监听当前页变化，同步跳转输入框
watch(() => props.currentPage, (newPage) => {
  jumpPage.value = newPage
}, { immediate: true })
</script>

<style scoped>
.pagination-container {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px 0;
  flex-wrap: wrap;
}

.pagination-btn {
  padding: 8px 16px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.1);
  color: #cbd5e1;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.pagination-btn:hover:not(:disabled) {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.5);
  color: #f8fafc;
  transform: translateY(-1px);
}

.pagination-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.page-numbers {
  display: flex;
  align-items: center;
  gap: 6px;
}

.page-number {
  min-width: 36px;
  height: 36px;
  padding: 0 8px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.1);
  color: #cbd5e1;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-number:hover:not(:disabled):not(.active) {
  background: rgba(139, 92, 246, 0.2);
  border-color: rgba(139, 92, 246, 0.5);
  color: #f8fafc;
}

.page-number.active {
  background: rgba(139, 92, 246, 0.4);
  border-color: #8b5cf6;
  color: #f8fafc;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
}

.page-number.ellipsis {
  border: none;
  background: transparent;
  cursor: default;
  min-width: auto;
  padding: 0 4px;
}

.page-number.ellipsis:hover {
  background: transparent;
  color: #cbd5e1;
}

.page-jump {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: 12px;
  font-size: 14px;
  color: #94a3b8;
}

.jump-input {
  width: 60px;
  height: 36px;
  padding: 0 8px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.1);
  color: #cbd5e1;
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
  transition: all 0.2s ease;
}

.jump-input:focus {
  outline: none;
  border-color: #8b5cf6;
  background: rgba(139, 92, 246, 0.15);
  color: #f8fafc;
}

.jump-input::-webkit-inner-spin-button,
.jump-input::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.jump-input[type=number] {
  -moz-appearance: textfield;
}

.jump-btn {
  padding: 6px 12px;
  border: 1px solid rgba(139, 92, 246, 0.3);
  background: rgba(139, 92, 246, 0.2);
  color: #cbd5e1;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.jump-btn:hover {
  background: rgba(139, 92, 246, 0.3);
  border-color: rgba(139, 92, 246, 0.5);
  color: #f8fafc;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .pagination-container {
    gap: 8px;
  }

  .page-numbers {
    gap: 4px;
  }

  .page-number {
    min-width: 32px;
    height: 32px;
    font-size: 13px;
  }

  .pagination-btn {
    padding: 6px 12px;
    font-size: 13px;
  }

  .page-jump {
    width: 100%;
    justify-content: center;
    margin-left: 0;
    margin-top: 12px;
  }
}
</style>

