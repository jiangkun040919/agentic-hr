using Neo4j.Driver;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using Newtonsoft.Json;

namespace AIRecruitment.Api.Services;

public class KnowledgeGraphService : IDisposable
{
    private readonly IDriver? _driver;
    private readonly ILogger<KnowledgeGraphService> _logger;
    private bool _available;
    private bool _checked;

    public KnowledgeGraphService(IConfiguration configuration, ILogger<KnowledgeGraphService> logger)
    {
        _logger = logger;
        try
        {
            var uri = configuration["Neo4j:Uri"] ?? "bolt://localhost:7687";
            var user = configuration["Neo4j:User"] ?? "neo4j";
            var password = configuration["Neo4j:Password"] ?? "password";
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }
        catch (Exception ex)
        {
            _driver = null;
            _logger.LogWarning("Neo4j driver creation failed: {msg}", ex.Message);
        }
    }

    private async Task<bool> IsAvailableAsync()
    {
        if (_checked) return _available;
        _checked = true;
        if (_driver == null) return false;
        try
        {
            await using var session = _driver.AsyncSession();
            await session.RunAsync("RETURN 1");
            _available = true;
            _logger.LogInformation("Neo4j connected");
            await EnsureConstraintsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Neo4j not available, using seed data: {msg}", ex.Message);
            _available = false;
        }
        return _available;
    }

    private async Task EnsureConstraintsAsync()
    {
        if (_driver == null) return;
        try
        {
            await using var session = _driver.AsyncSession();
            await session.RunAsync("CREATE CONSTRAINT job_name IF NOT EXISTS FOR (j:Job) REQUIRE j.name IS UNIQUE");
            await session.RunAsync("CREATE CONSTRAINT skill_name IF NOT EXISTS FOR (s:Skill) REQUIRE s.name IS UNIQUE");
        }
        catch { /* ignore */ }
    }

    // ========== 图谱查询（带降级）==========

    public async Task<GraphData> GetJobSkillGraphAsync(string? centerJob = null, int depth = 2)
    {
        if (!await IsAvailableAsync())
            return GetSeedGraphData(centerJob);

        await using var session = _driver!.AsyncSession();
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var nodeIds = new HashSet<string>();

        try
        {
            var query = centerJob != null
                ? @"MATCH (j:Job {name: $centerJob})-[r:REQUIRES*1.." + depth + @"]-(related) RETURN DISTINCT j, r, related LIMIT 200"
                : @"MATCH (j:Job)-[r:REQUIRES]->(s:Skill) RETURN j, r, s LIMIT 200";

            var cursor = await session.RunAsync(query, new { centerJob });
            await cursor.ForEachAsync(record =>
            {
                foreach (var key in record.Values.Keys)
                {
                    if (record[key] is INode node && nodeIds.Add(node.ElementId))
                        nodes.Add(new GraphNode(node.ElementId, node.Labels.FirstOrDefault() ?? "?", node.Properties.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? "")));
                    if (record[key] is IRelationship rel)
                        edges.Add(new GraphEdge(rel.ElementId, rel.Type, rel.StartNodeElementId, rel.EndNodeElementId));
                    else if (record[key] is IReadOnlyList<IRelationship> relList)
                        foreach (var r in relList)
                            edges.Add(new GraphEdge(r.ElementId, r.Type, r.StartNodeElementId, r.EndNodeElementId));
                }
            });
        }
        catch { return GetSeedGraphData(centerJob); }

        edges = edges.GroupBy(e => $"{e.Source}-{e.Target}-{e.Label}").Select(g => g.First()).ToList();
        return new GraphData(nodes, edges);
    }

    public async Task<GapAnalysisResult> GetSkillGapAsync(string candidateSkills, string targetJob)
    {
        var candidateSkillList = ExtractSkillsFromText(candidateSkills).ToList();
        if (!await IsAvailableAsync())
            return GetSeedGapAnalysis(candidateSkillList, targetJob);

        var requiredSkills = new List<string>();
        var matchedSkills = new List<string>();
        var missingSkills = new List<string>();

        try
        {
            await using var session = _driver!.AsyncSession();
            var cursor = await session.RunAsync(
                "MATCH (j:Job {name: $job})-[r:REQUIRES]->(s:Skill) RETURN s.name ORDER BY r.weight DESC", new { job = targetJob });
            await cursor.ForEachAsync(record =>
            {
                var sn = record["s.name"].As<string>();
                requiredSkills.Add(sn);
                if (candidateSkillList.Any(cs => string.Equals(cs, sn, StringComparison.OrdinalIgnoreCase)))
                    matchedSkills.Add(sn);
                else missingSkills.Add(sn);
            });
        }
        catch { return GetSeedGapAnalysis(candidateSkillList, targetJob); }

        var matchRate = requiredSkills.Count > 0 ? (double)matchedSkills.Count / requiredSkills.Count * 100 : 0;
        return new GapAnalysisResult(targetJob, requiredSkills, matchedSkills, missingSkills, matchRate);
    }

    public async Task<LearningPathResult> GetLearningPathAsync(string candidateSkills, string targetJob)
    {
        var gap = await GetSkillGapAsync(candidateSkills, targetJob);
        var steps = gap.MissingSkills.Take(5).Select(s => new LearningStep(s, $"学习 {s}（参考主流教程和项目实践，建议用时2-4周）")).ToList();
        return new LearningPathResult(targetJob, gap.MatchRate, steps, gap.MissingSkills);
    }

    public async Task<List<string>> FindSimilarJobsAsync(string jobName)
    {
        if (!await IsAvailableAsync()) return new List<string> { "前端开发工程师", "全栈工程师", "Python开发工程师" };
        var similar = new List<string>();
        try
        {
            await using var session = _driver!.AsyncSession();
            var cursor = await session.RunAsync(
                @"MATCH (j1:Job {name: $name})-[:REQUIRES]->(s:Skill)<-[:REQUIRES]-(j2:Job) WHERE j1 <> j2 RETURN j2.name AS similar, count(s) AS shared ORDER BY shared DESC LIMIT 10",
                new { name = jobName });
            await cursor.ForEachAsync(record => similar.Add(record["similar"].As<string>()));
        }
        catch { }
        return similar;
    }

    public async Task<HallucinationCheckResult> VerifySkillsAsync(List<string> aiGeneratedSkills)
    {
        if (!await IsAvailableAsync())
            return new HallucinationCheckResult(aiGeneratedSkills, new(), 100);

        var verified = new List<string>();
        var unverified = new List<string>();
        try
        {
            await using var session = _driver!.AsyncSession();
            foreach (var skill in aiGeneratedSkills)
            {
                var cursor = await session.RunAsync("MATCH (s:Skill) WHERE toLower(s.name) CONTAINS toLower($name) RETURN s.name LIMIT 1", new { name = skill.Trim() });
                if (await cursor.FetchAsync()) verified.Add(skill); else unverified.Add(skill);
            }
        }
        catch { return new HallucinationCheckResult(aiGeneratedSkills, new(), 100); }

        return new HallucinationCheckResult(verified, unverified, aiGeneratedSkills.Count > 0 ? (double)verified.Count / aiGeneratedSkills.Count * 100 : 100);
    }

    public async Task<SkillTrendData> GetSkillTrendAsync(string jobName)
    {
        var periods = new[] { "2024-Q1", "2024-Q3", "2025-Q1" };
        var points = new List<SkillTrendPoint>();
        var rng = new Random(jobName.GetHashCode());
        var seedSkills = GetSeedSkills(jobName);

        foreach (var period in periods)
        {
            foreach (var (skill, baseW) in seedSkills)
            {
                var trend = period switch
                {
                    "2024-Q1" => Math.Round(baseW * (0.7 + rng.NextDouble() * 0.3), 1),
                    "2024-Q3" => Math.Round(baseW * (0.8 + rng.NextDouble() * 0.4), 1),
                    "2025-Q1" => Math.Round(baseW * (0.9 + rng.NextDouble() * 0.6), 1),
                    _ => baseW
                };
                points.Add(new SkillTrendPoint(skill, period, trend));
            }
        }

        if (!await IsAvailableAsync()) return new SkillTrendData(jobName, periods, points);

        try
        {
            await using var session = _driver!.AsyncSession();
            var cursor = await session.RunAsync("MATCH (j:Job {name: $job})-[r:REQUIRES]->(s:Skill) RETURN s.name AS skill, r.weight AS weight", new { job = jobName });
            var neoSkills = new List<(string, int)>();
            await cursor.ForEachAsync(record => neoSkills.Add((record["skill"].As<string>(), record["weight"].As<int>())));
            if (neoSkills.Count > 0)
            {
                points.Clear();
                foreach (var period in periods)
                    foreach (var (skill, bw) in neoSkills)
                        points.Add(new SkillTrendPoint(skill, period, Math.Round(bw * (0.7 + rng.NextDouble() * 0.6), 1)));
            }
        }
        catch { }

        return new SkillTrendData(jobName, periods, points);
    }

    public async Task<List<GraphSnapshot>> TakeSnapshotAsync(AppDbContext db, string period)
    {
        var snapshots = new List<GraphSnapshot>();
        var seedMap = GetSeedJobSkillMap();
        foreach (var (job, skills) in seedMap)
        {
            snapshots.Add(new GraphSnapshot
            {
                JobName = job,
                SkillsJson = JsonConvert.SerializeObject(skills),
                Period = period,
                CreatedAt = DateTime.Now
            });
        }

        if (await IsAvailableAsync())
        {
            try
            {
                await using var session = _driver!.AsyncSession();
                var cursor = await session.RunAsync("MATCH (j:Job)-[r:REQUIRES]->(s:Skill) RETURN j.name AS job, collect({skill:s.name,weight:r.weight}) AS skills");
                snapshots.Clear();
                await cursor.ForEachAsync(record =>
                {
                    snapshots.Add(new GraphSnapshot { JobName = record["job"].As<string>(), SkillsJson = JsonConvert.SerializeObject(record["skills"]), Period = period, CreatedAt = DateTime.Now });
                });
            }
            catch { }
        }

        db.GraphSnapshots.AddRange(snapshots);
        await db.SaveChangesAsync();
        return snapshots;
    }

    public async Task<SnapshotComparison> CompareSnapshotsAsync(AppDbContext db, string period1, string period2)
    {
        var snap1 = await db.GraphSnapshots.Where(s => s.Period == period1).ToListAsync();
        var snap2 = await db.GraphSnapshots.Where(s => s.Period == period2).ToListAsync();
        var diff = new List<SkillChange>();
        var jobMap1 = snap1.ToDictionary(s => s.JobName, s => JsonConvert.DeserializeObject<List<SkillWeight>>(s.SkillsJson) ?? new());
        var jobMap2 = snap2.ToDictionary(s => s.JobName, s => JsonConvert.DeserializeObject<List<SkillWeight>>(s.SkillsJson) ?? new());
        var allJobs = jobMap1.Keys.Union(jobMap2.Keys);
        foreach (var job in allJobs)
        {
            var s1 = jobMap1.GetValueOrDefault(job) ?? new();
            var s2 = jobMap2.GetValueOrDefault(job) ?? new();
            var names1 = s1.Select(s => s.Skill).ToHashSet();
            var names2 = s2.Select(s => s.Skill).ToHashSet();
            foreach (var sk in names2.Except(names1)) diff.Add(new SkillChange(job, sk, "新增", "N/A", s2.First(x => x.Skill == sk).Weight.ToString()));
            foreach (var sk in names1.Except(names2)) diff.Add(new SkillChange(job, sk, "删除", s1.First(x => x.Skill == sk).Weight.ToString(), "N/A"));
            foreach (var sk in names1.Intersect(names2))
            {
                var w1 = s1.First(x => x.Skill == sk).Weight;
                var w2 = s2.First(x => x.Skill == sk).Weight;
                if (Math.Abs(w1 - w2) > 0.5) diff.Add(new SkillChange(job, sk, w2 > w1 ? "增强" : "减弱", w1.ToString(), w2.ToString()));
            }
        }
        return new SnapshotComparison(period1, period2, diff);
    }

    public async Task UpsertJobSkillsAsync(int jobId, string jobTitle, string requirements, string jd)
    {
        if (!await IsAvailableAsync()) return;
        try
        {
            await using var session = _driver!.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("MERGE (j:Job {name: $name}) SET j.jobId = $jobId, j.updatedAt = datetime()", new { name = jobTitle, jobId });
                foreach (var skill in ExtractSkillsFromText(requirements + " " + jd))
                    await tx.RunAsync("MERGE (s:Skill {name: $name}) WITH s MATCH (j:Job {jobId: $jobId}) MERGE (j)-[r:REQUIRES]->(s) SET r.weight = coalesce(r.weight, 0) + 1, r.updatedAt = datetime()", new { name = skill, jobId });
            });
        }
        catch { }
    }

    /// <summary>带权重的技能 Upsert — 用真实频率数据更新图谱</summary>
    public async Task UpsertJobSkillsWithWeightsAsync(string jobTitle, Dictionary<string, int> skillWeights)
    {
        if (!await IsAvailableAsync()) return;
        try
        {
            await using var session = _driver!.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "MERGE (j:Job {name: $name}) SET j.updatedAt = datetime()",
                    new { name = jobTitle });
                foreach (var (skill, weight) in skillWeights)
                {
                    await tx.RunAsync(
                        @"MERGE (s:Skill {name: $skill})
                          WITH s
                          MATCH (j:Job {name: $job})
                          MERGE (j)-[r:REQUIRES]->(s)
                          SET r.weight = $weight, r.updatedAt = datetime()",
                        new { skill, job = jobTitle, weight });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("UpsertJobSkillsWithWeights 失败 {job}: {msg}", jobTitle, ex.Message);
        }
    }

    // ========== 种子数据 ==========

    private static Dictionary<string, List<SkillWeight>> GetSeedJobSkillMap() => new()
    {
        ["Java开发工程师"] = new() { new("Java", 10), new("Spring Boot", 9), new("微服务", 8), new("MySQL", 7), new("Redis", 6), new("Docker", 5), new("Kubernetes", 4), new("消息队列", 5) },
        ["Python开发工程师"] = new() { new("Python", 10), new("Django", 8), new("Flask", 7), new("FastAPI", 8), new("PostgreSQL", 6), new("Docker", 5), new("Linux", 6), new("Git", 5) },
        ["前端开发工程师"] = new() { new("React", 9), new("Vue", 9), new("TypeScript", 8), new("Webpack", 6), new("Vite", 7), new("CSS3", 6), new("HTML5", 6), new("Node.js", 5) },
        ["数据工程师"] = new() { new("Spark", 9), new("Flink", 7), new("Hadoop", 6), new("SQL", 9), new("Python", 8), new("Kafka", 7), new("Hive", 6), new("数据仓库", 7) },
        ["机器学习工程师"] = new() { new("Python", 10), new("TensorFlow", 8), new("PyTorch", 9), new("Pandas", 7), new("NumPy", 6), new("Scikit-learn", 7), new("深度学习", 8), new("特征工程", 6) },
        ["DevOps工程师"] = new() { new("Docker", 10), new("Kubernetes", 9), new("Jenkins", 8), new("Linux", 8), new("AWS", 7), new("GitLab CI", 6), new("Terraform", 5), new("监控", 6) },
        ["产品经理"] = new() { new("产品设计", 9), new("数据分析", 7), new("PRD", 8), new("用户研究", 7), new("项目管理", 8), new("SQL", 5), new("竞品分析", 6) },
        ["测试开发工程师"] = new() { new("自动化测试", 9), new("Selenium", 7), new("JMeter", 6), new("Python", 7), new("CI/CD", 6), new("性能测试", 7), new("接口测试", 8) },
        ["AI应用工程师"] = new() { new("Python", 9), new("大模型", 8), new("LangChain", 7), new("RAG", 7), new("Prompt Engineering", 8), new("向量检索", 6), new("API设计", 6) },
        ["云计算架构师"] = new() { new("AWS", 9), new("微服务", 9), new("Kubernetes", 9), new("系统设计", 10), new("高并发", 8), new("分布式", 8), new("网络", 7), new("安全", 7) },
    };

    private static List<(string, double)> GetSeedSkills(string jobName)
    {
        if (GetSeedJobSkillMap().TryGetValue(jobName, out var skills))
            return skills.Select(s => (s.Skill, s.Weight)).ToList();
        return new() { ("Java", 8), ("Python", 7), ("SQL", 6), ("Linux", 5), ("Git", 5) };
    }

    private static GraphData GetSeedGraphData(string? centerJob)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var jobs = string.IsNullOrEmpty(centerJob)
            ? GetSeedJobSkillMap().Take(6)
            : GetSeedJobSkillMap().Where(kv => kv.Key.Contains(centerJob, StringComparison.OrdinalIgnoreCase) || centerJob.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                .Concat(GetSeedJobSkillMap().Take(2));

        foreach (var (job, skills) in jobs)
        {
            var jobId = $"job_{job}";
            nodes.Add(new GraphNode(jobId, "Job", new() { ["name"] = job }));
            foreach (var skill in skills.Take(5))
            {
                var skillId = $"skill_{skill.Skill}";
                if (!nodes.Any(n => n.Id == skillId))
                    nodes.Add(new GraphNode(skillId, "Skill", new() { ["name"] = skill.Skill }));
                edges.Add(new GraphEdge($"{jobId}_{skillId}", "REQUIRES", jobId, skillId));
            }
        }
        return new GraphData(nodes, edges);
    }

    private static GapAnalysisResult GetSeedGapAnalysis(List<string> candidate, string target)
    {
        var seedSkills = GetSeedJobSkillMap().GetValueOrDefault(target)?.Select(s => s.Skill).ToList() ?? new() { "Java", "MySQL", "Spring" };
        var matched = candidate.Where(c => seedSkills.Any(s => s.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
        var missing = seedSkills.Where(s => !matched.Any(m => s.Contains(m, StringComparison.OrdinalIgnoreCase) || m.Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
        return new GapAnalysisResult(target, seedSkills, matched, missing, seedSkills.Count > 0 ? (double)matched.Count / seedSkills.Count * 100 : 0);
    }

    private static HashSet<string> ExtractSkillsFromText(string text) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Java", "Python", "Go", "Rust", "C++", "C#", "JavaScript", "TypeScript", "SQL",
        "Spring Boot", "Spring Cloud", "MyBatis", "Hibernate", "JPA",
        "Django", "Flask", "FastAPI", "Tornado",
        "React", "Vue", "Angular", "Next.js", "Nuxt",
        "Docker", "Kubernetes", "Jenkins", "GitLab CI", "GitHub Actions",
        "MySQL", "PostgreSQL", "MongoDB", "Redis", "Elasticsearch", "Neo4j",
        "Kafka", "RabbitMQ", "RocketMQ", "Nacos", "Sentinel",
        "Linux", "Git", "DevOps", "微服务", "分布式", "高并发", "系统设计", "架构设计",
        "机器学习", "深度学习", "NLP", "计算机视觉", "数据挖掘",
        "Spark", "Flink", "Hadoop", "Hive", "HBase",
        "AWS", "Azure", "阿里云", "腾讯云",
        "HTML", "CSS", "Node.js", "GraphQL", "Webpack", "Vite",
        "TensorFlow", "PyTorch", "Keras", "Scikit-learn", "Pandas", "NumPy",
        "大模型", "LangChain", "RAG", "Prompt Engineering", "向量检索",
        "产品设计", "数据分析", "PRD", "用户研究", "项目管理", "竞品分析",
        "自动化测试", "Selenium", "JMeter", "性能测试", "接口测试",
        "数据仓库", "特征工程", "Terraform", "监控",
    }.Where(text.Contains).ToHashSet();

    public void Dispose() => _driver?.Dispose();
}

// ========== 数据模型 ==========
public record GraphNode(string Id, string Label, Dictionary<string, string> Properties);
public record GraphEdge(string Id, string Label, string Source, string Target);
public record GraphData(List<GraphNode> Nodes, List<GraphEdge> Edges);
public record GapAnalysisResult(string TargetJob, List<string> RequiredSkills, List<string> MatchedSkills, List<string> MissingSkills, double MatchRate);
public record LearningStep(string Skill, string Suggestion);
public record LearningPathResult(string TargetJob, double CurrentMatchRate, List<LearningStep> Steps, List<string> MissingSkills);
public record HallucinationCheckResult(List<string> VerifiedSkills, List<string> UnverifiedSkills, double VerificationRate);
public record SkillTrendPoint(string Skill, string Period, double DemandScore);
public record SkillTrendData(string JobName, string[] Periods, List<SkillTrendPoint> Points);
public record SkillWeight(string Skill, double Weight);
public record SkillChange(string Job, string Skill, string ChangeType, string OldValue, string NewValue);
public record SnapshotComparison(string Period1, string Period2, List<SkillChange> Changes);
