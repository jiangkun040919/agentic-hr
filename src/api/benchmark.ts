import { request } from '@/utils/request'

// ====== 准确率评测 API ======

/** 运行四方法对比评测 (keyword / AI / KG / fusion) */
export const runBenchmark = () =>
  request.post('/demo/benchmark')

/** 运行三率准确率测试 (JD解析/简历提取/匹配) */
export const runAccuracyBenchmark = () =>
  request.post('/competition/accuracy-benchmark')

/** 获取对比实验静态数据 (来自对比实验报告.md) */
export const getBenchmarkStaticData = (): Promise<BenchmarkStaticData> => {
  return Promise.resolve({
    methods: [
      { name: '关键词匹配', key: 'keyword', accuracy: 60, precision: 55, recall: 62, f1: 58, time: 0.1, color: 'var(--chart-1)' },
      { name: 'AI语义匹配', key: 'ai', accuracy: 78, precision: 76, recall: 80, f1: 78, time: 2.5, color: 'var(--chart-2)' },
      { name: 'AI+知识图谱', key: 'kg', accuracy: 83, precision: 85, recall: 80, f1: 82, time: 3.8, color: 'var(--chart-3)' },
      { name: '三通道融合', key: 'fusion', accuracy: 87, precision: 88, recall: 85, f1: 86, time: 2.0, color: 'var(--chart-4)' },
    ],
    ndcg5: { keyword: 0.62, ai: 0.79, kg: 0.83, fusion: 0.88 },
    avgRank: { keyword: 3.8, ai: 2.4, kg: 2.1, fusion: 1.5 },
    summary: '三通道融合方案在20组测试样本上达到87%准确率，比纯关键词匹配提升27个百分点。反幻觉机制让AI建议经过知识图谱验证，有效防止胡编技能。',
  })
}

export interface BenchmarkMethod {
  name: string
  key: string
  accuracy: number
  precision: number
  recall: number
  f1: number
  time: number
  color: string
}

export interface BenchmarkStaticData {
  methods: BenchmarkMethod[]
  ndcg5: Record<string, number>
  avgRank: Record<string, number>
  summary: string
}
