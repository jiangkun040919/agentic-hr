# ============================================
# 企业AI智能招聘管理系统 - 推送到 GitHub
# ============================================
# 使用方法:
#   1. 先在 GitHub 创建新仓库: https://github.com/new
#      仓库名: Enterprise-AI-Recruitment
#      描述: 企业AI智能招聘管理系统
#      选 Public, 不要勾选任何初始化选项
#   2. 在 PowerShell 中运行此脚本
# ============================================

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "=== 企业AI智能招聘 - 推送到 GitHub ===" -ForegroundColor Cyan

# 移除旧 remote
git remote remove origin 2>$null

# 添加新 remote
$repoUrl = "https://github.com/jiangkun040919/Enterprise-AI-Recruitment.git"
git remote add origin $repoUrl
Write-Host "已添加 remote: $repoUrl" -ForegroundColor Green

# 确保敏感文件不会被提交
Write-Host "`n检查 .gitignore..." -ForegroundColor Yellow
git rm --cached -r .env 2>$null
git rm --cached .env.development 2>$null
git rm --cached Backend/AIRecruitment.Api/appsettings.json 2>$null

# 添加模板文件
git add .gitignore
git add Backend/AIRecruitment.Api/appsettings.template.json

# 暂存所有文件
git add -A

# 提交
$commitMsg = "feat: 企业AI智能招聘完整版 v2.0`n`n- 暖木MUJI设计系统（日系原木风）`n- 多智能体AI匹配引擎`n- Neo4j知识图谱进化 + GraphRAG`n- 智能Kanban筛选看板`n- AI对话式岗位匹配`n- Obsidian风格知识库`n- 岗位采集（爬虫+LLM混合）`n- 模板/种子岗位管理`n- 完整竞赛功能（基准测试/合规/演化演示）`n- 13部门色彩系统`n- 简历PDF预览`n- 13+ 新增Controller, 14+ 新增Service`n- 3 新增管理页面"

git commit -m $commitMsg
Write-Host "已提交" -ForegroundColor Green

# 推送
Write-Host "`n正在推送到 GitHub..." -ForegroundColor Yellow
git push -u origin main

Write-Host "`n✅ 推送完成!" -ForegroundColor Green
Write-Host "   查看: https://github.com/jiangkun040919/Enterprise-AI-Recruitment" -ForegroundColor Cyan
