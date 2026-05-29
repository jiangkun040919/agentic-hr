using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// 技能知识库 API — Obsidian 风格，纯 SQL 驱动，不依赖 Neo4j
/// 实时从 Job.Requirements 提取技能，构建关联网络
/// </summary>
[ApiController]
[Route("api/kb")]
[Authorize(Roles = "hr,admin")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly AppDbContext _ctx;
    private readonly IAIService _ai;

    public KnowledgeBaseController(AppDbContext ctx, IAIService ai)
    {
        _ctx = ctx;
        _ai = ai;
    }

    // ══════════════════════════════════════════
    // 技能提取引擎
    // ══════════════════════════════════════════

    /// <summary>技能词表 — 分类 + 别名映射</summary>
    private static readonly Dictionary<string, string[]> SkillCategories = new()
    {
        ["后端"] = new[] { "Java", "Spring", "SpringBoot", "Spring Cloud", "MyBatis", "Hibernate", "JPA",
            "Go", "Gin", "Python", "Django", "Flask", "FastAPI", "Node.js", "Express", "NestJS",
            ".NET", "C#", "PHP", "Laravel", "Ruby", "Rails", "Rust", "C++",
            "微服务", "分布式", "高并发", "多线程", "消息队列", "RabbitMQ", "Kafka", "RocketMQ",
            "Redis", "Memcached", "Nginx", "Tomcat", "Netty", "gRPC", "Dubbo",
            "RESTful", "GraphQL", "WebSocket", "OAuth", "JWT", "SSO" },
        ["前端"] = new[] { "Vue", "Vue3", "React", "Angular", "TypeScript", "JavaScript", "ES6",
            "HTML5", "CSS3", "Sass", "Less", "Tailwind", "Webpack", "Vite", "Rollup",
            "小程序", "UniApp", "Taro", "Flutter", "React Native", "Electron",
            "Redux", "Vuex", "Pinia", "Next.js", "Nuxt", "Bootstrap", "Ant Design", "Element UI" },
        ["数据"] = new[] { "SQL", "MySQL", "PostgreSQL", "MongoDB", "Redis", "Elasticsearch",
            "Oracle", "SQLServer", "SQLite", "ClickHouse", "TiDB", "HBase",
            "Hadoop", "Spark", "Flink", "Hive", "Kafka", "Airflow", "ETL", "数据仓库",
            "数据湖", "数据挖掘", "数据分析", "Pandas", "NumPy", "Tableau", "PowerBI" },
        ["AI/ML"] = new[] { "Python", "机器学习", "深度学习", "PyTorch", "TensorFlow", "Keras",
            "NLP", "CV", "计算机视觉", "自然语言处理", "大模型", "LLM", "Transformer",
            "Scikit-learn", "XGBoost", "推荐系统", "强化学习", "AIGC", "RAG", "Agent" },
        ["DevOps"] = new[] { "Docker", "Kubernetes", "K8s", "Jenkins", "GitLab CI", "GitHub Actions",
            "Terraform", "Ansible", "Prometheus", "Grafana", "ELK", "CI/CD",
            "Linux", "Shell", "AWS", "阿里云", "腾讯云", "Azure", "DevOps", "SRE" },
        ["软技能"] = new[] { "沟通", "团队协作", "项目管理", "Scrum", "Agile", "领导力",
            "问题解决", "需求分析", "技术方案", "架构设计", "代码审查", "技术文档" },
    };

    // 别名映射（归一化）
    private static readonly Dictionary<string, string> SkillAliases = new()
    {
        ["spring boot"] = "Spring Boot", ["springboot"] = "Spring Boot",
        ["spring cloud"] = "Spring Cloud", ["springcloud"] = "Spring Cloud",
        ["nodejs"] = "Node.js", ["node"] = "Node.js",
        ["reactjs"] = "React", ["vuejs"] = "Vue", ["vue.js"] = "Vue",
        ["typescript"] = "TypeScript", ["ts"] = "TypeScript",
        ["javascript"] = "JavaScript", ["js"] = "JavaScript",
        ["k8s"] = "Kubernetes", ["kubernetes"] = "Kubernetes",
        ["机器学习"] = "机器学习", ["ml"] = "机器学习",
        ["深度学习"] = "深度学习", ["dl"] = "深度学习",
        ["大模型"] = "大模型", ["llm"] = "大模型",
        ["自然语言处理"] = "NLP", ["nlp"] = "NLP",
        ["计算机视觉"] = "CV", ["cv"] = "CV",
        ["docker"] = "Docker", ["git"] = "Git",
        ["mysql"] = "MySQL", ["postgresql"] = "PostgreSQL",
        ["mongodb"] = "MongoDB", ["redis"] = "Redis",
        ["elasticsearch"] = "Elasticsearch", ["es"] = "Elasticsearch",
        ["kafka"] = "Kafka", ["rabbitmq"] = "RabbitMQ",
    };

    /// <summary>从所有活跃岗位的 Requirements 字段提取技能列表</summary>
    private async Task<Dictionary<string, SkillInfo>> ExtractAllSkillsAsync()
    {
        var jobs = await _ctx.Jobs
            .Where(j => j.Status == 1 && j.Requirements != null)
            .Select(j => new { j.JobId, j.Title, j.Requirements, j.SalaryMin, j.SalaryMax, j.Location })
            .ToListAsync();

        var skillMap = new Dictionary<string, SkillInfo>(StringComparer.OrdinalIgnoreCase);
        var reverseIndex = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        // Step 1: 从词表匹配所有技能
        foreach (var job in jobs)
        {
            var text = job.Requirements ?? "";
            foreach (var (category, skills) in SkillCategories)
            {
                foreach (var skill in skills)
                {
                    if (!text.Contains(skill, StringComparison.OrdinalIgnoreCase)) continue;

                    var normalized = NormalizeSkill(skill);
                    if (!skillMap.ContainsKey(normalized))
                    {
                        skillMap[normalized] = new SkillInfo
                        {
                            Name = normalized,
                            Category = category,
                            JobIds = new List<int>(),
                            JobCount = 0,
                        };
                    }
                    skillMap[normalized].JobIds.Add(job.JobId);
                    skillMap[normalized].JobCount++;

                    if (!reverseIndex.ContainsKey(normalized))
                        reverseIndex[normalized] = new HashSet<int>();
                    reverseIndex[normalized].Add(job.JobId);
                }
            }
        }

        // Step 2: 计算共现关系（同一岗位中同时出现的技能）
        var cooccurrence = new Dictionary<string, Dictionary<string, int>>();
        foreach (var job in jobs)
        {
            var skillsInJob = reverseIndex
                .Where(kv => kv.Value.Contains(job.JobId))
                .Select(kv => kv.Key)
                .ToList();

            for (int i = 0; i < skillsInJob.Count; i++)
            {
                for (int j = i + 1; j < skillsInJob.Count; j++)
                {
                    var a = skillsInJob[i];
                    var b = skillsInJob[j];
                    if (!cooccurrence.ContainsKey(a)) cooccurrence[a] = new();
                    if (!cooccurrence[a].ContainsKey(b)) cooccurrence[a][b] = 0;
                    cooccurrence[a][b]++;
                    if (!cooccurrence.ContainsKey(b)) cooccurrence[b] = new();
                    if (!cooccurrence[b].ContainsKey(a)) cooccurrence[b][a] = 0;
                    cooccurrence[b][a]++;
                }
            }
        }

        // Step 3: 附加关联技能列表
        foreach (var (skill, info) in skillMap)
        {
            info.RelatedSkills = cooccurrence.GetValueOrDefault(skill)
                ?.OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => new RelatedSkill { Name = kv.Key, Weight = kv.Value })
                .ToList() ?? new();
        }

        // Step 4: 附加岗位快照
        foreach (var (skill, info) in skillMap)
        {
            info.JobSnapshots = jobs
                .Where(j => info.JobIds.Contains(j.JobId))
                .Select(j => new JobSnapshot
                {
                    JobId = j.JobId,
                    Title = j.Title ?? "",
                    Location = j.Location ?? "",
                    SalaryMin = j.SalaryMin,
                    SalaryMax = j.SalaryMax,
                })
                .ToList();
        }

        return skillMap;
    }

    private static string NormalizeSkill(string raw)
    {
        var lower = raw.Trim().ToLowerInvariant();
        return SkillAliases.GetValueOrDefault(lower, raw.Trim());
    }

    // ══════════════════════════════════════════
    // API 端点
    // ══════════════════════════════════════════

    /// <summary>获取所有技能列表</summary>
    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills([FromQuery] string? search, [FromQuery] string? category,
        [FromQuery] string sort = "count")
    {
        var all = await ExtractAllSkillsAsync();
        var query = all.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);

        query = sort switch
        {
            "name" => query.OrderBy(s => s.Name),
            _ => query.OrderByDescending(s => s.JobCount),
        };

        var result = query.Select(s => new
        {
            s.Name,
            s.Category,
            s.JobCount,
            relatedCount = s.RelatedSkills.Count,
        });

        return Ok(new { code = 200, data = result });
    }

    /// <summary>获取技能详情（含关联岗位、相关技能、图谱）</summary>
    [HttpGet("skills/detail")]
    public async Task<IActionResult> GetSkillDetail([FromQuery] string name)
    {
        var all = await ExtractAllSkillsAsync();
        var decoded = Uri.UnescapeDataString(name);

        if (!all.TryGetValue(decoded, out var skill) &&
            !all.TryGetValue(NormalizeSkill(decoded), out skill))
        {
            // 模糊匹配
            skill = all.Values
                .FirstOrDefault(s => s.Name.Contains(decoded, StringComparison.OrdinalIgnoreCase));
        }

        if (skill == null)
            return NotFound(new { code = 404, message = "技能不存在" });

        // 构建局部图谱
        var graphNodes = new List<object> { new { id = skill.Name, type = "skill", size = skill.JobCount } };
        var graphEdges = new List<object>();
        foreach (var rs in skill.RelatedSkills.Take(8))
        {
            graphNodes.Add(new { id = rs.Name, type = "skill", size = rs.Weight });
            graphEdges.Add(new { source = skill.Name, target = rs.Name, weight = rs.Weight });
        }

        // 热度趋势（按创建月份统计引用此技能的岗位数）
        var trend = await _ctx.Jobs
            .Where(j => j.Status == 1 && j.Requirements != null && j.Requirements.Contains(skill.Name))
            .GroupBy(j => new { j.CreatedAt.Year, j.CreatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new { month = $"{g.Key.Year}-{g.Key.Month:D2}", count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            code = 200,
            data = new
            {
                skill.Name,
                skill.Category,
                skill.JobCount,
                jobs = skill.JobSnapshots,
                relatedSkills = skill.RelatedSkills.Select(r => new { r.Name, r.Weight }),
                graph = new { nodes = graphNodes, edges = graphEdges },
                trend,
            }
        });
    }

    /// <summary>获取分类列表</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var all = await ExtractAllSkillsAsync();
        var categories = all.Values
            .GroupBy(s => s.Category)
            .Select(g => new { category = g.Key, count = g.Count(), skills = g.Select(s => s.Name) });
        return Ok(new { code = 200, data = categories });
    }

    /// <summary>保存技能笔记（Markdown 内容）</summary>
    [HttpPut("skills/content")]
    public async Task<IActionResult> SaveContent([FromQuery] string name, [FromBody] SaveContentRequest req)
    {
        // 存到 SysConfig 表（key = kb:content:{skillName}）
        var decoded = Uri.UnescapeDataString(name);
        var config = await _ctx.SysConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == $"kb:content:{decoded}");

        if (config == null)
        {
            config = new SysConfig
            {
                ConfigKey = $"kb:content:{decoded}",
                ConfigValue = req.Content ?? "",
                Description = $"技能知识库笔记: {decoded}",
            };
            _ctx.SysConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = req.Content ?? "";
        }
        await _ctx.SaveChangesAsync();
        return Ok(new { code = 200, message = "保存成功" });
    }

    /// <summary>获取技能笔记</summary>
    [HttpGet("skills/content")]
    public async Task<IActionResult> GetContent([FromQuery] string name)
    {
        var decoded = Uri.UnescapeDataString(name);
        var config = await _ctx.SysConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == $"kb:content:{decoded}");
        return Ok(new { code = 200, data = new { content = config?.ConfigValue ?? "" } });
    }
    /// <summary>AI 生成技能笔记</summary>
    [HttpPost("ai/generate")]
    public async Task<IActionResult> AiGenerate([FromBody] AiGenerateRequest req)
    {
        try
        {
            var result = await _ai.ChatAsync(req.Prompt ?? "");
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }
}

// ══════════════════════════════════════════
// 辅助类型
// ══════════════════════════════════════════

public class SkillInfo
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int JobCount { get; set; }
    public List<int> JobIds { get; set; } = new();
    public List<RelatedSkill> RelatedSkills { get; set; } = new();
    public List<JobSnapshot> JobSnapshots { get; set; } = new();
}

public class RelatedSkill
{
    public string Name { get; set; } = "";
    public int Weight { get; set; }
}

public class JobSnapshot
{
    public int JobId { get; set; }
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
}

public class SaveContentRequest
{
    public string? Content { get; set; }
}

public class AiGenerateRequest
{
    public string? Prompt { get; set; }
}
