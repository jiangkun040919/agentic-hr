using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Services;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Options;
using AIRecruitment.Api.Controllers;
using Microsoft.Extensions.Options;

namespace AIRecruitment.Tests;

/// <summary>
/// 核心服务单元测试 — 覆盖匹配引擎、ML预测、多智能体、Graph RAG、知识图谱
/// </summary>
public class CoreServicesTests
{
    // ── 测试辅助 ──

    private static ILogger<T> CreateLogger<T>() => new Mock<ILogger<T>>().Object;

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<IAIService> CreateMockAI(string response = "{}")
    {
        var mock = new Mock<IAIService>();
        mock.Setup(x => x.ChatAsync(It.IsAny<string>())).ReturnsAsync(response);
        return mock;
    }

    // ═══════════════════════════════════════════════════════
    // P0: MLMatchingService 测试
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void MLMatchingService_TrainAndPredict_ReturnsValidProbability()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new MLMatchingService(config, CreateLogger<MLMatchingService>());

        // Act: 高匹配特征
        var highMatch = service.Predict(new MatchFeatures
        {
            SkillMatchCount = 5,
            RequiredSkillCount = 6,
            SkillMatchRatio = 0.83f,
            EduMatchScore = 85,
            ExpYears = 5,
            RequiredYears = 3,
            ExpRatio = 1.67f,
            ResumeLength = 2000,
            HasPhone = 1,
            HasEmail = 1
        });

        // Act: 低匹配特征
        var lowMatch = service.Predict(new MatchFeatures
        {
            SkillMatchCount = 2,
            RequiredSkillCount = 6,
            SkillMatchRatio = 0.33f,
            EduMatchScore = 50,
            ExpYears = 0,
            RequiredYears = 3,
            ExpRatio = 0f,
            ResumeLength = 100,
            HasPhone = 0,
            HasEmail = 0
        });

        // Assert
        Assert.InRange(highMatch.Probability, 0, 1);
        Assert.InRange(lowMatch.Probability, 0, 1);
        Assert.True(highMatch.Probability > lowMatch.Probability,
            $"高匹配概率({highMatch.Probability})应 > 低匹配概率({lowMatch.Probability})");

        Assert.NotNull(highMatch.Confidence);
        Assert.NotEmpty(highMatch.Features);
    }

    [Fact]
    public void MLMatchingService_IncrementalTraining_DoesNotThrow()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new MLMatchingService(config, CreateLogger<MLMatchingService>());

        var newSamples = new List<MatchFeatures>
        {
            new() { SkillMatchRatio = 0.9f, EduMatchScore = 90, ExpRatio = 1.5f, ResumeLength = 3000, HasPhone = 1, HasEmail = 1, Label = true },
            new() { SkillMatchRatio = 0.2f, EduMatchScore = 30, ExpRatio = 0.3f, ResumeLength = 50, HasPhone = 0, HasEmail = 0, Label = false }
        };

        // Should not throw
        service.UpdateModel(newSamples);

        // Predict should still work
        var pred = service.Predict(newSamples[0]);
        Assert.InRange(pred.Probability, 0, 1);
    }

    // ═══════════════════════════════════════════════════════
    // P1: EnhancedMatchingService 测试
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task EnhancedMatchingService_MatchAsync_ReturnsValidScore()
    {
        // Arrange
        var db = CreateDbContext();
        var job = new Job
        {
            Title = "Java开发工程师",
            Requirements = "3年以上Java开发经验，精通Spring Boot、MySQL",
            HrId = 1
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var mockAI = CreateMockAI("75");
        var config = new ConfigurationBuilder().Build();
        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());
        var ml = new MLMatchingService(config, CreateLogger<MLMatchingService>());

        var service = new EnhancedMatchingService(
            graph, mockAI.Object, db, CreateLogger<EnhancedMatchingService>(),
            ml, null, null);

        // Act
        var resume = "Java开发工程师，5年经验。精通Java、Spring Boot、MySQL、Redis。";
        var result = await service.MatchAsync(resume, job.JobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Java开发工程师", result.JobTitle);
        Assert.InRange(result.OverallScore, 0, 100);
        Assert.NotEmpty(result.Dimensions);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task EnhancedMatchingService_MatchAsync_MissingJob_Throws()
    {
        var db = CreateDbContext();
        var mockAI = CreateMockAI();
        var config = new ConfigurationBuilder().Build();
        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());

        var service = new EnhancedMatchingService(
            graph, mockAI.Object, db, CreateLogger<EnhancedMatchingService>());

        await Assert.ThrowsAsync<Exception>(() =>
            service.MatchAsync("任意简历", 999));
    }

    [Fact]
    public async Task EnhancedMatchingService_MatchV2Async_FusionWorks()
    {
        var db = CreateDbContext();
        var job = new Job
        {
            Title = "Python开发工程师",
            Requirements = "3年Python开发，熟悉Django/FastAPI",
            HrId = 1
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var mockAI = CreateMockAI("80");
        var config = new ConfigurationBuilder().Build();
        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());
        var ml = new MLMatchingService(config, CreateLogger<MLMatchingService>());

        var service = new EnhancedMatchingService(
            graph, mockAI.Object, db, CreateLogger<EnhancedMatchingService>(),
            ml, null, null);

        var resume = "Python开发工程师，4年经验。精通Django、FastAPI、PostgreSQL、Docker。";

        // Act
        var result = await service.MatchV2Async(resume, job.JobId);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.RuleScore, 0, 100);
        Assert.InRange(result.FusionScore, 0, 100);
        Assert.NotNull(result.FusionLevel);
        Assert.True(result.MLProbability.HasValue,
            "ML通道应该产生预测概率");
    }

    // ═══════════════════════════════════════════════════════
    // P2: KnowledgeGraphService 降级测试
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task KnowledgeGraphService_GetSkillGap_ReturnsSeedData_WhenNeo4jUnavailable()
    {
        // Arrange: 不配置 Neo4j 连接 = 自动降级
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Neo4j:Uri"] = "bolt://nonexistent:9999"
            })
            .Build();

        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());

        // Act
        var result = await graph.GetSkillGapAsync("Java,Spring,Docker", "Java开发工程师");

        // Assert: 降级到种子数据
        Assert.NotNull(result);
        Assert.NotEmpty(result.RequiredSkills);
        Assert.InRange(result.MatchRate, 0, 100);
    }

    [Fact]
    public async Task KnowledgeGraphService_VerifySkills_ReturnsComplete()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Neo4j:Uri"] = "bolt://nonexistent:9999"
            })
            .Build();

        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());

        var aiSkills = new List<string> { "Java", "Spring", "Kubernetes", "虚幻引擎" };
        var result = await graph.VerifySkillsAsync(aiSkills);

        // 降级模式下所有技能都被认为"已验证"（避免误报）
        Assert.Equal(100, result.VerificationRate);
        Assert.Equal(4, result.VerifiedSkills.Count);
    }

    // ═══════════════════════════════════════════════════════
    // P3: GraphRAGService 测试
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GraphRAGService_RecommendJobs_ReturnsResults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Neo4j:Uri"] = "bolt://nonexistent:9999"
            })
            .Build();

        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());
        var mockAI = CreateMockAI(@"{""recommendations"":[
            {""jobTitle"":""Java开发工程师"",""graphMatchScore"":85,""reason"":""图谱匹配度85%"",""skillGaps"":[""Kubernetes""],""suggestedActions"":[""学习K8s""]}
        ]}");

        var rag = new GraphRAGService(graph, mockAI.Object, CreateLogger<GraphRAGService>());

        var result = await rag.RecommendJobsAsync("Java,Spring,Docker", 3);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task GraphRAGService_LearningPath_ReturnsOutput()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Neo4j:Uri"] = "bolt://nonexistent:9999"
            })
            .Build();

        var graph = new KnowledgeGraphService(config, CreateLogger<KnowledgeGraphService>());
        var mockAI = CreateMockAI(@"{""learningPath"":[{""phase"":""基础"",""topics"":[]}]}");

        var rag = new GraphRAGService(graph, mockAI.Object, CreateLogger<GraphRAGService>());

        var result = await rag.GenerateLearningPathAsync("Java,Spring", "AI算法工程师");

        Assert.NotNull(result);
        Assert.NotEmpty(result.MissingSkills);
    }

    // ═══════════════════════════════════════════════════════
    // P4: BenchmarkDataGenerator 测试
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void BenchmarkDataGenerator_GenerateTestPairs_ReturnsCorrectCount()
    {
        var pairs = BenchmarkDataGenerator.GenerateTestPairs();

        Assert.NotEmpty(pairs);
        Assert.Contains(pairs, p => p.IsExpectedMatch);
        Assert.Contains(pairs, p => !p.IsExpectedMatch);

        // 每个测试对有标签
        foreach (var pair in pairs)
            Assert.False(string.IsNullOrEmpty(pair.Label));
    }
}
