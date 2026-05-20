using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InterviewController : ControllerBase
{
    private readonly IInterviewService _interviewService;

    public InterviewController(IInterviewService interviewService)
    {
        _interviewService = interviewService;
    }

    [HttpGet("list")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> GetInterviewList([FromQuery] InterviewListParams p)
    {
        var result = await _interviewService.GetInterviewListAsync(p);
        return Ok(new { code = 200, data = new { items = result.Items, total = result.Total } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInterviewDetail(int id)
    {
        var result = await _interviewService.GetInterviewDetailAsync(id);
        return Ok(new { code = 200, data = result });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ScheduleInterview([FromBody] InterviewFormData data)
    {
        try
        {
            Console.WriteLine($"ScheduleInterview - DeliveryId: {data.DeliveryId}, InterviewerId: {data.InterviewerId}, ScheduleTime: {data.ScheduleTime}, Location: {data.Location}");
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage));
                Console.WriteLine($"Model validation errors: {string.Join(", ", errors)}");
                return BadRequest(new { code = 400, message = "数据验证失败", errors = errors });
            }
            
            var result = await _interviewService.ScheduleInterviewAsync(data);
            return Ok(new { code = 200, message = "面试安排成功", data = result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ScheduleInterview Error: {ex.Message}");
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> UpdateInterview(int id, [FromBody] UpdateInterviewRequest data)
    {
        await _interviewService.UpdateInterviewAsync(id, data);
        return Ok(new { code = 200, message = "更新成功" });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> UpdateInterviewStatus(int id, [FromBody] int status)
    {
        await _interviewService.UpdateInterviewStatusAsync(id, status);
        return Ok(new { code = 200, message = "状态更新成功" });
    }

    [HttpPut("{id}/result")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordResult(int id, [FromBody] RecordResultRequest request)
    {
        Console.WriteLine($"RecordResult - InterviewId: {id}, Result: {request.Result}, Record: {request.Record}");
        
        await _interviewService.RecordResultAsync(id, request.Result, request.Record);
        return Ok(new { code = 200, message = "记录成功" });
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelInterview(int id)
    {
        await _interviewService.CancelInterviewAsync(id);
        return Ok(new { code = 200, message = "取消成功" });
    }

    [HttpPost("{id}/notify")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> SendNotification(int id, [FromBody] NotifyRequest request)
    {
        try
        {
            await _interviewService.SendNotificationAsync(id, request.Channels);
            return Ok(new { code = 200, message = "通知已发送" });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpGet("check-conflict")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> CheckConflict([FromQuery] int interviewerId, [FromQuery] string scheduleTime)
    {
        try
        {
            var hasConflict = await _interviewService.CheckConflictAsync(interviewerId, DateTime.Parse(scheduleTime));
            return Ok(new { code = 200, data = hasConflict });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }
}

public record RecordResultRequest(string Result, string Record);
public record NotifyRequest(string[] Channels);