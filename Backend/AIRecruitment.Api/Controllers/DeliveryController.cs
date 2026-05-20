using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly IAIService _aiService;

    public DeliveryController(IDeliveryService deliveryService, IAIService aiService)
    {
        _deliveryService = deliveryService;
        _aiService = aiService;
    }

    [HttpGet("list")]
    [Authorize(Roles = "admin,hr,candidate")]
    public async Task<IActionResult> GetDeliveryList([FromQuery] DeliveryListParams p)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        // 只对 hr 角色做精确过滤，admin 和 candidate 不传 HrId 看自己相关的
        // 开发阶段：不隔离数据，所有 HR/Admin 共享视图
        // 如需按 HR 隔离，取消下面注释：
        // if (userIdStr != null && role == "hr" && !p.HrId.HasValue)
        // {
        //     p = p with { HrId = int.Parse(userIdStr) };
        // }
        _ = role; _ = userIdStr; // suppress unused warnings
        var result = await _deliveryService.GetDeliveryListAsync(p);
        return Ok(new { code = 200, data = new { items = result.Items, total = result.Total } });
    }

    [HttpGet("my")]
    [Authorize(Roles = "candidate")]
    public async Task<IActionResult> GetMyDeliveries()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _deliveryService.GetMyDeliveriesAsync(userId);
        return Ok(new { code = 200, data = result });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetDeliveryDetail(int id)
    {
        var result = await _deliveryService.GetDeliveryDetailAsync(id);
        return Ok(new { code = 200, data = result });
    }

    [HttpPost]
    [Authorize(Roles = "candidate,admin,hr")]
    public async Task<IActionResult> SubmitDelivery([FromBody] DeliveryFormData data)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _deliveryService.SubmitDeliveryAsync(data, userId);
        return Ok(new { code = 200, message = "投递成功", data = result });
    }

    [HttpPut("{id}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateDeliveryStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        await _deliveryService.UpdateDeliveryStatusAsync(id, request.Status, request.Remark);
        return Ok(new { code = 200, message = "状态更新成功" });
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelDelivery(int id)
    {
        await _deliveryService.CancelDeliveryAsync(id);
        return Ok(new { code = 200, message = "取消成功" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "candidate")]
    public async Task<IActionResult> UpdateDeliveryInfo(int id, [FromBody] DeliveryFormData data)
    {
        var result = await _deliveryService.UpdateDeliveryInfoAsync(id, data);
        return Ok(new { code = 200, message = "信息更新成功", data = result });
    }

    /// <summary>HR：设置是否允许候选人进行AI面试</summary>
    [HttpPut("{id}/ai-interview-permission")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> SetAIInterviewPermission(int id, [FromBody] SetAIInterviewPermissionRequest request)
    {
        await _deliveryService.SetAIInterviewPermissionAsync(id, request.Allow, request.Deadline);
        return Ok(new { code = 200, message = request.Allow ? "已允许AI面试" : "已禁止AI面试" });
    }

    /// <summary>开始实习 — 将面试通过的候选人状态改为"实习中"</summary>
    [HttpPut("{id}/start-internship")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> StartInternship(int id, [FromBody] StartInternshipRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var operatedBy = userIdStr != null ? int.Parse(userIdStr) : 0;
            await _deliveryService.StartInternshipAsync(id, request, operatedBy);
            return Ok(new { code = 200, message = "已开始实习" });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>正式入职 — 将实习中的候选人状态改为"正式入职"</summary>
    [HttpPut("{id}/formal-hire")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> FormalHire(int id, [FromBody] FormalHireRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var operatedBy = userIdStr != null ? int.Parse(userIdStr) : 0;
            await _deliveryService.FormalHireAsync(id, request, operatedBy);
            return Ok(new { code = 200, message = "已正式入职" });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>批量 AI 打分并按匹配度排序</summary>
    [HttpPost("batch-score")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> BatchScore([FromBody] BatchScoreRequest request)
    {
        try
        {
            var results = new List<object>();
            foreach (var deliveryId in request.DeliveryIds)
            {
                try
                {
                    var score = await _aiService.ScoreResumeAsync(deliveryId);
                    results.Add(new { deliveryId, score = score.Score, reason = score.Reason });
                }
                catch
                {
                    results.Add(new { deliveryId, score = 0, reason = "评分失败" });
                }
            }
            var sorted = results.OrderByDescending(r => ((dynamic)r).score).ToList();
            return Ok(new { code = 200, data = sorted });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }

    /// <summary>批量操作：状态变更 / 面试邀请等</summary>
    [HttpPost("batch")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> BatchOperation([FromBody] BatchOperationRequest request)
    {
        try
        {
            foreach (var id in request.DeliveryIds)
            {
                await _deliveryService.UpdateDeliveryStatusAsync(id, request.Status, request.Remark);
            }
            return Ok(new { code = 200, message = $"已批量处理 {request.DeliveryIds.Length} 条记录" });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }

    /// <summary>上传简历文件（PDF/Word）并提取文本</summary>
    [HttpPost("{id}/upload-resume")]
    [Authorize]
    public async Task<IActionResult> UploadResume(int id, [FromBody] UploadResumeRequest request)
    {
        try
        {
            var fileName = request.FileName ?? "resume.pdf";
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".docx" && ext != ".doc")
                return Ok(new { code = 400, message = "仅支持 PDF (.pdf) 和 Word (.docx/.doc) 格式" });

            var pdfService = HttpContext.RequestServices.GetRequiredService<IPdfExtractService>();
            var text = await pdfService.ExtractBase64Async(request.FileBase64, fileName);
            if (!string.IsNullOrEmpty(text))
            {
                await _deliveryService.SaveResumeTextAsync(id, text);
                var fileType = ext == ".pdf" ? "PDF" : "Word";
                return Ok(new { code = 200, message = $"{fileType}解析成功，提取{text.Length}字", data = new { textLength = text.Length } });
            }
            return Ok(new { code = 400, message = "文件无法解析或内容为空" });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
    /// <summary>多候选人横向对比 — AI驱动的对比决策支持</summary>
    [HttpPost("compare")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> CompareCandidates([FromBody] CompareRequest request)
    {
        try
        {
            if (request.DeliveryIds == null || request.DeliveryIds.Length < 2 || request.DeliveryIds.Length > 4)
                return Ok(new { code = 400, message = "请选择2-4位候选人进行对比" });

            var candidates = new List<object>();
            foreach (var id in request.DeliveryIds)
            {
                try
                {
                    var score = await _aiService.ScoreResumeAsync(id);
                    var delivery = await _deliveryService.GetDeliveryDetailAsync(id);
                    var strengths = new List<string>();
                    var weaknesses = new List<string>();
                    if (score is AIScoreResultEx ex)
                    {
                        strengths = ex.Strengths ?? new List<string>();
                        weaknesses = ex.Weaknesses ?? new List<string>();
                    }
                    candidates.Add(new
                    {
                        deliveryId = id,
                        candidateName = delivery.CandidateName,
                        jobTitle = delivery.JobTitle,
                        overallScore = score.Score,
                        strengths,
                        weaknesses,
                        report = score.Report ?? "",
                        education = delivery.Education ?? "",
                        workYears = delivery.WorkYears ?? 0,
                    });
                }
                catch
                {
                    candidates.Add(new { deliveryId = id, candidateName = "未知", jobTitle = "-", overallScore = 0, strengths = new List<string>(), weaknesses = new List<string>(), report = "评分失败", education = "", workYears = 0 });
                }
            }

            var sorted = candidates.OrderByDescending(c => ((dynamic)c).overallScore).ToList();
            var top = sorted.First();
            var recommendation = new
            {
                topCandidateIndex = 0,
                reasoning = $"综合评分最高（{((dynamic)top).overallScore}分），建议优先考虑",
                riskFactors = ((dynamic)top).weaknesses as List<string> ?? new List<string>(),
                suggestedQuestions = new[] { "请描述您最有挑战性的项目经验", "您对未来3年的职业规划是什么？", "您如何处理团队中的技术分歧？" }
            };

            return Ok(new { code = 200, data = new { candidates = sorted, recommendation, comparedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }
}

public record SetAIInterviewPermissionRequest(bool Allow, DateTime? Deadline = null);
public record UploadResumeRequest(string FileBase64, string? FileName);

public record UpdateStatusRequest(int Status, string? Remark);

public record BatchScoreRequest(int[] DeliveryIds);

public record CompareRequest(int[] DeliveryIds);

public record BatchOperationRequest(int[] DeliveryIds, int Status, string? Remark);