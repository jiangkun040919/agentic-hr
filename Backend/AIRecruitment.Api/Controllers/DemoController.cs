using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// 动态演化演示 & 准确率评测 API
/// </summary>
[ApiController]
[Route("api/demo")]
[Authorize]
public class DemoController : ControllerBase
{
    private readonly EvolutionDemoService _evolution;
    private readonly EnhancedMatchingService _matching;
    private readonly KnowledgeGraphService _graph;

    public DemoController(
        EvolutionDemoService evolution,
        EnhancedMatchingService matching,
        KnowledgeGraphService graph)
    {
        _evolution = evolution;
        _matching = matching;
        _graph = graph;
    }

    /// <summary>生成动态演化演示数据（3个时间周期）</summary>
    [HttpPost("evolution/generate")]
    public async Task<IActionResult> GenerateEvolutionData()
    {
        var result = await _evolution.GenerateDemoDataAsync();
        return Ok(new
        {
            message = "动态演化演示数据已生成",
            summary = new
            {
                result.TotalSnapshotCount,
                result.NewSkills,
                result.DecliningSkills,
                changeCount = result.Changes.Count
            },
            result.Periods,
            result.Snapshots,
            result.Changes
        });
    }

    /// <summary>获取演化对比数据（前端力导图用）</summary>
    [HttpGet("evolution/trend")]
    public async Task<IActionResult> GetTrendData([FromQuery] string? jobName)
    {
        var data = await _graph.GetSkillTrendAsync(jobName ?? "Java开发工程师");
        return Ok(data);
    }

    /// <summary>运行对比实验（4种方法准确率测试）</summary>
    [HttpPost("benchmark")]
    public async Task<IActionResult> RunBenchmark()
    {
        // 构造 20 组测试样本
        var testPairs = BenchmarkDataGenerator.GenerateTestPairs();

        // 方法1: 纯关键词匹配
        var keywordResults = TestKeywordMatching(testPairs);

        // 方法2: AI 语义匹配（仅规则引擎，无图谱）
        var aiResults = new List<BenchmarkResult>();
        foreach (var pair in testPairs.Take(10))
        {
            try
            {
                var result = await _matching.MatchAsync(pair.ResumeText, pair.JobId);
                aiResults.Add(new BenchmarkResult
                {
                    Label = pair.Label,
                    Score = result.OverallScore,
                    IsAccurate = (result.OverallScore >= 70) == pair.IsExpectedMatch
                });
            }
            catch { aiResults.Add(new BenchmarkResult { Label = pair.Label, Score = 0, IsAccurate = false }); }
        }

        // 方法3: AI + 知识图谱（当前系统）
        var kgResults = new List<BenchmarkResult>();
        foreach (var pair in testPairs.Take(10))
        {
            try
            {
                var result = await _matching.MatchV2Async(pair.ResumeText, pair.JobId);
                kgResults.Add(new BenchmarkResult
                {
                    Label = pair.Label,
                    Score = result.FusionScore,
                    IsAccurate = (result.FusionScore >= 70) == pair.IsExpectedMatch
                });
            }
            catch { kgResults.Add(new BenchmarkResult { Label = pair.Label, Score = 0, IsAccurate = false }); }
        }

        // 计算各方法指标
        var summary = new
        {
            keyword = ComputeMetrics(keywordResults),
            aiSemantic = ComputeMetrics(aiResults),
            aiWithKG = ComputeMetrics(kgResults),
            testCount = testPairs.Count,
            note = "关键词匹配基于技能命中率；AI语义基于规则引擎；AI+KG基于三通道融合。实际准确率受AI API可用性影响。"
        };

        return Ok(summary);
    }

    private static List<BenchmarkResult> TestKeywordMatching(List<TestPair> pairs)
    {
        var allSkills = new[] { "Java", "Python", "Spring", "React", "Docker", "MySQL", "Redis", "Kubernetes",
            "TypeScript", "Go", "微服务", "机器学习", "NLP", "大模型", "Pandas", "PyTorch" };

        var results = new List<BenchmarkResult>();
        foreach (var pair in pairs)
        {
            var matchCount = allSkills.Count(s => pair.ResumeText.Contains(s));
            var score = Math.Min(matchCount * 8.0, 95);
            results.Add(new BenchmarkResult
            {
                Label = pair.Label,
                Score = score,
                IsAccurate = (score >= 70) == pair.IsExpectedMatch
            });
        }
        return results;
    }

    private static object ComputeMetrics(List<BenchmarkResult> results)
    {
        if (results.Count == 0) return new { accuracy = 0, precision = 0, recall = 0, f1 = 0 };

        var tp = results.Count(r => r.IsAccurate && r.Score >= 70);
        var fp = results.Count(r => !r.IsAccurate && r.Score >= 70);
        var tn = results.Count(r => !r.IsAccurate && r.Score < 70);
        var fn = results.Count(r => r.IsAccurate && r.Score < 70);

        var accuracy = (double)(tp + tn) / results.Count * 100;
        var precision = tp + fp > 0 ? (double)tp / (tp + fp) * 100 : 0;
        var recall = tp + fn > 0 ? (double)tp / (tp + fn) * 100 : 0;
        var f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;

        return new
        {
            accuracy = Math.Round(accuracy, 1),
            precision = Math.Round(precision, 1),
            recall = Math.Round(recall, 1),
            f1 = Math.Round(f1, 1),
            samples = results.Count
        };
    }
}

public class BenchmarkResult
{
    public string Label { get; set; } = "";
    public double Score { get; set; }
    public bool IsAccurate { get; set; }
}

/// <summary>
/// 对比实验数据生成器：构造 20 组简历+岗位测试对
/// </summary>
public static class BenchmarkDataGenerator
{
    public static List<TestPair> GenerateTestPairs()
    {
        return new List<TestPair>
        {
            // === 高度匹配 ===
            new() { Label = "Java资深-正样本", ResumeText = "Java开发工程师，8年经验，精通Spring Boot、MySQL、Redis、微服务架构，主导过亿级并发系统设计。", JobId = 1, IsExpectedMatch = true },
            new() { Label = "Python中高级-正样本", ResumeText = "Python开发，5年经验，熟练Django、FastAPI、PostgreSQL、Docker、Linux运维。", JobId = 2, IsExpectedMatch = true },
            new() { Label = "前端高级-正样本", ResumeText = "前端架构师，6年经验，React+TypeScript技术栈，精通Webpack、Vite、Node.js。", JobId = 3, IsExpectedMatch = true },
            new() { Label = "AI算法-正样本", ResumeText = "AI研究员，博士学历，3年大模型研发经验，精通PyTorch、Transformer、RAG架构。", JobId = 4, IsExpectedMatch = true },
            new() { Label = "数据分析-正样本", ResumeText = "数据分析师，4年经验，精通SQL、Python、Pandas，擅长数据可视化和统计建模。", JobId = 5, IsExpectedMatch = true },

            // === 部分匹配 ===
            new() { Label = "Java转AI-部分匹配", ResumeText = "Java开发3年经验，自学Python和机器学习，做过简单的NLP项目。", JobId = 4, IsExpectedMatch = false },
            new() { Label = "前端转后端-部分匹配", ResumeText = "前端开发2年，会用Node.js写简单API，了解Express和MySQL基础。", JobId = 1, IsExpectedMatch = false },
            new() { Label = "应届生-部分匹配", ResumeText = "计算机科学本科应届生，课程项目用过Java和Spring，实习做过测试。", JobId = 1, IsExpectedMatch = false },

            // === 低匹配 ===
            new() { Label = "产品经理-低匹配", ResumeText = "产品经理，5年经验，擅长需求分析和PRD撰写，不懂编程。", JobId = 4, IsExpectedMatch = false },
            new() { Label = "UI设计师-低匹配", ResumeText = "UI设计师，3年经验，精通Figma和Sketch，无编程背景。", JobId = 1, IsExpectedMatch = false },
        };
    }
}
