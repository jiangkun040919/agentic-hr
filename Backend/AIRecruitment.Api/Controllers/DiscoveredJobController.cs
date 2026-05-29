using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;
using System.Text.Json;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/discovered-jobs")]
[Authorize(Roles = "hr,admin")]
public class DiscoveredJobController : ControllerBase
{
    private readonly AppDbContext _context;

    public DiscoveredJobController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DiscoveredJobListParams p)
    {
        var query = _context.DiscoveredJobs.AsQueryable();
        if (!string.IsNullOrEmpty(p.Status))
            query = query.Where(d => d.Status == p.Status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .ToListAsync();

        return Ok(new { code = 200, data = new { items, total } });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var job = await _context.DiscoveredJobs.FindAsync(id);
        if (job == null) return NotFound(new { code = 404, message = "记录不存在" });

        job.Status = "approved";
        job.ReviewedAt = DateTime.Now;
        job.ReviewedBy = User.FindFirst("username")?.Value ?? "admin";

        // 自动推断部门分类
        var dept = InferDept(job.Title);
        var category = $"{dept}/{job.Title[..Math.Min(job.Title.Length, 4)]}";

        // 自动生成新模板草案（带正确分类）
        var template = new SeedTemplate
        {
            Name = job.Title,
            Category = category,
            Aliases = JsonSerializer.Serialize(new[] { job.Title }),
            SearchKeywords = JsonSerializer.Serialize(new[] { job.Title + " 招聘", job.Title + " 社招" }),
            MaxInstances = 5,
            CurrentInstances = 0,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        _context.SeedTemplates.Add(template);
        await _context.SaveChangesAsync(); // 先保存获取 templateId

        // 同时创建正式岗位入库
        var newJob = new Job
        {
            Title = job.Title,
            Dept = dept,
            Location = "待定",
            JD = job.RawDescription ?? "",
            Requirements = "",
            SalaryMin = 10,
            SalaryMax = 25,
            HeadCount = 1,
            Status = 1,
            HrId = 1,
            CreatedAt = DateTime.Now
        };
        _context.Jobs.Add(newJob);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            code = 200,
            message = $"已确认，归入「{dept}」并生成模板与岗位",
            data = new { templateId = template.TemplateId, jobId = newJob.JobId, dept }
        });
    }

    /// <summary>根据岗位标题推断部门</summary>
    private static string InferDept(string title)
    {
        var rules = new (string[] keywords, string dept)[]
        {
            (new[]{"AI","机器学习","NLP","算法","深度学习","大模型","人工智能","CV","自然语言","AIGC","LLM"}, "AI部"),
            (new[]{"数据","大数据","ETL","数据仓库","数据分析","数据科学","数仓","BI"}, "数据部"),
            (new[]{"产品","产品经理","PO","需求"}, "产品部"),
            (new[]{"前端","Vue","React","Angular","H5","小程序","Flutter","iOS","Android","移动端"}, "前端部"),
            (new[]{"Java","Python","Go","C++","C#","PHP","Rust","Node","后端","全栈","服务端"}, "技术部"),
            (new[]{"运维","SRE","DevOps","Linux","Docker","K8s","Kubernetes","CI/CD"}, "运维部"),
            (new[]{"测试","QA","质量","自动化测试"}, "测试部"),
            (new[]{"运营","市场","销售","商务","BD","客服"}, "运营部"),
            (new[]{"财务","会计","出纳","审计"}, "财务部"),
            (new[]{"人力","HR","招聘","人事","行政","培训"}, "人力资源部"),
            (new[]{"安全","渗透","等保","密码","攻防"}, "安全部"),
            (new[]{"设计","UI","UX","视觉","交互","平面"}, "设计部"),
            (new[]{"架构","架构师","系统设计","技术专家"}, "架构部"),
        };

        foreach (var (keywords, dept) in rules)
        {
            foreach (var kw in keywords)
            {
                if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return dept;
            }
        }
        return "技术部"; // 默认
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        var job = await _context.DiscoveredJobs.FindAsync(id);
        if (job == null) return NotFound(new { code = 404, message = "记录不存在" });

        job.Status = "rejected";
        job.ReviewedAt = DateTime.Now;
        job.ReviewedBy = User.FindFirst("username")?.Value ?? "admin";

        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "已驳回" });
    }
}
