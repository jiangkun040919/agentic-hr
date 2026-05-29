const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9";
pres.author = "挑战杯参赛团队";
pres.title = "企业AI智能招聘管理系统";

// ═══ Color Palette ═══
const C = {
  bg: "0A1628",       // deep navy bg
  bg2: "0F1F3D",      // lighter navy
  card: "142746",     // card bg
  accent: "00D4AA",   // teal accent
  accent2: "22C5DE",  // cyan accent
  gold: "F0A500",     // gold highlight
  white: "FFFFFF",
  light: "B0C4DE",    // light steel blue
  muted: "6B7FA3",    // muted text
  dim: "1E3A5F",      // dim line color
  red: "F4586D",
  green: "2DD4A3",
};

// ═══ Helpers ═══
const addFooter = (slide, num, total) => {
  slide.addText(`${num} / ${total}`, {
    x: 9.2, y: 5.2, w: 0.6, h: 0.3,
    fontSize: 9, color: C.muted, align: "right", fontFace: "Calibri"
  });
};

const slideNum = (() => { let n = 0; return () => ++n; })();
const TOTAL = 12;

// ═══ Slide 1: Cover ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  // accent bar top
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  // decorative circles
  slide.addShape(pres.shapes.OVAL, { x: 8.5, y: 0.5, w: 2, h: 2, fill: { color: C.accent, transparency: 90 } });
  slide.addShape(pres.shapes.OVAL, { x: -0.5, y: 3.5, w: 2.5, h: 2.5, fill: { color: C.accent2, transparency: 92 } });
  // main title
  slide.addText("企业AI智能招聘管理系统", {
    x: 0.8, y: 1.5, w: 8.5, h: 1,
    fontSize: 38, fontFace: "Arial Black", color: C.white, bold: true, margin: 0
  });
  // subtitle
  slide.addText("多源异构数据驱动岗位和能力图谱构建与动态演化分析", {
    x: 0.8, y: 2.5, w: 8, h: 0.6,
    fontSize: 16, fontFace: "Calibri", color: C.accent, margin: 0
  });
  // line
  slide.addShape(pres.shapes.LINE, { x: 0.8, y: 3.3, w: 2.5, h: 0, line: { color: C.accent, width: 2 } });
  // info
  slide.addText([
    { text: "赛题：XH-202621", options: { breakLine: true } },
    { text: "发榜单位：科大讯飞  |  领域：新一代信息技术", options: {} }
  ], { x: 0.8, y: 3.6, w: 6, h: 0.8, fontSize: 13, color: C.muted, fontFace: "Calibri", margin: 0 });
  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 2: Problem & Background ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("痛点与背景", { x: 0.6, y: 0.3, w: 5, h: 0.6, fontSize: 28, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 0.9, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  const problems = [
    { icon: "🔍", title: "信息过载", desc: "海量简历人工筛选效率低、主观性强" },
    { icon: "🤖", title: "AI幻觉", desc: "大模型可能'编造'技能，匹配结果不可信" },
    { icon: "📉", title: "能力滞后", desc: "岗位技能要求快速变化，JD更新不及时" },
    { icon: "🔗", title: "数据孤岛", desc: "招聘数据分散，无法形成全局洞察" },
  ];

  problems.forEach((p, i) => {
    const y = 1.3 + i * 1.05;
    slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: y, w: 3.8, h: 0.85, fill: { color: C.card } });
    slide.addText(p.icon, { x: 0.8, y: y + 0.15, w: 0.5, h: 0.5, fontSize: 22, margin: 0 });
    slide.addText(p.title, { x: 1.4, y: y + 0.1, w: 2.8, h: 0.35, fontSize: 14, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
    slide.addText(p.desc, { x: 1.4, y: y + 0.45, w: 2.8, h: 0.3, fontSize: 11, color: C.light, margin: 0 });
  });

  // Right: solution
  slide.addShape(pres.shapes.RECTANGLE, { x: 4.8, y: 1.3, w: 4.8, h: 3.8, fill: { color: C.card } });
  slide.addText("我们的方案", { x: 5.0, y: 1.5, w: 4.4, h: 0.5, fontSize: 18, fontFace: "Arial Black", color: C.accent, bold: true, margin: 0 });
  slide.addText([
    { text: "✓ AI + Neo4j 知识图谱深度融合", options: { breakLine: true, color: C.green } },
    { text: "✓ 四通道融合人岗匹配引擎", options: { breakLine: true, color: C.green } },
    { text: "✓ 反幻觉机制：图谱事实交叉验证", options: { breakLine: true, color: C.green } },
    { text: "✓ 岗位能力图谱动态演化分析", options: { breakLine: true, color: C.green } },
    { text: "✓ 多智能体协作 (5-Agent)", options: { breakLine: true, color: C.green } },
    { text: "✓ 多源数据交叉验证 (反抄袭/反通胀)", options: { color: C.green } }
  ], { x: 5.0, y: 2.1, w: 4.4, h: 2.8, fontSize: 13, fontFace: "Calibri", lineSpacingMultiple: 1.6, margin: 0 });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 3: Technical Architecture ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("技术架构", { x: 0.6, y: 0.3, w: 5, h: 0.6, fontSize: 28, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 0.9, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  const layers = [
    { label: "前端展示层", tech: "Vue 3 + Element Plus + ECharts + G6", color: C.accent },
    { label: "业务逻辑层", tech: ".NET 8 Web API + JWT + SignalR + Hangfire", color: C.accent2 },
    { label: "AI 智能层", tech: "MiniMax M2.7 + ML.NET LightGBM + 多智能体 + Graph RAG", color: C.gold },
    { label: "数据存储层", tech: "SQL Server + Neo4j 图数据库 + Redis + MinIO", color: C.muted },
    { label: "基础设施层", tech: "Docker + Nginx 反向代理", color: C.muted },
  ];

  layers.forEach((l, i) => {
    const y = 1.2 + i * 0.82;
    slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: y, w: 8.8, h: 0.65, fill: { color: C.card } });
    slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: y, w: 0.08, h: 0.65, fill: { color: l.color } });
    slide.addText(l.label, { x: 1.0, y: y + 0.08, w: 2.5, h: 0.5, fontSize: 16, fontFace: "Arial Black", color: l.color, bold: true, margin: 0 });
    slide.addText(l.tech, { x: 3.5, y: y + 0.12, w: 5.5, h: 0.4, fontSize: 11, color: C.light, margin: 0 });
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 4: Innovation 1 - Multi-channel Fusion ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("创新 ①", { x: 0.6, y: 0.15, w: 3, h: 0.4, fontSize: 14, color: C.accent, fontFace: "Calibri", margin: 0 });
  slide.addText("四通道融合人岗匹配引擎", { x: 0.6, y: 0.5, w: 8, h: 0.6, fontSize: 26, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 1.05, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  const channels = [
    { name: "规则引擎", weight: "30%", tech: "五维加权评分", adv: "可解释性强", color: C.accent },
    { name: "ML 预测", weight: "25%", tech: "LightGBM 二分类", adv: "速度 <1ms", color: C.accent2 },
    { name: "多智能体 AI", weight: "35%", tech: "5-Agent 协作", adv: "语义理解深", color: C.gold },
    { name: "Graph RAG", weight: "10%", tech: "图谱检索增强", adv: "天然反幻觉", color: C.green },
  ];

  channels.forEach((ch, i) => {
    const x = 0.6 + i * 2.3;
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: 1.3, w: 2.1, h: 2.5, fill: { color: C.card } });
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: 1.3, w: 2.1, h: 0.06, fill: { color: ch.color } });
    slide.addText(ch.weight, { x: x + 0.3, y: 1.5, w: 1.5, h: 0.5, fontSize: 24, fontFace: "Arial Black", color: ch.color, bold: true, align: "center", margin: 0 });
    slide.addText(ch.name, { x: x + 0.2, y: 2.05, w: 1.7, h: 0.35, fontSize: 13, color: C.white, bold: true, align: "center", margin: 0 });
    slide.addText(ch.tech, { x: x + 0.2, y: 2.45, w: 1.7, h: 0.35, fontSize: 10, color: C.light, align: "center", margin: 0 });
    slide.addText(ch.adv, { x: x + 0.2, y: 3.2, w: 1.7, h: 0.35, fontSize: 11, color: ch.color, align: "center", margin: 0 });
  });

  // Bottom: result highlight
  slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: 4.1, w: 8.8, h: 1.0, fill: { color: C.card } });
  slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: 4.1, w: 0.08, h: 1.0, fill: { color: C.green } });
  slide.addText([
    { text: "准确率 60% → 87%  (+27pp)", options: { breakLine: true, color: C.green, bold: true, fontSize: 18 } },
    { text: "F1 58% → 86% (+28pp)   |   精确率 +9%（反幻觉贡献）", options: { fontSize: 11, color: C.light } }
  ], { x: 1.0, y: 4.25, w: 8, h: 0.8, fontFace: "Calibri", margin: 0 });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 5: Innovation 2 - Anti-Hallucination ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("创新 ②", { x: 0.6, y: 0.15, w: 3, h: 0.4, fontSize: 14, color: C.accent, fontFace: "Calibri", margin: 0 });
  slide.addText("反幻觉机制：AI输出 × 图谱事实交叉验证", { x: 0.6, y: 0.5, w: 8.5, h: 0.6, fontSize: 24, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 1.05, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Flow
  const steps = [
    "AI 生成\n技能建议", "Neo4j\n图谱回查", "交叉\n验证标注", "验证率\n量化输出"
  ];
  steps.forEach((s, i) => {
    const x = 0.8 + i * 2.3;
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: 1.4, w: 1.8, h: 1.2, fill: { color: C.card } });
    slide.addShape(pres.shapes.OVAL, { x: x + 0.6, y: 1.5, w: 0.6, h: 0.6, fill: { color: C.accent } });
    slide.addText(`${i + 1}`, { x: x + 0.6, y: 1.5, w: 0.6, h: 0.6, fontSize: 20, color: C.bg, bold: true, align: "center", valign: "middle", margin: 0 });
    slide.addText(s, { x: x, y: 2.2, w: 1.8, h: 0.6, fontSize: 12, color: C.light, align: "center", margin: 0 });
    if (i < 3) {
      slide.addText("→", { x: x + 1.8, y: 1.7, w: 0.5, h: 0.5, fontSize: 20, color: C.accent, align: "center", margin: 0 });
    }
  });

  // Example
  slide.addText("示例", { x: 0.6, y: 3.0, w: 2, h: 0.4, fontSize: 14, fontFace: "Arial Black", color: C.accent, margin: 0 });
  slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: 3.4, w: 8.8, h: 1.8, fill: { color: C.card } });

  slide.addText("AI 输出：候选人掌握 Kubernetes、Docker、TensorFlow", {
    x: 1.0, y: 3.55, w: 8, h: 0.35, fontSize: 12, color: C.light, margin: 0
  });
  slide.addText("↓ Neo4j 图谱回查验证", {
    x: 1.0, y: 3.85, w: 8, h: 0.3, fontSize: 11, color: C.muted, margin: 0
  });
  slide.addText([
    { text: "✓ 已验证: Kubernetes, Docker  (置信度: confirmed)", options: { breakLine: true, color: C.green, fontSize: 12 } },
    { text: "⚠ 未验证: TensorFlow  验证率 = 2/3 = 67%  → 触发人工复核", options: { color: C.gold, fontSize: 12 } }
  ], { x: 1.0, y: 4.15, w: 8, h: 0.8, fontFace: "Calibri", lineSpacingMultiple: 1.5, margin: 0 });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 6: Innovation 3 - Multi-Agent ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("创新 ③", { x: 0.6, y: 0.15, w: 3, h: 0.4, fontSize: 14, color: C.accent, fontFace: "Calibri", margin: 0 });
  slide.addText("多智能体协作架构", { x: 0.6, y: 0.5, w: 8, h: 0.6, fontSize: 26, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 1.05, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Orchestrator
  slide.addShape(pres.shapes.RECTANGLE, { x: 2.5, y: 1.3, w: 5, h: 0.65, fill: { color: C.accent } });
  slide.addText("Agent Orchestrator — 任务分配 · 结果聚合 · 冲突裁决", {
    x: 2.5, y: 1.3, w: 5, h: 0.65, fontSize: 13, color: C.bg, bold: true, align: "center", valign: "middle", fontFace: "Calibri", margin: 0
  });

  const agents = [
    ["简历解析师", "结构化提取技能/学历/经历"],
    ["岗位分析师", "拆解JD为能力需求清单"],
    ["差距诊断师", "对比候选人与岗位能力差距"],
    ["学习规划师", "根据差距生成学习路径"],
    ["AI面试官", "语音交互 + 评分"],
  ];

  agents.forEach((a, i) => {
    const x = 0.4 + i * 1.9;
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: 2.3, w: 1.7, h: 1.5, fill: { color: C.card }, shadow: { type: "outer", blur: 4, offset: 2, color: "000000", opacity: 0.3 } });
    slide.addShape(pres.shapes.OVAL, { x: x + 0.45, y: 2.45, w: 0.8, h: 0.8, fill: { color: C.accent2 } });
    slide.addText(`${i + 1}`, { x: x + 0.45, y: 2.45, w: 0.8, h: 0.8, fontSize: 24, color: C.white, bold: true, align: "center", valign: "middle", margin: 0 });
    slide.addText(a[0], { x: x + 0.1, y: 3.35, w: 1.5, h: 0.3, fontSize: 13, fontFace: "Arial Black", color: C.white, align: "center", margin: 0 });
    slide.addText(a[1], { x: x + 0.1, y: 3.65, w: 1.5, h: 0.3, fontSize: 10, color: C.light, align: "center", margin: 0 });
  });

  // Lines from orchestrator to agents
  for (let i = 0; i < 5; i++) {
    slide.addShape(pres.shapes.LINE, {
      x: 3.75, y: 1.95, w: 0, h: 0.35,
      line: { color: C.accent2, width: 1, dashType: "dash" }
    });
  }

  slide.addText("参照 CVPR 2025 最新多智能体协作范式  |  5 个专业 Agent 各司其职，互相校验", {
    x: 0.6, y: 4.2, w: 8.8, h: 0.5,
    fontSize: 11, color: C.muted, fontFace: "Calibri", align: "center", margin: 0
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 7: Innovation 4 - Cross Validation ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("创新 ④", { x: 0.6, y: 0.15, w: 3, h: 0.4, fontSize: 14, color: C.accent, fontFace: "Calibri", margin: 0 });
  slide.addText("多源交叉验证 + 图谱动态演化", { x: 0.6, y: 0.5, w: 8, h: 0.6, fontSize: 26, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 1.05, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Left: cross validation
  slide.addText("三维数据质检", { x: 0.6, y: 1.3, w: 3, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent, margin: 0 });

  const checks = [
    ["抄袭检测", "跨数据源JD相似度 > 0.7 标记"],
    ["通胀检测", "AI判断技能要求是否过度夸大"],
    ["时滞检测", "对比图谱，标记 >30天 过期技能"],
  ];

  checks.forEach((c, i) => {
    const y = 1.8 + i * 0.8;
    slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: y, w: 4.2, h: 0.65, fill: { color: C.card } });
    slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: y, w: 0.06, h: 0.65, fill: { color: C.gold } });
    slide.addText(c[0], { x: 0.9, y: y + 0.08, w: 1.5, h: 0.5, fontSize: 14, fontFace: "Arial Black", color: C.gold, margin: 0 });
    slide.addText(c[1], { x: 2.4, y: y + 0.1, w: 2.3, h: 0.45, fontSize: 11, color: C.light, margin: 0 });
  });

  // Right: dynamic evolution
  slide.addText("图谱动态演化", { x: 5.3, y: 1.3, w: 3, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent2, margin: 0 });
  slide.addShape(pres.shapes.RECTANGLE, { x: 5.3, y: 1.8, w: 4.3, h: 2.8, fill: { color: C.card } });
  slide.addText("Java 开发工程师 技能需求演化", {
    x: 5.5, y: 1.9, w: 3.8, h: 0.35, fontSize: 13, fontFace: "Arial Black", color: C.white, margin: 0
  });

  const evo = [
    ["2024 Q1", "Spring Boot, JSP, MySQL", C.accent2],
    ["2024 Q3", "+ Docker, + MicroServices", C.accent2],
    ["2025 Q2", "+ K8s, + RAG, − JSP, − Swing", C.green],
  ];
  evo.forEach((e, i) => {
    const y = 2.5 + i * 0.65;
    slide.addText(e[0], { x: 5.5, y: y, w: 1.2, h: 0.3, fontSize: 12, fontFace: "Arial Black", color: e[2], margin: 0 });
    slide.addText(e[1], { x: 5.5, y: y + 0.3, w: 3.8, h: 0.3, fontSize: 10, color: C.light, margin: 0 });
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 8: Innovation 5 - Graph RAG ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("创新 ⑤", { x: 0.6, y: 0.15, w: 3, h: 0.4, fontSize: 14, color: C.accent, fontFace: "Calibri", margin: 0 });
  slide.addText("Graph RAG + Neo4j 全景图谱", { x: 0.6, y: 0.5, w: 8, h: 0.6, fontSize: 26, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 1.05, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Left: explanation
  slide.addShape(pres.shapes.RECTANGLE, { x: 0.6, y: 1.3, w: 4.4, h: 3.6, fill: { color: C.card } });
  slide.addText("Graph RAG 原理", { x: 0.8, y: 1.5, w: 4, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent, margin: 0 });

  const ragSteps = [
    "① 用户查询 → 提取技能关键词",
    "② Neo4j Cypher 查询关联子图",
    "③ 图谱子图注入 AI 上下文窗口",
    "④ AI 基于图谱事实生成推荐",
    "⑤ 每条推荐可追溯到图谱节点",
  ];
  ragSteps.forEach((s, i) => {
    slide.addText(s, { x: 0.8, y: 2.1 + i * 0.55, w: 3.8, h: 0.4, fontSize: 12, color: i === 4 ? C.gold : C.light, margin: 0 });
  });

  // Right: advantages
  slide.addShape(pres.shapes.RECTANGLE, { x: 5.3, y: 1.3, w: 4.3, h: 3.6, fill: { color: C.card } });
  slide.addText("图谱能力", { x: 5.5, y: 1.5, w: 3.8, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent, margin: 0 });

  const caps = [
    { label: "50+", desc: "技能节点", color: C.green },
    { label: "8+", desc: "岗位分类", color: C.accent2 },
    { label: "100+", desc: "关联关系", color: C.gold },
    { label: "3期", desc: "时态快照", color: C.accent },
  ];
  caps.forEach((c, i) => {
    const x = 5.5 + (i % 2) * 2.0;
    const y = 2.2 + Math.floor(i / 2) * 1.3;
    slide.addText(c.label, { x: x, y: y, w: 1.8, h: 0.5, fontSize: 28, fontFace: "Arial Black", color: c.color, bold: true, align: "center", margin: 0 });
    slide.addText(c.desc, { x: x, y: y + 0.55, w: 1.8, h: 0.3, fontSize: 11, color: C.light, align: "center", margin: 0 });
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 9: Experimental Results ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("实验数据与对比", { x: 0.6, y: 0.3, w: 8, h: 0.6, fontSize: 28, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 0.9, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Bar chart: accuracy comparison
  slide.addChart(pres.charts.BAR, [
    { name: "准确率", labels: ["关键词", "AI语义", "AI+KG", "三通道融合"], values: [60, 78, 83, 87] },
    { name: "精确率", labels: ["关键词", "AI语义", "AI+KG", "三通道融合"], values: [55, 76, 85, 88] },
    { name: "F1", labels: ["关键词", "AI语义", "AI+KG", "三通道融合"], values: [58, 78, 82, 86] },
  ], {
    x: 0.6, y: 1.1, w: 5.5, h: 4.0,
    barDir: "col",
    chartColors: [C.accent, C.accent2, C.gold],
    showValue: true, dataLabelPosition: "outEnd", dataLabelColor: C.light, dataLabelFontSize: 9,
    catAxisLabelColor: C.light, catAxisLabelFontSize: 10,
    valAxisLabelColor: C.muted, valAxisLabelFontSize: 8,
    valGridLine: { color: C.dim, size: 0.5 },
    catGridLine: { style: "none" },
    showLegend: true, legendPos: "b", legendColor: C.light, legendFontSize: 9,
    chartArea: { fill: { color: C.bg } },
    plotArea: { fill: { color: C.bg } },
    valAxisMaxVal: 100,
  });

  // Right side: key findings
  slide.addShape(pres.shapes.RECTANGLE, { x: 6.4, y: 1.1, w: 3.2, h: 4.0, fill: { color: C.card } });
  slide.addText("关键发现", { x: 6.6, y: 1.3, w: 2.8, h: 0.4, fontSize: 14, fontFace: "Arial Black", color: C.accent, margin: 0 });
  const findings = [
    "+27%", "+9%", "<1ms", "5"
  ];
  const fDesc = [
    "vs 关键词匹配",
    "精确率提升 (KG反幻觉)",
    "ML通道单次预测",
    "Agent协作验证"
  ];
  findings.forEach((f, i) => {
    const y = 1.9 + i * 0.78;
    slide.addText(f, { x: 6.6, y: y, w: 2.8, h: 0.35, fontSize: 22, fontFace: "Arial Black", color: C.gold, bold: true, margin: 0 });
    slide.addText(fDesc[i], { x: 6.6, y: y + 0.35, w: 2.8, h: 0.25, fontSize: 10, color: C.light, margin: 0 });
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 10: System Features ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("系统功能矩阵", { x: 0.6, y: 0.3, w: 8, h: 0.6, fontSize: 28, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 0.9, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  const features = [
    ["新岗位发现", "AI + 图谱消歧", C.accent],
    ["能力动态更新", "时态快照 + 趋势", C.accent2],
    ["AI简历解析", "三遍分析结构化", C.gold],
    ["人岗匹配诊断", "五维评分 + 差距分析", C.green],
    ["反幻觉验证", "图谱交叉校验", C.accent],
    ["全景图谱", "G6力导图 + 筛选", C.accent2],
    ["AI面试", "语音交互 + 评分", C.gold],
    ["AI合规审计", "公平性 + 决策追溯", C.green],
  ];

  features.forEach((f, i) => {
    const x = 0.6 + (i % 4) * 2.3;
    const y = 1.2 + Math.floor(i / 4) * 2.1;
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: y, w: 2.1, h: 1.8, fill: { color: C.card } });
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: y, w: 2.1, h: 0.06, fill: { color: f[2] } });
    slide.addText(f[0], { x: x + 0.2, y: y + 0.3, w: 1.7, h: 0.5, fontSize: 16, fontFace: "Arial Black", color: C.white, margin: 0 });
    slide.addText(f[1], { x: x + 0.2, y: y + 1.0, w: 1.7, h: 0.5, fontSize: 11, color: C.light, margin: 0 });
    slide.addText("✅", { x: x + 1.5, y: y + 0.3, w: 0.5, h: 0.5, fontSize: 20, margin: 0 });
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 11: Deployment & Stack ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });
  slide.addText("技术栈与部署", { x: 0.6, y: 0.3, w: 8, h: 0.6, fontSize: 28, fontFace: "Arial Black", color: C.white, bold: true, margin: 0 });
  slide.addShape(pres.shapes.LINE, { x: 0.6, y: 0.9, w: 1.2, h: 0, line: { color: C.accent, width: 2 } });

  // Left: tech stack
  slide.addText("技术栈", { x: 0.6, y: 1.2, w: 3, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent, margin: 0 });
  const stack = [
    "前端: Vue 3 + TS + Element Plus + ECharts + G6",
    "后端: .NET 8 + EF Core + JWT + SignalR",
    "AI: MiniMax M2.7 + ML.NET LightGBM",
    "图谱: Neo4j + Graph RAG",
    "存储: SQL Server + Redis + MinIO",
    "部署: Docker Compose 一键部署",
  ];
  stack.forEach((s, i) => {
    slide.addText(`• ${s}`, {
      x: 0.8, y: 1.7 + i * 0.48, w: 4.4, h: 0.4,
      fontSize: 11, color: C.light, margin: 0
    });
  });

  // Right: key numbers
  slide.addText("项目数据", { x: 5.5, y: 1.2, w: 3, h: 0.4, fontSize: 16, fontFace: "Arial Black", color: C.accent2, margin: 0 });
  const nums = [
    ["100+", "测试 JD"],
    ["20 组", "匹配对测试"],
    ["5个", "AI Agent"],
    ["4通道", "匹配引擎"],
    ["3维", "质检机制"],
    ["17张", "数据表"],
  ];
  nums.forEach((n, i) => {
    const x = 5.5 + (i % 2) * 2.2;
    const y = 1.7 + Math.floor(i / 2) * 0.9;
    slide.addShape(pres.shapes.RECTANGLE, { x: x, y: y, w: 2.0, h: 0.75, fill: { color: C.card } });
    slide.addText(n[0], { x: x + 0.1, y: y + 0.08, w: 1.8, h: 0.35, fontSize: 20, fontFace: "Arial Black", color: C.accent2, bold: true, align: "center", margin: 0 });
    slide.addText(n[1], { x: x + 0.1, y: y + 0.42, w: 1.8, h: 0.25, fontSize: 10, color: C.light, align: "center", margin: 0 });
  });

  // Bottom: startup
  slide.addText("docker-compose up -d  →  前端 :3000  |  后端 :5000  |  Neo4j :7474", {
    x: 0.6, y: 4.8, w: 8.8, h: 0.4,
    fontSize: 10, color: C.muted, fontFace: "Calibri", align: "center", margin: 0
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Slide 12: Thank You ═══
(() => {
  const slide = pres.addSlide();
  slide.background = { color: C.bg };
  slide.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 0.06, fill: { color: C.accent } });

  slide.addShape(pres.shapes.OVAL, { x: 7, y: 0.5, w: 3.5, h: 3.5, fill: { color: C.accent, transparency: 92 } });
  slide.addShape(pres.shapes.OVAL, { x: -1, y: 2.5, w: 3, h: 3, fill: { color: C.accent2, transparency: 90 } });

  slide.addText("感谢聆听", {
    x: 0.8, y: 1.5, w: 8.5, h: 1,
    fontSize: 48, fontFace: "Arial Black", color: C.white, bold: true, align: "center", margin: 0
  });
  slide.addShape(pres.shapes.LINE, { x: 3.5, y: 2.7, w: 3, h: 0, line: { color: C.accent, width: 2 } });

  slide.addText([
    { text: "企业AI智能招聘管理系统", options: { breakLine: true, fontSize: 18, color: C.accent } },
    { text: "赛题 XH-202621  |  科大讯飞挑战杯", options: { fontSize: 14, color: C.muted } }
  ], { x: 0.8, y: 3.0, w: 8.5, h: 1.2, fontFace: "Calibri", align: "center", margin: 0 });

  slide.addText("欢迎提问", {
    x: 0.8, y: 4.3, w: 8.5, h: 0.6,
    fontSize: 20, color: C.accent2, fontFace: "Calibri", align: "center", margin: 0
  });

  addFooter(slide, slideNum(), TOTAL);
})();

// ═══ Output ═══
pres.writeFile({ fileName: "E:/企业 AI智能招聘管理系统260430/企业 AI智能招聘管理系统/20260417091104/答辩PPT.pptx" })
  .then(() => console.log("PPT generated successfully!"))
  .catch(err => console.error("Error:", err));
