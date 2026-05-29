<template>
  <el-container class="admin-layout">
    <!-- ═══ 移动端遮罩 ═══ -->
    <div v-if="mobileSidebarOpen" class="mobile-overlay" @click="closeMobileSidebar" />

    <!-- ═══ 侧边栏 (玻璃拟态) ═══ -->
    <el-aside :width="isCollapsed ? 'var(--sidebar-collapsed)' : 'var(--sidebar-width)'" class="admin-aside" :class="{ collapsed: isCollapsed, 'mobile-open': mobileSidebarOpen }">
      <!-- 侧边栏发光背景 -->
      <div class="sidebar-glow-orb" />

      <!-- Logo -->
      <div class="sidebar-logo" @click="$router.push('/admin/dashboard')">
        <div class="logo-icon">
          <el-icon :size="22"><Briefcase /></el-icon>
        </div>
        <span v-show="!isCollapsed" class="logo-text gradient-text">AI智能招聘</span>
      </div>

      <!-- 导航菜单 -->
      <el-menu :default-active="activeMenu" class="sidebar-menu" :collapse="isCollapsed" router>
        <div class="menu-group-title" v-show="!isCollapsed">招聘执行</div>
        <el-menu-item index="/admin/dashboard">
          <el-icon><DataAnalysis /></el-icon><span>工作台</span>
        </el-menu-item>
        <el-menu-item index="/admin/jobs">
          <el-icon><Briefcase /></el-icon><span>岗位管理</span>
        </el-menu-item>
        <el-menu-item index="/admin/smart-screening">
          <el-icon><MagicStick /></el-icon><span>智能筛选</span>
        </el-menu-item>
        <el-menu-item index="/admin/interviews">
          <el-icon><Calendar /></el-icon><span>面试管理</span>
        </el-menu-item>
        <el-menu-item index="/admin/ai-interviews">
          <el-icon><VideoCamera /></el-icon><span>AI面试</span>
        </el-menu-item>

        <div class="menu-group-title" v-show="!isCollapsed">决策支持</div>
        <el-menu-item index="/admin/candidate-comparison">
          <el-icon><TrendCharts /></el-icon><span>人才对比</span>
        </el-menu-item>
        <el-menu-item index="/admin/recruitment-strategy">
          <el-icon><PieChart /></el-icon><span>招聘策略</span>
        </el-menu-item>
        <el-menu-item index="/admin/benchmark">
          <el-icon><DataLine /></el-icon><span>准确率评测</span>
        </el-menu-item>
        <el-menu-item index="/admin/compliance">
          <el-icon><Checked /></el-icon><span>AI合规</span>
        </el-menu-item>

        <div class="menu-group-title" v-show="!isCollapsed">图谱智能</div>
        <el-menu-item index="/admin/knowledge-graph">
          <el-icon><Connection /></el-icon><span>知识图谱</span>
        </el-menu-item>
      </el-menu>

      <!-- 底部用户区 -->
      <div class="sidebar-footer">
        <div class="sidebar-user" @click="$router.push('/admin/profile')">
          <el-avatar :size="36" class="user-avatar">
            {{ userInfo?.realName?.charAt(0) || 'U' }}
          </el-avatar>
          <div v-show="!isCollapsed" class="user-meta">
            <div class="user-name">{{ userInfo?.realName || userInfo?.username }}</div>
            <div class="user-role">
              <span class="status-dot" /> HR管理员
            </div>
          </div>
        </div>
        <!-- 主题切换 -->
        <button v-show="!isCollapsed" class="theme-toggle-btn" @click="toggleTheme()" :title="isDark() ? '切换亮色模式' : '切换暗色模式'">
          <el-icon :size="16"><Sunny v-if="isDark()" /><Moon v-else /></el-icon>
        </button>
      </div>
    </el-aside>

    <!-- ═══ 右侧内容区 ═══ -->
    <el-container class="admin-right">
      <!-- 顶栏 (玻璃拟态) -->
      <el-header class="admin-header glass-panel">
        <div class="header-left">
          <el-button class="collapse-btn" :icon="isCollapsed ? Expand : Fold" text @click="toggleSidebar" />
          <!-- 移动端汉堡菜单 -->
          <el-button class="mobile-menu-btn" :icon="Operation" text @click="toggleMobileSidebar" />
          <el-breadcrumb separator="/">
            <el-breadcrumb-item :to="{ path: '/admin' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item v-if="currentRoute">{{ currentRoute }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>

        <div class="header-right">
          <!-- 通知 -->
          <el-popover placement="bottom" :width="360" trigger="click" @show="notifyStore.fetchNotifications()">
            <template #reference>
              <el-badge :value="notifyCount" :hidden="notifyCount === 0" class="header-badge">
                <el-button :icon="Bell" circle text class="header-icon-btn" />
              </el-badge>
            </template>
            <div class="notify-popover">
              <div class="notify-pop-header">
                <span class="notify-pop-title">消息通知</span>
                <el-button type="primary" link size="small" @click="notifyStore.markAllAsRead()">全部已读</el-button>
              </div>
              <el-empty v-if="notifications.length === 0" description="暂无新消息" :image-size="48" />
              <div v-else class="notify-pop-list">
                <div
                  v-for="n in notifications"
                  :key="n.notificationId"
                  class="notify-pop-item"
                  :class="{ unread: !n.isRead }"
                  @click="notifyStore.markAsRead(n.notificationId)"
                >
                  <div class="notify-pop-item-title">{{ n.title }}</div>
                  <div class="notify-pop-item-content">{{ n.content }}</div>
                  <div class="notify-pop-item-time">{{ dayjs(n.createdAt).format('MM-DD HH:mm') }}</div>
                </div>
              </div>
            </div>
          </el-popover>

          <!-- 主题切换 (折叠时显示) -->
          <button v-if="isCollapsed" class="theme-toggle-btn header-theme-btn" @click="toggleTheme()" :title="isDark() ? '切换亮色' : '切换暗色'">
            <el-icon :size="14"><Sunny v-if="isDark()" /><Moon v-else /></el-icon>
          </button>

          <!-- 用户下拉 -->
          <el-dropdown @command="handleCommand" trigger="click">
            <div class="header-user">
              <el-avatar :size="32" class="header-avatar-neon">
                {{ userInfo?.realName?.charAt(0) || 'U' }}
              </el-avatar>
              <span class="header-username">{{ userInfo?.realName || userInfo?.username }}</span>
              <el-icon class="header-arrow"><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="profile">
                  <el-icon><User /></el-icon>个人中心
                </el-dropdown-item>
                <el-dropdown-item command="logout" divided>
                  <el-icon><SwitchButton /></el-icon>退出登录
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <!-- 主内容 -->
      <el-main class="admin-main">
        <RouterView />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useNotificationStore } from '@/stores/notification'
import { useTheme } from '@/utils/theme'
import { Briefcase, DataAnalysis, MagicStick, Calendar, PieChart, ArrowDown, User, SwitchButton, VideoCamera, TrendCharts, Fold, Expand, Bell, Sunny, Moon, Operation, Share, Monitor, DataLine, Checked, Connection, Collection } from '@element-plus/icons-vue'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const notifyStore = useNotificationStore()
const { toggleTheme, isDark } = useTheme()

const isCollapsed = ref(false)
const mobileSidebarOpen = ref(false)
const isMobile = ref(window.innerWidth < 768)

const toggleSidebar = () => {
  if (isMobile.value) {
    toggleMobileSidebar()
  } else {
    isCollapsed.value = !isCollapsed.value
  }
}

const toggleMobileSidebar = () => {
  mobileSidebarOpen.value = !mobileSidebarOpen.value
}

const closeMobileSidebar = () => {
  mobileSidebarOpen.value = false
}

const handleResize = () => {
  const wasMobile = isMobile.value
  isMobile.value = window.innerWidth < 768
  if (!isMobile.value && mobileSidebarOpen.value) {
    mobileSidebarOpen.value = false
  }
  if (isMobile.value && !wasMobile) {
    isCollapsed.value = true
  }
}

const activeMenu = computed(() => route.path)
const currentRoute = computed(() => route.meta.title as string)
const userInfo = computed(() => userStore.userInfo)
const notifyCount = computed(() => notifyStore.unreadCount)
const notifications = computed(() => notifyStore.notifications.slice(0, 5))

const handleCommand = (command: string) => {
  if (command === 'logout') userStore.logout()
  else if (command === 'profile') router.push('/admin/profile')
}

let notifyTimer: any = null

onMounted(() => {
  // Auto-collapse on mobile
  if (window.innerWidth < 768) isCollapsed.value = true
  window.addEventListener('resize', handleResize)
  notifyStore.fetchUnreadCount()
  notifyTimer = setInterval(() => notifyStore.fetchUnreadCount(), 60000)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  if (notifyTimer) clearInterval(notifyTimer)
})
</script>

<style scoped lang="scss">
.admin-layout {
  height: 100vh;
  width: 100%;
}

// ====== 主题切换按钮 ======
.theme-toggle-btn {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all var(--duration-fast) var(--ease-out);
  flex-shrink: 0;

  &:hover {
    color: var(--color-primary);
    border-color: var(--color-primary);
    background: var(--color-primary-bg);
  }
}

// ====== 侧边栏 ======
.admin-aside {
  background: var(--color-sidebar-bg);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  transition: width var(--duration-slow) var(--ease-out);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  position: relative;
  border-right: 1px solid var(--color-sidebar-border);

  .sidebar-glow-orb {
    position: absolute;
    top: -120px;
    right: -80px;
    width: 240px;
    height: 240px;
    border-radius: 50%;
    background: radial-gradient(circle, rgba(196, 169, 106, 0.06) 0%, transparent 70%);
    pointer-events: none;
  }
}

// Logo
.sidebar-logo {
  height: 56px;
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 0 var(--space-5);
  cursor: pointer;
  border-bottom: 1px solid var(--color-sidebar-border);
  flex-shrink: 0;
  position: relative;
  z-index: 1;

  .logo-icon {
    width: 34px; height: 34px;
    border-radius: var(--radius-md);
    background: var(--gradient-primary);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    flex-shrink: 0;
    box-shadow: 0 0 12px rgba(196, 169, 106, 0.25);
  }

  .logo-text {
    font-size: var(--text-md);
    font-weight: var(--weight-semibold);
    white-space: nowrap;
  }
}

.menu-group-title {
  padding: var(--space-4) var(--space-5) var(--space-2);
  font-size: 11px;
  font-weight: var(--weight-semibold);
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.1em;
  white-space: nowrap;
}

.sidebar-menu {
  border-right: none;
  background: transparent;
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding: var(--space-2) 0;
  position: relative;
  z-index: 1;

  :deep(.el-menu-item) {
    color: var(--color-sidebar-text);
    height: 44px;
    line-height: 44px;
    margin: 2px var(--space-2);
    border-radius: var(--radius-md);
    font-size: var(--text-base);
    transition: all var(--duration-fast) var(--ease-out);
    position: relative;

    .el-icon { font-size: 18px; }

    &:hover {
      background: var(--color-sidebar-hover);
      color: var(--color-sidebar-text-hover);
      box-shadow: inset 0 0 0 1px rgba(196, 169, 106, 0.10);
    }

    &.is-active {
      background: var(--color-sidebar-active);
      color: var(--color-sidebar-text-hover);
      font-weight: var(--weight-medium);
      box-shadow: inset 2px 0 0 var(--color-primary);
    }
  }
}

// 底部用户区
.sidebar-footer {
  flex-shrink: 0;
  border-top: 1px solid var(--color-sidebar-border);
  padding: var(--space-3) var(--space-4);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  position: relative;
  z-index: 1;
}

.sidebar-user {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all var(--duration-fast) var(--ease-out);
  flex: 1;
  min-width: 0;

  &:hover {
    background: var(--color-sidebar-hover);
  }

  .user-avatar {
    background: var(--gradient-primary);
    color: #fff;
    font-weight: var(--weight-semibold);
    flex-shrink: 0;
    box-shadow: 0 0 10px rgba(196, 169, 106, 0.20);
  }

  .user-meta {
    overflow: hidden;
    white-space: nowrap;

    .user-name {
      font-size: var(--text-sm);
      font-weight: var(--weight-medium);
      color: var(--color-sidebar-text-hover);
      line-height: 1.3;
    }

    .user-role {
      font-size: 11px;
      color: var(--color-sidebar-text);
      line-height: 1.3;
      display: flex;
      align-items: center;
      gap: 4px;

      .status-dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: #7A8B5E;
        box-shadow: 0 0 6px rgba(122, 139, 94, 0.5);
      }
    }
  }
}

// 折叠状态
.admin-aside.collapsed {
  .sidebar-logo { justify-content: center; padding: 0; }
  .sidebar-footer { justify-content: center; padding: var(--space-2); }
  .sidebar-user { justify-content: center; }
}

// ====== 右侧容器 ======
.admin-right {
  flex: 1;
  flex-direction: column;
  background: var(--color-bg);
  min-width: 0;
}

// ====== 顶栏 (玻璃拟态) ======
.admin-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 var(--space-5);
  height: var(--header-height);
  z-index: 10;
  flex-shrink: 0;
  border-radius: 0;
  border-bottom: 1px solid var(--color-border);
}

.header-left {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.collapse-btn {
  color: var(--color-text-secondary);
  transition: all var(--duration-fast) var(--ease-out);

  &:hover {
    color: var(--color-primary-hover);
    background: var(--color-primary-bg);
  }
}

.header-right {
  display: flex;
  align-items: center;
  gap: var(--space-4);
}

.header-icon-btn {
  color: var(--color-text-secondary);
  transition: all var(--duration-fast) var(--ease-out);

  &:hover {
    color: var(--color-primary-hover);
    background: var(--color-primary-bg);
  }
}

.header-badge {
  :deep(.el-badge__content) {
    background: var(--color-danger);
    font-size: 11px;
  }
}

.header-theme-btn {
  width: 28px;
  height: 28px;
  font-size: 14px;
}

.header-user {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-md);
  transition: background var(--duration-fast) var(--ease-out);

  &:hover { background: var(--color-surface-hover); }

  .header-avatar-neon {
    background: var(--gradient-primary);
    color: #fff;
    font-weight: var(--weight-semibold);
    font-size: 14px;
    box-shadow: 0 0 10px rgba(99, 102, 241, 0.2);
  }

  .header-username {
    font-size: var(--text-sm);
    color: var(--color-text);
    font-weight: var(--weight-medium);
  }

  .header-arrow {
    font-size: 12px;
    color: var(--color-text-muted);
    transition: transform var(--duration-fast) var(--ease-out);
  }
}

// ====== 主内容区 ======
.admin-main {
  flex: 1;
  background: var(--color-bg);
  padding: var(--space-6);
  overflow-y: auto;
  background-image:
    radial-gradient(circle at 20% 0%, rgba(99, 102, 241, 0.03) 0%, transparent 50%),
    radial-gradient(circle at 80% 100%, rgba(168, 85, 247, 0.02) 0%, transparent 50%);
}

// ====== 通知弹出框 ======
.notify-popover {
  .notify-pop-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: var(--space-3);
    border-bottom: 1px solid var(--color-border);
    margin-bottom: var(--space-3);

    .notify-pop-title {
      font-size: var(--text-md);
      font-weight: var(--weight-semibold);
      color: var(--color-text);
    }
  }
}

// ═══ 移动端响应式 ═══
.mobile-menu-btn {
  display: none !important;
  color: var(--color-text-secondary);
}

.mobile-overlay {
  display: none;
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.5);
  z-index: 99;
}

@media (max-width: 768px) {
  .mobile-menu-btn {
    display: flex !important;
  }
  .collapse-btn {
    display: none !important;
  }
  .header-username {
    display: none;
  }
  .admin-aside {
    position: fixed;
    top: 0;
    left: 0;
    bottom: 0;
    z-index: 100;
    transform: translateX(-100%);
    transition: transform var(--duration-slow) var(--ease-out);

    &.collapsed {
      transform: translateX(-100%);
    }
    &.mobile-open {
      transform: translateX(0);
      width: var(--sidebar-width) !important;
    }
  }
  .mobile-overlay {
    display: block;
  }
  .admin-main {
    padding: var(--space-4);
  }
  .admin-header {
    padding: 0 var(--space-3);
  }
  .header-right {
    gap: var(--space-2);
  }
  .header-badge {
    display: none;
  }
}
</style>
