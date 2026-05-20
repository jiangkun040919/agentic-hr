import { request } from '@/utils/request'

export interface NotificationItem {
  notificationId: number
  userId: number
  type: string
  title: string
  content: string
  isRead: boolean
  relatedId?: number
  relatedType?: string
  createdAt: string
  readAt?: string
}

/** 获取通知列表 */
export const getNotificationList = (params?: { page?: number; pageSize?: number }) => {
  return request.get<NotificationItem[]>('/notification/list', { params })
}

/** 获取未读消息数 */
export const getUnreadCount = () => {
  return request.get<number>('/notification/unread-count')
}

/** 标记单条已读 */
export const markNotificationRead = (id: number) => {
  return request.put(`/notification/${id}/read`)
}

/** 全部标记已读 */
export const markAllNotificationRead = () => {
  return request.put('/notification/read-all')
}

/** 删除通知 */
export const deleteNotification = (id: number) => {
  return request.delete(`/notification/${id}`)
}
