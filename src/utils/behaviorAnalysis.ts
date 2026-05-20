/**
 * 行为分析服务 — MediaPipe 纯浏览器端
 * 功能：姿态检测、注意力检测（视线）、手势识别
 * 表情分析走后端腾讯云 DetectFace 代理（见 api/face.ts）
 *
 * 优化：模型文件通过 IndexedDB 缓存，首次下载后后续零等待
 */
import {
  PoseLandmarker,
  FaceLandmarker,
  GestureRecognizer,
  FilesetResolver,
  type PoseLandmarkerResult,
  type FaceLandmarkerResult,
  type GestureRecognizerResult
} from '@mediapipe/tasks-vision'

// ── IndexedDB 模型缓存 ──────────────────────────────────
const DB_NAME = 'mediapipe-models'
const DB_VERSION = 1
const STORE_NAME = 'models'

/** 模型远程 URL — 主用 jsDelivr CDN（国内速度快） */
const MODEL_URLS = {
  pose: 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm/pose_landmarker_lite.task',
  face: 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm/face_landmarker.task',
  gesture: 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm/gesture_recognizer.task'
}
// 备用 CDN（Google 官方 + unpkg）
const MODEL_URLS_FALLBACK = {
  pose: 'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task',
  face: 'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task',
  gesture: 'https://storage.googleapis.com/mediapipe-models/gesture_recognizer/gesture_recognizer/float16/1/gesture_recognizer.task'
}

/** 打开 IndexedDB */
function openDB(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION)
    req.onupgradeneeded = () => {
      const db = req.result
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME)
      }
    }
    req.onsuccess = () => resolve(req.result)
    req.onerror = () => reject(req.error)
  })
}

/** 从 IndexedDB 读取缓存的模型 ArrayBuffer */
async function getCachedModel(name: string): Promise<ArrayBuffer | null> {
  try {
    const db = await openDB()
    return new Promise((resolve) => {
      const tx = db.transaction(STORE_NAME, 'readonly')
      const store = tx.objectStore(STORE_NAME)
      const req = store.get(name)
      req.onsuccess = () => resolve(req.result || null)
      req.onerror = () => resolve(null)
    })
  } catch {
    return null
  }
}

/** 将模型 ArrayBuffer 存入 IndexedDB */
async function setCachedModel(name: string, buffer: ArrayBuffer): Promise<void> {
  try {
    const db = await openDB()
    return new Promise((resolve) => {
      const tx = db.transaction(STORE_NAME, 'readwrite')
      const store = tx.objectStore(STORE_NAME)
      const req = store.put(buffer, name)
      req.onsuccess = () => resolve()
      req.onerror = () => resolve() // 静默失败，不影响主流程
    })
  } catch { /* 忽略 */ }
}

/** 带超时的 fetch（超时自动切换备用 CDN，超时时间 ms） */
async function fetchModelWithFallback(
  primaryUrl: string,
  fallbackUrl: string,
  timeoutMs = 5000   // 原 8000，缩短到 5s 更快切换备用
): Promise<ArrayBuffer> {
  // 先试主 CDN
  const primary = fetch(primaryUrl, { mode: 'cors' })
    .then(r => {
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      return r.arrayBuffer()
    })

  let timeoutId: ReturnType<typeof setTimeout> | null = null
  const timeout = new Promise<ArrayBuffer>((_, reject) => {
    timeoutId = setTimeout(() => reject(new Error('timeout')), timeoutMs)
  })

  try {
    const result = await Promise.race([primary, timeout])
    if (timeoutId) clearTimeout(timeoutId)
    return result as ArrayBuffer
  } catch {
    // 主 CDN 失败，切换备用
    if (timeoutId) clearTimeout(timeoutId)
    console.log('[行为分析] 主 CDN 超时，切换备用 CDN')
    const res = await fetch(fallbackUrl, { mode: 'cors' })
    if (!res.ok) throw new Error(`备用 CDN 也失败: HTTP ${res.status}`)
    return res.arrayBuffer()
  }
}

/** 获取模型 ArrayBuffer（优先读缓存，未命中则下载并缓存） */
async function getModelBuffer(
  name: keyof typeof MODEL_URLS
): Promise<ArrayBuffer> {
  // 1. 先查 IndexedDB 缓存
  const cached = await getCachedModel(name)
  if (cached) {
    console.log(`[行为分析] 使用缓存模型: ${name}`)
    return cached
  }

  // 2. 未命中，从网络下载（带 fallback）
  console.log(`[行为分析] 下载模型: ${name}`)
  let buffer: ArrayBuffer
  try {
    buffer = await fetchModelWithFallback(MODEL_URLS[name], MODEL_URLS_FALLBACK[name])
  } catch (e) {
    throw new Error(`模型 ${name} 下载失败: ${e}`)
  }

  // 3. 存入 IndexedDB 供下次使用
  setCachedModel(name, buffer)
  return buffer
}

// ── 类型 ──────────────────────────────────────────────
export interface BehaviorState {
  /** 坐姿状态 */
  posture: 'good' | 'warning' | 'bad'
  postureLabel: string
  /** 注意力状态 */
  attention: 'focused' | 'distracted' | 'unknown'
  attentionLabel: string
  /** 手势名称（null 表示未检测到手） */
  gesture: string | null
  /** 置信度 0-100 */
  confidence: number
}

export type BehaviorCallback = (state: BehaviorState) => void

// ── 单例 ──────────────────────────────────────────────
let poseLandmarker: PoseLandmarker | null = null
let faceLandmarker: FaceLandmarker | null = null
let gestureRecognizer: GestureRecognizer | null = null
let animationId: number | null = null
let videoEl: HTMLVideoElement | null = null
let callback: BehaviorCallback | null = null
let lastGesture = ''

// 初始化完成标记
let modelsReady = false
let initPromise: Promise<boolean> | null = null

// ── 模型初始化 ─────────────────────────────────────────
/** 注册加载进度回调（可选，供 UI 显示进度条） */
export let onModelLoadProgress:
  | ((stage: string, current: number, total: number) => void)
  | null = null

/** 设置模型加载进度回调（推荐用这个，避免打包工具冻结 export let） */
export function setModelLoadCallback(
  cb: ((stage: string, current: number, total: number) => void) | null
): void {
  onModelLoadProgress = cb
}

export async function initBehaviorAnalysis(): Promise<boolean> {
  if (modelsReady) return true
  if (initPromise) return initPromise

  initPromise = (async () => {
    try {
      const report = (stage: string, current: number, total: number) => {
        console.log(`[行为分析] ${stage} (${current}/${total})`)
        onModelLoadProgress?.(stage, current, total)
      }

      report('下载 WASM 运行时', 0, 4)

      // WASM 运行时也换用国内镜像加速
      const vision = await FilesetResolver.forVisionTasks(
        'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm'
      )

      report('获取缓存/下载模型', 1, 4)

      // 并行获取三个模型的 ArrayBuffer（优先从 IndexedDB 读）
      const [poseBuffer, faceBuffer, gestureBuffer] = await Promise.all([
        getModelBuffer('pose'),
        getModelBuffer('face'),
        getModelBuffer('gesture')
      ])

      report('初始化姿态模型', 2, 4)

      const [pose, face, gesture] = await Promise.all([
        PoseLandmarker.createFromOptions(vision, {
          baseOptions: {
            modelAssetBuffer: new Uint8Array(poseBuffer),
            delegate: 'GPU'
          },
          runningMode: 'VIDEO',
          numPoses: 1
        }),
        FaceLandmarker.createFromOptions(vision, {
          baseOptions: {
            modelAssetBuffer: new Uint8Array(faceBuffer),
            delegate: 'GPU'
          },
          runningMode: 'VIDEO',
          numFaces: 1,
          outputFaceBlendshapes: false,
          outputFacialTransformationMatrixes: false
        }),
        GestureRecognizer.createFromOptions(vision, {
          baseOptions: {
            modelAssetBuffer: new Uint8Array(gestureBuffer),
            delegate: 'GPU'
          },
          runningMode: 'VIDEO',
          numHands: 1
        })
      ])

      poseLandmarker = pose
      faceLandmarker = face
      gestureRecognizer = gesture
      modelsReady = true
      report('模型加载完成', 4, 4)
      console.log('[行为分析] MediaPipe 模型加载完成（含缓存加速）')
      return true
    } catch (e) {
      console.error('[行为分析] 模型初始化失败:', e)
      modelsReady = false
      initPromise = null  // 允许重试
      return false
    }
  })()

  return initPromise
}

// ── 启动检测循环 ───────────────────────────────────────
export function startDetection(video: HTMLVideoElement, cb: BehaviorCallback): void {
  videoEl = video
  callback = cb
  lastGesture = ''

  const detect = () => {
    if (!videoEl || !modelsReady) return
    const nowMs = performance.now()

    let poseResult: PoseLandmarkerResult | null = null
    let faceResult: FaceLandmarkerResult | null = null
    let gestureResult: GestureRecognizerResult | null = null

    try {
      if (poseLandmarker) {
        poseResult = poseLandmarker.detectForVideo(videoEl, nowMs)
      }
    } catch { /* 忽略偶发帧错误 */ }

    try {
      if (faceLandmarker) {
        faceResult = faceLandmarker.detectForVideo(videoEl, nowMs)
      }
    } catch { /* 忽略偶发帧错误 */ }

    try {
      if (gestureRecognizer) {
        gestureResult = gestureRecognizer.recognizeForVideo(videoEl, nowMs)
      }
    } catch { /* 忽略偶发帧错误 */ }

    const state = analyzeFrame(poseResult, faceResult, gestureResult)
    if (callback) callback(state)

    animationId = requestAnimationFrame(detect)
  }

  detect()
}

// ── 停止检测 ───────────────────────────────────────────
export function stopDetection(): void {
  if (animationId) {
    cancelAnimationFrame(animationId)
    animationId = null
  }
  videoEl = null
  callback = null
}

// ── 帧分析逻辑 ─────────────────────────────────────────
function analyzeFrame(
  pose: PoseLandmarkerResult | null,
  face: FaceLandmarkerResult | null,
  gesture: GestureRecognizerResult | null
): BehaviorState {
  const posture = analyzePosture(pose)
  const attention = analyzeAttention(face)
  const gestureInfo = analyzeGesture(gesture)

  const confidence = Math.max(posture.confidence, attention.confidence, gestureInfo.confidence)

  return {
    posture: posture.status,
    postureLabel: posture.label,
    attention: attention.status,
    attentionLabel: attention.label,
    gesture: gestureInfo.name,
    confidence
  }
}

// ── 姿态分析 ───────────────────────────────────────────
function analyzePosture(pose: PoseLandmarkerResult | null): {
  status: 'good' | 'warning' | 'bad'
  label: string
  confidence: number
} {
  if (!pose || !pose.landmarks || pose.landmarks.length === 0) {
    return { status: 'good', label: '未检测', confidence: 0 }
  }

  const lm = pose.landmarks[0]
  // 关键索引：11=左肩 12=右肩 23=左髋 24=右髋 0=鼻子

  const leftShoulder = lm[11]
  const rightShoulder = lm[12]
  const leftHip = lm[23]
  const rightHip = lm[24]
  const nose = lm[0]

  // 1. 肩膀倾斜角度（判断身体是否歪斜）
  const shoulderDx = rightShoulder.x - leftShoulder.x
  const shoulderDy = rightShoulder.y - leftShoulder.y
  const shoulderAngle = Math.abs(Math.atan2(shoulderDy, shoulderDx) * 180 / Math.PI)
  const isLeaning = shoulderAngle > 15 // 超过15度认为歪斜

  // 2. 脊柱弯曲（鼻子相对于肩膀中心的偏移）
  const shoulderCenterX = (leftShoulder.x + rightShoulder.x) / 2
  const headOffset = Math.abs(nose.x - shoulderCenterX)
  const isSlouching = headOffset > 0.08 // 严重偏移

  // 3. 肩膀到髋部的垂直距离（判断是否驼背/前倾）
  const shoulderMidY = (leftShoulder.y + rightShoulder.y) / 2
  const hipMidY = (leftHip.y + rightHip.y) / 2
  const torsoLength = hipMidY - shoulderMidY
  const isHunched = torsoLength < 0.15 // 躯干太短说明含胸

  if (isLeaning && isSlouching) {
    return { status: 'bad', label: '坐姿歪斜，请坐正', confidence: 85 }
  }
  if (isLeaning) {
    return { status: 'warning', label: '身体倾斜', confidence: 70 }
  }
  if (isSlouching) {
    return { status: 'warning', label: '头部偏移', confidence: 65 }
  }
  if (isHunched) {
    return { status: 'warning', label: '可能含胸', confidence: 60 }
  }

  return { status: 'good', label: '坐姿端正', confidence: 90 }
}

// ── 注意力分析（基于面部关键点） ───────────────────────
function analyzeAttention(face: FaceLandmarkerResult | null): {
  status: 'focused' | 'distracted' | 'unknown'
  label: string
  confidence: number
} {
  if (!face || !face.faceLandmarks || face.faceLandmarks.length === 0) {
    return { status: 'unknown', label: '未检测到面部', confidence: 0 }
  }

  const lm = face.faceLandmarks[0]
  // 虹膜关键点（MediaPipe 478点模型）：
  // 左虹膜中心: 468-472  右虹膜中心: 473-477
  // 左眼角: 33  右眼角: 263
  // 鼻尖: 1

  const leftIris = lm.length > 468 ? lm[468] : null
  const rightIris = lm.length > 473 ? lm[473] : null
  const leftEyeCorner = lm[33]
  const rightEyeCorner = lm[263]
  const noseTip = lm[1]

  // 鼻子中心点用于判断头部朝向
  const faceCenterX = (leftEyeCorner.x + rightEyeCorner.x) / 2
  const headYaw = noseTip.x - faceCenterX

  if (leftIris && rightIris) {
    // 左眼内偏：虹膜相对眼角的位置
    const leftEyeWidth = Math.abs(rightEyeCorner.x - leftEyeCorner.x)
    const irisOffset = Math.abs(
      (leftIris.x + rightIris.x) / 2 - faceCenterX
    ) / leftEyeWidth

    // 综合判断：头部偏转 + 瞳孔偏移
    const isLookingAway = headYaw > 0.06 || irisOffset > 0.35

    if (isLookingAway) {
      return { status: 'distracted', label: '视线偏移', confidence: 75 }
    }
    return { status: 'focused', label: '注意力集中', confidence: 85 }
  }

  // 没有虹膜数据时，仅用头部朝向判断
  if (Math.abs(headYaw) > 0.08) {
    return { status: 'distracted', label: '头部偏转', confidence: 60 }
  }

  return { status: 'focused', label: '注意力集中', confidence: 60 }
}

// ── 手势分析 ───────────────────────────────────────────
function analyzeGesture(gesture: GestureRecognizerResult | null): {
  name: string | null
  confidence: number
} {
  if (!gesture || !gesture.gestures || gesture.gestures.length === 0) {
    lastGesture = ''
    return { name: null, confidence: 0 }
  }

  const topCategories = gesture.gestures[0]
  if (!topCategories || topCategories.length === 0) {
    lastGesture = ''
    return { name: null, confidence: 0 }
  }
  const top = topCategories[0]
  const gestureName = top.categoryName

  // 过滤 "None" 手势（MediaPipe 默认返回无手势时的占位）
  if (gestureName === 'None' || gestureName === 'Unknown') {
    // 保持上一次有效手势一段时间（防抖）
    const result = lastGesture ? { name: lastGesture, confidence: top.score * 100 } : { name: null, confidence: 0 }
    if (gestureName === 'None') lastGesture = ''
    return result
  }

  lastGesture = gestureName
  // 中文映射
  const nameMap: Record<string, string> = {
    'Open_Palm': '举手 ✋',
    'Closed_Fist': '握拳 ✊',
    'Pointing_Up': '指向上 👆',
    'Thumb_Up': '点赞 👍',
    'Thumb_Down': '点赞 👎',
    'Victory': '胜利 ✌️',
    'ILoveYou': '我爱你 🤟',
    'Calling': '打电话 🤙',
    'OK': 'OK 👌',
    'Raised_fist': '举起拳头 ✊'
  }

  return {
    name: nameMap[gestureName] || gestureName,
    confidence: top.score * 100
  }
}

// ── 资源释放 ───────────────────────────────────────────
export function dispose(): void {
  stopDetection()
  poseLandmarker?.close()
  faceLandmarker?.close()
  gestureRecognizer?.close()
  poseLandmarker = null
  faceLandmarker = null
  gestureRecognizer = null
  modelsReady = false
  initPromise = null
}
