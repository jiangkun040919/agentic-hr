import { request } from '@/utils/request'

// ====== 公平性审计 & AI合规 API ======

/** 运行公平性审计 */
export const runFairnessAudit = () =>
  request.post('/fairness/audit')

/** AI决策日志列表 (模拟数据，后端暂未完整实现) */
export const getAiAuditLog = (params?: { page?: number; pageSize?: number }) =>
  request.get('/fairness/audit-log', { params })

/** 获取公平性报告静态数据 */
export const getFairnessStaticData = (): Promise<FairnessStaticData> => {
  return Promise.resolve({
    auditTime: new Date().toISOString(),
    overallScore: 82,
    dimensions: [
      { name: '学历偏差', score: 78, status: 'warning', detail: '硕士及以上学历候选人匹配分平均高12%，建议引入学历归一化', icon: 'School' },
      { name: '经验偏差', score: 85, status: 'good', detail: '工作经验年限与岗位要求匹配度良好，无显著偏差', icon: 'Star' },
      { name: '地域偏差', score: 72, status: 'warning', detail: '一线城市候选人简历通过率比非一线高18%，需关注', icon: 'Location' },
      { name: '性别偏差', score: 92, status: 'good', detail: '性别在各筛选阶段分布均衡，无显著差异', icon: 'User' },
      { name: '年龄偏差', score: 88, status: 'good', detail: '各年龄段筛选比例基本一致，偏差在可接受范围', icon: 'Calendar' },
    ],
    aiDecisions: [
      { id: 1, type: '简历筛选', candidate: '张三', job: 'Java高级工程师', score: 87, reason: '技能匹配度高', timestamp: '2026-05-22T10:30:00' },
      { id: 2, type: '匹配评分', candidate: '李四', job: '前端架构师', score: 72, reason: '部分技能缺失', timestamp: '2026-05-22T09:15:00' },
      { id: 3, type: '自动推荐', candidate: '王五', job: 'Python开发', score: 91, reason: '经验高度匹配', timestamp: '2026-05-21T16:45:00' },
      { id: 4, type: '面试评估', candidate: '赵六', job: 'AI研究员', score: 65, reason: '研究经验不足', timestamp: '2026-05-21T14:20:00' },
      { id: 5, type: '简历筛选', candidate: '钱七', job: '数据分析师', score: 83, reason: '工具掌握全面', timestamp: '2026-05-21T11:00:00' },
    ],
    dataSources: [
      { name: '简历解析', source: '用户上传的简历文件', purpose: '候选人技能、教育、工作经历提取', storage: '加密存储，7天自动删除' },
      { name: 'AI匹配评分', source: 'MiniMax API + 本地规则引擎', purpose: '候选人与岗位匹配度评估', storage: '评分结果存储30天' },
      { name: '知识图谱', source: 'Neo4j图数据库', purpose: '技能关系推理与反幻觉验证', storage: '永久存储（脱敏后）' },
      { name: '公平性审计', source: '系统自动统计分析', purpose: '检测招聘各环节的偏差指标', storage: '汇总报告存储90天' },
    ],
    userRights: [
      '您有权查看AI系统对您简历的评分依据',
      '您有权要求人工复核AI的筛选决定',
      '您有权请求删除个人数据（部分数据受法律法规保护）',
      '您有权对不公平的筛选结果提出申诉',
      '系统每季度进行一次公平性审计，结果公开可查',
    ],
  })
}

export interface FairnessDimension {
  name: string
  score: number
  status: 'good' | 'warning' | 'danger'
  detail: string
  icon: string
}

export interface AiDecisionLog {
  id: number
  type: string
  candidate: string
  job: string
  score: number
  reason: string
  timestamp: string
}

export interface DataSource {
  name: string
  source: string
  purpose: string
  storage: string
}

export interface FairnessStaticData {
  auditTime: string
  overallScore: number
  dimensions: FairnessDimension[]
  aiDecisions: AiDecisionLog[]
  dataSources: DataSource[]
  userRights: string[]
}
