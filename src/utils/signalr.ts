import { ref, readonly } from 'vue'
import { HubConnectionBuilder, LogLevel, HubConnection } from '@microsoft/signalr'
import { useNotificationStore } from '@/stores/notification'
import { useUserStore } from '@/stores/user'

// SignalR 连接状态
export const connectionState = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')

let connection: HubConnection | null = null

// 创建 SignalR 连接
export function initSignalR() {
  const userStore = useUserStore()
  if (!userStore.token) return

  const notificationStore = useNotificationStore()
  
  connectionState.value = 'connecting'
  notificationStore.setConnectionStatus('connecting')

  try {
    // 创建连接（假设后端 SignalR Hub 地址为 /hubs/notification）
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/notification', {
        accessTokenFactory: () => userStore.token,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build()

    // 注册事件处理程序
    registerHandlers(connection, notificationStore)

    // 启动连接
    connection.start()
      .then(() => {
        connectionState.value = 'connected'
        notificationStore.setConnectionStatus('connected')
        console.log('SignalR 连接已建立')
      })
      .catch((err) => {
        console.error('SignalR 连接失败:', err)
        connectionState.value = 'disconnected'
        notificationStore.setConnectionStatus('disconnected')
      })

    // 处理连接关闭
    connection.onclose(() => {
      connectionState.value = 'disconnected'
      notificationStore.setConnectionStatus('disconnected')
    })

    // 处理重新连接
    connection.onreconnecting(() => {
      connectionState.value = 'connecting'
      notificationStore.setConnectionStatus('connecting')
    })

    connection.onreconnected(() => {
      connectionState.value = 'connected'
      notificationStore.setConnectionStatus('connected')
    })

  } catch (error) {
    console.error('初始化 SignalR 失败:', error)
    connectionState.value = 'disconnected'
    notificationStore.setConnectionStatus('disconnected')
  }
}

// 注册事件处理程序
function registerHandlers(hubConnection: HubConnection, notificationStore: any) {
  // 新简历投递通知（HR工作台角标）
  hubConnection.on('NewDelivery', (data: any) => {
    notificationStore.addNotification({
      id: `delivery_${Date.now()}`,
      type: 'delivery',
      title: '新简历投递',
      content: `收到来自 ${data.candidateName} 的简历投递`,
      read: false,
      createdAt: new Date().toISOString(),
      data,
    })
  })

  // 简历状态变更通知（求职者端）
  hubConnection.on('DeliveryStatusChanged', (data: any) => {
    notificationStore.addNotification({
      id: `status_${Date.now()}`,
      type: 'status',
      title: '投递状态更新',
      content: `您的简历投递状态已更新为：${getStatusText(data.status)}`,
      read: false,
      createdAt: new Date().toISOString(),
      data,
    })
  })

  // AI处理进度通知
  hubConnection.on('AIProcessingProgress', (data: any) => {
    notificationStore.addNotification({
      id: `ai_${Date.now()}`,
      type: 'ai',
      title: 'AI处理中',
      content: data.message || 'AI正在处理您的简历...',
      read: false,
      createdAt: new Date().toISOString(),
      data,
    })
  })

  // AI处理完成通知
  hubConnection.on('AIProcessingComplete', (data: any) => {
    notificationStore.addNotification({
      id: `ai_complete_${Date.now()}`,
      type: 'ai',
      title: 'AI处理完成',
      content: data.message || 'AI分析已完成',
      read: false,
      createdAt: new Date().toISOString(),
      data,
    })
  })

  // 面试安排通知
  hubConnection.on('InterviewScheduled', (data: any) => {
    notificationStore.addNotification({
      id: `interview_${Date.now()}`,
      type: 'interview',
      title: '面试安排',
      content: `您有新的面试安排：${data.time}`,
      read: false,
      createdAt: new Date().toISOString(),
      data,
    })
  })
}

// 获取状态文本
function getStatusText(status: number): string {
  const statusMap: Record<number, string> = {
    0: '待查看',
    1: '已查看',
    2: '面试中',
    3: '实习中',
    4: '正式入职',
    5: '已淘汰',
  }
  return statusMap[status] || '未知'
}

// 断开 SignalR 连接
export function disconnectSignalR() {
  if (connection) {
    connection.stop()
    connection = null
    connectionState.value = 'disconnected'
  }
}

// 发送消息到服务器（如果有需要）
export function sendMessage(method: string, ...args: any[]) {
  if (connection && connectionState.value === 'connected') {
    return connection.invoke(method, ...args)
  }
  return Promise.reject(new Error('SignalR 未连接'))
}

export default {
  connectionState: readonly(connectionState),
  initSignalR,
  disconnectSignalR,
  sendMessage,
}