import { request } from '@/utils/request'

// ========== 候选人端 ==========

/** 开始AI面试 */
export const startAIInterview = (data: { deliveryId: number; candidateId: number; jobId: number }) => {
  return request.post('/ai-interview/start', data)
}

/** 提交回答 */
export const submitAIAnswer = (data: { sessionId: number; answer: string }) => {
  return request.post('/ai-interview/answer', data)
}

/** 结束面试 */
export const endAIInterview = (sessionId: number) => {
  return request.post('/ai-interview/end', { sessionId })
}

/** 获取面试结果 */
export const getAIInterviewResult = (sessionId: number) => {
  return request.get(`/ai-interview/result/${sessionId}`)
}

/** 获取会话状态 */
export const getAISessionStatus = (sessionId: number) => {
  return request.get(`/ai-interview/session/${sessionId}`)
}

/** 候选人：获取自己的AI面试记录 */
export const getMyAISessions = () => {
  return request.get('/ai-interview/my-sessions')
}

// ========== HR管理端 ==========

/** 获取AI面试记录列表（后端从JWT获取用户身份，Admin看全部，HR只看自己的） */
export const getAIInterviewList = (params: {
  page?: number
  pageSize?: number
  keyword?: string
}) => {
  return request.get('/ai-interview/admin/list', { params })
}

/** 获取AI面试对话详情 */
export const getAIInterviewMessages = (sessionId: number) => {
  return request.get(`/ai-interview/admin/messages/${sessionId}`)
}

// ========== 语音 ==========

/** 云端语音转文字（MiniMax ASR） */
export const speechToText = (audioBase64: string, format: string = 'webm') => {
  return request.post('/ai-interview/speech-to-text', { audioBase64, format })
}

/** 文字转语音（MiniMax TTS） */
export const textToSpeech = (text: string, voiceId?: string) => {
  return request.post('/ai-interview/text-to-speech', { text, voiceId })
}

/** 纯语音模式：开始面试，直接返回AI第一问的语音 */
export const voiceStartInterview = (data: { deliveryId: number; candidateId: number; jobId: number }) => {
  return request.post('/ai-interview/voice-start', data)
}

/** 纯语音模式：提交语音回答，直接返回AI语音回复 */
export const voiceSubmitAnswer = (data: { sessionId: number; audioBase64: string; format?: string }) => {
  return request.post('/ai-interview/voice-answer', data)
}
