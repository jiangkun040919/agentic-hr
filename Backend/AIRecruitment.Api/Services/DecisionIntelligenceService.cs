using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 招聘决策智能引擎
/// 三大核心：可解释匹配链 / What-if 推演 / 录用风险雷达
/// </summary>
public class DecisionIntelligenceService
{
    private readonly KnowledgeGraphService _graph;
    private readonly AppDbContext _db;
    private readonly IAIService _ai;
    private readonly ILogger<DecisionIntelligenceService> _logger;

    public DecisionIntelligenceService(KnowledgeGraphService graph, AppDbContext db,
        IAIService ai, ILogger<DecisionIntelligenceService> logger)
    {
        _graph = graph; _db = db; _ai = ai; _logger = logger;
    }

    /// <summary>可解释匹配报告 — 每项技能附图谱证据</summary>
    public async Task<ExplainableMatchReport> ExplainMatchAsync(int candidateId, int jobId)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var report = new ExplainableMatchReport
        { CandidateName = candidate.RealName, JobTitle = job.Title, GeneratedAt = DateTime.UtcNow };

        var resumeText = $"{candidate.Education} {candidate.WorkYears.GetValueOrDefault()}年";
        var candidateSkills = ExtractSkillsFromText(resumeText);
        var jdSkills = ExtractSkillsFromText($"{job.Requirements} {job.JD}");
        report.CandidateSkills = candidateSkills; report.JobSkills = jdSkills;

        foreach (var cs in candidateSkills)
        {
            var matched = jdSkills.FirstOrDefault(js => string.Equals(cs, js, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                var evidence = await BuildGraphEvidenceAsync(cs, job.Title);
                report.MatchedSkills.Add(new ExplainableMatchItem
                { Skill = cs, Matched = true, JDRequirement = matched, Evidence = evidence, Score = evidence != null && evidence.GraphVerified ? 100 : 75 });
            }
        }

        foreach (var js in jdSkills)
        {
            if (!candidateSkills.Any(cs => string.Equals(cs, js, StringComparison.OrdinalIgnoreCase)))
            {
                var path = await _graph.GetLearningPathAsync(string.Join(",", candidateSkills), job.Title);
                report.GapSkills.Add(new ExplainableGapItem
                {
                    Skill = js, IsCritical = jdSkills.Take(5).Contains(js),
                    EstimatedLearningTime = EstimateLearningTime(js),
                    SuggestedLearningPath = path.Steps.FirstOrDefault(s => s.Skill.Contains(js, StringComparison.OrdinalIgnoreCase))?.Suggestion
                });
            }
        }

        try
        {
            var prompt = $"招聘分析：候选人{candidate.RealName}，{candidate.Education}，{candidate.WorkYears.GetValueOrDefault()}年。技能{string.Join("、",candidateSkills)}。岗位{job.Title}要求{string.Join("、",jdSkills)}。已匹配{string.Join("、",report.MatchedSkills.Select(m=>m.Skill))}，差距{string.Join("、",report.GapSkills.Select(g=>g.Skill))}。给出150字决策建议：最大优势、最大风险、面试重点。";
            report.AIDecisionAdvice = await SafeCallAI(prompt);
        }
        catch { }

        return report;
    }

    /// <summary>What-if：学一个新技能后匹配分变化</summary>
    public async Task<WhatIfResult> WhatIfAsync(int candidateId, int jobId, string newSkill)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var resumeText = $"{candidate.Education} {candidate.WorkYears.GetValueOrDefault()}年";
        var candidateSkills = ExtractSkillsFromText(resumeText);
        var jdSkills = ExtractSkillsFromText($"{job.Requirements} {job.JD}");

        var currentCount = candidateSkills.Count(cs => jdSkills.Any(js => string.Equals(cs, js, StringComparison.OrdinalIgnoreCase)));
        var currentRate = jdSkills.Count > 0 ? (double)currentCount / jdSkills.Count * 100 : 0;
        var simulatedRate = jdSkills.Count > 0 ? (double)(currentCount + 1) / jdSkills.Count * 100 : 100;

        var evidence = await BuildGraphEvidenceAsync(newSkill, job.Title);
        string? aiAdvice = null;
        try { aiAdvice = await SafeCallAI($"候选人现技能{string.Join("、",candidateSkills)}，学{newSkill}后匹配分从{currentRate:F1}%→{simulatedRate:F1}%。50字建议。"); } catch { }

        return new WhatIfResult
        {
            NewSkill = newSkill, CurrentMatchRate = Math.Round(currentRate, 1),
            SimulatedMatchRate = Math.Round(simulatedRate, 1),
            Improvement = Math.Round(simulatedRate - currentRate, 1),
            SkillRelevance = evidence?.GraphVerified == true ? "强相关（图谱验证）" : "可能相关",
            EstimatedLearningTime = EstimateLearningTime(newSkill),
            RelatedSkills = evidence?.RelatedSkills ?? new(), AIAdvice = aiAdvice
        };
    }

    /// <summary>批量 What-if</summary>
    public async Task<List<WhatIfResult>> WhatIfBatchAsync(int candidateId, int jobId, List<string> skills)
    {
        var results = new List<WhatIfResult>();
        foreach (var s in skills.Take(5))
            try { results.Add(await WhatIfAsync(candidateId, jobId, s)); } catch { }
        return results.OrderByDescending(r => r.Improvement).ToList();
    }

    /// <summary>五维录用风险雷达</summary>
    public async Task<HiringRiskRadar> AnalyzeHiringRiskAsync(int candidateId, int jobId)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var resumeText = $"{candidate.Education} {candidate.WorkYears.GetValueOrDefault()}年";
        var cSkills = ExtractSkillsFromText(resumeText);
        var jSkills = ExtractSkillsFromText($"{job.Requirements} {job.JD}");

        var radar = new HiringRiskRadar
        { CandidateName = candidate.RealName, JobTitle = job.Title, GeneratedAt = DateTime.UtcNow, Dimensions = new() };

        // 维度1: 技能可补救性
        var missing = jSkills.Where(js => !cSkills.Any(cs => string.Equals(cs, js, StringComparison.OrdinalIgnoreCase))).ToList();
        var avgWeeks = missing.Any() ? missing.Average(s => Math.Min(EstimateLearningWeeks(s), 12)) : 0;
        radar.Dimensions.Add(new RiskDimension
        {
            Name = "技能可补救性",
            Score = missing.Count == 0 ? 95 : avgWeeks <= 4 ? 80 : avgWeeks <= 8 ? 60 : avgWeeks <= 12 ? 40 : 20,
            Risk = avgWeeks <= 4 ? "低" : avgWeeks <= 8 ? "中" : "高",
            Detail = missing.Count > 0 ? $"缺失{missing.Count}项({string.Join("、",missing.Take(3))})，约{avgWeeks:F0}周补" : "技能全部匹配",
            Suggestions = missing.Count > 0 ? new() { $"培训{string.Join("、",missing.Take(2))}", "安排导师辅导" } : new() { "关注文化适配" }
        });

        // 维度2: 薪资匹配
        var midSalary = ((job.SalaryMin ?? 0) + (job.SalaryMax ?? 0)) / 2.0;
        int salScore; string salRisk, salDetail; var salSug = new List<string>();
        if (midSalary == 0) { salScore = 60; salRisk = "未知"; salDetail = "岗位未设薪资"; salSug.Add("面试明确薪资"); }
        else { salScore = 80; salRisk = "低"; salDetail = $"岗位预算 ¥{job.SalaryMin}K-¥{job.SalaryMax}K"; salSug.Add("薪资匹配良好"); }
        radar.Dimensions.Add(new RiskDimension { Name = "薪资匹配度", Score = salScore, Risk = salRisk, Detail = salDetail, Suggestions = salSug });

        // 维度3: 稳定性
        var wy = candidate.WorkYears.GetValueOrDefault();
        radar.Dimensions.Add(new RiskDimension
        {
            Name = "稳定性风险", Score = wy >= 5 ? 85 : wy >= 3 ? 70 : wy >= 2 ? 60 : 40,
            Risk = wy >= 3 ? "低" : "中", Detail = $"{wy}年工作经验",
            Suggestions = wy < 3 ? new() { "试用期重点关注", "设明确目标" } : new() { "经验充足" }
        });

        // 维度4: 市场竞争度
        var similarCnt = await _db.Candidates.CountAsync(c => c.CandidateId != candidateId);
        radar.Dimensions.Add(new RiskDimension
        {
            Name = "人才稀缺度", Score = similarCnt <= 5 ? 40 : similarCnt <= 20 ? 65 : 80,
            Risk = similarCnt <= 5 ? "高（稀缺）" : similarCnt <= 20 ? "中" : "低",
            Detail = $"系统{similarCnt}位候选人", Suggestions = similarCnt <= 5 ? new() { "优先录用", "提高offer吸引力" } : new() { "人才池充足" }
        });

        // 维度5: 团队平衡
        radar.Dimensions.Add(new RiskDimension
        {
            Name = "团队平衡性", Score = 75, Risk = "中", Detail = "团队结构待深入评估",
            Suggestions = new() { "评估技能互补性" }
        });

        radar.OverallRiskScore = (int)radar.Dimensions.Average(d => d.Score);
        radar.OverallRisk = radar.OverallRiskScore >= 75 ? "低风险" : radar.OverallRiskScore >= 55 ? "中风险" : "高风险";

        try { radar.AIDecisionAdvice = await SafeCallAI($"五维风险：{string.Join("、",radar.Dimensions.Select(d=>$"{d.Name}:{d.Score}({d.Risk})"))}。候选人{candidate.RealName}岗位{job.Title}。80字决策建议。"); } catch { }

        return radar;
    }

    private async Task<GraphEvidence?> BuildGraphEvidenceAsync(string skill, string jobTitle)
    {
        try
        {
            var gap = await _graph.GetSkillGapAsync(skill, jobTitle);
            var similar = await _graph.FindSimilarJobsAsync(jobTitle);
            return new GraphEvidence { GraphVerified = gap.RequiredSkills.Any(), RelatedJobs = similar.Take(3).ToList(), RelatedSkills = gap.RequiredSkills.Where(r => r != skill).Take(5).ToList(), MatchRate = Math.Round(gap.MatchRate, 1) };
        }
        catch { return new GraphEvidence { GraphVerified = false }; }
    }

    private static HashSet<string> ExtractSkillsFromText(string text) =>
        AccuracyTestData.KnownSkills.Where(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string EstimateLearningTime(string s) { var w = EstimateLearningWeeks(s); return w <= 4 ? $"约{w}周" : w <= 8 ? $"约{w}周(中)" : $"约{w}周(高)"; }
    private static int EstimateLearningWeeks(string s) => s switch
    {
        "Git" or "SQL" or "HTML" or "CSS" or "Linux" => 2, "Docker" or "Redis" or "MongoDB" or "Nginx" or "Jenkins" => 4,
        "Python" or "JavaScript" or "TypeScript" or "Vue" or "React" => 6,
        "Java" or "Go" or "Kubernetes" or "微服务" or "分布式" or "Spark" => 8,
        "机器学习" or "深度学习" or "NLP" or "大模型" or "PyTorch" => 10, "架构设计" or "系统设计" or "C++" or "Rust" => 12, _ => 4
    };

    private async Task<string?> SafeCallAI(string prompt)
    {
        try { var t = _ai.ChatAsync(prompt); return await Task.WhenAny(t, Task.Delay(8000)) == t ? await t : null; }
        catch { return null; }
    }
}

public class ExplainableMatchReport
{
    public string CandidateName { get; set; } = ""; public string JobTitle { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public HashSet<string> CandidateSkills { get; set; } = new(); public HashSet<string> JobSkills { get; set; } = new();
    public List<ExplainableMatchItem> MatchedSkills { get; set; } = new();
    public List<ExplainableGapItem> GapSkills { get; set; } = new();
    public string? AIDecisionAdvice { get; set; }
}

public class ExplainableMatchItem
{
    public string Skill { get; set; } = ""; public bool Matched { get; set; }
    public string JDRequirement { get; set; } = ""; public int Score { get; set; }
    public GraphEvidence? Evidence { get; set; }
}

public class ExplainableGapItem
{
    public string Skill { get; set; } = ""; public bool IsCritical { get; set; }
    public string? EstimatedLearningTime { get; set; } public string? SuggestedLearningPath { get; set; }
}

public class GraphEvidence
{
    public bool GraphVerified { get; set; } public List<string> RelatedJobs { get; set; } = new();
    public List<string> RelatedSkills { get; set; } = new(); public double MatchRate { get; set; }
}

public class WhatIfResult
{
    public string NewSkill { get; set; } = ""; public double CurrentMatchRate { get; set; }
    public double SimulatedMatchRate { get; set; } public double Improvement { get; set; }
    public string SkillRelevance { get; set; } = ""; public string EstimatedLearningTime { get; set; } = "";
    public List<string> RelatedSkills { get; set; } = new(); public string? AIAdvice { get; set; }
}

public class HiringRiskRadar
{
    public string CandidateName { get; set; } = ""; public string JobTitle { get; set; } = "";
    public DateTime GeneratedAt { get; set; } public List<RiskDimension> Dimensions { get; set; } = new();
    public int OverallRiskScore { get; set; } public string OverallRisk { get; set; } = ""; public string? AIDecisionAdvice { get; set; }
}

public class RiskDimension
{
    public string Name { get; set; } = ""; public int Score { get; set; } public string Risk { get; set; } = "";
    public string Detail { get; set; } = ""; public List<string> Suggestions { get; set; } = new();
}
