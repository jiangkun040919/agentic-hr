<template>
  <Teleport to="body">
    <div
      v-if="visible"
      ref="popupRef"
      class="draggable-resume-popup"
      :class="{ 'popup-wide': viewMode === 'file' }"
      :style="{ left: pos.x + 'px', top: pos.y + 'px' }"
    >
      <div class="popup-header" @mousedown="startDrag">
        <span>📄 原始简历</span>
        <div class="popup-actions">
          <!-- 视图切换 -->
          <el-button-group v-if="deliveryId" size="small" class="view-toggle">
            <el-button :type="viewMode === 'text' ? 'primary' : ''" @click.stop="viewMode = 'text'">
              <el-icon><Document /></el-icon>文本
            </el-button>
            <el-button :type="viewMode === 'file' ? 'primary' : ''" @click.stop="viewMode = 'file'">
              <el-icon><View /></el-icon>原文件
            </el-button>
          </el-button-group>
          <el-button v-if="deliveryId" link title="下载原文件" @click.stop="downloadFile">
            <el-icon><Download /></el-icon>
          </el-button>
          <el-button link @click="toggleMinimize">
            <el-icon><Minus v-if="!minimized" /><Plus v-else /></el-icon>
          </el-button>
          <el-button link @click="$emit('close')">
            <el-icon><Close /></el-icon>
          </el-button>
        </div>
      </div>
      <div v-show="!minimized" class="popup-body">
        <!-- 文本视图 -->
        <div v-if="viewMode === 'text'">
          <div v-if="!content" class="popup-empty">
            <el-empty description="暂无简历原文" :image-size="40" />
          </div>
          <div v-else class="popup-content">
            <pre>{{ content }}</pre>
          </div>
        </div>

        <!-- 原文件视图 -->
        <div v-else class="file-preview-container" v-loading="fileLoading">
          <template v-if="filePreviewUrl">
            <iframe
              :src="filePreviewUrl"
              class="pdf-iframe"
              frameborder="0"
            />
          </template>
          <div v-else-if="!fileLoading" class="popup-empty">
            <el-empty description="暂无原始文件" :image-size="40" />
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onBeforeUnmount, watch } from 'vue'
import { Minus, Plus, Close, Download, Document, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { request } from '@/utils/request'

const props = defineProps<{
  visible: boolean
  content: string
  deliveryId?: number
  resumeUrl?: string
}>()

defineEmits<{ (e: 'close'): void }>()

const popupRef = ref<HTMLElement>()
const minimized = ref(false)
const viewMode = ref<'text' | 'file'>('text')
const pos = reactive({ x: 0, y: 0 })
let dragging = false
let startX = 0, startY = 0

// 判断文件类型
const isPdf = computed(() => {
  const url = props.resumeUrl || ''
  return url.toLowerCase().endsWith('.pdf')
})
const isWord = computed(() => {
  const url = props.resumeUrl || ''
  return url.match(/\.(doc|docx)$/i)
})

// 文件预览 URL（带 token 的图片类请求走 blob 方式，PDF 可直接用 iframe）
const filePreviewUrl = ref('')
const fileLoading = ref(false)

// 切换到文件视图时加载文件
watch(viewMode, async (mode) => {
  if (mode === 'file' && props.deliveryId && !filePreviewUrl.value) {
    await loadFileForPreview()
  }
})

const loadFileForPreview = async () => {
  if (!props.deliveryId) return
  fileLoading.value = true
  try {
    const response = await request.get(`/delivery/${props.deliveryId}/download-resume`, {
      responseType: 'blob'
    })
    const blob: Blob = (response as any).data || response
    if (filePreviewUrl.value) URL.revokeObjectURL(filePreviewUrl.value)
    filePreviewUrl.value = URL.createObjectURL(blob)
  } catch (e: any) {
    ElMessage.error('加载文件失败')
  } finally {
    fileLoading.value = false
  }
}

// Start at bottom-right of viewport
if (typeof window !== 'undefined') {
  pos.x = window.innerWidth - 420
  pos.y = window.innerHeight - 350
}

// 原文件视图需要更宽的弹窗
watch(viewMode, (mode) => {
  if (mode === 'file') {
    pos.x = Math.max(0, window.innerWidth - 820)
    pos.y = Math.max(0, window.innerHeight - 650)
  }
})

const startDrag = (e: MouseEvent) => {
  if ((e.target as HTMLElement).closest('.popup-actions')) return
  dragging = true
  startX = e.clientX - pos.x
  startY = e.clientY - pos.y
  document.addEventListener('mousemove', onDrag)
  document.addEventListener('mouseup', stopDrag)
}

const onDrag = (e: MouseEvent) => {
  if (!dragging) return
  pos.x = Math.max(0, Math.min(e.clientX - startX, window.innerWidth - 400))
  pos.y = Math.max(0, Math.min(e.clientY - startY, window.innerHeight - 40))
}

const stopDrag = () => {
  dragging = false
  document.removeEventListener('mousemove', onDrag)
  document.removeEventListener('mouseup', stopDrag)
}

const toggleMinimize = () => {
  minimized.value = !minimized.value
}

const downloadFile = async () => {
  if (!props.deliveryId) return
  try {
    const response = await request.get(`/delivery/${props.deliveryId}/download-resume`, {
      responseType: 'blob'
    })
    const blob: Blob = (response as any).data || response
    const url = URL.createObjectURL(blob)
    const ext = isPdf.value ? '.pdf' : isWord.value ? (props.resumeUrl?.match(/\.(docx?)/i)?.[0] || '.doc') : ''
    const a = document.createElement('a')
    a.href = url
    a.download = `resume_${props.deliveryId}${ext}`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
    ElMessage.success('下载成功')
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '下载失败，文件可能不存在')
  }
}

onBeforeUnmount(() => {
  document.removeEventListener('mousemove', onDrag)
  document.removeEventListener('mouseup', stopDrag)
})
</script>

<style scoped>
.draggable-resume-popup {
  position: fixed;
  width: 400px;
  max-height: 80vh;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0,0,0,0.15);
  z-index: 9999;
  overflow: hidden;
  user-select: none;
  transition: width 0.2s ease;
}
.draggable-resume-popup.popup-wide {
  width: 800px;
}
.popup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--el-color-primary-light-9);
  cursor: move;
  font-weight: 600;
  font-size: 14px;
  border-bottom: 1px solid var(--el-border-color-light);
  flex-wrap: wrap;
  gap: 6px;
}
.popup-actions { display: flex; align-items: center; gap: 4px; cursor: default; }
.view-toggle { margin-right: 8px; }
.view-toggle .el-button { font-size: 12px; }
.popup-body { padding: 0; max-height: calc(80vh - 45px); overflow-y: auto; }
.popup-content pre {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 13px;
  line-height: 1.7;
  margin: 0;
  padding: 14px;
  color: var(--el-text-color-primary);
}
.popup-empty { padding: 20px 0; }

/* 文件预览 */
.file-preview-container {
  height: calc(80vh - 45px);
  display: flex;
  flex-direction: column;
}
.pdf-iframe {
  width: 100%;
  height: 100%;
  border: none;
}
.word-preview-notice {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  gap: 12px;
  color: var(--el-text-color-secondary);
}
.word-preview-notice p {
  margin: 0;
  font-size: 14px;
}
</style>
