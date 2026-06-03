using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Options;
using System.Text;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 模版生成与岗位补全服务 — LLM 驱动的种子模版批量生成 + 残缺岗位数据补全
/// </summary>
public class TemplateGenerationService
{
    private readonly AppDbContext _ctx;
    private readonly ILogger<TemplateGenerationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AIOptions _aiOptions;

    public TemplateGenerationService(
        AppDbContext ctx,
        IOptions<AIOptions> aiOptions,
        ILogger<TemplateGenerationService> logger)
    {
        _ctx = ctx;
        _logger = logger;
        _aiOptions = aiOptions.Value;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. 按部门批量生成种子模版
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 输入部门名 → LLM 返回该部门 5-8 个典型岗位的完整模版 → 入库
    /// </summary>
    public async Task<List<SeedTemplate>> GenerateTemplatesByDeptAsync(string department)
    {
        var systemPrompt = @"你是资深 HR 专家，精通各行业岗位画像。";
        var userPrompt = $@"为「{department}」部门列出 5-8 个典型招聘岗位，每个岗位提供完整的模版信息。

严格按照以下 JSON 格式返回（只返回 JSON 数组，不要 markdown 代码块）：

[
  {{
    ""name"": ""岗位名称"",
    ""category"": ""{department}/子类"",
    ""aliases"": [""别名1"", ""别名2"", ""别名3""],
    ""responsibilities"": [""职责1"", ""职责2"", ""职责3"", ""职责4"", ""职责5""],
    ""hardSkillsRequired"": [""必备技能1"", ""必备技能2"", ""必备技能3"", ""必备技能4"", ""必备技能5""],
    ""hardSkillsPreferred"": [""加分技能1"", ""加分技能2"", ""加分技能3""],
    ""softSkills"": [""软技能1"", ""软技能2"", ""软技能3""],
    ""educationLevel"": ""学历要求"",
    ""educationMajor"": ""专业要求"",
    ""expJunior"": ""初级年限"",
    ""expMid"": ""中级年限"",
    ""expSenior"": ""高级年限"",
    ""certifications"": [""证书1"", ""证书2""],
    ""searchKeywords"": [""搜索关键词1"", ""搜索关键词2"", ""搜索关键词3""]
  }}
]

要求：
- 每个岗位的名称必须是该部门真实存在的典型岗位
- 技能标签具体明确，不要泛泛而谈（如写""Spring Boot""而不是""框架""）
- 搜索关键词要包含""招聘""或""社招""以适配主流招聘平台
- excluseKeywords 统一为 [""实习"", ""兼职"", ""外包""]";

        var json = await CallMiniMaxAsync(systemPrompt, userPrompt);
        _logger.LogInformation($"[模板生成] LLM返回长度: {json?.Length ?? 0}");

        var templates = ParseTemplateList(json);
        if (templates.Count == 0)
            throw new Exception("LLM 未能生成有效模版，请重试");

        // 入库
        int saved = 0;
        foreach (var tpl in templates)
        {
            // 去重
            var exists = await _ctx.SeedTemplates.AnyAsync(t => t.Name == tpl.Name);
            if (exists) continue;

            tpl.Category = $"{department}/{tpl.Category.Split('/').LastOrDefault() ?? "未分类"}";
            tpl.ExcludeKeywords = JsonConvert.SerializeObject(new[] { "实习", "兼职", "外包" });
            tpl.SourcePlatforms = JsonConvert.SerializeObject(new[] { "BOSS直聘", "拉勾网", "猎聘" });
            tpl.MaxInstances = 5;
            tpl.CurrentInstances = 0;
            tpl.IsActive = true;
            tpl.CreatedAt = DateTime.UtcNow;

            _ctx.SeedTemplates.Add(tpl);
            saved++;
        }

        if (saved > 0)
        {
            await _ctx.SaveChangesAsync();
            _logger.LogInformation($"[模板生成] 部门={department}, 生成={templates.Count}, 入库={saved}");
        }

        return templates.Where(t => saved > 0).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. 用模版补全残缺岗位数据
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 爬虫抓到残缺岗位 → 匹配最佳模版 → LLM 补全缺失字段
    /// </summary>
    public async Task<JobEnrichResult> EnrichJobWithTemplateAsync(
        string jobTitle, string? rawJd, string? rawRequirements,
        string? location, decimal? salaryMin, decimal? salaryMax,
        int templateId)
    {
        var tpl = await _ctx.SeedTemplates.FindAsync(templateId)
            ?? throw new Exception("模版不存在");

        var systemPrompt = @"你是招聘数据处理专家。根据模版信息补全残缺的岗位数据。只补全确实缺失或为空的字段，已有数据保持不变。";
        var userPrompt = $@"根据以下模版参考，补全残缺的岗位数据。

=== 模版参考 ===
岗位名称: {tpl.Name}
类别: {tpl.Category}
职责参考: {tpl.Responsibilities ?? "无"}
必备技能: {tpl.HardSkillsRequired ?? "无"}
加分技能: {tpl.HardSkillsPreferred ?? "无"}
软技能: {tpl.SoftSkills ?? "无"}
学历要求: {tpl.EducationLevel ?? "无"}
专业要求: {tpl.EducationMajor ?? "无"}
证书要求: {tpl.Certifications ?? "无"}
经验层级: 初级{tpl.ExpJunior ?? "?"} / 中级{tpl.ExpMid ?? "?"} / 高级{tpl.ExpSenior ?? "?"}

=== 爬虫原始数据 ===
岗位名称: {jobTitle}
JD描述: {rawJd ?? "（缺失）"}
技能要求: {rawRequirements ?? "（缺失）"}
工作地点: {location ?? "（缺失）"}
薪资范围: {(salaryMin.HasValue ? $"{salaryMin}K" : "?")} - {(salaryMax.HasValue ? $"{salaryMax}K" : "?")}

请返回 JSON（不要 markdown）：
{{
  ""jd"": ""补全的岗位描述（100-200字，如果已有则返回原值）"",
  ""requirements"": ""补全的技能要求（包含必备+加分技能，如果已有则返回原值）"",
  ""dept"": ""部门名（从模版类别提取，如'技术部'从'技术研发/后端'提取'技术研发'）"",
  ""salaryMin"": {salaryMin?.ToString() ?? "null"},
  ""salaryMax"": {salaryMax?.ToString() ?? "null"}
}}";

        var json = await CallMiniMaxAsync(systemPrompt, userPrompt);
        _logger.LogInformation($"[岗位补全] 标题={jobTitle}, 模版={tpl.Name}");

        try
        {
            var enriched = JsonConvert.DeserializeObject<JobEnrichResult>(json);
            return enriched ?? new JobEnrichResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[岗位补全] JSON解析失败: {ex.Message}, 原始: {json[..Math.Min(json.Length, 200)]}");
            // 降级：用模版字段手动补全
            return FallbackEnrich(tpl, jobTitle, location, salaryMin, salaryMax);
        }
    }

    /// <summary>
    /// LLM匹配：在全部活跃模版中找到最佳匹配
    /// </summary>
    public async Task<(SeedTemplate? template, float confidence)> MatchTemplateAsync(string jobTitle)
    {
        var templates = await _ctx.SeedTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.TemplateId, t.Name, t.Category, t.Aliases })
            .ToListAsync();

        if (templates.Count == 0)
            return (null, 0);

        // 先用简单包含匹配提速
        var titleLower = jobTitle.ToLower();
        foreach (var t in templates)
        {
            if (titleLower.Contains(t.Name.ToLower()) || t.Name.ToLower().Contains(titleLower))
            {
                return (await _ctx.SeedTemplates.FindAsync(t.TemplateId), 0.9f);
            }
            var aliases = ParseJsonList(t.Aliases);
            foreach (var alias in aliases)
            {
                if (titleLower.Contains(alias.ToLower()))
                    return (await _ctx.SeedTemplates.FindAsync(t.TemplateId), 0.75f);
            }
        }

        // 归一化标题（去括号后缀、多余空格）后重试包含匹配
        var normalized = NormalizeTitle(jobTitle);
        if (normalized != titleLower)
        {
            foreach (var t in templates)
            {
                var tName = t.Name.ToLower();
                if (normalized.Contains(tName) || tName.Contains(normalized))
                {
                    return (await _ctx.SeedTemplates.FindAsync(t.TemplateId), 0.8f);
                }
                var aliases = ParseJsonList(t.Aliases);
                foreach (var alias in aliases)
                {
                    if (normalized.Contains(alias.ToLower()))
                        return (await _ctx.SeedTemplates.FindAsync(t.TemplateId), 0.7f);
                }
            }
        }

        // 字符级模糊匹配（处理 typo，如 pathon → python）
        var bestFuzzy = (template: (SeedTemplate?)null, confidence: 0f);
        foreach (var t in templates)
        {
            var sim = CharSimilarity(normalized, t.Name.ToLower());
            if (sim > bestFuzzy.confidence && sim >= 0.65f)
                bestFuzzy = (await _ctx.SeedTemplates.FindAsync(t.TemplateId), sim * 0.9f);
        }
        if (bestFuzzy.template != null)
            return bestFuzzy;

        // 没有精确匹配，调 LLM 做语义匹配
        try
        {
            var tplList = templates.Select(t => new { t.TemplateId, t.Name, t.Category }).ToList();
            var tplJson = JsonConvert.SerializeObject(tplList);

            var result = await CallMiniMaxAsync(
                "你是招聘岗位匹配专家。只返回 JSON，不要 markdown。",
                $"从以下模版列表中找到与「{jobTitle}」最匹配的模版。\n模版列表: {tplJson}\n返回: {{\"templateId\": 数字, \"confidence\": 0.0~1.0}} 如果不匹配任何模版返回: {{\"templateId\": 0, \"confidence\": 0}}");

            var match = JsonConvert.DeserializeObject<dynamic>(result);
            int matchedId = (int?)match?.templateId ?? 0;

            if (matchedId > 0)
            {
                var tpl = await _ctx.SeedTemplates.FindAsync(matchedId);
                float conf = (float?)match?.confidence ?? 0.5f;
                return (tpl, conf);
            }

            return (null, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[模版匹配] LLM匹配失败: {ex.Message}");
            return (null, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. LLM 提取 — 从模版生成模拟岗位数据
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<Job>> GenerateJobsFromTemplateAsync(int templateId)
    {
        var tpl = await _ctx.SeedTemplates.FindAsync(templateId)
            ?? throw new Exception("模版不存在");

        if (tpl.CurrentInstances >= tpl.MaxInstances)
            throw new Exception($"已达上限 ({tpl.MaxInstances}条)");

        int remaining = tpl.MaxInstances - tpl.CurrentInstances;
        int count = Math.Min(remaining, 3);

        var systemPrompt = @"你是招聘岗位生成专家。根据模版生成逼真的招聘岗位数据。只返回 JSON 数组，不要 markdown。";
        var userPrompt = $@"根据以下模版生成 {count} 条逼真的招聘岗位数据。

=== 模版 ===
岗位名: {tpl.Name}
类别: {tpl.Category}
职责: {tpl.Responsibilities ?? "无"}
必备技能: {tpl.HardSkillsRequired ?? "无"}
加分技能: {tpl.HardSkillsPreferred ?? "无"}
软技能: {tpl.SoftSkills ?? "无"}
学历: {tpl.EducationLevel ?? "无"}
专业: {tpl.EducationMajor ?? "无"}
经验: 初级{tpl.ExpJunior ?? "?"}/中级{tpl.ExpMid ?? "?"}/高级{tpl.ExpSenior ?? "?"}

返回 JSON 数组（{count} 条）：
[
  {{
    ""title"": ""具体的岗位名称（如'高级Java后端工程师'）"",
    ""dept"": ""部门（从类别提取）"",
    ""location"": ""城市（北京/上海/深圳/杭州/广州随机）"",
    ""jd"": ""100-200字岗位描述，包含职责和技术栈"",
    ""requirements"": ""100-150字技能要求，具体到框架/工具名"",
    ""salaryMin"": 月薪下限（K，整数，如15表示15K）,
    ""salaryMax"": 月薪上限（K，整数，如35表示35K）
  }}
]

要求：每条岗位名称不同、城市不同、薪资有梯度、描述逼真不重复。";

        var json = await CallMiniMaxAsync(systemPrompt, userPrompt);
        _logger.LogInformation($"[LLM提取] 模版={tpl.Name}, 返回长度={json?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(json))
            throw new Exception("AI 服务返回空内容，请稍后重试");

        List<GeneratedJob> generated;
        try
        {
            // 尝试多种清理方式
            var cleaned = CleanJsonResponse(json);
            generated = JsonConvert.DeserializeObject<List<GeneratedJob>>(cleaned) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[LLM提取] JSON解析失败，原始返回(前500字): {json?[..Math.Min(json.Length, 500)]}");
            throw new Exception($"AI 返回格式异常，请重试。如持续失败请联系管理员。");
        }

        if (generated.Count == 0)
            throw new Exception("AI 未生成有效岗位数据，请检查模版内容是否完整");

        var jobs = new List<Job>();
        foreach (var gj in generated.Take(remaining))
        {
            // 去重
            var exists = await _ctx.Jobs.AnyAsync(j => j.Title == gj.Title);
            if (exists) continue;

            var job = new Job
            {
                Title = gj.Title,
                Dept = gj.Dept ?? tpl.Category?.Split('/').FirstOrDefault() ?? "未分类",
                Location = gj.Location ?? "北京",
                JD = gj.Jd ?? "",
                Requirements = gj.Requirements ?? "",
                SalaryMin = gj.SalaryMin > 0 ? (int)gj.SalaryMin : null,
                SalaryMax = gj.SalaryMax > 0 ? (int)gj.SalaryMax : null,
                Status = 1,
                HrId = 1,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Jobs.Add(job);
            await _ctx.SaveChangesAsync();

            jobs.Add(job);
            tpl.CurrentInstances++;
        }

        tpl.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();

        return jobs;
    }

    // ═══════════════════════════════════════════════════════════════
    // 私有方法
    // ═══════════════════════════════════════════════════════════════

    private async Task<string> CallMiniMaxAsync(string systemPrompt, string userPrompt)
    {
        var endpoint = $"{_aiOptions.BaseUrl}/text/chatcompletion_v2";
        var requestBody = new
        {
            model = _aiOptions.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiOptions.ApiKey}");

        var response = await _httpClient.PostAsync(endpoint, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"MiniMax API调用失败: {response.StatusCode} - {responseContent}");
            throw new Exception($"AI服务调用失败: {response.StatusCode}");
        }

        var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
        var content = result?.choices?[0]?.message?.content?.ToString() ?? "";
        content = CleanJsonResponse(content);
        return content;
    }

    private static string CleanJsonResponse(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```json")) content = content[7..];
        else if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }

    private List<SeedTemplate> ParseTemplateList(string json)
    {
        try
        {
            var items = JsonConvert.DeserializeObject<List<dynamic>>(json);
            if (items == null) return new();

            return items.Select(item => new SeedTemplate
            {
                Name = (string?)item?.name ?? "",
                Category = (string?)item?.category ?? "",
                Aliases = JsonList(item?.aliases),
                Responsibilities = JsonList(item?.responsibilities),
                HardSkillsRequired = JsonList(item?.hardSkillsRequired),
                HardSkillsPreferred = JsonList(item?.hardSkillsPreferred),
                SoftSkills = JsonList(item?.softSkills),
                EducationLevel = (string?)item?.educationLevel,
                EducationMajor = (string?)item?.educationMajor,
                ExpJunior = (string?)item?.expJunior,
                ExpMid = (string?)item?.expMid,
                ExpSenior = (string?)item?.expSenior,
                Certifications = JsonList(item?.certifications),
                SearchKeywords = JsonList(item?.searchKeywords),
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[模板解析] 失败: {ex.Message}");
            return new();
        }
    }

    private static string? JsonList(dynamic? arr)
    {
        if (arr == null) return null;
        try { return JsonConvert.SerializeObject(JsonConvert.DeserializeObject<List<string>>(arr.ToString())); }
        catch { return null; }
    }

    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try { return JsonConvert.DeserializeObject<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static JobEnrichResult FallbackEnrich(SeedTemplate tpl, string title, string? location,
        decimal? salaryMin, decimal? salaryMax)
    {
        var respParts = ParseJsonList(tpl.Responsibilities);
        var reqSkills = ParseJsonList(tpl.HardSkillsRequired);
        var prefSkills = ParseJsonList(tpl.HardSkillsPreferred);
        var allSkills = reqSkills.Concat(prefSkills).Distinct().ToList();

        return new JobEnrichResult
        {
            Jd = respParts.Any()
                ? $"岗位职责：{string.Join("；", respParts.Take(4))}。"
                : $"{title}相关岗位。",
            Requirements = allSkills.Any()
                ? $"技能要求：{string.Join("、", allSkills.Take(6))}。学历要求：{tpl.EducationLevel ?? "本科及以上"}，专业：{tpl.EducationMajor ?? "相关专业"}。"
                : "详见JD。",
            Dept = tpl.Category?.Split('/').FirstOrDefault()?.Trim() ?? "未分类",
            SalaryMin = salaryMin,
            SalaryMax = salaryMax
        };
    }

    /// <summary>
    /// 归一化岗位标题：去除中文/英文括号内的后缀（如"（ML平台方向）"）、多余空格
    /// 例如: "高级Python开发工程师（ML平台方向）" → "高级python开发工程师"
    /// </summary>
    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        var result = System.Text.RegularExpressions.Regex.Replace(title, @"[（(][^）)]*[）)]", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", "");
        return result.ToLower().Trim();
    }

    /// <summary>
    /// 字符级相似度：Dice coefficient，容忍 typo
    /// 例如: "高级pathon开发工程师" vs "高级python开发工程师" → ~0.88
    /// </summary>
    private static float CharSimilarity(string a, string b)
    {
        if (a == b) return 1f;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
        var setA = new HashSet<string>();
        var setB = new HashSet<string>();
        for (int i = 0; i < a.Length - 1; i++) setA.Add(a.Substring(i, 2));
        for (int i = 0; i < b.Length - 1; i++) setB.Add(b.Substring(i, 2));
        var intersection = setA.Count(x => setB.Contains(x));
        var total = setA.Count + setB.Count;
        return total == 0 ? 0f : (float)(2.0 * intersection / total);
    }
}

// ═══ DTO ═══

public class JobEnrichResult
{
    public string Jd { get; set; } = "";
    public string Requirements { get; set; } = "";
    public string Dept { get; set; } = "未分类";
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
}

public class GeneratedJob
{
    public string Title { get; set; } = "";
    public string? Dept { get; set; }
    public string? Location { get; set; }
    public string? Jd { get; set; }
    public string? Requirements { get; set; }
    public int SalaryMin { get; set; }
    public int SalaryMax { get; set; }
}

public class LlmGenerateRequest
{
    public string Department { get; set; } = "";
}
