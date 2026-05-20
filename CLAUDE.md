---
name: enterprise-ai-recruitment
description: 企业AI智能招聘管理系统 — 项目路径、启动方式、服务状态
metadata:
  type: project
---

项目路径: `E:\企业 AI智能招聘管理系统260430\企业 AI智能招聘管理系统\20260417091104\`

## 技术栈
- 前端: Vue 3 + TypeScript + Vite + Pinia + Element Plus + ECharts (端口 3000)
- 后端: .NET 8 Web API + EF Core + JWT + SignalR + Hangfire (端口 5000)
- 数据库: SQL Server (localdb)\MSSQLLocalDB + Redis + Neo4j + MinIO

## 启动命令

```bash
# 前端
cd "/e/企业 AI智能招聘管理系统260430/企业 AI智能招聘管理系统/20260417091104"
npx vite --host 0.0.0.0 --port 3000

# 后端
cd "/e/企业 AI智能招聘管理系统260430/企业 AI智能招聘管理系统/20260417091104/Backend/AIRecruitment.Api"
AI__ApiKey="sk-cp-Srbu-..." dotnet run --urls "http://localhost:5000"

# Docker 服务（Neo4j + MinIO）
docker compose up -d neo4j minio
```

## Docker 注意事项
- Docker 命令需带完整路径或先 `export PATH="$PATH:/c/Program Files/Docker/Docker/resources/bin"`
- `~/.docker/config.json` 里移除了 `credsStore: "desktop"` 以免 credential helper 报错

## 今天完成的修复
1. 新增 `PUT /api/auth/profile` 接口 + 前端 CandidateProfile.vue 保存按钮接入
2. 注册 CommonInterviewerSetting 路由 `/admin/interviewer-settings`
3. API Key 从 appsettings.json 移到环境变量
4. 安装 Docker Desktop 并启动 Neo4j + MinIO 容器

## 测试账号
注册页面: http://localhost:3000/register
