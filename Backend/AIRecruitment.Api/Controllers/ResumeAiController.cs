using AIRecruitment.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/resume")]
public class ResumeAiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IResumeAiService _resumeAi;

    public ResumeAiController(AppDbContext context, IResumeAiService resumeAi)
    { _context = context; _resumeAi = resumeAi; }

    /// <summary>AI简历解析</summary>
    [HttpPost("ai-parse")]
    public async Task<IActionResult> ParseResume([FromBody] ParseRequest req)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Candidate)
                .Include(d => d.Job)
                .FirstOrDefaultAsync(d => d.DeliveryId == req.ResumeId);
            if (delivery == null) return NotFound(new { code = 404, message = "简历不存在" });

            var text = delivery.ResumeText
                ?? $"姓名：{delivery.ContactName}\n电话：{delivery.ContactPhone}\n学历：{delivery.ContactEducation}\n工作年限：{delivery.ContactWorkYears}年";
            var result = await _resumeAi.ParseResumeAsync(text);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>智能匹配评分</summary>
    [HttpPost("ai-match")]
    public async Task<IActionResult> ScoreMatch([FromBody] MatchRequest req)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Candidate)
                .Include(d => d.Job)
                .FirstOrDefaultAsync(d => d.DeliveryId == req.ResumeId);
            if (delivery?.Candidate == null) return NotFound(new { code = 404, message = "简历不存在" });

            var jd = req.JobId.HasValue
                ? (await _context.Jobs.FindAsync(req.JobId.Value))?.JD
                : delivery.Job?.JD;
            if (string.IsNullOrWhiteSpace(jd)) jd = delivery.Job?.Requirements ?? "";

            var result = await _resumeAi.ScoreMatchAsync(delivery.Candidate, jd!);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>AI面试建议</summary>
    [HttpPost("ai-interview-guide")]
    public async Task<IActionResult> GenerateInterviewGuide([FromBody] InterviewGuideRequest req)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Candidate)
                .Include(d => d.Job)
                .FirstOrDefaultAsync(d => d.DeliveryId == req.ResumeId);
            if (delivery?.Candidate == null) return NotFound(new { code = 404, message = "简历不存在" });

            var jd = req.JobId.HasValue
                ? (await _context.Jobs.FindAsync(req.JobId.Value))?.JD
                : delivery.Job?.JD;
            if (string.IsNullOrWhiteSpace(jd)) jd = delivery.Job?.Requirements ?? "";

            // 可选：先做匹配评分以提供更精准的面试方案
            MatchScoreResult? match = null;
            try { match = await _resumeAi.ScoreMatchAsync(delivery.Candidate, jd!); } catch { }

            var result = await _resumeAi.GenerateInterviewGuideAsync(delivery.Candidate, jd!, match);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}

public record ParseRequest(int ResumeId);
public record MatchRequest(int ResumeId, int? JobId);
public record InterviewGuideRequest(int ResumeId, int? JobId);
