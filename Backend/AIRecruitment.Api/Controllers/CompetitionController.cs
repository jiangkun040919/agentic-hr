using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// 比赛专项 API：新岗位发现、能力更新、数据交叉验证、三率评测
/// </summary>
[ApiController]
[Route("api/competition")]
[Authorize]
public class CompetitionController : ControllerBase
{
    private readonly JobDiscoveryService _discovery;
    private readonly DataCrossValidationService _crossValidation;
    private readonly BenchmarkDataService _benchmark;
    private readonly EvolutionDemoService _evolution;

    public CompetitionController(
        JobDiscoveryService discovery,
        DataCrossValidationService crossValidation,
        BenchmarkDataService benchmark,
        EvolutionDemoService evolution)
    {
        _discovery = discovery;
        _crossValidation = crossValidation;
        _benchmark = benchmark;
        _evolution = evolution;
    }

    /// <summary>【核心功能1】新岗位发现与定义</summary>
    [HttpGet("discover/new-jobs")]
    public async Task<IActionResult> DiscoverNewJobs()
    {
        var result = await _discovery.DiscoverEmergingJobsAsync();
        return Ok(new
        {
            result.TotalDiscovered,
            result.EmergingSkills,
            result.DiscoveredJobs,
            note = "每个新岗位包含：岗位名称、核心职责、必备技能、加分技能、典型应用场景、市场需求预估"
        });
    }

    /// <summary>【核心功能2】既有岗位能力动态更新</summary>
    [HttpGet("evolution/analyze")]
    public async Task<IActionResult> AnalyzeJobEvolution([FromQuery] string jobTitle = "Java开发工程师")
    {
        var result = await _discovery.AnalyzeJobEvolutionAsync(jobTitle);
        return Ok(new
        {
            result.JobTitle,
            result.AddedSkills,
            result.RemovedSkills,
            result.UpgradedSkills,
            result.TrendSummary,
            note = "新增/删除/修改的能力项均有数据源标注"
        });
    }

    /// <summary>【创新功能1】多源数据交叉验证（时滞+噪音+抄袭检测）</summary>
    [HttpPost("cross-validation")]
    public async Task<IActionResult> CrossValidateJDs([FromBody] List<RawJD> rawJDs)
    {
        var result = await _crossValidation.CrossValidateAsync(rawJDs);
        return Ok(new
        {
            result.TotalAnalyzed,
            result.PlagiarizedCount,
            result.InflatedCount,
            result.OutdatedCount,
            result.QualityPassRate,
            result.Items
        });
    }

    /// <summary>【创新功能2】AI数据清洗与标准化</summary>
    [HttpPost("clean-jds")]
    public async Task<IActionResult> CleanJDs([FromBody] List<RawJD> rawJDs)
    {
        var result = await _crossValidation.CleanJDDataAsync(rawJDs);
        return Ok(new { cleanedCount = result.Count, items = result });
    }

    /// <summary>【评测功能】生成100条测试JD并入库</summary>
    [HttpPost("seed-test-jobs")]
    public async Task<IActionResult> SeedTestJobs()
    {
        var testJobs = await _benchmark.GenerateTestJobsAsync();
        return Ok(new { totalCount = testJobs.Count, categories = testJobs.GroupBy(j => j.Dept).ToDictionary(g => g.Key, g => g.Count()) });
    }

    /// <summary>【评测功能】运行三率准确率测试</summary>
    [HttpPost("accuracy-benchmark")]
    public async Task<IActionResult> RunAccuracyBenchmark()
    {
        var result = await _benchmark.RunAccuracyBenchmarkAsync();
        return Ok(new
        {
            jdParseAccuracy = $"{result.AvgJDParseAccuracy:F1}%",
            resumeExtractionAccuracy = $"{result.AvgResumeAccuracy:F1}%",
            matchingAccuracy = $"{result.AvgMatchingAccuracy:F1}%",
            passed = result.AvgJDParseAccuracy >= 90 && result.AvgResumeAccuracy >= 90 && result.AvgMatchingAccuracy >= 90,
            requirement = "竞赛要求：JD解析≥90%、简历提取≥90%、匹配准确率≥90%",
            result.JDParseResults,
            result.ResumeExtractionResults,
            result.MatchingResults
        });
    }

    /// <summary>【演示功能】生成演化演示数据</summary>
    [HttpPost("evolution/generate-demo")]
    public async Task<IActionResult> GenerateEvolutionDemo()
    {
        var result = await _evolution.GenerateDemoDataAsync();
        return Ok(new
        {
            message = "动态演化演示数据已生成",
            result.Periods,
            result.NewSkills,
            result.DecliningSkills,
            changeCount = result.Changes.Count
        });
    }
}
