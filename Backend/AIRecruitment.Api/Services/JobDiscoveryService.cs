using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AIRecruitment.Api.Services;

/// <summary>
/// AI驱动的新兴岗位发现引擎。
/// 分析多源数据中的高频技能组合、岗位名称变化趋势，自动发现尚未标准化但市场需求增长的新岗位。
/// </summary>
public class JobDiscoveryService
{
    private readonly IAIService _ai;
    private readonly KnowledgeGraphService _graph;
    private readonly AppDbContext _db;
    private readonly ILogger<JobDiscoveryService> _logger;

    public JobDiscoveryService(IAIService ai, KnowledgeGraphService graph, AppDbContext db, ILogger<JobDiscoveryService> logger)
    {
        _ai = ai; _graph = graph; _db = db; _logger = logger;
    }

    /// <summary>
    /// 扫描数据库中的岗位数据，分析高频新兴技能组合，发现潜在的新岗位。
    /// </summary>
    public async Task<EmergingJobReport> DiscoverEmergingJobsAsync()
    {
        var report = new EmergingJobReport { GeneratedAt = DateTime.Now };

        // 1. 从数据库收集所有岗位的技能要求
        var allJobs = await _db.Jobs.Where(j => j.Status == 1).ToListAsync();
        var allRequirements = allJobs.Select(j => j.Requirements + " " + j.JD).ToList();

        // 2. 提取高频技能
        var skillFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var skillSet = new HashSet<string> { "大模型", "LangChain", "RAG", "Prompt Engineering", "向量检索", "AIGC",
            "LLMOps", "多模态", "具身智能", "AI Agent", "GraphRAG", "联邦学习", "边缘AI", "AI安全",
            "Rust", "WebAssembly", "Bun", "Tauri", "SolidJS", "Qwik", "HTMX", "Astro",
            "WebGPU", "Three.js", "数字孪生", "空间计算", "Vision Pro", "AR/VR",
            "量子计算", "绿色AI", "可解释AI", "数据编织", "Data Mesh", "隐私计算" };

        foreach (var req in allRequirements)
            foreach (var skill in skillSet)
                if (req.Contains(skill, StringComparison.OrdinalIgnoreCase))
                    skillFreq[skill] = skillFreq.GetValueOrDefault(skill) + 1;

        // 3. 识别高增长技能（出现频率 > 阈值）
        var emergingSkills = skillFreq.Where(kv => kv.Value >= 1)
            .OrderByDescending(kv => kv.Value).Take(20).Select(kv => kv.Key).ToList();

        // 4. 使用 AI 推理新兴岗位
        var discoveredJobs = new List<DiscoveredJob>();
        if (emergingSkills.Count >= 3)
        {
            var prompt = $$"""
你是岗位分析专家。以下技能在当前招聘市场上高频出现但尚未形成标准化岗位：
{{string.Join("、", emergingSkills)}}

请分析这些技能组合可能催生的新兴岗位。对于每个岗位，给出：
1. 岗位名称（简洁专业）
2. 核心职责（2-3句）
3. 必备技能（5-8个）
4. 加分技能（3-5个）
5. 典型应用场景
6. 市场需求预估（高/中/低）

只输出JSON数组格式：[{"name":"","responsibilities":"","requiredSkills":[],"plusSkills":[],"scenarios":"","demandLevel":""}]
""";
            try
            {
                var aiTask = _ai.ChatAsync(prompt);
                var aiResult = await Task.WhenAny(aiTask, Task.Delay(10000)) == aiTask ? await aiTask : "";
                var json = ExtractJson(aiResult);
                if (!string.IsNullOrEmpty(json))
                {
                    var items = JsonSerializer.Deserialize<List<JsonElement>>(json);
                    if (items != null)
                        foreach (var item in items)
                        {
                            try
                            {
                                discoveredJobs.Add(new DiscoveredJob
                                {
                                    Name = item.GetProperty("name").GetString() ?? "",
                                    Responsibilities = item.GetProperty("responsibilities").GetString() ?? "",
                                    RequiredSkills = item.GetProperty("requiredSkills").EnumerateArray().Select(e => e.GetString()!).ToList(),
                                    PlusSkills = item.GetProperty("plusSkills").EnumerateArray().Select(e => e.GetString()!).ToList(),
                                    Scenarios = item.GetProperty("scenarios").GetString() ?? "",
                                    DemandLevel = item.GetProperty("demandLevel").GetString() ?? "中",
                                    DiscoveredAt = DateTime.Now,
                                    SourceSkills = emergingSkills.Take(5).ToList()
                                });
                            }
                            catch { }
                        }
                }
            }
            catch (Exception ex) { _logger.LogWarning("AI job discovery failed: {msg}", ex.Message); }
        }

        // 5. 如果 AI 不可用，使用规则引擎生成
        if (discoveredJobs.Count == 0)
            discoveredJobs = RuleBasedDiscovery(emergingSkills);

        report.EmergingSkills = emergingSkills;
        report.DiscoveredJobs = discoveredJobs;
        report.TotalDiscovered = discoveredJobs.Count;

        return report;
    }

    /// <summary>
    /// 对既有岗位进行能力要求动态更新分析
    /// </summary>
    public async Task<JobEvolutionReport> AnalyzeJobEvolutionAsync(string jobTitle)
    {
        var report = new JobEvolutionReport { JobTitle = jobTitle, AnalyzedAt = DateTime.Now };

        // 查询图谱快照对比
        var snapshots = await _db.GraphSnapshots
            .Where(s => s.JobName.Contains(jobTitle) || jobTitle.Contains(s.JobName))
            .OrderBy(s => s.Period)
            .ToListAsync();

        if (snapshots.Count < 2)
        {
            // 无历史快照时用 AI 推理趋势
            try
            {
                var prompt = $$"""
分析"{{jobTitle}}"岗位在过去2年中的能力要求变化。输出JSON：
{
  "addedSkills": ["新出现的技能需求"],
  "removedSkills": ["逐渐被淘汰的技能"],
  "upgradedSkills": ["重要度提升的技能"],
  "trendSummary": "整体变化趋势总结"
}
""";
                var aiTask = _ai.ChatAsync(prompt);
                var aiResult = await Task.WhenAny(aiTask, Task.Delay(10000)) == aiTask ? await aiTask : "";
                var json = ExtractJson(aiResult);
                if (!string.IsNullOrEmpty(json))
                {
                    var doc = JsonDocument.Parse(json).RootElement;
                    report.AddedSkills = doc.TryGetProperty("addedSkills", out var a) ? a.EnumerateArray().Select(e => e.GetString()!).ToList() : new();
                    report.RemovedSkills = doc.TryGetProperty("removedSkills", out var r) ? r.EnumerateArray().Select(e => e.GetString()!).ToList() : new();
                    report.UpgradedSkills = doc.TryGetProperty("upgradedSkills", out var u) ? u.EnumerateArray().Select(e => e.GetString()!).ToList() : new();
                    report.TrendSummary = doc.TryGetProperty("trendSummary", out var t) ? t.GetString() ?? "" : "";
                }
            }
            catch { }
        }
        else
        {
            // 从图谱快照比对中提取变化
            var comparison = await _graph.CompareSnapshotsAsync(_db, snapshots.First().Period, snapshots.Last().Period);
            report.AddedSkills = comparison.Changes.Where(c => c.ChangeType == "新增").Select(c => c.Skill).ToList();
            report.RemovedSkills = comparison.Changes.Where(c => c.ChangeType == "删除").Select(c => c.Skill).ToList();
            report.UpgradedSkills = comparison.Changes.Where(c => c.ChangeType == "增强").Select(c => c.Skill).ToList();
            report.TrendSummary = $"从 {snapshots.First().Period} 到 {snapshots.Last().Period}，该岗位共发生 {comparison.Changes.Count} 项技能要求变化。";
        }

        return report;
    }

    /// <summary>从 AI 返回文本中提取 JSON</summary>
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start >= 0 && end > start) return text[start..(end + 1)];
        start = text.IndexOf('{');
        end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text[start..(end + 1)];
        return "";
    }

    /// <summary>规则引擎：基于高频技能组合推断新兴岗位</summary>
    private static List<DiscoveredJob> RuleBasedDiscovery(List<string> skills)
    {
        var discoveries = new List<DiscoveredJob>();
        var skillSet = skills.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 规则1：大模型相关技能组合 → AI Agent开发工程师
        if (skillSet.Contains("大模型") && (skillSet.Contains("LangChain") || skillSet.Contains("RAG")))
            discoveries.Add(MakeDiscovery("AI Agent开发工程师",
                "设计并开发基于大语言模型的智能Agent系统，实现自主决策与任务执行能力。",
                new() { "Python", "LangChain", "大模型API", "RAG技术", "Prompt Engineering", "API设计" },
                new() { "Multi-Agent协作", "强化学习", "向量数据库", "模型微调" },
                "智能客服、自动化办公、代码助手等场景",
                "高", skills.Take(5).ToList()));

        // 规则2：Rust + WebAssembly → WASM全栈工程师
        if (skillSet.Contains("Rust") || skillSet.Contains("WebAssembly"))
            discoveries.Add(MakeDiscovery("WASM全栈开发工程师",
                "利用WebAssembly技术开发高性能浏览器端应用，将传统后端能力迁移至前端。",
                new() { "Rust", "WebAssembly", "TypeScript", "Node.js", "系统编程" },
                new() { "C++", "WebGPU", "WASI", "Docker" },
                "高性能Web应用、在线设计工具、浏览器游戏引擎等场景",
                "中", skills.Take(5).ToList()));

        // 规则3：多模态 + AIGC → 多模态内容生成工程师
        if (skillSet.Contains("多模态") || skillSet.Contains("AIGC"))
            discoveries.Add(MakeDiscovery("多模态内容生成工程师",
                "负责文生图、文生视频、图生3D等多模态AI内容生成系统的开发与优化。",
                new() { "Python", "Stable Diffusion", "多模态学习", "GAN", "Transformer" },
                new() { "3D建模", "视频编解码", "模型压缩", "GPU编程" },
                "数字内容创作、影视特效、游戏资产生成等场景",
                "高", skills.Take(5).ToList()));

        // 规则4：隐私计算 + 联邦学习
        if (skillSet.Contains("隐私计算") || skillSet.Contains("联邦学习"))
            discoveries.Add(MakeDiscovery("隐私AI工程师",
                "设计开发保护数据隐私的AI系统，实现数据可用不可见的安全计算。",
                new() { "联邦学习", "多方安全计算", "差分隐私", "Python", "密码学基础" },
                new() { "TEE", "同态加密", "区块链", "分布式系统" },
                "金融风控、医疗AI、政务数据共享等场景",
                "中", skills.Take(5).ToList()));

        // 规则5：空间计算
        if (skillSet.Contains("空间计算") || skillSet.Contains("Vision Pro") || skillSet.Contains("AR/VR"))
            discoveries.Add(MakeDiscovery("空间计算应用工程师",
                "开发面向Apple Vision Pro等空间计算设备的沉浸式应用。",
                new() { "Swift/SwiftUI", "Unity/Unreal", "3D数学", "空间交互设计", "ARKit/RealityKit" },
                new() { "计算机图形学", "计算机视觉", "Shader编程", "物理引擎" },
                "教育培训、虚拟展厅、远程协作、沉浸式游戏等场景",
                "中", skills.Take(5).ToList()));

        // 规则6：绿色AI + 模型压缩
        if (skillSet.Contains("绿色AI") || skillSet.Contains("可解释AI"))
            discoveries.Add(MakeDiscovery("AI可持续性工程师",
                "负责AI系统的能效优化、碳足迹评估和绿色部署方案设计。",
                new() { "模型量化", "知识蒸馏", "Python", "MLOps", "能效分析" },
                new() { "碳核算", "边缘计算", "神经架构搜索", "云原生" },
                "大规模AI推理优化、绿色数据中心、边缘AI部署等场景",
                "低", skills.Take(5).ToList()));

        return discoveries;
    }

    private static DiscoveredJob MakeDiscovery(string name, string resp, List<string> req, List<string> plus, string scenarios, string demand, List<string> source)
        => new() { Name = name, Responsibilities = resp, RequiredSkills = req, PlusSkills = plus, Scenarios = scenarios, DemandLevel = demand, SourceSkills = source, DiscoveredAt = DateTime.Now };
}

// ========== DTOs ==========
public class EmergingJobReport
{
    public DateTime GeneratedAt { get; set; }
    public List<string> EmergingSkills { get; set; } = new();
    public List<DiscoveredJob> DiscoveredJobs { get; set; } = new();
    public int TotalDiscovered { get; set; }
}

public class DiscoveredJob
{
    public string Name { get; set; } = "";
    public string Responsibilities { get; set; } = "";
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> PlusSkills { get; set; } = new();
    public string Scenarios { get; set; } = "";
    public string DemandLevel { get; set; } = "中";
    public DateTime DiscoveredAt { get; set; }
    public List<string> SourceSkills { get; set; } = new();
}

public class JobEvolutionReport
{
    public string JobTitle { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }
    public List<string> AddedSkills { get; set; } = new();
    public List<string> RemovedSkills { get; set; } = new();
    public List<string> UpgradedSkills { get; set; } = new();
    public string TrendSummary { get; set; } = "";
}
