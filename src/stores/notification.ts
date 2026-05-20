import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getNotificationList, getUnreadCount, markNotificationRead, markAllNotificationRead, deleteNotification } from '@/api/notification'
import type { NotificationItem } from '@/api/notification'

export const useNotificationStore = defineStore('notification', () => {
  // 状态
  const notifications = ref<NotificationItem[]>([])
  const unreadCount = ref(0)
  const loading = ref(false)
  const dialogVisible = ref(false)

  // 获取通知列表
  async function fetchNotifications(page = 1, pageSize = 20) {
    loading.value = true
    try {
      const res = await getNotificationList({ page, pageSize })
      notifications.value = Array.isArray(res) ? res : []
    } catch (error) {
      console.error('获取通知失败', error)
    } finally {
      loading.value = false
    }
  }

  // 获取未读数
  async function fetchUnreadCount() {
    try {
      const res = await getUnreadCount()
      unreadCount.value = typeof res === 'number' ? res : 0
    } catch (error) {
      console.error('获取未读数失败', error)
    }
  }

  // 标记单条已读
  async function markAsRead(id: number) {
    try {
      await markNotificationRead(id)
      const item = notifications.value.find(n => n.notificationId === id)
      if (item && !item.isRead) {
        item.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
    } catch (error) {
      console.error('标记已读失败', error)
    }
  }

  // 全部标记已读
  async function markAllAsRead() {
    try {
      await markAllNotificationRead()
      notifications.value.forEach(n => n.isRead = true)
      unreadCount.value = 0
    } catch (error) {
      console.error('全部已读失败', error)
    }
  }

  // 删除通知
  async function removeNotification(id: number) {
    try {
      await deleteNotification(id)
      const item = notifications.value.find(n => n.notificationId === id)
      if (item && !item.isRead) {
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
      notifications.value = notifications.value.filter(n => n.notificationId !== id)
    } catch (error) {
      console.error('删除通知失败', error)
    }
  }

  // 打开通知弹窗
  function openDialog() {
    dialogVisible.value = true
    fetchNotifications()
  }

  // SignalR 连接状态
  const connectionStatus = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  function setConnectionStatus(status: 'disconnected' | 'connecting' | 'connected') {
    connectionStatus.value = status
  }

  // 添加通知（SignalR 实时推送）
  function addNotification(notification: {
    id: string
    type: string
    title: string
    content: string
    read: boolean
    createdAt: string
    data: any
  }) {
    notifications.value.unshift({
      notificationId: Date.now(),
      userId: 0,
      type: notification.type,
      title: notification.title,
      content: notification.content,
      isRead: notification.read,
      createdAt: notification.createdAt,
    })
    unreadCount.value++
  }

  return {
    notifications,
    unreadCount,
    loading,
    dialogVisible,
    connectionStatus,
    setConnectionStatus,
    addNotification,
    fetchNotifications,
    fetchUnreadCount,
    markAsRead,
    markAllAsRead,
    removeNotification,
    openDialog,
  }
})
