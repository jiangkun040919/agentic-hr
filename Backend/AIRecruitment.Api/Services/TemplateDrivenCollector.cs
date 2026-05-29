using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using System.Text.Json;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 模板驱动采集引擎：按种子模板精准搜索、LLM提取、去重入库、模版匹配补全、新岗位检测
/// </summary>
public class TemplateDrivenCollector
{
    private readonly AppDbContext _ctx;
    private readonly TemplateGenerationService _gen;
    private readonly IAIService _ai;
    private readonly ICacheService _cache;
    private readonly ILogger<TemplateDrivenCollector> _logger;
    private readonly HttpClient _http;

    public TemplateDrivenCollector(AppDbContext ctx, TemplateGenerationService gen,
        IAIService ai, ICacheService cache, ILogger<TemplateDrivenCollector> logger,
        IHttpClientFactory httpFactory)
    {
        _ctx = ctx;
        _gen = gen;
        _ai = ai;
        _cache = cache;
        _logger = logger;
        _http = httpFactory.CreateClient();
    }

    public class CollectJobResult
    {
        public string TemplateName { get; set; } = "";
        public int Collected { get; set; }
        public int NewJobsDetected { get; set; }
    }

    public class RawJobInput
    {
        public string Title { get; set; } = "";
        public string? Jd { get; set; }
        public string? Requirements { get; set; }
        public string? Location { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SourcePlatform { get; set; }
    }

    /// <summary>按模板ID采集岗位 — 混合策略：先尝试联网爬取，再用LLM补齐</summary>
    public async Task<CollectJobResult> CollectByTemplateAsync(int templateId)
    {
        var tpl = await _ctx.SeedTemplates.FindAsync(templateId)
            ?? throw new Exception("模板不存在");

        if (tpl.CurrentInstances >= tpl.MaxInstances)
            throw new Exception($"已达上限 ({tpl.MaxInstances}条)");

        var result = new CollectJobResult { TemplateName = tpl.Name };
        int remaining = tpl.MaxInstances - tpl.CurrentInstances;
        var keywords = ParseJsonList(tpl.SearchKeywords);
        var excludeWords = ParseJsonList(tpl.ExcludeKeywords);

        // ═══ 第一步：尝试联网爬取真实岗位 ═══
        int webCollected = 0;
        try
        {
            var searchKw = keywords.FirstOrDefault()?.Replace("招聘", "").Replace("社招", "").Trim()
                           ?? tpl.Name;
            var webJobs = await TryWebCrawlAsync(searchKw, Math.Min(3, remaining));
            webCollected = webJobs.Count;
            _logger.LogInformation($"[混合采集] 爬取到 {webCollected} 条真实岗位");

            // 爬取到的岗位直接入库（简单数据，不需要 enrich）
            foreach (var wj in webJobs)
            {
                var exists = await _ctx.Jobs.AnyAsync(j => j.Title == wj.Title);
                if (exists) continue;
                if (excludeWords.Any(w => wj.Title?.Contains(w) == true)) continue;

                var job = new Job
                {
                    Title = wj.Title ?? searchKw + "工程师",
                    Dept = tpl.Category?.Split('/').FirstOrDefault()?.Trim() ?? "技术部",
                    Location = wj.Location ?? "北京",
                    JD = wj.Jd ?? "",
                    Requirements = string.Join("、", ParseJsonList(tpl.HardSkillsRequired).Take(4)),
                    SalaryMin = wj.SalaryMin > 0 ? wj.SalaryMin : 10,
                    SalaryMax = wj.SalaryMax > 0 ? wj.SalaryMax : 25,
                    HeadCount = 1,
                    Status = 1,
                    HrId = 1,
                    CreatedAt = DateTime.Now
                };
                _ctx.Jobs.Add(job);
                await _ctx.SaveChangesAsync();
                result.Collected++;
                remaining--;
                tpl.CurrentInstances++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[混合采集] 爬取失败，将全部使用LLM生成: {ex.Message}");
        }

        // ═══ 第二步：LLM 生成补齐剩余配额 ═══
        int toGenerate = Math.Min(remaining, 5);
        var requiredSkills = ParseJsonList(tpl.HardSkillsRequired);
        var preferredSkills = ParseJsonList(tpl.HardSkillsPreferred);
        var softSkills = ParseJsonList(tpl.SoftSkills);

        if (toGenerate <= 0)
        {
            // 爬取已满配额，直接返回
            tpl.UpdatedAt = DateTime.Now;
            await _ctx.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync("jobs:list:");
            result.NewJobsDetected = await _ctx.DiscoveredJobs.CountAsync(d => d.Status == "pending");
            return result;
        }

        var prompt = $@"你是一个专业的HR招聘专员。请根据以下岗位模板，生成 {toGenerate} 个真实感岗位招聘信息。

【岗位模板】
- 岗位名称：{tpl.Name}
- 类别：{tpl.Category}
- 核心职责：{tpl.Responsibilities ?? "负责相关领域的开发与维护工作"}
- 必备技能：{string.Join("、", requiredSkills)}
- 加分技能：{string.Join("、", preferredSkills)}
- 软技能：{string.Join("、", softSkills)}
- 学历：{tpl.EducationLevel ?? "本科及以上"}
- 专业：{tpl.EducationMajor ?? "计算机相关"}
- 经验要求-初级：{tpl.ExpJunior ?? "1-3年"}
- 经验要求-中级：{tpl.ExpMid ?? "3-5年"}
- 经验要求-高级：{tpl.ExpSenior ?? "5-10年"}
- 证书：{tpl.Certifications ?? "无特殊要求"}

请生成 {toGenerate} 个不同等级（初级/中级/高级混合）、不同城市的真实感岗位。
每个岗位返回JSON格式：
{{""title"":""岗位标题"",""location"":""城市"",""salaryMin"":15,""salaryMax"":30,""jd"":""岗位职责描述"",""requirements"":""任职要求"",""dept"":""部门""}}

要求：
1. 标题要真实多样，包含具体技术栈（如""高级Java开发工程师(微服务方向)""）
2. 薪资合理（初级8-18K，中级15-30K，高级25-50K）
3. JD和任职要求要详细、具体，不能泛泛而谈
4. 城市随机分布在北上广深杭成等一线城市
5. 整体风格贴近BOSS直聘/猎聘真实岗位

直接返回JSON数组，不要任何解释文字。";

        try
        {
            var aiResponse = await _ai.ChatAsync(prompt);
            _logger.LogInformation($"[LLM采集] 模板={tpl.Name}, AI返回长度={aiResponse?.Length ?? 0}");

            // 解析 JSON
            var jobs = ParseAIJobList(aiResponse);
            if (jobs.Count == 0)
            {
                _logger.LogWarning($"[LLM采集] AI返回无法解析: {aiResponse?[..Math.Min(200, aiResponse.Length)]}");
                throw new Exception("AI生成失败，未返回有效岗位数据");
            }

            var rng = new Random();
            int generated = 0;
            foreach (var rawJob in jobs.Take(toGenerate))
            {
                var title = rawJob.Title?.Trim() ?? "";
                if (string.IsNullOrEmpty(title)) continue;

                // 排除词过滤
                if (excludeWords.Any(w => title.Contains(w))) continue;

                // 去重
                var exists = await _ctx.Jobs.AnyAsync(j => j.Title == title);
                if (exists) continue;

                var job = new Job
                {
                    Title = title,
                    Dept = rawJob.Dept ?? tpl.Category?.Split('/').FirstOrDefault()?.Trim() ?? "未分类",
                    Location = rawJob.Location ?? "北京",
                    JD = rawJob.Jd ?? $"负责{tpl.Name}相关工作",
                    Requirements = rawJob.Requirements ?? string.Join("、", requiredSkills.Take(4)),
                    SalaryMin = rawJob.SalaryMin > 0 ? rawJob.SalaryMin : 10,
                    SalaryMax = rawJob.SalaryMax > 0 ? rawJob.SalaryMax : 25,
                    HeadCount = rng.Next(1, 4),
                    Status = 1,
                    HrId = 1,
                    CreatedAt = DateTime.Now
                };
                _ctx.Jobs.Add(job);
                await _ctx.SaveChangesAsync();

                result.Collected++;
                generated++;
                tpl.CurrentInstances++;

                // 新岗位检测
                await DetectNewJob(title, templateId);
            }

            if (generated == 0)
                throw new Exception("AI生成的岗位全部被去重过滤");

            tpl.UpdatedAt = DateTime.Now;
            await _ctx.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync("jobs:list:");

            result.NewJobsDetected = await _ctx.DiscoveredJobs.CountAsync(d => d.Status == "pending");
            _logger.LogInformation($"[LLM采集] 模板={tpl.Name}, 生成{generated}条");
            return result;
        }
        catch (Exception ex) when (ex.Message.Contains("AI生成失败") || ex.Message.Contains("去重过滤"))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[LLM采集] 模板={tpl.Name} 失败");
            throw new Exception($"LLM采集失败: {ex.Message}");
        }
    }

    /// <summary>解析 AI 返回的岗位 JSON 数组</summary>
    private List<RawJobResult> ParseAIJobList(string? aiResponse)
    {
        var results = new List<RawJobResult>();
        if (string.IsNullOrEmpty(aiResponse)) return results;

        try
        {
            // 尝试直接解析 JSON 数组
            var json = aiResponse.Trim();
            // 去掉可能的 markdown 代码块标记
            if (json.StartsWith("```"))
            {
                var end = json.IndexOf("\n");
                if (end > 0) json = json[(end + 1)..];
                if (json.EndsWith("```")) json = json[..^3];
                json = json.Trim();
            }

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = System.Text.Json.JsonSerializer.Deserialize<List<RawJobResult>>(json, options);
            if (list != null) results.AddRange(list);
        }
        catch
        {
            // 尝试逐个提取 JSON 对象
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    aiResponse, @"\{[^}]+\}", System.Text.RegularExpressions.RegexOptions.Singleline);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    try
                    {
                        var job = System.Text.Json.JsonSerializer.Deserialize<RawJobResult>(m.Value, options);
                        if (job != null && !string.IsNullOrEmpty(job.Title))
                            results.Add(job);
                    }
                    catch { }
                }
            }
            catch { }
        }

        return results;
    }

    public class RawJobResult
    {
        public string? Title { get; set; }
        public string? Location { get; set; }
        public int SalaryMin { get; set; }
        public int SalaryMax { get; set; }
        public string? Jd { get; set; }
        public string? Requirements { get; set; }
        public string? Dept { get; set; }
    }

    /// <summary>
    /// 核心新方法：接收爬虫的残缺原始数据 → 匹配模版 → LLM补全 → 入库
    /// </summary>
    public async Task<CollectJobResult> CollectWithEnrichmentAsync(RawJobInput raw)
    {
        var result = new CollectJobResult();

        // 1. 匹配模版
        var (template, confidence) = await _gen.MatchTemplateAsync(raw.Title);

        if (template == null || confidence < 0.5f)
        {
            // 无匹配 → 标记为新岗位发现
            var exists = await _ctx.DiscoveredJobs.AnyAsync(d => d.Title == raw.Title);
            if (!exists)
            {
                _ctx.DiscoveredJobs.Add(new Models.DiscoveredJob
                {
                    Title = raw.Title,
                    RawDescription = raw.Jd,
                    SourcePlatform = raw.SourcePlatform ?? "采集系统",
                    SimilarityScore = confidence,
                    Status = "pending",
                    CreatedAt = DateTime.Now
                });
                await _ctx.SaveChangesAsync();
                result.NewJobsDetected = 1;
            }
            _logger.LogInformation($"[采集] 标题={raw.Title} 未匹配模版，已标记发现");
            return result;
        }

        // 2. 检查模版上限
        if (template.CurrentInstances >= template.MaxInstances)
        {
            _logger.LogInformation($"[采集] 模版={template.Name} 已达上限");
            return result;
        }

        // 3. 去重
        var dup = await _ctx.Jobs.AnyAsync(j => j.Title == raw.Title);
        if (dup)
        {
            _logger.LogInformation($"[采集] 标题={raw.Title} 已存在，跳过");
            return result;
        }

        // 4. LLM补全
        var enriched = await _gen.EnrichJobWithTemplateAsync(
            raw.Title, raw.Jd, raw.Requirements,
            raw.Location, raw.SalaryMin, raw.SalaryMax,
            template.TemplateId);

        // 5. 入库
        var job = new Job
        {
            Title = raw.Title,
            Dept = enriched.Dept ?? template.Category?.Split('/').FirstOrDefault() ?? "未分类",
            Location = raw.Location ?? "未知",
            JD = enriched.Jd ?? raw.Jd ?? "",
            Requirements = enriched.Requirements ?? raw.Requirements ?? "",
            SalaryMin = enriched.SalaryMin.HasValue ? (int)enriched.SalaryMin.Value : raw.SalaryMin.HasValue ? (int)raw.SalaryMin.Value : null,
            SalaryMax = enriched.SalaryMax.HasValue ? (int)enriched.SalaryMax.Value : raw.SalaryMax.HasValue ? (int)raw.SalaryMax.Value : null,
            Status = 1,
            HrId = 1,
            CreatedAt = DateTime.Now
        };

        _ctx.Jobs.Add(job);
        template.CurrentInstances++;
        template.UpdatedAt = DateTime.Now;
        await _ctx.SaveChangesAsync();

        await _cache.RemoveByPrefixAsync("jobs:list:");

        result.Collected = 1;
        result.TemplateName = template.Name;
        _logger.LogInformation($"[采集] 标题={raw.Title} → 模版={template.Name}({confidence:P0}) → 已补全入库");

        return result;
    }

    /// <summary>
    /// 批量采集：接受多条爬虫数据，逐一匹配补全入库
    /// </summary>
    public async Task<CollectJobResult> BatchCollectWithEnrichmentAsync(List<RawJobInput> rawJobs)
    {
        var batchResult = new CollectJobResult();
        foreach (var raw in rawJobs)
        {
            var r = await CollectWithEnrichmentAsync(raw);
            batchResult.Collected += r.Collected;
            batchResult.NewJobsDetected += r.NewJobsDetected;
        }
        batchResult.TemplateName = $"批量采集 {rawJobs.Count} 条";
        return batchResult;
    }

    private async Task DetectNewJob(string title, int sourceTemplateId)
    {
        if (await _ctx.DiscoveredJobs.AnyAsync(d => d.Title == title)) return;

        var allTemplates = await _ctx.SeedTemplates.Where(t => t.IsActive)
            .Select(t => new { t.TemplateId, t.Name, t.Aliases }).ToListAsync();

        float maxSim = 0;
        foreach (var t in allTemplates)
        {
            if (title.Contains(t.Name) || t.Name.Contains(title)) { maxSim = 0.9f; break; }
            foreach (var alias in ParseJsonList(t.Aliases))
                if (title.Contains(alias)) { maxSim = 0.7f; break; }
        }

        if (maxSim < 0.5f)
        {
            _ctx.DiscoveredJobs.Add(new Models.DiscoveredJob
            {
                Title = title,
                SourcePlatform = "采集系统",
                MatchedTemplateId = sourceTemplateId,
                SimilarityScore = maxSim,
                Status = "pending",
                CreatedAt = DateTime.Now
            });
            await _ctx.SaveChangesAsync();
        }
    }

    /// <summary>联网爬取：通过Bing搜索聚合招聘信息</summary>
    private async Task<List<RawJobResult>> TryWebCrawlAsync(string keyword, int max)
    {
        var results = new List<RawJobResult>();
        try
        {
            var query = Uri.EscapeDataString($"{keyword} 招聘 北京 上海 深圳");
            var url = $"https://www.bing.com/search?q={query}&count=20";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            
            var response = await _http.GetStringAsync(url);
            
            // 提取搜索结果
            var blockMatches = System.Text.RegularExpressions.Regex.Matches(
                response, @"<li class=""b_algo"".*?</li>", 
                System.Text.RegularExpressions.RegexOptions.Singleline);
            
            foreach (System.Text.RegularExpressions.Match block in blockMatches)
            {
                if (results.Count >= max) break;
                
                // 提取标题和链接
                var titleMatch = System.Text.RegularExpressions.Regex.Match(
                    block.Value, @"<a[^>]*href=""([^""]+)""[^>]*>(.*?)</a>",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!titleMatch.Success) continue;
                
                var title = System.Text.RegularExpressions.Regex.Replace(
                    titleMatch.Groups[2].Value, @"<[^>]+>", "").Trim();
                
                // 过滤：标题必须像招聘信息
                if (!title.Contains("招聘") && !title.Contains("工程师") && 
                    !title.Contains("开发") && !title.Contains("经理"))
                    continue;
                if (title.Length < 8 || title.Length > 100) continue;
                
                // 提取描述
                var descMatch = System.Text.RegularExpressions.Regex.Match(
                    block.Value, @"<p[^>]*>(.*?)</p>",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                var desc = descMatch.Success 
                    ? System.Text.RegularExpressions.Regex.Replace(descMatch.Groups[1].Value, @"<[^>]+>", " ").Trim()
                    : "";
                desc = System.Text.RegularExpressions.Regex.Replace(desc, @"\s+", " ");
                
                // 提取薪资
                int? salMin = null, salMax = null;
                var salMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+)[kK千]\s*[-~]\s*(\d+)[kK千]");
                if (salMatch.Success)
                {
                    salMin = int.Parse(salMatch.Groups[1].Value);
                    salMax = int.Parse(salMatch.Groups[2].Value);
                }
                
                // 提取城市
                var cityMatch = System.Text.RegularExpressions.Regex.Match(
                    desc, @"(北京|上海|广州|深圳|杭州|成都|武汉|南京)");
                var location = cityMatch.Success ? cityMatch.Groups[1].Value : "北京";
                
                results.Add(new RawJobResult
                {
                    Title = title,
                    Location = location,
                    SalaryMin = salMin ?? 0,
                    SalaryMax = salMax ?? 0,
                    Jd = desc.Length > 1000 ? desc[..1000] : desc,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WebCrawl] 搜索失败: {ex.Message}");
        }
        return results;
    }

    private List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new List<string>(); }
    }

    private int? ParseSalary(string s, bool isMin)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var parts = s.Replace("K","000").Replace("k","000").Split('-');
        if (parts.Length == 2 && int.TryParse(parts[isMin ? 0 : 1].Trim(), out int v))
            return v;
        return null;
    }
}
