using AIRecruitment.Api.Services;
using AIRecruitment.Api.Models;
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
            if (string.IsNullOrEmpty(result.Name) && result.Skills.Count == 0)
                result = BuildFallbackParse(delivery);
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
            if (result.Overall == 0 && result.Strengths.Count == 0)
                result = BuildFallbackMatch(delivery.Candidate);
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

            var result = await _resumeAi.GenerateInterviewGuideAsync(delivery.Candidate, jd!, null);
            
            // 如果AI返回空，用本地策略兜底
            if (result.Questions.Count == 0 && string.IsNullOrEmpty(result.Strategy))
                result = BuildFallbackGuide(delivery.Candidate, jd!);
                
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) 
        { 
            // 异常时也返回兜底数据
            var fb = BuildFallbackGuide(new Candidate { RealName = "", Education = "本科", WorkYears = 1 }, "");
            return Ok(new { code = 200, data = fb, note = $"AI暂不可用，使用推荐方案: {ex.Message}" }); 
        }
    }

    private static InterviewGuideResult BuildFallbackGuide(Candidate c, string jd)
    {
        return new InterviewGuideResult
        {
            Strategy = $"针对{c.Education ?? "该"}学历候选人，建议采用结构化面试，重点考察技术深度和项目经验。先技术面(30分钟)，再综合面(20分钟)。",
            FocusTags = new List<string> { "技术基础", "项目深度", "问题解决", "团队协作" },
            Warnings = new List<string> { "注意验证简历信息的真实性", "关注候选人对自己短板的认知" },
            Questions = new List<IQItem>
            {
                new() { Type = "tech", Category = "技术能力", Question = "请介绍你最熟悉的技术栈及其核心原理", Purpose = "验证技术深度", ExpectedAnswer = "能清晰表达技术选型理由和原理" },
                new() { Type = "experience", Category = "项目经验", Question = "介绍你最有挑战性的一个项目，你承担了什么角色？", Purpose = "评估项目复杂度匹配度", ExpectedAnswer = "能用STAR法则描述，有量化成果" },
                new() { Type = "star", Category = "行为面试", Question = "描述一次你与团队意见分歧的经历，你是如何处理的？", Purpose = "评估沟通协作能力", ExpectedAnswer = "展示同理心和解决问题导向" },
                new() { Type = "scenario", Category = "场景模拟", Question = "如果项目上线前发现重大Bug，但Deadline已到，你会怎么做？", Purpose = "评估抗压和决策能力", ExpectedAnswer = "有理有据地权衡风险与交付" },
                new() { Type = "tech", Category = "技术能力", Question = "你是如何保持技术学习的？最近在学什么新技术？", Purpose = "评估学习能力和技术热情", ExpectedAnswer = "有具体的学习计划和实践" },
                new() { Type = "star", Category = "行为面试", Question = "请分享一次你通过技术手段显著提升效率的经历", Purpose = "评估工程化思维", ExpectedAnswer = "有具体的优化数据和方案" },
                new() { Type = "tech", Category = "技术能力", Question = "写一段你擅长的代码来解决一个常见问题（白板编程）", Purpose = "验证编码能力", ExpectedAnswer = "代码规范，思路清晰" },
                new() { Type = "scenario", Category = "场景模拟", Question = "如果让你从头设计一个系统，你会考虑哪些因素？", Purpose = "评估系统设计能力", ExpectedAnswer = "需求→架构→技术选型→扩展性" },
            },
            SuggestedDuration = "45分钟",
            Evaluation = new EvalRubric { TechnicalWeight = 40, ExperienceWeight = 30, CommunicationWeight = 15, CultureFitWeight = 15 }
        };
    }

    private static ParseResult BuildFallbackParse(Delivery d)
    {
        return new ParseResult
        {
            Name = d.ContactName, Phone = d.ContactPhone, Email = d.ContactEmail ?? "",
            Education = new EducationInfo { Level = d.ContactEducation ?? "", Major = "", School = "" },
            WorkYears = d.ContactWorkYears ?? 0,
            Skills = new List<ResumeSkill> { new() { Name = "简历解析中", Level = "熟练", Confidence = "inferred" } },
            WorkExperience = new List<WorkExp>(),
            Projects = new List<ResumeProject>(),
            EducationHistory = new List<EduHistory>(),
            AnalysisMode = "本地解析(AI暂不可用)", AnalyzedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static MatchScoreResult BuildFallbackMatch(Candidate c)
    {
        var score = (c.WorkYears ?? 0) >= 5 ? 85 : (c.WorkYears ?? 0) >= 3 ? 72 : 58;
        return new MatchScoreResult
        {
            Overall = score, SkillMatch = score - 5, ExperienceMatch = score, EducationMatch = 70, FitScore = 65,
            Strengths = new List<string> { "基本信息完整", "有相关经验基础" },
            Gaps = new List<string> { "需简历全文获取精准评分" },
            Recommendation = "候选人基本满足岗位要求，建议安排面试进一步考察。",
            HiringSuggestion = score >= 70 ? "建议面试" : "建议复试",
            LevelEstimate = c.WorkYears >= 5 ? "高级" : c.WorkYears >= 3 ? "中级" : "初级",
            InterviewFocus = new List<string> { "技术基础", "项目经验", "学习能力" }
        };
    }
}

public record ParseRequest(int ResumeId);
public record MatchRequest(int ResumeId, int? JobId);
public record InterviewGuideRequest(int ResumeId, int? JobId);
