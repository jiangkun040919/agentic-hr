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
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { Graph, type GraphOptions } from '@antv/g6'
import { Loading, WarningFilled } from '@element-plus/icons-vue'

interface G6Node {
  id: string
  label: string
  type: string
  category: string
  size: number
  style?: Record<string, any>
}

interface G6Edge {
  id: string
  source: string
  target: string
  label: string
  relationship: string
}

const props = withDefaults(defineProps<{
  nodes: G6Node[]
  edges: G6Edge[]
  height?: number
  loading?: boolean
  error?: string
}>(), {
  height: 350,
  loading: false,
  error: ''
})

const emit = defineEmits<{
  (e: 'nodeClick', nodeId: string, type: string): void
}>()

const containerRef = ref<HTMLElement>()
let graph: Graph | null = null

const typeColors: Record<string, string> = {
  Job: '#8A9BA8',
  Skill: '#7A8B5E',
  Industry: '#C4945A',
  Candidate: 'var(--color-primary)',
  default: '#8B9A6E'
}

const initGraph = () => {
  if (!containerRef.value || props.nodes.length === 0) return
  if (graph) { graph.destroy(); graph = null }

  const graphOptions: GraphOptions = {
    container: containerRef.value,
    width: containerRef.value.clientWidth,
    height: props.height,
    autoFit: 'view',
    layout: {
      type: 'force',
      preventOverlap: true,
      linkDistance: 120
    },
    behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
    animation: true,
    node: {
      style: (data: any) => ({
        fill: typeColors[data.type] || typeColors.default,
        stroke: '#fff',
        lineWidth: 2,
        labelText: data.label || data.id,
        labelFill: '#333',
        labelFontSize: 12,
        size: Math.max((data.size || 30), 20)
      })
    },
    edge: {
      style: (data: any) => ({
        stroke: '#C0C0C0',
        lineWidth: 1.5,
        endArrow: true,
        labelText: data.label || ''
      })
    },
    data: {
      nodes: props.nodes.map(n => ({
        id: n.id,
        data: { label: n.label, type: n.type, size: n.size || 30 }
      })),
      edges: props.edges.map(e => ({
        source: e.source,
        target: e.target,
        data: { label: e.label }
      }))
    }
  }

  graph = new Graph(graphOptions)
  graph.on('node:click', (evt: any) => {
    const id = evt.target?.id
    const type = evt.target?.getData?.()?.data?.type || ''
    if (id) emit('nodeClick', id, type)
  })
  graph.render()
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
  border-radius: 8px;
  border: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  overflow: hidden;
  min-height: 200px;
}
.graph-loading, .graph-error, .graph-empty {
  position: absolute; inset: 0;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center;
  gap: 8px; color: var(--el-text-color-secondary);
  background: var(--el-bg-color);
}
</style>
