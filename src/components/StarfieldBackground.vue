<template>
  <canvas ref="canvasRef" class="starfield-canvas" />
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const canvasRef = ref<HTMLCanvasElement>()
let animId = 0
let particles: { x: number; y: number; r: number; vx: number; vy: number; alpha: number; pulse: number }[] = []

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

  particles = Array.from({ length: 80 }, () => ({
    x: Math.random() * window.innerWidth, y: Math.random() * window.innerHeight,
    r: Math.random() * 1.5 + 0.5, vx: (Math.random() - 0.5) * 0.3, vy: (Math.random() - 0.5) * 0.3,
    alpha: Math.random() * 0.5 + 0.2, pulse: Math.random() * Math.PI * 2,
  }))

  function draw() {
    if (!canvas || !ctx) return
    ctx.clearRect(0, 0, window.innerWidth, window.innerHeight)
    const w = window.innerWidth, h = window.innerHeight
    particles.forEach((p) => {
      p.pulse += 0.015
      const a = p.alpha + Math.sin(p.pulse) * 0.15
      ctx!.beginPath(); ctx!.arc(p.x, p.y, p.r, 0, Math.PI * 2)
      ctx!.fillStyle = `rgba(129, 140, 248, ${Math.max(0, a)})`; ctx!.fill()
      ctx!.beginPath(); ctx!.arc(p.x, p.y, p.r * 3, 0, Math.PI * 2)
      ctx!.fillStyle = `rgba(99, 102, 241, ${Math.max(0, a * 0.3)})`; ctx!.fill()
      p.x += p.vx; p.y += p.vy
      if (p.x < 0 || p.x > w) p.vx *= -1
      if (p.y < 0 || p.y > h) p.vy *= -1
    })
    particles.forEach((a, i) => {
      particles.slice(i + 1).forEach((b) => {
        const dx = a.x - b.x, dy = a.y - b.y, dist = Math.sqrt(dx * dx + dy * dy)
        if (dist < 120) {
          ctx!.beginPath(); ctx!.moveTo(a.x, a.y); ctx!.lineTo(b.x, b.y)
          ctx!.strokeStyle = `rgba(99, 102, 241, ${0.04 * (1 - dist / 120)})`
          ctx!.lineWidth = 0.5; ctx!.stroke()
        }
      })
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
