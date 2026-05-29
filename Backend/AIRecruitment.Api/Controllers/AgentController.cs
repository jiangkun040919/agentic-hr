using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,hr")]
public class AgentController : ControllerBase
{
    private readonly RecruitmentAgentService _agent;

    public AgentController(RecruitmentAgentService agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Agentic AI 一句话招聘。
    /// 输入：自然语言招聘需求 → AI 解析意图 → 自动执行 → 返回报告
    /// </summary>
    [HttpPost("recruit")]
    public async Task<IActionResult> Recruit([FromBody] AgentRequest request)
    {
        try
        {
            var report = await _agent.ExecuteAsync(request.Query);
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }
}

public record AgentRequest(string Query);
