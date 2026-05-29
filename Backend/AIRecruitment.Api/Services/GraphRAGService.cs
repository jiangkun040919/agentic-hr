using Neo4j.Driver;
using Newtonsoft.Json;

namespace AIRecruitment.Api.Services;

/// <summary>
/// Graph RAG（图谱检索增强生成）服务。
///
/// 将 Neo4j 知识图谱作为 RAG 的检索层：
///   1. 从图谱检索与查询相关的岗位-技能-能力子图
///   2. 将子图转为自然语言上下文
///   3. 注入 AI prompt 中，使 AI 回答基于图谱事实
///
/// 核心价值：AI 的推荐和解释都经过图谱验证（反幻觉），
/// 同时比纯图谱匹配更灵活（LLM 自然语言理解）。
/// </summary>
public class GraphRAGService
{
    private readonly KnowledgeGraphService _graph;
    private readonly IAIService _ai;
    private readonly ILogger<GraphRAGService> _logger;

    public GraphRAGService(
        KnowledgeGraphService graph,
        IAIService ai,
        ILogger<GraphRAGService> logger)
    {
        _graph = graph;
        _ai = ai;
        _logger = logger;
    }

    /// <summary>
    /// 基于图谱的智能岗位推荐。
    /// 1. 从图谱中找到与候选人技能匹配的岗位
    /// 2. 检索岗位的技能要求子图作为 RAG 上下文
    /// 3. AI 基于图谱事实生成推荐理由
    /// </summary>
    public async Task<GraphRAGRecommendation> RecommendJobsAsync(string candidateSkills, int topN = 5)
    {
        // Step 1: 从图谱检索相关岗位节点
        var graphData = await _graph.GetJobSkillGraphAsync();
        var jobs = graphData.Nodes
            .Where(n => n.Label == "Job")
            .Select(n => n.Properties.GetValueOrDefault("name", ""))
            .Distinct().ToList();

        // Step 2: 对每个岗位计算图谱匹配度
        var scoredJobs = new List<(string job, double score, GapAnalysisResult gap)>();
        foreach (var job in jobs.Take(30))
        {
            var gap = await _graph.GetSkillGapAsync(candidateSkills, job);
            scoredJobs.Add((job, gap.MatchRate, gap));
        }

        var topJobs = scoredJobs.OrderByDescending(x => x.score).Take(topN).ToList();

        // Step 3: 构建 RAG 上下文（图谱子图 → 自然语言）
        var ragContext = new System.Text.StringBuilder();
        ragContext.AppendLine("【图谱事实】以下信息来自知识图谱，确保推荐基于图谱数据：");
        foreach (var (job, score, gap) in topJobs)
        {
            ragContext.AppendLine($"\n岗位：{job}（图谱匹配度：{score:F1}%）");
            if (gap.RequiredSkills.Count > 0)
                ragContext.AppendLine($"  要求技能：{string.Join("、", gap.RequiredSkills.Take(8))}");
            if (gap.MatchedSkills.Count > 0)
                ragContext.AppendLine($"  候选人匹配：{string.Join("、", gap.MatchedSkills)}");
            if (gap.MissingSkills.Count > 0)
                ragContext.AppendLine($"  技能缺口：{string.Join("、", gap.MissingSkills.Take(5))}");
        }

        // Step 4: AI 基于图谱语境生成推荐
        var prompt = $@"{ragContext}

候选人的技能：{candidateSkills}

基于以上图谱数据，为候选人推荐最适合的岗位（不超过{topN}个），
对每个推荐说明理由，理由必须引用图谱中的匹配/缺口信息。
返回JSON：{{""recommendations"":[
  {{""jobTitle"":"""",""graphMatchScore"":0,""reason"":""基于图谱的理由"",""skillGaps"":[""需补充的技能""],""suggestedActions"":[""行动建议""]}}
]}}";

        try
        {
            var aiResponse = await _ai.ChatAsync(prompt);
            var result = JsonConvert.DeserializeObject<dynamic>(CleanJson(aiResponse));
            var recs = new List<GraphRAGRecommendationItem>();

            if (result?.recommendations != null)
            {
                foreach (var r in result.recommendations)
                {
                    recs.Add(new GraphRAGRecommendationItem
                    {
                        JobTitle = r.jobTitle?.ToString() ?? "",
                        GraphMatchScore = (double?)r.graphMatchScore ?? 0,
                        Reason = r.reason?.ToString() ?? "",
                        SkillGaps = DeserializeList(r.skillGaps),
                        SuggestedActions = DeserializeList(r.suggestedActions)
                    });
                }
            }

            return new GraphRAGRecommendation
            {
                CandidateSkills = candidateSkills,
                Recommendations = recs,
                GraphJobCount = jobs.Count,
                GeneratedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[GraphRAG] AI推荐生成失败，回退到纯图谱推荐: {msg}", ex.Message);
            return FallbackRecommendation(candidateSkills, topJobs);
        }
    }

    /// <summary>
    /// 基于图谱的技能差距分析 + AI 学习路径规划。
    /// 图谱提供结构化的缺失技能清单，
    /// AI 基于图谱数据生成个性化的学习路径。
    /// </summary>
    public async Task<GraphRAGLearningPath> GenerateLearningPathAsync(string candidateSkills, string targetJob)
    {
        // 图谱差距分析
        var gap = await _graph.GetSkillGapAsync(candidateSkills, targetJob);

        // 查找目标岗位相关的进阶能力
        var similarJobs = await _graph.FindSimilarJobsAsync(targetJob);

        // 构建 RAG 上下文
        var context = $@"【图谱差距分析】
目标岗位：{targetJob}
候选人技能：{candidateSkills}
当前匹配度：{gap.MatchRate:F1}%
已匹配技能：{string.Join("、", gap.MatchedSkills)}
缺失技能：{string.Join("、", gap.MissingSkills)}
相关岗位：{string.Join("、", similarJobs)}";

        var prompt = $@"{context}

基于以上图谱数据，为候选人规划从当前技能水平到{targetJob}岗位的学习路径。
路径分3个阶段（基础→进阶→精通），每个阶段不超过4个学习主题。
每个主题包含：学习资源类型（课程/书籍/项目/认证）、建议用时。
返回JSON：{{""learningPath"":[
  {{""phase"":""基础补齐"",""topics"":[{{""skill"":"""",""resourceType"":"""",""hours"":0,""prerequisites"":[]}}]}},
  {{""phase"":""能力进阶"",""topics"":[...]}},
  {{""phase"":""岗位精通"",""topics"":[...]}}
],""totalHours"":0,""milestones"":[""里程碑1"",""里程碑2""]}}";

        try
        {
            var aiResponse = await _ai.ChatAsync(prompt);
            return new GraphRAGLearningPath
            {
                TargetJob = targetJob,
                CurrentMatchRate = gap.MatchRate,
                MissingSkills = gap.MissingSkills,
                RawAIResponse = CleanJson(aiResponse),
                GeneratedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[GraphRAG] 学习路径生成失败: {msg}", ex.Message);
            return new GraphRAGLearningPath
            {
                TargetJob = targetJob,
                CurrentMatchRate = gap.MatchRate,
                MissingSkills = gap.MissingSkills,
                RawAIResponse = "{}",
                GeneratedAt = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 智能问答：用户自然语言提问 → 图谱检索 → AI 回答（带图谱引用）
    /// 例如："我们公司还缺什么技能的候选人？""Java岗最近需求变化是什么？"
    /// </summary>
    public async Task<string> AnswerQuestionAsync(string question)
    {
        var graphData = await _graph.GetJobSkillGraphAsync();

        // 构建图谱摘要作为上下文
        var ctx = new System.Text.StringBuilder();
        ctx.AppendLine("【知识图谱摘要】");
        foreach (var node in graphData.Nodes.Where(n => n.Label == "Job").Take(10))
            ctx.AppendLine($"- 岗位: {node.Properties.GetValueOrDefault("name", "")}");
        foreach (var node in graphData.Nodes.Where(n => n.Label == "Skill").Take(15))
            ctx.AppendLine($"- 技能: {node.Properties.GetValueOrDefault("name", "")}");

        var prompt = $@"{ctx}

用户问题：{question}

请基于以上图谱数据回答问题。如果图谱数据不足以回答，请诚实说明。
回答中引用具体岗位和技能名称。";

        return await _ai.ChatAsync(prompt);
    }

    // ═══ 辅助方法 ═══

    /// <summary>
    /// 社区检测：将技能/岗位按共现关系聚类，生成社区级摘要
    /// 对标微软 GraphRAG 的 Leiden 社区检测
    /// </summary>
    public async Task<GraphCommunityReport> DetectCommunitiesAsync()
    {
        var graphData = await _graph.GetJobSkillGraphAsync();
        var communities = new List<GraphCommunity>();

        // 从边中提取技能共现关系
        var skillNodes = graphData.Nodes
            .Where(n => n.Label == "Skill" || n.Properties.ContainsKey("skill"))
            .ToList();
        var jobNodes = graphData.Nodes
            .Where(n => n.Label == "Job" || n.Properties.ContainsKey("title"))
            .ToList();

        if (skillNodes.Count < 2)
        {
            return new GraphCommunityReport { Communities = communities, TotalNodes = graphData.Nodes.Count };
        }

        // 简单社区检测：按岗位关联聚类技能
        var jobSkillMap = new Dictionary<string, HashSet<string>>();
        foreach (var edge in graphData.Edges)
        {
            var sourceIsJob = jobNodes.Any(j => j.Id == edge.Source);
            var targetIsJob = jobNodes.Any(j => j.Id == edge.Target);
            if (sourceIsJob)
            {
                if (!jobSkillMap.ContainsKey(edge.Source))
                    jobSkillMap[edge.Source] = new HashSet<string>();
                jobSkillMap[edge.Source].Add(edge.Target);
            }
            else if (targetIsJob)
            {
                if (!jobSkillMap.ContainsKey(edge.Target))
                    jobSkillMap[edge.Target] = new HashSet<string>();
                jobSkillMap[edge.Target].Add(edge.Source);
            }
        }

        // 将技能按岗位关联合并为社区
        var assignedSkills = new HashSet<string>();
        var communityIdx = 0;
        var themeNames = new[] { "后端与微服务", "前端与全栈", "AI与数据科学", "云原生与DevOps", "安全与合规", "产品与设计" };

        foreach (var (jobId, skills) in jobSkillMap.Take(6))
        {
            var jobName = jobNodes.FirstOrDefault(j => j.Id == jobId)?.Properties.GetValueOrDefault("title", "未知岗位") ?? "未知";
            var communitySkills = skills.Where(s => !assignedSkills.Contains(s)).Take(12).ToList();
            foreach (var s in communitySkills) assignedSkills.Add(s);

            if (communitySkills.Count < 2) continue;

            var theme = communityIdx < themeNames.Length ? themeNames[communityIdx] : $"技能簇{communityIdx + 1}";

            // 用 AI 生成社区摘要
            string aiSummary = "";
            try
            {
                var summaryPrompt = $"以下是一个技能社区的主题和成员技能。请用1-2句话描述这个社区的技术方向和行业定位。\n主题：{theme}\n技能：{string.Join("、", communitySkills)}";
                aiSummary = await _ai.ChatAsync(summaryPrompt);
                if (aiSummary.Length > 200) aiSummary = aiSummary[..200];
            }
            catch { aiSummary = $"以{theme}为核心的技能社区，包含{communitySkills.Count}个相关技能"; }

            communities.Add(new GraphCommunity
            {
                Id = $"community_{communityIdx++}",
                Theme = theme,
                JobRepresentative = jobName,
                Skills = communitySkills.Select(s =>
                {
                    var node = skillNodes.FirstOrDefault(n => n.Id == s);
                    return node?.Properties.GetValueOrDefault("name", s) ?? s;
                }).ToList(),
                MemberCount = communitySkills.Count,
                AiSummary = aiSummary
            });
        }

        // 未归类的技能
        var unassigned = skillNodes
            .Select(n => n.Properties.GetValueOrDefault("name", n.Id))
            .Where(s => !assignedSkills.Contains(s))
            .ToList();
        if (unassigned.Count > 0)
        {
            communities.Add(new GraphCommunity
            {
                Id = "community_other",
                Theme = "其他新兴技能",
                JobRepresentative = "多领域",
                Skills = unassigned.Take(15).ToList(),
                MemberCount = unassigned.Count,
                AiSummary = "跨领域的通用或新兴技能，尚未形成明确社区归属"
            });
        }

        return new GraphCommunityReport
        {
            Communities = communities,
            TotalNodes = graphData.Nodes.Count,
            TotalEdges = graphData.Edges.Count,
            GeneratedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 全局洞察：基于社区摘要回答全局性问题
    /// 例如："我们公司的技术栈偏向哪个方向？""哪些技能在上升？"
    /// </summary>
    public async Task<string> GenerateGlobalInsightAsync(string question)
    {
        var communityReport = await DetectCommunitiesAsync();

        var context = new System.Text.StringBuilder();
        context.AppendLine("【知识图谱全局摘要】");
        context.AppendLine($"总计 {communityReport.TotalNodes} 个节点，{communityReport.TotalEdges} 条关系");
        context.AppendLine($"检测到 {communityReport.Communities.Count} 个技能社区：\n");

        foreach (var c in communityReport.Communities)
        {
            context.AppendLine($"## {c.Theme}（{c.MemberCount}个技能，代表岗位：{c.JobRepresentative}）");
            context.AppendLine($"摘要：{c.AiSummary}");
            context.AppendLine($"核心技能：{string.Join("、", c.Skills.Take(6))}");
            context.AppendLine();
        }

        var prompt = $@"{context}

用户问题：{question}

请基于以上知识图谱的全局社区摘要回答问题。要求：
1. 引用具体的社区和技能数据
2. 如果数据不足，诚实说明
3. 给出数据驱动的洞察，而非空泛意见";

        try
        {
            return await _ai.ChatAsync(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[GraphRAG] 全局洞察生成失败: {msg}", ex.Message);
            return $"基于知识图谱分析：检测到 {communityReport.Communities.Count} 个技能社区，涵盖 {communityReport.TotalNodes} 个节点。建议通过API获取详细社区报告。";
        }
    }

    // ═══ 辅助方法 ═══

    private static string CleanJson(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```json")) raw = raw[7..];
        else if (raw.StartsWith("```")) raw = raw[3..];
        if (raw.EndsWith("```")) raw = raw[..^3];
        return raw.Trim();
    }

    private static List<string> DeserializeList(dynamic? arr)
    {
        if (arr == null) return new List<string>();
        try { return JsonConvert.DeserializeObject<List<string>>(arr.ToString()) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private GraphRAGRecommendation FallbackRecommendation(string candidateSkills,
        List<(string job, double score, GapAnalysisResult gap)> topJobs)
    {
        var recs = topJobs.Select(j => new GraphRAGRecommendationItem
        {
            JobTitle = j.job,
            GraphMatchScore = j.score,
            Reason = $"基于知识图谱，候选人与{j.job}岗位的技能匹配度为{j.score:F1}%",
            SkillGaps = j.gap.MissingSkills,
            SuggestedActions = j.gap.MissingSkills.Take(3).Select(s => $"学习{s}").ToList()
        }).ToList();

        return new GraphRAGRecommendation
        {
            CandidateSkills = candidateSkills,
            Recommendations = recs,
            GraphJobCount = topJobs.Count,
            GeneratedAt = DateTime.Now
        };
    }
}

// ═══ DTOs ═══

public class GraphRAGRecommendation
{
    public string CandidateSkills { get; set; } = "";
    public List<GraphRAGRecommendationItem> Recommendations { get; set; } = new();
    public int GraphJobCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class GraphRAGRecommendationItem
{
    public string JobTitle { get; set; } = "";
    public double GraphMatchScore { get; set; }
    public string Reason { get; set; } = "";
    public List<string> SkillGaps { get; set; } = new();
    public List<string> SuggestedActions { get; set; } = new();
}

public class GraphRAGLearningPath
{
    public string TargetJob { get; set; } = "";
    public double CurrentMatchRate { get; set; }
    public List<string> MissingSkills { get; set; } = new();
    public string RawAIResponse { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; }
}

// ═══ GraphRAG 社区模型 ═══

public class GraphCommunityReport
{
    public List<GraphCommunity> Communities { get; set; } = new();
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class GraphCommunity
{
    public string Id { get; set; } = "";
    public string Theme { get; set; } = "";
    public string JobRepresentative { get; set; } = "";
    public List<string> Skills { get; set; } = new();
    public int MemberCount { get; set; }
    public string AiSummary { get; set; } = "";
}
