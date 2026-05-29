using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Services;
using Newtonsoft.Json;

namespace AIRecruitment.Api.Controllers;

/// <summary>图谱自演化 API — 从 GraphController 拆分</summary>
[ApiController]
[Route("api/graph")]
public class GraphEvolutionController : ControllerBase
{
    private readonly GraphEvolutionService _evolution;

    public GraphEvolutionController(GraphEvolutionService evolution)
    {
        _evolution = evolution;
    }

    /// <summary>执行一次演化周期</summary>
    [HttpPost("evolution/run")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> RunEvolution()
    {
        try
        {
            var report = await _evolution.RunEvolutionCycleAsync();
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>获取演化时间线</summary>
    [HttpGet("evolution/timeline")]
    public async Task<IActionResult> GetEvolutionTimeline()
    {
        try
        {
            var timeline = await _evolution.GetEvolutionTimelineAsync();
            return Ok(new { code = 200, data = timeline });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>检测技能漂移</summary>
    [HttpGet("evolution/drift")]
    public async Task<IActionResult> DetectDrift()
    {
        try
        {
            var currentSkills = await _evolution.ExtractSkillFrequenciesFromDBAsync();
            var n = DateTime.Now; var q = (n.Month - 1) / 3 + 1; q--; var y = n.Year;
            if (q < 1) { q = 4; y--; } var prevPeriod = $"{y}-Q{q}";
            var prevFreq = new Dictionary<string, int>();
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var snaps = await db.GraphSnapshots.Where(s => s.Period == prevPeriod).ToListAsync();
            foreach (var s in snaps)
            {
                try
                {
                    var skills = JsonConvert.DeserializeObject<List<SkillWeightItem>>(s.SkillsJson) ?? new();
                    foreach (var sk in skills)
                        prevFreq[sk.Skill] = prevFreq.GetValueOrDefault(sk.Skill) + (int)sk.Weight;
                }
                catch { }
            }
            var drift = _evolution.DetectSkillDrift(currentSkills, prevFreq, prevPeriod);
            return Ok(new { code = 200, data = drift });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>获取技能生命周期</summary>
    [HttpGet("evolution/skill-lifecycle")]
    public async Task<IActionResult> GetSkillLifecycle([FromQuery] string skill)
    {
        try
        {
            var lifecycle = await _evolution.GetSkillLifecycleAsync(skill);
            return Ok(new { code = 200, data = lifecycle });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}
