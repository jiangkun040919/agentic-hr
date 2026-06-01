using Newtonsoft.Json;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 动态演化演示数据生成器。
/// 模拟 3 个时间周期的技能需求变化，用于图谱时态快照演示。
/// </summary>
public class EvolutionDemoService
{
    private readonly KnowledgeGraphService _graph;
    private readonly AppDbContext _db;
    private readonly ILogger<EvolutionDemoService> _logger;

    public EvolutionDemoService(KnowledgeGraphService graph, AppDbContext db, ILogger<EvolutionDemoService> logger)
    {
        _graph = graph; _db = db; _logger = logger;
    }

    /// <summary>生成 3 个时间周期的演示数据</summary>
    public async Task<EvolutionDemoResult> GenerateDemoDataAsync()
    {
        var periods = new[] { "2024-Q1", "2024-Q3", "2025-Q2" };
        var result = new EvolutionDemoResult { Periods = periods.ToList() };

        // 模拟 8 个典型 IT 岗位的技能需求演化
        var jobEvolutions = new Dictionary<string, List<(string skill, int weight)>>
        {
            ["Java开发工程师"] = new() {
                new("Java", 95), new("Spring Boot", 90), new("MySQL", 85), new("Redis", 70), new("微服务", 60), new("Docker", 50), new("Kafka", 30)
            },
            ["前端开发工程师"] = new() {
                new("JavaScript", 90), new("React", 80), new("TypeScript", 70), new("Vue", 65), new("CSS", 60), new("Webpack", 40)
            },
            ["Python开发工程师"] = new() {
                new("Python", 95), new("Django", 75), new("FastAPI", 60), new("PostgreSQL", 55), new("Docker", 50)
            },
            ["AI算法工程师"] = new() {
                new("Python", 95), new("PyTorch", 85), new("Transformer", 75), new("NLP", 60), new("大模型", 40), new("模型部署", 35)
            },
            ["数据分析师"] = new() {
                new("SQL", 90), new("Python", 75), new("Pandas", 70), new("数据可视化", 65), new("Tableau", 40)
            },
            ["DevOps工程师"] = new() {
                new("Linux", 90), new("Docker", 85), new("Kubernetes", 70), new("Jenkins", 60), new("Terraform", 40), new("CI/CD", 55)
            },
            ["产品经理"] = new() {
                new("需求分析", 90), new("PRD", 85), new("竞品分析", 70), new("数据分析", 55), new("敏捷开发", 50)
            },
            ["测试工程师"] = new() {
                new("自动化测试", 85), new("Selenium", 70), new("Python", 65), new("JMeter", 50), new("接口测试", 60)
            }
        };

        var rng = new Random(42);
        var snapshots = new List<EvolutionSnapshot>();

        foreach (var period in periods)
        {
            var periodSnapshots = new List<EvolutionSnapshot>();

            foreach (var (job, skills) in jobEvolutions)
            {
                // 模拟技能需求权重随时间变化
                var evolvedSkills = new List<(string skill, int weight)>();
                foreach (var (skill, baseWeight) in skills)
                {
                    var trend = period switch
                    {
                        "2024-Q1" => baseWeight,
                        "2024-Q3" => (int)(baseWeight * (0.9 + rng.NextDouble() * 0.3)),  // 部分技能上升
                        "2025-Q2" => (int)(baseWeight * (0.8 + rng.NextDouble() * 0.5)),  // 新兴技能大幅上升
                        _ => baseWeight
                    };

                    // 模拟新兴技能出现
                    if (period == "2025-Q2" && rng.NextDouble() > 0.85)
                    {
                        var newSkills = new[] { "Kubernetes", "大模型", "RAG", "Prompt Engineering", "LangChain", "GraphQL", "Rust", "WebAssembly" };
                        evolvedSkills.Add((newSkills[rng.Next(newSkills.Length)], rng.Next(20, 40)));
                    }

                    evolvedSkills.Add((skill, Math.Clamp(trend, 5, 100)));
                }

                // 模拟淘汰技能
                if (period != "2024-Q1" && rng.NextDouble() > 0.7 && evolvedSkills.Count > 3)
                {
                    evolvedSkills.RemoveAt(evolvedSkills.Count - 1);
                }

                periodSnapshots.Add(new EvolutionSnapshot
                {
                    JobName = job,
                    Period = period,
                    Skills = evolvedSkills.Select(s => new SkillWithWeight { Name = s.skill, Weight = s.weight }).ToList()
                });

                // 写入图谱
                try
                {
                    var skillStr = string.Join(",", evolvedSkills.Select(s => s.skill).Take(8));
                    await _graph.UpsertJobSkillsAsync(0, job, skillStr, $"技能需求快照 {period}");
                }
                catch { }
            }

            snapshots.AddRange(periodSnapshots);

            // 写入数据库
            foreach (var snap in periodSnapshots)
            {
                _db.GraphSnapshots.Add(new Models.GraphSnapshot
                {
                    JobName = snap.JobName,
                    Period = snap.Period,
                    SkillsJson = JsonConvert.SerializeObject(snap.Skills),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        result.Snapshots = snapshots;
        result.TotalSnapshotCount = snapshots.Count;

        // 检测变化
        result.Changes = DetectChanges(snapshots);
        result.NewSkills = snapshots.Where(s => s.Period == "2025-Q2")
            .SelectMany(s => s.Skills.Select(sk => sk.Name))
            .Distinct()
            .Except(snapshots.Where(s => s.Period == "2024-Q1")
                .SelectMany(s => s.Skills.Select(sk => sk.Name)))
            .Distinct().ToList();

        result.DecliningSkills = snapshots.Where(s => s.Period == "2024-Q1")
            .SelectMany(s => s.Skills.Select(sk => sk.Name))
            .Distinct()
            .Except(snapshots.Where(s => s.Period == "2025-Q2")
                .SelectMany(s => s.Skills.Select(sk => sk.Name)))
            .Distinct().ToList();

        _logger.LogInformation("[EvolutionDemo] 演示数据生成完成: {count} 条快照, {newCount} 新增技能, {decliningCount} 衰退技能",
            result.TotalSnapshotCount, result.NewSkills.Count, result.DecliningSkills.Count);

        return result;
    }

    private static List<EvolutionChange> DetectChanges(List<EvolutionSnapshot> allSnapshots)
    {
        var changes = new List<EvolutionChange>();
        var byPeriod = allSnapshots.GroupBy(s => s.Period).ToDictionary(g => g.Key, g => g.ToList());
        var periods = byPeriod.Keys.OrderBy(p => p).ToList();

        for (int i = 1; i < periods.Count; i++)
        {
            var prev = byPeriod[periods[i - 1]];
            var curr = byPeriod[periods[i]];

            foreach (var prevJob in prev)
            {
                var currJob = curr.FirstOrDefault(c => c.JobName == prevJob.JobName);
                if (currJob == null) continue;

                var prevSkills = prevJob.Skills.Select(s => s.Name).ToHashSet();
                var currSkills = currJob.Skills.Select(s => s.Name).ToHashSet();

                var added = currSkills.Except(prevSkills).ToList();
                var removed = prevSkills.Except(currSkills).ToList();

                if (added.Any() || removed.Any())
                {
                    changes.Add(new EvolutionChange
                    {
                        Period = $"{periods[i-1]} → {periods[i]}",
                        JobName = prevJob.JobName,
                        AddedSkills = added,
                        RemovedSkills = removed
                    });
                }
            }
        }

        return changes;
    }
}

// ═══ DTOs ═══
public class EvolutionDemoResult
{
    public List<string> Periods { get; set; } = new();
    public List<EvolutionSnapshot> Snapshots { get; set; } = new();
    public int TotalSnapshotCount { get; set; }
    public List<EvolutionChange> Changes { get; set; } = new();
    public List<string> NewSkills { get; set; } = new();
    public List<string> DecliningSkills { get; set; } = new();
}

public class EvolutionSnapshot
{
    public string JobName { get; set; } = "";
    public string Period { get; set; } = "";
    public List<SkillWithWeight> Skills { get; set; } = new();
}

public class SkillWithWeight
{
    public string Name { get; set; } = "";
    public int Weight { get; set; }
}

public class EvolutionChange
{
    public string Period { get; set; } = "";
    public string JobName { get; set; } = "";
    public List<string> AddedSkills { get; set; } = new();
    public List<string> RemovedSkills { get; set; } = new();
}
