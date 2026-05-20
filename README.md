# 多源异构数据驱动岗位能力图谱构建与动态演化分析系统

## 赛事信息

- **题目**：多源异构数据驱动岗位和能力图谱构建与动态演化分析研究
- **领域**：新一代信息技术（人工智能、大数据、智能系统、物联网）

## 系统架构

```
数据采集层 → 知识图谱层 → 智能推理层 → 分析应用层 → 可视化交互层
    │            │            │            │            │
Hangfire ETL  Neo4j图数据库  MiniMax AI  差距分析引擎  Vue 3 + ECharts
定时采集      实体-关系存储   大模型推理    学习路径规划  力导图+趋势图
```

## 核心功能

| 功能模块 | 实现 | 对应评分 |
|---------|------|---------|
| **多源数据采集** | Hangfire定时ETL，3类数据源，105+测试JD | 完整性 |
| **新岗位发现** | AI驱动JD生成 + 知识图谱实体消歧 | 创新性 |
| **能力图谱** | Neo4j岗位-技能-能力三元组，力导图可视化 | 完整性 |
| **动态演化** | 图谱时态快照，版本对比，技能趋势折线图 | 创新性 |
| **人岗匹配** | 语义匹配 + 图谱路径距离 + 多维评分 | 实用性 |
| **差距分析** | 缺失技能识别 + 学习路径规划 | 实用性 |
| **反幻觉** | AI输出 × 图谱事实交叉校验，验证率量化 | 创新性 |
| **简历解析** | AI结构化提取，技能标签化 | 实用性 |

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3, Vite 5, Element Plus, Pinia, ECharts, TypeScript |
| 后端 | .NET 8 Web API, EF Core, JWT, SignalR, Hangfire |
| 数据库 | SQL Server, Redis, Neo4j |
| 对象存储 | MinIO |
| AI | MiniMax API (LLM推理), 可替换讯飞星火等 |
| 部署 | Docker Compose 一键部署 |

## 快速启动

### Docker 一键部署（推荐）

```bash
docker-compose up -d
```

访问：
- 前端：http://localhost:3000
- 后端 API：http://localhost:5000/swagger
- Hangfire 仪表盘：http://localhost:5000/hangfire
- Neo4j 浏览器：http://localhost:7474

### 本地开发

```bash
# 后端
cd Backend/AIRecruitment.Api
dotnet run --urls "http://localhost:5000"

# 前端
npm install
npm run dev
```

## 测试数据

- 105 条岗位 JD 测试数据集
- 3 个时间快照版本对比
- 自动准确率评测：`POST /api/data-collection/evaluate-accuracy`

## 项目结构

```
├── docker-compose.yml
├── Dockerfile.frontend
├── Backend/AIRecruitment.Api/
│   ├── Controllers/     # 11个API控制器
│   ├── Services/        # 14个业务服务
│   ├── Models/          # 数据实体 + DTO
│   ├── Extensions/      # DI配置
│   ├── Options/         # 强类型配置
│   └── Data/            # EF Core + 初始化
└── src/
    ├── views/admin/      # HR管理后台
    ├── views/public/     # 求职者端
    ├── api/              # API封装
    └── stores/           # Pinia状态
```
