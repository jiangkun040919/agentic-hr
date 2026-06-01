# 多源异构数据驱动岗位能力图谱构建与动态演化分析系统

> 科大讯飞"挑战杯"竞赛作品 | 赛题 XH-202621  
> 领域：新一代信息技术（人工智能、大数据、智能系统、物联网）

---

## 系统概述

本系统是一个**企业级AI智能招聘管理平台**，以 Neo4j 知识图谱为核心，融合大语言模型（MiniMax）、ML.NET 机器学习、多智能体协作和 Graph RAG 技术，实现从岗位发布、简历投递、AI面试、智能筛选到人才图谱分析的全流程智能化招聘管理。

系统包含三个端：
- **求职者端** — 浏览岗位、投递简历、参加AI面试、查看推荐
- **HR/面试官端** — 岗位管理、简历筛选、安排面试、人才分析
- **管理端** — 系统配置、数据监控、合规审计、模板管理

---

## 技术架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (Vue 3 + Vite)                       │
│  求职者端 / HR管理端 / Admin端 / 面试页面 / 图谱可视化           │
├─────────────────────────────────────────────────────────────────┤
│                      API 网关 (.NET 9 Web API)                   │
│  JWT认证 / SignalR实时推送 / 文件上传 / 30+控制器               │
├──────────┬──────────┬──────────┬──────────┬─────────────────────┤
│ Neo4j    │ SQL      │ Redis    │ MinIO    │ Hangfire            │
│ 知识图谱  │ Server   │ 缓存      │ 文件存储  │ 定时任务             │
├──────────┴──────────┴──────────┴──────────┴─────────────────────┤
│                        AI & ML 引擎                              │
│  MiniMax LLM │ ML.NET │ 多智能体 │ Graph RAG │ 行为分析(TF.js)  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 核心功能模块

### 一、求职者端

| 功能 | 说明 |
|------|------|
| 岗位浏览 | 卡片式展示，支持搜索、筛选、分页，查看详情和技能要求 |
| 简历投递 | 单页表单投递，支持 PDF/Word 上传，base64 编码传输 |
| AI 智能面试 | 多轮对话面试，AI自动提问评分；支持文字/语音双模式 |
| 语音识别 | 浏览器 Web Speech API + MiniMax ASR 云端双通道 |
| TTS 播报 | 面试官语音播报，MiniMax TTS + 浏览器降级 |
| 行为分析 | TensorFlow.js 实时面部+姿态检测（注意力、手势、表情） |
| 面试报告 | 面试后即时生成雷达图评分报告（专业/沟通/解决问题/文化） |
| 推荐岗位 | 基于技能匹配的AI推荐，横向卡片展示，含匹配理由和技能标签 |
| 个人信息 | 候选人资料管理，简历上传后自动持久化 |
| 我的投递 | 投递记录管理，AI面试邀请卡片 + 面试历史记录 |

### 二、HR 管理端

| 功能 | 说明 |
|------|------|
| 岗位管理 | 岗位 CRUD，发布/下架，批量操作，岗位模板管理 |
| 简历管理 | 简历查看、筛选、批量下载，状态流转 |
| 智能筛选 | AI 自动评分排序，简历与 JD 匹配度分析 |
| 面试管理 | 安排面试官，面试日程，全流程跟踪 |
| AI面试管理 | 查看候选人AI面试记录，对话回放，评分详情 |
| 人才对比 | 多候选人横向对比，技能雷达图，匹配度排序 |
| 知识图谱 | 岗位-技能-候选人关联图谱，G6力导向图，拖拽交互 |
| 招聘策略 | 漏斗分析、渠道分析、转化率仪表板 |
| 合规管理 | 招聘合规审计、公平性分析、数据隐私 |
| 竞争力分析 | 候选人竞争排名、优势/劣势分析 |

### 三、数据分析与竞赛功能

| 功能 | 说明 |
|------|------|
| 多源数据采集 | Hangfire 定时 ETL，支持 JD 模板驱动采集 |
| 能力图谱 | Neo4j 实体-关系建模，技能树 + 岗位网络 |
| 动态演化 | 图谱时态快照对比，技能热度趋势分析 |
| 新岗位发现 | AI 生成 JD + 知识图谱实体消歧 |
| 人岗匹配 | 语义匹配 + 图谱路径距离 + 多维加权评分 |
| 差距分析 | 缺失技能识别，学习路径规划建议 |
| Graph RAG | 基于知识图谱的增强检索生成 |
| 反幻觉验证 | AI 输出 × 图谱事实交叉校验，量化验证率 |
| 简历解析 | AI 结构化提取，技能标签化，自动入图 |
| 准确率评测 | 自动化基准测试，匹配准确率量化 |

---

## 技术栈详情

### 前端
| 类别 | 技术 |
|------|------|
| 框架 | Vue 3 (Composition API + `<script setup>`) |
| 构建 | Vite 5 |
| UI 组件 | Element Plus + 自定义黏土风格组件(VBtn/VTag/VEmpty/VDialog) |
| 状态管理 | Pinia (user / resume stores) |
| 路由 | Vue Router 4 |
| 图谱 | @antv/g6 (力导向图，3D黏土节点) |
| 图表 | ECharts 5 |
| 行为分析 | TensorFlow.js (face-landmarks-detection + hand-pose-detection) |
| 语音 | Web Speech API (STT) + Web Audio API (播放) |
| 语言 | TypeScript |
| 样式 | SCSS + CSS Variables (柔光深空主题) |

### 后端
| 类别 | 技术 |
|------|------|
| 框架 | .NET 9 Web API |
| ORM | Entity Framework Core |
| 认证 | JWT (Bearer Token) |
| 实时通信 | SignalR |
| 定时任务 | Hangfire |
| 消息队列 | RabbitMQ |
| 文件存储 | MinIO |
| AI SDK | MiniMax API (chat / TTS / ASR) |
| ML | ML.NET (回归/分类/聚类) |
| 文档处理 | EPPlus (Excel), Spire.PDF |

### 数据库与中间件
| 组件 | 用途 |
|------|------|
| SQL Server | 主业务数据库（用户、岗位、投递、面试） |
| Neo4j | 知识图谱（岗位-技能-候选人实体关系） |
| Redis | 缓存、Session、消息通知 |
| MinIO | 简历文件、头像等对象存储 |

---

## 快速启动

### 环境要求

- .NET 9 SDK
- Node.js 18+
- SQL Server (或 LocalDB)
- Neo4j 5.x
- Redis (可选)
- MinIO (可选，简历存储)

### 1. 克隆项目

```bash
git clone https://github.com/jiangkun040919/agentic-hr.git
cd agentic-hr
```

### 2. 配置

编辑 `Backend/AIRecruitment.Api/appsettings.json`，配置数据库连接字符串和 AI API Key：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AI_Recruitment;Trusted_Connection=true;TrustServerCertificate=true",
    "Neo4j": "bolt://localhost:7687",
    "Redis": "localhost:6379"
  },
  "AI": {
    "ApiKey": "your-minimax-api-key",
    "BaseUrl": "https://api.minimax.chat/v1"
  },
  "Minio": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  }
}
```

### 3. 初始化数据库

```bash
# 项目启动自动执行 EF Core EnsureCreated（需手动创建空数据库）
# 或执行 SQL 初始化脚本
sqlcmd -S localhost -d AI_Recruitment -i SQL_Update_AI_Interview_Permission.sql
```

### 4. 启动后端

```bash
cd Backend/AIRecruitment.Api
dotnet run --urls "http://localhost:5001"
```

### 5. 启动前端

```bash
npm install
npm run dev
```

访问：
- 前端：http://localhost:3000
- Swagger API：http://localhost:5001/swagger
- Neo4j Browser：http://localhost:7474

### Docker 部署（可选）

```bash
docker-compose up -d
```

---

## 项目结构

```
agentic-hr/
├── Backend/
│   └── AIRecruitment.Api/
│       ├── Controllers/          # 33 个 API 控制器
│       │   ├── AuthController.cs          # 认证（登录/注册/JWT）
│       │   ├── JobController.cs           # 岗位管理
│       │   ├── DeliveryController.cs      # 投递管理
│       │   ├── AIInterviewController.cs   # AI面试（文字+语音+TTS）
│       │   ├── InterviewController.cs     # 传统面试管理
│       │   ├── GraphController.cs         # 知识图谱
│       │   ├── MatchController.cs         # 人岗匹配
│       │   ├── MatchingV2Controller.cs    # 增强匹配 v2
│       │   ├── ResumeAiController.cs      # AI简历解析
│       │   ├── StrategyController.cs      # 招聘策略分析
│       │   ├── GraphRagController.cs      # Graph RAG
│       │   ├── AgentController.cs         # 多智能体
│       │   ├── DataCollectionController.cs # 数据采集
│       │   ├── StatController.cs          # 统计分析
│       │   ├── NotificationController.cs  # 消息通知
│       │   ├── FaceController.cs          # 表情分析(腾讯云)
│       │   ├── WorkflowController.cs      # 审批流程
│       │   ├── SysConfigController.cs     # 系统配置
│       │   ├── ComplianceController.cs    # 合规审计
│       │   ├── FairnessController.cs      # 公平性分析
│       │   └── ...
│       ├── Services/              # 42 个业务服务
│       │   ├── AIService.cs               # AI 推理核心
│       │   ├── AIInterviewService.cs      # AI面试逻辑
│       │   ├── KnowledgeGraphService.cs   # Neo4j 图谱操作
│       │   ├── MLMatchingService.cs       # ML.NET 匹配
│       │   ├── MultiAgentMatchingService.cs # 多智能体匹配
│       │   ├── GraphRAGService.cs         # Graph RAG 检索
│       │   ├── GraphEvolutionService.cs   # 图谱演化
│       │   ├── EnhancedMatchingService.cs # 增强匹配引擎
│       │   ├── RecruitmentAgentService.cs # 招聘智能体
│       │   ├── JobDiscoveryService.cs     # 新岗位发现
│       │   ├── DataCollectionService.cs   # 数据采集
│       │   ├── TemplateGenerationService.cs # 模板生成
│       │   ├── ResumeAiService.cs         # 简历AI解析
│       │   ├── StatisticsService.cs       # 统计分析
│       │   ├── DecisionIntelligenceService.cs # 决策智能
│       │   ├── FairnessAuditService.cs    # 公平性审计
│       │   ├── HealthMonitorService.cs    # 系统健康监控
│       │   ├── SignalRService.cs          # 实时推送
│       │   ├── HangfireServices.cs        # 定时任务
│       │   └── ...
│       ├── Models/                # 数据实体 + DTO
│       ├── Data/                  # EF Core DbContext
│       ├── Extensions/            # DI 注册扩展
│       ├── Options/               # 强类型配置类
│       └── Middleware/            # 中间件
├── src/
│   ├── views/
│   │   ├── public/               # 求职者端 (9 页面)
│   │   │   ├── JobList.vue              # 岗位列表
│   │   │   ├── JobDetail.vue            # 岗位详情
│   │   │   ├── ResumeSubmit.vue         # 投递简历
│   │   │   ├── Login.vue / Register.vue # 登录注册
│   │   │   ├── AIInterview.vue          # AI面试（摄像头+语音+对话）
│   │   │   ├── AIInterviewReport.vue    # 面试报告（雷达图）
│   │   │   ├── MyDeliveries.vue         # 我的投递+面试记录
│   │   │   └── CandidateProfile.vue     # 个人资料
│   │   └── admin/                # 管理端 (15 页面)
│   │       ├── Dashboard.vue            # 仪表板
│   │       ├── JobManagement.vue        # 岗位管理
│   │       ├── ResumeManagement.vue     # 简历管理
│   │       ├── SmartScreening.vue       # 智能筛选
│   │       ├── InterviewManagement.vue  # 面试管理
│   │       ├── AIInterviewManagement.vue # AI面试记录管理
│   │       ├── CandidateComparison.vue  # 人才对比
│   │       ├── KnowledgeGraph.vue       # 知识图谱
│   │       ├── RecruitmentStrategy.vue  # 招聘策略
│   │       ├── CompliancePage.vue       # 合规管理
│   │       ├── BenchmarkDashboard.vue   # 基准测试
│   │       └── ...
│   ├── components/
│   │   ├── ui/                   # 自定义 UI 组件（黏土风格）
│   │   │   ├── VBtn.vue / VTag.vue / VEmpty.vue / VDialog.vue
│   │   ├── graph/                # 图谱组件
│   │   │   └── GraphCanvas.vue          # G6 力导向图
│   │   ├── interview/            # 面试组件
│   │   └── ...
│   ├── api/                      # API 封装（按模块）
│   ├── stores/                   # Pinia 状态
│   ├── utils/                    # 工具函数 + 行为分析
│   └── router/                   # 路由配置
├── SQL_Update_AI_Interview_Permission.sql
├── docker-compose.yml
└── README.md
```

---

## API 接口概览

### 认证
| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/auth/login` | 用户登录 |
| POST | `/api/auth/register` | 用户注册 |
| GET | `/api/auth/userinfo` | 获取当前用户信息 |
| POST | `/api/auth/upload-resume` | 上传简历 (base64) |
| PUT | `/api/auth/profile` | 更新个人资料 |

### 岗位
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/job/list` | 岗位列表（搜索/筛选/分页） |
| GET | `/api/job/{id}` | 岗位详情 |
| POST | `/api/job/create` | 创建岗位 |
| PUT | `/api/job/{id}` | 更新岗位 |
| DELETE | `/api/job/{id}` | 删除岗位 |

### AI 面试
| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/ai-interview/start` | 开始AI面试 |
| POST | `/api/ai-interview/answer` | 提交回答 |
| POST | `/api/ai-interview/end` | 结束面试 |
| GET | `/api/ai-interview/result/{sessionId}` | 获取面试结果 |
| GET | `/api/ai-interview/session/{sessionId}` | 会话状态 |
| GET | `/api/ai-interview/my-sessions` | 我的面试记录 |
| POST | `/api/ai-interview/speech-to-text` | 语音转文字 |
| POST | `/api/ai-interview/text-to-speech` | 文字转语音 |
| POST | `/api/ai-interview/voice-start` | 语音模式开始 |
| POST | `/api/ai-interview/voice-answer` | 语音模式回答 |

### 知识图谱
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/graph/job-skill` | 岗位-技能图谱 |
| GET | `/api/graph/candidate-skill/{id}` | 候选人技能图谱 |
| GET | `/api/graph/search?keyword=` | 图谱搜索 |
| GET | `/api/graph/evolution?jobId=` | 图谱演化数据 |

### 匹配分析
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/match/job/{jobId}` | 岗位匹配候选人 |
| GET | `/api/match/candidate/{id}` | 候选人匹配岗位 |
| GET | `/api/match/detail/{jobId}/{candidateId}` | 详细匹配分析 |

---

## AI 面试系统

AI 面试是本系统最具特色的功能模块，实现了全自动化的多轮面试：

### 技术亮点
- **MiniMax LLM 驱动**：AI 根据岗位要求和候选人简历自动生成面试题
- **自适应轮次**：AI 根据回答质量自行决定面试轮数（通常 5-10 轮）
- **四维评分**：专业能力、沟通表达、问题解决、文化适配
- **双模交互**：支持文字输入和语音输入（Web Speech API + MiniMax ASR）
- **语音播报**：AI面试官语音提问（MiniMax TTS），支持浏览器降级
- **行为分析**：TensorFlow.js 实时检测面部、姿态、手势、注意力
- **即时报告**：面试结束后即时生成雷达图评分报告

### 面试流程
```
开始面试 → AI自我介绍提问 → 候选人回答 → AI追问/下一题 → ... → AI评分结束 → 生成报告
    │                                                         │
    └── 语音识别 ── 行为分析 ── TTS播报 ──── 实时反馈 ──────┘
```

---

## 知识图谱系统

基于 Neo4j 构建的岗位-技能-候选人三元组知识图谱：

### 实体类型
- **Job** — 岗位节点（名称、部门、薪资、JD）
- **Skill** — 技能节点（名称、类别、热度）
- **Candidate** — 候选人节点（简历技能映射）
- **Certificate** — 证书/资质节点

### 关系类型
- `REQUIRES` — 岗位要求技能
- `POSSESSES` — 候选人拥有技能
- `RELATED_TO` — 技能间关联关系
- `EVOLVED_FROM` — 技能演化关系

### 可视化
- @antv/g6 力导向图渲染
- 黏土 3D 风格节点，Obsidian 深色画布
- 拖拽交互，缩放平移
- 技能粒子流动动画

---

## 竞赛评分覆盖

| 评分维度 | 权重 | 实现 |
|---------|------|------|
| 完整性 | 25% | 全流程覆盖 + 105条测试JD + 30+API |
| 创新性 | 30% | AI面试 + 行为分析 + 图谱演化 + Graph RAG + 反幻觉 |
| 实用性 | 30% | 完整招聘流程 + AI筛选 + 多端适配 + 合规审计 |
| 文档与展示 | 15% | 详细 README + API文档 + 竞赛报告 |

---

## 测试数据

- 105 条岗位 JD 测试数据集（覆盖 IT、金融、制造等多行业）
- 3 个时间快照版本用于演化对比
- 自动准确率评测端点
- 基准测试仪表板

---

## 常见问题

### 端口冲突
端口 5000 常被 Windows SYSTEM 进程占用，后端改用 **5001**。前端 Vite proxy 指向 5001。

### 登录刷新掉线
JWT Token 存储在 localStorage，7天有效。如遇掉线，检查后端 `Program.cs` 是否误删了 `EnsureDeleted()`。

### AI 面试未完成退出
在"我的投递"页面可以看到进行中的面试记录，点击可继续。已完成面试生成评分报告。

### 中文路径
项目路径含中文不影响编译运行。Shell 操作需注意编码（GBK）。

---

## 开发团队

- 竞赛：科大讯飞"挑战杯"
- 赛题：XH-202621
- 仓库：https://github.com/jiangkun040919/agentic-hr

---

## License

MIT
