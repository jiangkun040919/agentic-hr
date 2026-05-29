using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 实时岗位数据采集服务 — 调用 Python 爬虫从真实招聘平台采集数据。
/// 通过 Hangfire 定时调度，实现岗位库的持续真实更新。
/// 
/// 数据源:
///   1. 51job（前程无忧） — DrissionPage + sensorsdata JSON 提取
///   2. 智联招聘           — DrissionPage + 文本规则解析
/// </summary>
public class RealtimeJobCollectorService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RealtimeJobCollectorService> _logger;

    private static readonly string PythonPath =
        @"C:\Users\Lenovo\AppData\Local\Python\pythoncore-3.14-64\python.exe";

    private static readonly string DispatcherPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
        "scripts", "run_scraper.py");

    // 默认采集城市（每次随机选2个）
    private static readonly string[] Cities = { "北京", "上海", "深圳", "杭州", "成都", "广州" };

    // 不同部门的岗位关键词（用于智能分配部门）
    private static readonly Dictionary<string, string[]> DeptKeywords = new()
    {
        ["技术部"] = new[] { "Java开发", "Python开发", "前端开发", ".NET开发", "Go开发", "C++开发" },
        ["数据部"] = new[] { "数据分析", "算法工程师", "AI工程师", "大数据开发" },
        ["质量部"] = new[] { "测试工程师" },
        ["运维部"] = new[] { "运维工程师" },
        ["产品部"] = new[] { "产品经理" },
        ["设计部"] = new[] { "UI设计" },
        ["安全部"] = new[] { "网络安全" },
        ["运营部"] = new[] { "运营专员" },
        ["市场部"] = new[] { "市场推广" },
        ["人力资源部"] = new[] { "HR", "招聘专员" },
        ["财务部"] = new[] { "财务", "会计" },
    };

    public RealtimeJobCollectorService(AppDbContext db, ILogger<RealtimeJobCollectorService> logger)
    {
        _db = db; _logger = logger;
    }

    /// <summary>
    /// Hangfire 定时任务入口。建议 Cron: "0 */4 * * *"（每4小时）
    /// 每次采集随机1-2个城市、2-4个部门、从2个平台各取1页
    /// </summary>
    public async Task<RealtimeCollectReport> CollectAsync()
    {
        var report = new RealtimeCollectReport { StartedAt = DateTime.Now };
        var rng = new Random();
        var allJobs = new List<ScrapedJob>();

        // 随机选城市和部门（轮转覆盖）
        var cities = Cities.OrderBy(_ => rng.Next()).Take(2).ToArray();
        var depts = DeptKeywords.Keys.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).ToArray();

        _logger.LogInformation("采集任务启动: 城市={cities}, 部门={depts}",
            string.Join(",", cities), string.Join(",", depts));

        foreach (var city in cities)
        {
            var keywords = depts
                .SelectMany(d => DeptKeywords.GetValueOrDefault(d, Array.Empty<string>()))
                .Distinct()
                .OrderBy(_ => rng.Next())
                .Take(rng.Next(3, 7))
                .ToArray();

            if (keywords.Length == 0) continue;

            // ── 数据源1: 51job ──
            try
            {
                var jobs51 = await RunScraperAsync("job51", keywords, city, 1);
                foreach (var j in jobs51) j.Source = "51job";
                allJobs.AddRange(jobs51);
                report.PlatformCount += jobs51.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("51job采集异常({city}): {msg}", city, ex.Message);
            }

            // ── 数据源2: 智联招聘 ──
            try
            {
                var jobsZl = await RunScraperAsync("zhaopin", keywords, city, 1);
                foreach (var j in jobsZl) j.Source = "智联招聘";
                allJobs.AddRange(jobsZl);
                report.AICount += jobsZl.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("智联采集异常({city}): {msg}", city, ex.Message);
            }
        }

        // ── 去重 & 入库 ──
        var existingTitles = await _db.Jobs
            .Where(j => j.Status == 1)
            .Select(j => j.Title.ToLower())
            .ToListAsync();

        foreach (var job in allJobs)
        {
            if (existingTitles.Contains(job.Title.ToLower())) continue;

            // 智能分配部门
            var dept = GuessDept(job.Title, job.Keyword);

            var entity = new Job
            {
                Title = job.Title,
                Dept = dept,
                Location = job.Location ?? "北京",
                JD = $"[真实采集:{job.Source} {DateTime.Now:yyyy-MM-dd}]\n" +
                     $"详情: https://jobs.51job.com/all/{job.CompanyId ?? ""}\n" +
                     $"职位: {job.Title}\n" +
                     $"公司: {job.Company}\n" +
                     (job.Experience != null ? $"经验: {job.Experience}\n" : "") +
                     (job.Education != null ? $"学历: {job.Education}\n" : ""),
                Requirements = job.Salary ?? "",
                SalaryMin = ParseSalaryMin(job.Salary),
                SalaryMax = ParseSalaryMax(job.Salary),
                HeadCount = rng.Next(1, 5),
                Status = 1,
                HrId = 2,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                ExpiredAt = DateTime.Now.AddMonths(3)
            };

            _db.Jobs.Add(entity);
            existingTitles.Add(job.Title.ToLower());
            report.Inserted++;
        }

        await _db.SaveChangesAsync();

        report.CompletedAt = DateTime.Now;
        _logger.LogInformation(
            "✅ 实时采集完成: +{new}条新岗位 (51job:{p51}, 智联:{pzl}), 城市={cities}, 数据库总计={total}",
            report.Inserted, report.PlatformCount, report.AICount,
            string.Join(",", cities), await _db.Jobs.CountAsync());

        return report;
    }

    /// <summary>调用 Python 调度脚本</summary>
    private async Task<List<ScrapedJob>> RunScraperAsync(
        string source, string[] keywords, string city, int pages)
    {
        var kw = string.Join(",", keywords);
        _logger.LogInformation("调用爬虫: {source} {city} kw={kw}", source, city, kw);
        
        var psi = new ProcessStartInfo
        {
            FileName = PythonPath,
            Arguments = $"\"{DispatcherPath}\" --source {source} --city {city} --pages {pages} --count {keywords.Length}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("无法启动 Python 进程");

        var rawOutput = await process.StandardOutput.ReadToEndAsync();
        var errOutput = await process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(cts.Token);

        _logger.LogInformation("Python退出码:{code}, stdout长度:{outLen}, stderr长度:{errLen}",
            process.ExitCode, rawOutput.Length, errOutput.Length);
        if (!string.IsNullOrWhiteSpace(errOutput))
            _logger.LogWarning("Python stderr: {err}", errOutput[..Math.Min(500, errOutput.Length)]);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Python 进程退出码 {process.ExitCode}: {errOutput}");
        }

        // 处理 Windows 编码问题：去除 BOM，提取 JSON
        var output = rawOutput.TrimStart('\uFEFF', ' ', '\r', '\n');
        // 找到第一个 '{' 和最后一个 '}'，确保只解析 JSON 部分
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end < 0)
        {
            _logger.LogError("Python输出非JSON, 前200字符: {out}", 
                rawOutput[..Math.Min(200, rawOutput.Length)]);
            throw new Exception($"Python输出非JSON: {rawOutput[..Math.Min(200, rawOutput.Length)]}...");
        }
        output = output[start..(end + 1)];
        _logger.LogInformation("JSON解析: total字段值, 输出长度={len}", output.Length);

        var result = JsonSerializer.Deserialize<ScraperResult>(output, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        _logger.LogInformation("反序列化结果: Success={s}, Total={t}, Jobs={j}", 
            result?.Success, result?.Total, result?.Jobs?.Count ?? 0);
        return result?.Jobs ?? new List<ScrapedJob>();
    }

    /// <summary>解析薪资下限</summary>
    private static int ParseSalaryMin(string? salary)
    {
        if (string.IsNullOrWhiteSpace(salary)) return 0;
        return ParseSalary(salary).min;
    }

    /// <summary>解析薪资上限</summary>
    private static int ParseSalaryMax(string? salary)
    {
        if (string.IsNullOrWhiteSpace(salary)) return 0;
        return ParseSalary(salary).max;
    }

    private static (int min, int max) ParseSalary(string s)
    {
        // "1.5-3万" → (15000, 30000), "8000-10000元" → (8000, 10000)
        s = s.Replace("以上", "").Replace("以下", "");
        int unit = 1;
        if (s.Contains("万")) { unit = 10000; s = s.Replace("万", ""); }
        else if (s.Contains("千")) { unit = 1000; s = s.Replace("千", ""); }
        s = s.Replace("元", "").Replace("/月", "").Replace("/天", "").Trim();

        var nums = System.Text.RegularExpressions.Regex.Matches(s, @"[\d.]+");
        if (nums.Count >= 2)
            return ((int)(float.Parse(nums[0].Value) * unit), (int)(float.Parse(nums[1].Value) * unit));
        if (nums.Count == 1)
        {
            var v = (int)(float.Parse(nums[0].Value) * unit);
            return (v, v);
        }
        return (0, 0);
    }

    private static string GuessDept(string title, string? keyword)
    {
        var t = title.ToLower();
        var k = keyword?.ToLower() ?? "";

        if (t.Contains("运营") || k.Contains("运营")) return "运营部";
        if (t.Contains("市场") || t.Contains("品牌") || t.Contains("推广") || k.Contains("市场")) return "市场部";
        if (t.Contains("hr") || t.Contains("人力") || t.Contains("招聘") || t.Contains("人事")) return "人力资源部";
        if (t.Contains("财务") || t.Contains("会计") || t.Contains("出纳") || t.Contains("审计")) return "财务部";
        if (t.Contains("测试")) return "质量部";
        if (t.Contains("产品")) return "产品部";
        if (t.Contains("设计") || t.Contains("ui")) return "设计部";
        if (t.Contains("运维")) return "运维部";
        if (t.Contains("数据") || t.Contains("算法") || t.Contains("ai") || t.Contains("机器学习")) return "数据部";
        if (t.Contains("安全")) return "安全部";
        return "技术部";
    }
}

// ── DTO ──

public class ScrapedJob
{
    public string Title { get; set; } = "";
    public string? Salary { get; set; }
    public string? Location { get; set; }
    public string? Company { get; set; }
    public string? Experience { get; set; }
    public string? Education { get; set; }
    public string? Keyword { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("company_id")]
    public string? CompanyId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Source { get; set; }
}

public class ScraperResult
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public List<string>? Errors { get; set; }
    public List<string>? KeywordsUsed { get; set; }
    public List<ScrapedJob>? Jobs { get; set; }
}

public class RealtimeCollectReport
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int PlatformCount { get; set; }   // 51job 本次采集数
    public int AICount { get; set; }         // 智联招聘 本次采集数
    public int Inserted { get; set; }        // 实际入库数
}
