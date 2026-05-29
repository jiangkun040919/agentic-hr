using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/seed-templates")]
[Authorize(Roles = "hr,admin")]
public class SeedTemplateController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TemplateDrivenCollector _collector;
    private readonly TemplateGenerationService _genService;
    private readonly ICacheService _cache;

    public SeedTemplateController(AppDbContext context, TemplateDrivenCollector collector,
        TemplateGenerationService genService, ICacheService cache)
    {
        _context = context;
        _collector = collector;
        _genService = genService;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] SeedTemplateListParams p)
    {
        var query = _context.SeedTemplates.AsQueryable();
        if (!string.IsNullOrEmpty(p.Keyword))
            query = query.Where(t => t.Name.Contains(p.Keyword));
        if (!string.IsNullOrEmpty(p.Category))
            query = query.Where(t => t.Category == p.Category);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Category).ThenBy(t => t.Name)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(t => new
            {
                t.TemplateId, t.Name, t.Category,
                t.MaxInstances, t.CurrentInstances,
                t.IsActive, t.CreatedAt, t.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { code = 200, data = new { items, total } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var t = await _context.SeedTemplates.FindAsync(id);
        if (t == null) return NotFound(new { code = 404, message = "模板不存在" });
        return Ok(new { code = 200, data = t });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SeedTemplateFormData data)
    {
        var exists = await _context.SeedTemplates.AnyAsync(t => t.Name == data.Name);
        if (exists) return BadRequest(new { code = 400, message = "模板名称已存在" });

        var template = new SeedTemplate
        {
            Name = data.Name,
            Category = data.Category,
            Aliases = data.Aliases,
            Responsibilities = data.Responsibilities,
            HardSkillsRequired = data.HardSkillsRequired,
            HardSkillsPreferred = data.HardSkillsPreferred,
            SoftSkills = data.SoftSkills,
            EducationLevel = data.EducationLevel,
            EducationMajor = data.EducationMajor,
            ExpJunior = data.ExpJunior,
            ExpMid = data.ExpMid,
            ExpSenior = data.ExpSenior,
            Certifications = data.Certifications,
            SearchKeywords = data.SearchKeywords,
            ExcludeKeywords = data.ExcludeKeywords,
            SourcePlatforms = data.SourcePlatforms,
            MaxInstances = data.MaxInstances,
            CreatedAt = DateTime.Now
        };

        _context.SeedTemplates.Add(template);
        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "创建成功", data = new { template.TemplateId } });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SeedTemplateFormData data)
    {
        var t = await _context.SeedTemplates.FindAsync(id);
        if (t == null) return NotFound(new { code = 404, message = "模板不存在" });

        t.Name = data.Name;
        t.Category = data.Category;
        t.Aliases = data.Aliases;
        t.Responsibilities = data.Responsibilities;
        t.HardSkillsRequired = data.HardSkillsRequired;
        t.HardSkillsPreferred = data.HardSkillsPreferred;
        t.SoftSkills = data.SoftSkills;
        t.EducationLevel = data.EducationLevel;
        t.EducationMajor = data.EducationMajor;
        t.ExpJunior = data.ExpJunior;
        t.ExpMid = data.ExpMid;
        t.ExpSenior = data.ExpSenior;
        t.Certifications = data.Certifications;
        t.SearchKeywords = data.SearchKeywords;
        t.ExcludeKeywords = data.ExcludeKeywords;
        t.SourcePlatforms = data.SourcePlatforms;
        t.MaxInstances = data.MaxInstances;
        t.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "更新成功" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _context.SeedTemplates.FindAsync(id);
        if (t == null) return NotFound(new { code = 404, message = "模板不存在" });
        _context.SeedTemplates.Remove(t);
        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "删除成功" });
    }

    /// <summary>手动触发采集 - 针对单个模板</summary>
    [HttpPost("{id}/collect")]
    public async Task<IActionResult> Collect(int id)
    {
        try
        {
            var result = await _collector.CollectByTemplateAsync(id);
            return Ok(new { code = 200, message = "采集完成", data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>LLM批量生成模版 - 按部门</summary>
    [HttpPost("llm-generate")]
    public async Task<IActionResult> LlmGenerate([FromBody] LlmGenerateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Department))
            return BadRequest(new { code = 400, message = "请输入部门名称" });

        try
        {
            var templates = await _genService.GenerateTemplatesByDeptAsync(req.Department);
            return Ok(new { code = 200, message = $"成功生成 {templates.Count} 个模版", data = new { templates, count = templates.Count } });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>LLM提取 - 根据模版生成模拟岗位</summary>
    [HttpPost("{id}/llm-extract")]
    public async Task<IActionResult> LlmExtract(int id)
    {
        try
        {
            var jobs = await _genService.GenerateJobsFromTemplateAsync(id);
            return Ok(new { code = 200, message = $"LLM提取完成：新增 {jobs.Count} 条岗位", data = new { collected = jobs.Count, jobs } });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>LLM匹配补全 - 用模版补全残缺岗位数据</summary>
    [HttpPost("enrich")]
    public async Task<IActionResult> EnrichJob([FromBody] EnrichJobRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { code = 400, message = "请输入岗位名称" });

        try
        {
            var (template, confidence) = await _genService.MatchTemplateAsync(req.Title);
            if (template == null)
                return Ok(new { code = 200, message = "未找到匹配模版，已标记为新岗位发现", data = new { matched = false, confidence } });

            var enriched = await _genService.EnrichJobWithTemplateAsync(
                req.Title, req.Jd, req.Requirements,
                req.Location, req.SalaryMin, req.SalaryMax,
                template.TemplateId);

            return Ok(new { code = 200, message = $"已匹配模版「{template.Name}」(置信度 {confidence:P0})并补全", 
                data = new { matched = true, templateName = template.Name, templateId = template.TemplateId, confidence, enriched } });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>清空所有岗位数据 - 用SET NOCHECK绕过外键</summary>
    [HttpPost("clear-all-jobs")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ClearAllJobs()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                -- 禁用所有外键约束
                ALTER TABLE Job NOCHECK CONSTRAINT ALL;
                ALTER TABLE Delivery NOCHECK CONSTRAINT ALL;
                ALTER TABLE Interview NOCHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIResumeAnalysis', 'U') IS NOT NULL ALTER TABLE AIResumeAnalysis NOCHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIInterviewSession', 'U') IS NOT NULL ALTER TABLE AIInterviewSession NOCHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIInterviewSessions', 'U') IS NOT NULL ALTER TABLE AIInterviewSessions NOCHECK CONSTRAINT ALL;

                -- 按依赖顺序删除（子表先删）
                IF OBJECT_ID('AIInterviewSessions', 'U') IS NOT NULL DELETE FROM AIInterviewSessions;
                IF OBJECT_ID('AIInterviewSession', 'U') IS NOT NULL DELETE FROM AIInterviewSession;
                IF OBJECT_ID('AIResumeAnalysis', 'U') IS NOT NULL DELETE FROM AIResumeAnalysis;
                DELETE FROM Interview;
                DELETE FROM Delivery;
                DELETE FROM Job;
                DELETE FROM DiscoveredJob;
                UPDATE SeedTemplate SET CurrentInstances = 0, UpdatedAt = GETDATE();

                -- 恢复外键约束
                ALTER TABLE Job CHECK CONSTRAINT ALL;
                ALTER TABLE Delivery CHECK CONSTRAINT ALL;
                ALTER TABLE Interview CHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIResumeAnalysis', 'U') IS NOT NULL ALTER TABLE AIResumeAnalysis CHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIInterviewSession', 'U') IS NOT NULL ALTER TABLE AIInterviewSession CHECK CONSTRAINT ALL;
                IF OBJECT_ID('AIInterviewSessions', 'U') IS NOT NULL ALTER TABLE AIInterviewSessions CHECK CONSTRAINT ALL;
            ");
            await _cache.RemoveByPrefixAsync("jobs:list:");
            return Ok(new { code = 200, message = "已清空所有岗位数据" });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.InnerException?.Message ?? ex.Message });
        }
    }
}
