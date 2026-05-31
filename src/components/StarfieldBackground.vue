<template>
  <canvas ref="canvasRef" class="starfield-canvas" />
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const canvasRef = ref<HTMLCanvasElement>()
let animId = 0
let particles: { x: number; y: number; r: number; vx: number; vy: number; alpha: number; pulse: number; hue: number }[] = []

onMounted(() => {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const dpr = window.devicePixelRatio || 1

  function resize() {
    canvas!.width = window.innerWidth * dpr
    canvas!.height = window.innerHeight * dpr
    canvas!.style.width = window.innerWidth + 'px'
    canvas!.style.height = window.innerHeight + 'px'
    ctx!.setTransform(dpr, 0, 0, dpr, 0, 0)
  }
  resize()
  window.addEventListener('resize', resize)

  // Warm floating motes — like dust in sunlight (komorebi)
  particles = Array.from({ length: 50 }, () => ({
    x: Math.random() * window.innerWidth,
    y: Math.random() * window.innerHeight,
    r: Math.random() * 2.5 + 1,
    vx: (Math.random() - 0.5) * 0.2,
    vy: (Math.random() - 0.5) * 0.15 - 0.05,
    alpha: Math.random() * 0.35 + 0.08,
    pulse: Math.random() * Math.PI * 2,
    hue: Math.random() * 20 + 38,  // 38-58: amber to gold range
  }))

  function draw() {
    if (!canvas || !ctx) return
    ctx.clearRect(0, 0, window.innerWidth, window.innerHeight)
    const w = window.innerWidth, h = window.innerHeight
    particles.forEach((p) => {
      p.pulse += 0.008
      const a = p.alpha + Math.sin(p.pulse) * 0.12
      if (a <= 0.02) return
      
      // Warm glow around each mote
      ctx!.beginPath()
      ctx!.arc(p.x, p.y, p.r * 4, 0, Math.PI * 2)
      ctx!.fillStyle = `hsla(${p.hue}, 40%, 65%, ${Math.max(0, a * 0.15)})`
      ctx!.fill()
      
      // Core mote
      ctx!.beginPath()
      ctx!.arc(p.x, p.y, p.r, 0, Math.PI * 2)
      ctx!.fillStyle = `hsla(${p.hue}, 30%, 75%, ${Math.max(0, a)})`
      ctx!.fill()
      
      p.x += p.vx
      p.y += p.vy
      if (p.x < -20 || p.x > w + 20) p.vx *= -1
      if (p.y < -20 || p.y > h + 20) {
        p.vy *= -1
        if (p.y > h + 20) p.y = h + 19
        if (p.y < -20) p.y = -19
      }
    })
    animId = requestAnimationFrame(draw)
  }
  draw()
})

onUnmounted(() => cancelAnimationFrame(animId))
</script>

<style scoped>
.starfield-canvas { position: fixed; inset: 0; z-index: 0; pointer-events: none; }
</style>
