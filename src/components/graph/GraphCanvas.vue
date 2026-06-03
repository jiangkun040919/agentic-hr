<template>
  <div ref="containerRef" class="graph-canvas" :style="{ height: height + 'px' }">
    <div v-if="loading" class="graph-loading">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>加载图谱数据...</span>
    </div>
    <div v-if="error" class="graph-error">
      <el-icon><WarningFilled /></el-icon>
      <span>{{ error }}</span>
    </div>
    <div v-if="!loading && !error && nodes.length === 0" class="graph-empty">
      <el-empty description="暂无图谱数据" :image-size="60" />
    </div>
    <!-- 图例 -->
    <div v-if="!loading && nodes.length > 0 && showLegend" class="graph-legend">
      <div v-for="item in legendItems" :key="item.label" class="legend-item">
        <span class="legend-dot" :style="{ background: item.color, borderRadius: item.shape === 'circle' ? '50%' : item.shape === 'diamond' ? '2px' : '4px' }"></span>
        <span class="legend-text">{{ item.label }}</span>
      </div>
    </div>
    <!-- 节点详情弹窗 -->
    <div v-if="hoveredNode" class="graph-tooltip" :style="tooltipStyle">
      <div class="tooltip-title">{{ hoveredNode.label }}</div>
      <div class="tooltip-type">{{ hoveredNode.type }}</div>
      <div v-if="hoveredNode.source" class="tooltip-source">来源：{{ hoveredNode.source }}</div>
      <div v-if="hoveredNode.confidence != null" class="tooltip-confidence">
        置信度：
        <span :style="{ color: hoveredNode.confidence >= 80 ? '#67c23a' : hoveredNode.confidence >= 50 ? '#e6a23c' : '#f56c6c' }">
          {{ hoveredNode.confidence }}%
        </span>
      </div>
      <div v-if="hoveredNode.detail" class="tooltip-detail">{{ hoveredNode.detail }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { Graph, type GraphOptions } from '@antv/g6'
import { Loading, WarningFilled } from '@element-plus/icons-vue'

interface G6Node {
  id: string
  label: string
  type: string
  category: string
  size: number
  style?: Record<string, any>
  source?: string
  confidence?: number
  detail?: string
  shape?: string
}

interface G6Edge {
  id: string
  source: string
  target: string
  label: string
  relationship?: string
  confidence?: number
  dashed?: boolean
  edgeColor?: string
  lineWidth?: number
}

const props = withDefaults(defineProps<{
  nodes: G6Node[]
  edges: G6Edge[]
  height?: number
  loading?: boolean
  error?: string
  showLegend?: boolean
}>(), {
  height: 350,
  loading: false,
  error: '',
  showLegend: false
})

const emit = defineEmits<{
  (e: 'nodeClick', nodeId: string, type: string, data: any): void
}>()

const containerRef = ref<HTMLElement>()
let graph: Graph | null = null

// ═══ 节点颜色映射（category 优先，type 兜底） ═══
const categoryColors: Record<string, string> = {
  'candidate': '#409EFF',
  'job': '#8A9BA8',
  'claim-strong': '#67C23A',
  'claim-medium': '#E6A23C',
  'claim-weak': '#F56C6C',
  'evidence-graph': '#67C23A',
  'evidence-jd': '#409EFF',
  'evidence-interview': '#9B59B6',
  'evidence-consistency': '#E6A23C',
  'evidence-ml': '#00BCD4',
  'gap-critical': '#F56C6C',
  'gap-normal': '#E6A23C',
}

const typeColors: Record<string, string> = {
  Job: '#8A9BA8',
  Skill: '#7A8B5E',
  Industry: '#C4945A',
  Candidate: '#409EFF',
  Claim: '#E6A23C',
  Evidence: '#909399',
  Gap: '#F56C6C',
  default: '#8B9A6E'
}

const getNodeColor = (node: any) => {
  const data = node.data || node
  return categoryColors[data.category] || typeColors[data.type] || typeColors.default
}

// ═══ 边颜色映射 ═══
const getEdgeColor = (edge: any) => {
  const data = edge.data || edge
  if (data.edgeColor) return data.edgeColor
  const c = data.confidence
  if (c != null) {
    if (c >= 80) return '#67C23A'
    if (c >= 50) return '#E6A23C'
    return '#F56C6C'
  }
  return '#C0C0C0'
}

// ═══ 图例 ═══
const legendItems = computed(() => {
  const items = [
    { label: '候选人', color: categoryColors.candidate, shape: 'circle' },
    { label: '目标岗位', color: categoryColors['job'], shape: 'hexagon' },
    { label: '强证据声明', color: categoryColors['claim-strong'], shape: 'diamond' },
    { label: '中等声明', color: categoryColors['claim-medium'], shape: 'diamond' },
    { label: '弱/存疑声明', color: categoryColors['claim-weak'], shape: 'diamond' },
    { label: '证据节点', color: '#909399', shape: 'rect' },
    { label: '技能缺失', color: categoryColors['gap-critical'], shape: 'rect' },
  ]
  return items
})

// ═══ 悬浮详情 ═══
const hoveredNode = ref<any>(null)
const tooltipStyle = ref<Record<string, string>>({})

// ═══ 手动计算节点位置（Obsidian 风格分层辐射） ═══
const computePositions = (nodes: any[], edges: any[], width: number, height: number) => {
  const cx = width / 2
  const cy = height / 2

  // 分类节点
  const candidate = nodes.find(n => n.type === 'Candidate')
  const job = nodes.find(n => n.type === 'Job')
  const claims = nodes.filter(n => n.type === 'Claim')
  const evidence = nodes.filter(n => n.type === 'Evidence')
  const gaps = nodes.filter(n => n.type === 'Gap')

  // 找到每个 evidence 的父 claim（通过边）
  const evParentMap = new Map<string, string>()
  edges.forEach(e => {
    const evNode = evidence.find(n => n.id === e.target)
    if (evNode) evParentMap.set(evNode.id, e.source)
  })

  // ═══ 候选人放左中，岗位放右中 ═══
  const mainGap = Math.min(width * 0.32, 280)
  if (candidate) {
    candidate.x = cx - mainGap / 2
    candidate.y = cy
  }
  if (job) {
    job.x = cx + mainGap / 2
    job.y = cy
  }

  // ═══ 声明节点：围绕候选人画弧（左侧半圆） ═══
  const claimCount = claims.length
  if (claimCount > 0) {
    // 从候选人左侧展开，覆盖 240 度弧
    const startAngle = -120 * (Math.PI / 180)
    const endAngle = 120 * (Math.PI / 180)
    const claimRadius = Math.min(width * 0.2, 180)

    claims.forEach((node, i) => {
      const angle = startAngle + (endAngle - startAngle) * (i / Math.max(claimCount - 1, 1))
      node.x = (candidate?.x || cx) + Math.cos(angle) * claimRadius
      node.y = (candidate?.y || cy) + Math.sin(angle) * claimRadius
    })
  }

  // ═══ 证据节点：围绕各自的父 claim 小弧 ═══
  const claimEvMap = new Map<string, any[]>()
  evidence.forEach(ev => {
    const parentId = evParentMap.get(ev.id)
    if (!parentId) return
    if (!claimEvMap.has(parentId)) claimEvMap.set(parentId, [])
    claimEvMap.get(parentId)!.push(ev)
  })

  claimEvMap.forEach((evs, parentId) => {
    const parent = nodes.find(n => n.id === parentId)
    if (!parent || parent.x == null) return

    const evRadius = 70
    const baseAngle = Math.atan2(parent.y! - cy, parent.x! - cx)

    evs.forEach((ev, i) => {
      const spread = 0.6 // 扇形展开角度
      const angle = baseAngle + (i - (evs.length - 1) / 2) * spread
      ev.x = parent.x! + Math.cos(angle) * evRadius
      ev.y = parent.y! + Math.sin(angle) * evRadius
    })
  })

  // ═══ 缺失节点：围绕岗位画弧（右侧半圆） ═══
  const gapCount = gaps.length
  if (gapCount > 0) {
    const startAngle = -80 * (Math.PI / 180)
    const endAngle = 80 * (Math.PI / 180)
    const gapRadius = Math.min(width * 0.18, 160)

    gaps.forEach((node, i) => {
      const angle = startAngle + (endAngle - startAngle) * (i / Math.max(gapCount - 1, 1))
      node.x = (job?.x || cx) + Math.cos(angle) * gapRadius
      node.y = (job?.y || cy) + Math.sin(angle) * gapRadius
    })
  }

  return nodes
}

// ═══ 图谱初始化 ═══
const initGraph = async () => {
  if (!containerRef.value || props.nodes.length === 0) return
  if (graph) { graph.destroy(); graph = null }

  const w = containerRef.value.clientWidth
  const h = props.height

  // 深拷贝节点，手动计算位置
  const positionedNodes = computePositions(
    props.nodes.map(n => ({ ...n })),
    props.edges,
    w,
    h
  )

  // 调试
  console.log('[GraphCanvas] positions:', positionedNodes.map(n =>
    `${n.id}:(${Math.round(n.x||0)},${Math.round(n.y||0)})`
  ))

  // G6 v5 正确格式：x/y 放在 style 里，layout 设为 undefined
  graph = new Graph({
    container: containerRef.value,
    width: w,
    height: h,
    fitView: true,
    fitViewPadding: 40,
    layout: undefined,
    behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
    animation: {
      duration: 500,
      easing: 'ease-in-out',
    },
    node: {
      style: (data: any) => {
        const d = data.data || data
        const color = categoryColors[d.category] || typeColors[d.type] || typeColors.default
        const sz = Math.max(d.size || 30, 20)
        const isMain = d.type === 'Candidate' || d.type === 'Job'
        return {
          fill: color,
          stroke: isMain ? 'rgba(255,255,255,0.8)' : 'rgba(255,255,255,0.3)',
          lineWidth: isMain ? 3 : 1.5,
          size: sz,
          labelText: d.label || data.id,
          labelFill: 'rgba(255,255,255,0.9)',
          labelFontSize: isMain ? 15 : d.type === 'Claim' ? 12 : 10,
          labelFontWeight: isMain ? 'bold' : d.type === 'Claim' ? '600' : 'normal',
          labelPlacement: 'bottom',
          labelOffsetY: 12,
          cursor: 'pointer',
          shadowColor: color,
          shadowBlur: isMain ? 20 : d.type === 'Claim' ? 10 : 6,
          lineDash: (d.confidence != null && d.confidence < 50) ? [4, 4] : undefined,
          opacity: d.type === 'Evidence' ? 0.75 : 1,
        }
      }
    },
    edge: {
      style: (data: any) => {
        const d = data.data || data
        const c = d.confidence
        const color = d.edgeColor || (c != null ? (c >= 80 ? 'rgba(103,194,58,0.6)' : c >= 50 ? 'rgba(230,162,60,0.5)' : 'rgba(245,108,108,0.5)') : 'rgba(255,255,255,0.15)')
        const width = d.lineWidth || (c != null ? Math.max(1, c / 30) : 1)
        const isEvidenceEdge = d.source?.startsWith('claim-') && d.target?.startsWith('ev-')
        return {
          stroke: color,
          lineWidth: width,
          endArrow: true,
          lineDash: d.dashed ? [6, 4] : undefined,
          curveOffset: isEvidenceEdge ? 15 : 25,
          labelText: '',
          labelFill: 'transparent',
        }
      }
    },
    data: {
      nodes: positionedNodes.map(n => ({
        id: n.id,
        style: { x: n.x, y: n.y },
        data: { ...n }
      })),
      edges: props.edges.map(e => ({
        source: e.source,
        target: e.target,
        data: { ...e }
      }))
    }
  })

  await graph.render()

  // 点击事件
  graph.on('node:click', (evt: any) => {
    const id = evt.target?.id
    const nodeData = props.nodes.find(n => n.id === id)
    if (id) emit('nodeClick', id, nodeData?.type || '', nodeData)
  })

  // 悬浮事件
  graph.on('node:pointerover', (evt: any) => {
    const id = evt.target?.id
    const nodeData = props.nodes.find(n => n.id === id)
    if (nodeData) {
      hoveredNode.value = nodeData
      const rect = containerRef.value!.getBoundingClientRect()
      tooltipStyle.value = {
        left: `${(evt.client?.x || evt.canvas?.x || 0) - rect.left + 16}px`,
        top: `${(evt.client?.y || evt.canvas?.y || 0) - rect.top - 10}px`
      }
    }
  })

  graph.on('node:pointerout', () => {
    hoveredNode.value = null
  })
}

watch(() => [props.nodes, props.edges], () => {
  if (!props.loading && props.nodes.length > 0) nextTick(initGraph)
}, { deep: true })

let ro: ResizeObserver | null = null
onMounted(() => {
  if (props.nodes.length > 0) nextTick(initGraph)
  if (containerRef.value) {
    ro = new ResizeObserver(() => {
      if (graph && containerRef.value)
        graph.setSize(containerRef.value.clientWidth, props.height)
    })
    ro.observe(containerRef.value)
  }
})
onBeforeUnmount(() => { ro?.disconnect(); graph?.destroy() })
</script>

<style scoped lang="scss">
.graph-canvas {
  position: relative;
  border-radius: 12px;
  border: 1px solid rgba(0,0,0,0.08);
  // Obsidian 风格深色底
  background: linear-gradient(135deg, #1a1b2e 0%, #16213e 50%, #1a1b2e 100%);
  overflow: hidden;
  min-height: 200px;
  // 微妙的网格纹理
  background-image:
    linear-gradient(135deg, #1a1b2e 0%, #16213e 50%, #1a1b2e 100%),
    radial-gradient(circle at 25% 25%, rgba(64,158,255,0.03) 0%, transparent 50%),
    radial-gradient(circle at 75% 75%, rgba(103,194,58,0.03) 0%, transparent 50%);
}
.graph-loading, .graph-error, .graph-empty {
  position: absolute; inset: 0;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center;
  gap: 8px; color: rgba(255,255,255,0.5);
  background: transparent;
}

// 图例
.graph-legend {
  position: absolute;
  top: 12px;
  left: 12px;
  background: rgba(0,0,0,0.5);
  border: 1px solid rgba(255,255,255,0.1);
  border-radius: 8px;
  padding: 8px 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  z-index: 10;
  backdrop-filter: blur(8px);
  .legend-item {
    display: flex;
    align-items: center;
    gap: 5px;
    .legend-dot {
      width: 10px;
      height: 10px;
      flex-shrink: 0;
      box-shadow: 0 0 6px currentColor;
    }
    .legend-text {
      font-size: 11px;
      color: rgba(255,255,255,0.7);
    }
  }
}

// 悬浮详情
.graph-tooltip {
  position: absolute;
  z-index: 20;
  background: rgba(0,0,0,0.9);
  color: #fff;
  border-radius: 10px;
  padding: 12px 16px;
  max-width: 260px;
  pointer-events: none;
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255,255,255,0.1);
  box-shadow: 0 8px 24px rgba(0,0,0,0.4);
  .tooltip-title {
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 4px;
    color: #fff;
  }
  .tooltip-type {
    font-size: 11px;
    color: rgba(255,255,255,0.5);
    margin-bottom: 8px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
  .tooltip-source, .tooltip-confidence, .tooltip-detail {
    font-size: 12px;
    line-height: 1.6;
    color: rgba(255,255,255,0.8);
  }
  .tooltip-confidence span {
    font-weight: 600;
  }
  .tooltip-detail {
    margin-top: 6px;
    padding-top: 6px;
    border-top: 1px solid rgba(255,255,255,0.1);
    color: rgba(255,255,255,0.6);
  }
}
</style>
