<template>
  <div class="public-layout">
    <!-- ═══ 顶栏 (玻璃拟态) ═══ -->
    <header class="public-header glass-panel">
      <div class="header-container">
        <div class="logo" @click="$router.push('/')">
          <div class="logo-mark">
            <el-icon :size="20"><Briefcase /></el-icon>
          </div>
          <span class="logo-text gradient-text">AI智能招聘</span>
        </div>
        <nav class="header-menu">
          <router-link to="/jobs" class="menu-link" :class="{ active: $route.path === '/jobs' }">岗位列表</router-link>
          <router-link v-if="isLoggedIn && isCandidate" to="/my/deliveries" class="menu-link" :class="{ active: $route.path === '/my/deliveries' }">我的投递</router-link>
        </nav>
        <div class="header-actions">
          <!-- 主题切换 -->
          <button class="theme-toggle-btn" @click="toggleTheme()" :title="isDark() ? '切换亮色' : '切换暗色'">
            <el-icon :size="16"><Sunny v-if="isDark()" /><Moon v-else /></el-icon>
          </button>
          <!-- 移动端菜单按钮 -->
          <el-button class="mobile-menu-btn" :icon="mobileMenuOpen ? Close : Menu" text @click="mobileMenuOpen = !mobileMenuOpen" />
          <template v-if="isLoggedIn">
            <el-popover placement="bottom" :width="380" trigger="click">
              <template #reference>
                <el-badge :value="unreadCount" :hidden="unreadCount === 0" class="notification-badge">
                  <el-button :icon="Bell" circle text class="header-icon-btn" />
                </el-badge>
              </template>
              <div class="notify-pop">
                <div class="notify-pop-header">
                  <span class="notify-pop-title">站内消息</span>
                  <span class="unread-info" v-if="unreadCount > 0">{{ unreadCount }} 条未读</span>
                  <el-button type="primary" link size="small" @click="notificationStore.markAllAsRead()" :disabled="unreadCount === 0">全部已读</el-button>
                </div>
                <div v-loading="notificationStore.loading" class="notify-pop-list">
                  <div v-for="item in notificationStore.notifications" :key="item.notificationId"
                    class="notify-item" :class="{ unread: !item.isRead }"
                    @click="handleNotificationClick(item)">
                    <div class="notify-icon" :style="{ background: getTypeBg(item.type) }">
                      <el-icon :size="16" :color="getTypeColor(item.type)">
                        <Calendar v-if="item.type === 'interview'" />
                        <Document v-else-if="item.type === 'delivery'" />
                        <MagicStick v-else-if="item.type === 'ai'" />
                        <Bell v-else />
                      </el-icon>
                    </div>
                    <div class="notify-body">
                      <div class="notify-title">{{ item.title }}</div>
                      <div class="notify-content">{{ item.content }}</div>
                      <div class="notify-time">{{ formatTime(item.createdAt) }}</div>
                    </div>
                    <div class="notify-actions">
                      <el-button v-if="!item.isRead" type="primary" link size="small"
                        @click.stop="notificationStore.markAsRead(item.notificationId)">已读</el-button>
                      <el-button type="danger" link size="small"
                        @click.stop="notificationStore.removeNotification(item.notificationId)">删除</el-button>
                    </div>
                  </div>
                  <el-empty v-if="!notificationStore.loading && notificationStore.notifications.length === 0" description="暂无消息" :image-size="64" />
                </div>
              </div>
            </el-popover>

            <el-dropdown @command="handleCommand" trigger="click">
              <div class="user-info">
                <el-avatar :size="28" class="user-avatar-sm">
                  {{ userInfo?.realName?.charAt(0) || 'U' }}
                </el-avatar>
                <span class="user-name-text">{{ userInfo?.realName || userInfo?.username }}</span>
                <el-icon class="user-arrow"><ArrowDown /></el-icon>
              </div>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="profile"><el-icon><User /></el-icon>个人中心</el-dropdown-item>
                  <el-dropdown-item command="logout" divided><el-icon><SwitchButton /></el-icon>退出登录</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </template>
          <template v-else>
            <el-button type="primary" size="small" class="btn-gradient" @click="$router.push('/login')">登录</el-button>
            <el-button size="small" class="btn-outline" @click="$router.push('/register')">注册</el-button>
          </template>
        </div>
      </div>
    </header>

    <!-- ═══ 移动端抽屉 (玻璃面板) ═══ -->
    <Transition name="drawer-slide">
      <div v-if="mobileMenuOpen" class="mobile-drawer-overlay" @click.self="mobileMenuOpen = false">
        <div class="mobile-drawer glass-panel">
          <div class="mobile-drawer-header">
            <span class="mobile-drawer-title">导航</span>
            <el-button :icon="Close" text @click="mobileMenuOpen = false" />
          </div>
          <div class="mobile-drawer-menu">
            <div class="mobile-menu-item" :class="{ active: $route.path === '/jobs' }" @click="$router.push('/jobs'); mobileMenuOpen = false">
              <el-icon><Briefcase /></el-icon>岗位列表
            </div>
            <div v-if="isLoggedIn && isCandidate" class="mobile-menu-item" :class="{ active: $route.path === '/my/deliveries' }" @click="$router.push('/my/deliveries'); mobileMenuOpen = false">
              <el-icon><Document /></el-icon>我的投递
            </div>
            <div v-if="isLoggedIn" class="mobile-menu-item" @click="handleCommand('profile'); mobileMenuOpen = false">
              <el-icon><User /></el-icon>个人中心
            </div>
            <div class="mobile-menu-divider" />
            <div v-if="isLoggedIn" class="mobile-menu-item danger" @click="handleCommand('logout'); mobileMenuOpen = false">
              <el-icon><SwitchButton /></el-icon>退出登录
            </div>
            <template v-else>
              <div class="mobile-menu-item" @click="$router.push('/login'); mobileMenuOpen = false"><el-icon><User /></el-icon>登录</div>
              <div class="mobile-menu-item" @click="$router.push('/register'); mobileMenuOpen = false"><el-icon><Plus /></el-icon>注册</div>
            </template>
          </div>
        </div>
      </div>
    </Transition>

    <main class="public-main">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useNotificationStore } from '@/stores/notification'
import { useTheme } from '@/utils/theme'
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'
import { Briefcase, User, SwitchButton, Bell, Calendar, Document, MagicStick, ArrowDown, Menu, Close, Plus, Sunny, Moon } from '@element-plus/icons-vue'

dayjs.extend(relativeTime)
dayjs.locale('zh-cn')

const router = useRouter()
const userStore = useUserStore()
const notificationStore = useNotificationStore()
const { toggleTheme, isDark } = useTheme()
const mobileMenuOpen = ref(false)

const isLoggedIn = computed(() => userStore.isLoggedIn)
const isCandidate = computed(() => userStore.isCandidate)
const userInfo = computed(() => userStore.userInfo)
const unreadCount = computed(() => notificationStore.unreadCount)

onMounted(() => {
  if (userStore.isLoggedIn) {
    notificationStore.fetchUnreadCount()
    setInterval(() => notificationStore.fetchUnreadCount(), 60000)
  }
})

const formatTime = (date: string) => dayjs(date).fromNow()

const getTypeColor = (type: string) => {
  const colors: Record<string, string> = {
    interview: 'var(--color-accent)',
    delivery: 'var(--color-primary)',
    ai: '#A855F7',
    system: 'var(--color-text-muted)'
  }
  return colors[type] || 'var(--color-text-muted)'
}

const getTypeBg = (type: string) => {
  const bgs: Record<string, string> = {
    interview: 'var(--color-accent-bg)',
    delivery: 'var(--color-primary-bg)',
    ai: 'rgba(168, 85, 247, 0.08)',
    system: 'var(--color-bg)',
  }
  return bgs[type] || 'var(--color-bg)'
}

const handleNotificationClick = (item: any) => {
  if (!item.isRead) notificationStore.markAsRead(item.notificationId)
  if (item.relatedType === 'interview' || item.relatedType === 'delivery') {
    router.push('/my/deliveries')
  }
}

const handleCommand = (command: string) => {
  if (command === 'logout') {
    userStore.logout()
  } else if (command === 'profile') {
    router.push(userStore.isHR ? '/admin/profile' : '/profile')
  }
}
</script>

<style scoped lang="scss">
.public-layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--color-bg);
}

// ====== 顶栏 (玻璃拟态) ======
.public-header {
  padding: 0;
  position: sticky;
  top: 0;
  z-index: 100;
  border-radius: 0;
  border-bottom: 1px solid var(--color-border);

  .header-container {
    max-width: var(--content-max-width);
    margin: 0 auto;
    height: 56px;
    display: flex;
    align-items: center;
    padding: 0 var(--space-5);
  }
}

// Logo
.logo {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
  margin-right: var(--space-8);

  .logo-mark {
    width: 32px; height: 32px;
    border-radius: var(--radius-md);
    background: var(--gradient-primary);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    box-shadow: 0 0 12px rgba(99, 102, 241, 0.3);
  }

  .logo-text {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
  }
}

// 导航
.header-menu {
  flex: 1;
  display: flex;
  gap: var(--space-1);
}

.menu-link {
  font-size: var(--text-base);
  font-weight: var(--weight-medium);
  color: var(--color-text-secondary);
  height: 56px;
  line-height: 56px;
  padding: 0 var(--space-4);
  text-decoration: none;
  border-bottom: 2px solid transparent;
  transition: all var(--duration-fast) var(--ease-out);
  position: relative;

  &:hover {
    color: var(--color-primary-hover);
    text-decoration: none;
  }

  &.active {
    color: var(--color-primary);
    font-weight: var(--weight-semibold);
    border-bottom-color: var(--color-primary);

    &::after {
      content: '';
      position: absolute;
      bottom: -2px;
      left: 50%;
      transform: translateX(-50%);
      width: 60%;
      height: 2px;
      background: var(--gradient-primary);
      box-shadow: 0 0 8px rgba(99, 102, 241, 0.4);
    }
  }
}

// 右侧操作
.header-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);

  .header-icon-btn {
    color: var(--color-text-secondary);
    transition: all var(--duration-fast) var(--ease-out);
    &:hover { color: var(--color-primary-hover); background: var(--color-primary-bg); }
  }

  .notification-badge :deep(.el-badge__content) {
    background: var(--color-danger);
    font-size: 11px;
  }

  .btn-gradient {
    background: var(--gradient-primary);
    border: none;
    color: #fff;
    font-weight: var(--weight-medium);
    box-shadow: 0 0 12px rgba(99, 102, 241, 0.2);
    transition: all var(--duration-fast) var(--ease-out);
    &:hover { box-shadow: var(--glow-primary-lg); transform: translateY(-1px); color: #fff; }
  }

  .btn-outline {
    border: 1px solid var(--color-border);
    color: var(--color-text-secondary);
    background: transparent;
    transition: all var(--duration-fast) var(--ease-out);
    &:hover { border-color: var(--color-border-glow); color: var(--color-primary-hover); }
  }
}

.user-info {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-md);
  transition: background var(--duration-fast) var(--ease-out);
  &:hover { background: var(--color-surface-hover); }

  .user-avatar-sm {
    background: var(--gradient-primary);
    color: #fff;
    font-weight: var(--weight-semibold);
    font-size: 13px;
    box-shadow: 0 0 8px rgba(99, 102, 241, 0.2);
  }

  .user-name-text {
    font-size: var(--text-sm);
    color: var(--color-text);
    font-weight: var(--weight-medium);
  }

  .user-arrow { font-size: 12px; color: var(--color-text-muted); }
}

// ====== 主内容 ======
.public-main {
  flex: 1;
  background: var(--color-bg);
}

// ====== 通知弹窗 ======
.notify-pop {
  .notify-pop-header {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding-bottom: var(--space-3);
    border-bottom: 1px solid var(--color-border);
    margin-bottom: var(--space-3);
    .notify-pop-title { font-size: var(--text-md); font-weight: var(--weight-semibold); color: var(--color-text); }
    .unread-info { font-size: var(--text-xs); color: var(--color-accent); font-weight: var(--weight-medium); flex: 1; }
  }
}

.notify-pop-list { max-height: 420px; overflow-y: auto; }

.notify-item {
  display: flex; align-items: flex-start; gap: var(--space-3);
  padding: var(--space-3); border-radius: var(--radius-md);
  cursor: pointer; transition: background var(--duration-fast) var(--ease-out);
  &:hover { background: var(--color-surface-hover); }

  &.unread {
    background: var(--color-accent-bg);
    .notify-title { font-weight: var(--weight-semibold); }
  }
  .notify-icon {
    width: 36px; height: 36px; border-radius: var(--radius-md);
    display: flex; align-items: center; justify-content: center; flex-shrink: 0;
  }
  .notify-body { flex: 1; min-width: 0; }
  .notify-title { font-size: var(--text-sm); color: var(--color-text); margin-bottom: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .notify-content { font-size: var(--text-xs); color: var(--color-text-secondary); line-height: 1.5; max-height: 36px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
  .notify-time { font-size: 11px; color: var(--color-text-muted); margin-top: var(--space-1); }
  .notify-actions { flex-shrink: 0; display: flex; flex-direction: column; gap: 2px; }
}

// ====== 移动端 ======
.mobile-menu-btn { display: none; @media (max-width: 768px) { display: flex; } }

.mobile-drawer-overlay {
  position: fixed; inset: 0; z-index: 200;
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(4px);
  display: flex; justify-content: flex-end;
}

.mobile-drawer {
  width: 280px; height: 100%;
  display: flex; flex-direction: column;
  border-radius: 0;
  border-left: 1px solid var(--color-border-glow);
}

.mobile-drawer-header {
  display: flex; justify-content: space-between; align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--color-border);
  .mobile-drawer-title { font-size: var(--text-lg); font-weight: var(--weight-semibold); color: var(--color-text); }
}

.mobile-drawer-menu { flex: 1; padding: var(--space-3); overflow-y: auto; }

.mobile-menu-item {
  display: flex; align-items: center; gap: var(--space-3);
  padding: var(--space-3) var(--space-4); border-radius: var(--radius-md);
  font-size: var(--text-base); color: var(--color-text); cursor: pointer;
  transition: all var(--duration-fast) var(--ease-out);
  &:hover { background: var(--color-surface-hover); }
  &.active { background: var(--color-primary-bg); color: var(--color-primary); font-weight: var(--weight-medium); }
  &.danger { color: var(--color-danger); }
}

.mobile-menu-divider { height: 1px; background: var(--color-border); margin: var(--space-2) 0; }

// 抽屉动画
.drawer-slide-enter-active, .drawer-slide-leave-active {
  transition: opacity var(--duration-slow) var(--ease-out);
  .mobile-drawer { transition: transform var(--duration-slow) var(--ease-out); }
}
.drawer-slide-enter-from, .drawer-slide-leave-to {
  opacity: 0;
  .mobile-drawer { transform: translateX(100%); }
}

@media (max-width: 768px) {
  .header-menu { display: none !important; }
  .header-actions { .user-name-text, .user-arrow { display: none; } }
}
</style>
