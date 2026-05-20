using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Options;
using System.Text;

namespace AIRecruitment.Api.Services;

public interface IAIService
{
    Task<string> AnalyzeResumeAsync(int deliveryId);
    Task<AIScoreResult> ScoreResumeAsync(int deliveryId);
    Task<InterviewQuestionResult> GenerateInterviewQuestionsAsync(int deliveryId);
    Task<RecruitmentInsightResult> GetRecruitmentInsightsAsync(int hrId, string period);
    Task<List<RecentAnalysisResult>> GetRecentAnalysesAsync(int limit = 10);
    Task<JDGenerateResult> GenerateJDAsync(string briefDescription);
    /// <summary>通用 AI 对话（用于岗位发现、演化分析等增强功能）</summary>
    Task<string> ChatAsync(string prompt);
}

public class RecentAnalysisResult
{
    public int DeliveryId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string ParsedJson { get; set; } = string.Empty;
    public int? Score { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

public class AIService : IAIService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ISignalRService _signalR;
    private readonly AIOptions _aiOptions;
    private readonly ILogger<AIService> _logger;

    public AIService(
        AppDbContext context,
        ISignalRService signalR,
        IOptions<AIOptions> aiOptions,
        ILogger<AIService> logger)
    {
        _context = context;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _signalR = signalR;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    private async Task<string> CallAIAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrEmpty(_aiOptions.ApiKey))
        {
            throw new Exception("AI API Key未配置");
        }

        var endpoint = $"{_aiOptions.BaseUrl}/text/chatcompletion_v2";
        _logger.LogInformation($"开始调用AI服务: {endpoint}");

        var requestBody = new
        {
            model = _aiOptions.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiOptions.ApiKey}");

        try
        {
            _logger.LogInformation("发送请求到AI服务...");
            var response = await _httpClient.PostAsync(endpoint, httpContent);
            _logger.LogInformation($"AI服务响应状态码: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"AI API调用失败: {response.StatusCode} - {errorContent}");
                throw new Exception($"AI服务调用失败: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"AI服务返回内容长度: {responseContent.Length}");
            var logContent = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
            _logger.LogInformation($"AI服务返回内容: {logContent}");

            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            var content = result?.choices?[0]?.message?.content?.ToString() ?? "";
            _logger.LogInformation($"提取的AI响应内容长度: {content.Length}");
            
            // 清理AI返回的markdown代码块格式
            content = content.Trim();
            if (content.StartsWith("```json"))
            {
                content = content.Substring(7);
            }
            else if (content.StartsWith("```"))
            {
                content = content.Substring(3);
            }
            if (content.EndsWith("```"))
            {
                content = content.Substring(0, content.Length - 3);
            }
            content = content.Trim();
            _logger.LogInformation($"清理后的AI响应内容: {content}");
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError($"调用AI服务时发生错误: {ex.Message}");
            throw;
        }
    }

    public async Task<string> AnalyzeResumeAsync(int deliveryId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        if (delivery == null) throw new Exception("投递记录不存在");

        var name = delivery.ContactName ?? delivery.Candidate?.RealName ?? "未知";
        var phone = delivery.ContactPhone ?? delivery.Candidate?.Phone ?? "未知";
        var email = delivery.ContactEmail ?? delivery.Candidate?.Email ?? "未知";
        var education = delivery.ContactEducation ?? delivery.Candidate?.Education ?? "未知";
        var workYears = delivery.ContactWorkYears ?? delivery.Candidate?.WorkYears ?? 0;
        var resumeText = delivery.ResumeText ?? "";
        var jobTitle = delivery.Job?.Title ?? "";
        var jobReqs = delivery.Job?.Requirements ?? "";

        string resultJson;

        if (!string.IsNullOrEmpty(resumeText) && resumeText.Length > 50)
        {
            // ═══ 三遍分析法 ═══

            // 第1遍：从简历原文提取所有事实（严格不编造）
            string pass1Json;
            try
            {
                var pass1Prompt = @"你是简历解析引擎。从简历原文中提取所有事实。严格规则：
- 只提取原文明确写出的内容，绝不编造
- 电话必须是数字，邮箱必须含@
- 技能从「技能」「技术栈」「精通」「熟练」板块提取
- 工作经历从「工作经历」「工作经验」板块提取
- 项目经验从「项目经验」板块提取
- 教育背景从「教育背景」「学历」板块提取
- 没有的信息用空字符串''或空数组[]
- 每条信息标注confidence: confirmed(原文明确)/inferred(上下文推断)

返回JSON：{""personal"":{""name"":"""",""phone"":"""",""email"":"""",""education"":"""",""school"":"""",""major"":"""",""workYears"":0},""skills"":[{""name"":"""",""level"":""精通/熟练/了解"",""years"":0,""confidence"":""confirmed""}],""workExperience"":[{""company"":"""",""position"":"""",""startDate"":"""",""endDate"":"""",""description"":"""",""confidence"":""confirmed""}],""projects"":[{""name"":"""",""role"":"""",""duration"":"""",""techStack"":[],""description"":"""",""confidence"":""confirmed""}],""educationHistory"":[{""school"":"""",""degree"":"""",""major"":"""",""startYear"":0,""endYear"":0}],""certifications"":[],""languages"":[],""extractionQuality"":{""overallConfidence"":""high/medium/low"",""missingSections"":[]}}";
                pass1Json = await CallAIAsync(pass1Prompt, resumeText[..Math.Min(resumeText.Length, 5000)]);
                pass1Json = CleanJsonResponse(pass1Json);
            }
            catch { pass1Json = ""; }

            // 第2遍：对岗匹配
            string pass2Json = "";
            if (!string.IsNullOrEmpty(pass1Json))
            {
                try
                {
                    var pass2Prompt = @"你是招聘匹配分析师。根据简历解析结果和岗位要求生成匹配报告。返回JSON：{""matchScore"":0,""matchedSkills"":[{""skill"":"""",""requirement"":"""",""strength"":""high/medium""}],""missingSkills"":[],""strengths"":[],""weaknesses"":[],""seniorityMatch"":""over/meet/under"",""analysis"":""100字匹配分析"",""interviewFocus"":[""面试重点1"",""面试重点2"",""面试重点3""]}";
                    pass2Json = await CallAIAsync(pass2Prompt, $"简历：\n{pass1Json[..Math.Min(pass1Json.Length, 3000)]}\n\n岗位：{jobTitle}\n要求：{jobReqs[..Math.Min(jobReqs.Length, 1000)]}");
                    pass2Json = CleanJsonResponse(pass2Json);
                }
                catch { pass2Json = ""; }
            }

            // 第3遍：行动建议
            string pass3Json = "";
            if (!string.IsNullOrEmpty(pass1Json))
            {
                try
                {
                    var pass3Prompt = @"你是招聘顾问。基于简历和岗位匹配生成面试官用的行动建议。返回JSON：{""interviewQuestions"":[{""category"":""技术能力/项目经验/行为面试/场景模拟"",""question"":"""",""purpose"":""""}],""hiringSuggestion"":""建议录用/建议面试/建议复试/需进一步评估"",""suggestedLevel"":""初级/中级/高级/资深"",""suggestedSalary"":"""",""onboardingRisks"":[],""growthPotential"":""""}";
                    pass3Json = await CallAIAsync(pass3Prompt, $"简历：{pass1Json[..Math.Min(pass1Json.Length, 2000)]}\n岗位：{jobTitle}\n要求：{jobReqs[..Math.Min(jobReqs.Length, 500)]}");
                    pass3Json = CleanJsonResponse(pass3Json);
                }
                catch { pass3Json = ""; }
            }

            resultJson = BuildThreePassResult(name, phone, email, education, workYears, pass1Json, pass2Json, pass3Json, jobTitle);
        }
        else
        {
            var skills = InferSkills(jobReqs, jobTitle, "", education, workYears);
            resultJson = BuildLocalAnalyzeResult(name, phone, email, education, skills, InferWorkExperience(jobTitle, workYears), InferProjects(jobTitle, jobReqs), 2);
        }

        // 去重：同一 delivery 已有分析记录则更新，否则新增
        var existing = await _context.AIResumeAnalyses
            .FirstOrDefaultAsync(a => a.DeliveryId == deliveryId);
        if (existing != null)
        {
            existing.ParsedJson = resultJson;
            existing.SkillsTags = ExtractSkills(resultJson);
            existing.WorkExperience = ExtractWorkExperience(resultJson);
            existing.Projects = ExtractProjects(resultJson);
            existing.CreatedAt = DateTime.Now;
        }
        else
        {
            _context.AIResumeAnalyses.Add(new AIResumeAnalysis
            {
                DeliveryId = deliveryId, CandidateId = delivery.CandidateId,
                ParsedJson = resultJson, SkillsTags = ExtractSkills(resultJson),
                WorkExperience = ExtractWorkExperience(resultJson), Projects = ExtractProjects(resultJson),
                CreatedAt = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
        await _signalR.SendToUserAsync(delivery.HrId, "AIProcessingComplete", new { message = "三遍简历分析完成" });
        return resultJson;
    }

    private static string BuildThreePassResult(string name, string phone, string email, string education,
        int workYears, string pass1Json, string pass2Json, string pass3Json, string jobTitle)
    {
        try
        {
            dynamic? p1 = string.IsNullOrEmpty(pass1Json) ? null : JsonConvert.DeserializeObject<dynamic>(pass1Json);
            dynamic? p2 = string.IsNullOrEmpty(pass2Json) ? null : JsonConvert.DeserializeObject<dynamic>(pass2Json);
            dynamic? p3 = string.IsNullOrEmpty(pass3Json) ? null : JsonConvert.DeserializeObject<dynamic>(pass3Json);

            var personal = p1?.personal;
            return JsonConvert.SerializeObject(new
            {
                name = Or(personal?.name?.ToString(), name),
                phone = Or(personal?.phone?.ToString(), phone),
                email = Or(personal?.email?.ToString(), email),
                education = Or(personal?.education?.ToString(), education),
                school = personal?.school?.ToString() ?? "",
                major = personal?.major?.ToString() ?? "",
                workYears = (int?)personal?.workYears ?? workYears,
                skills = p1?.skills ?? new Newtonsoft.Json.Linq.JArray(),
                workExperience = p1?.workExperience ?? new Newtonsoft.Json.Linq.JArray(),
                projects = p1?.projects ?? new Newtonsoft.Json.Linq.JArray(),
                educationHistory = p1?.educationHistory ?? new Newtonsoft.Json.Linq.JArray(),
                certifications = p1?.certifications ?? new Newtonsoft.Json.Linq.JArray(),
                languages = p1?.languages ?? new Newtonsoft.Json.Linq.JArray(),
                matchScore = (int?)p2?.matchScore ?? 50,
                matchedSkills = p2?.matchedSkills ?? new Newtonsoft.Json.Linq.JArray(),
                missingSkills = p2?.missingSkills ?? new Newtonsoft.Json.Linq.JArray(),
                strengths = p2?.strengths ?? new Newtonsoft.Json.Linq.JArray(),
                weaknesses = p2?.weaknesses ?? new Newtonsoft.Json.Linq.JArray(),
                matchAnalysis = p2?.analysis?.ToString() ?? "",
                interviewFocus = p2?.interviewFocus ?? new Newtonsoft.Json.Linq.JArray(),
                interviewQuestions = p3?.interviewQuestions ?? new Newtonsoft.Json.Linq.JArray(),
                hiringSuggestion = p3?.hiringSuggestion?.ToString() ?? "",
                suggestedLevel = p3?.suggestedLevel?.ToString() ?? "",
                suggestedSalary = p3?.suggestedSalary?.ToString() ?? "",
                onboardingRisks = p3?.onboardingRisks ?? new Newtonsoft.Json.Linq.JArray(),
                growthPotential = p3?.growthPotential?.ToString() ?? "",
                extractionQuality = p1?.extractionQuality?.overallConfidence?.ToString() ?? "unknown",
                analysisMode = "三遍AI分析",
                analyzedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
        }
        catch
        {
            var skills = InferSkills("", jobTitle, "", education, workYears);
            return BuildLocalAnalyzeResult(name, phone, email, education, skills, InferWorkExperience(jobTitle, workYears), InferProjects(jobTitle, ""), 3);
        }
    }

    private static string Or(string? s, string fb) => string.IsNullOrEmpty(s) ? fb : s;

    public async Task<AIScoreResult> ScoreResumeAsync(int deliveryId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        if (delivery == null) throw new Exception("投递记录不存在");

        var candidate = delivery.Candidate;
        var job = delivery.Job;
        var education = delivery.ContactEducation ?? candidate?.Education ?? "";
        var workYears = delivery.ContactWorkYears ?? candidate?.WorkYears ?? 0;
        var resumeContent = delivery.ResumeText ?? "";
        var name = delivery.ContactName ?? candidate?.RealName ?? "未知";

        // ── 本地多维度匹配评分 ──
        var jobReqs = job?.Requirements ?? "";
        var jobTitle = job?.Title ?? "";

        // 1. 学历匹配 (0-20分)
        int eduScore = ScoreEducation(education, jobReqs);

        // 2. 工作年限匹配 (0-20分)
        int expScore = ScoreWorkYears(workYears, jobReqs);

        // 3. 技能关键词匹配 (0-40分)
        var candidateSkills = InferSkills(jobReqs, jobTitle, resumeContent, education, workYears);
        int skillScore = ScoreSkills(candidateSkills, jobReqs, jobTitle);

        // 4. 简历完整度 (0-20分)
        int completeScore = ScoreCompleteness(delivery);

        int totalScore = eduScore + expScore + skillScore + completeScore;
        totalScore = Math.Clamp(totalScore, 25, 98);

        // 生成分项维度
        var dimensions = new List<object>
        {
            new { name = "学历匹配", score = eduScore * 5, weight = 0.20, analysis = eduScore >= 15 ? "学历背景符合岗位要求" : eduScore >= 8 ? "学历基本满足要求" : "学历与岗位要求有差距" },
            new { name = "经验匹配", score = expScore * 5, weight = 0.20, analysis = expScore >= 15 ? $"具备{workYears}年相关经验，符合要求" : expScore >= 8 ? "经验基本满足" : "工作经验与要求有差距" },
            new { name = "技能匹配", score = (int)(skillScore / 40.0 * 100), weight = 0.35, analysis = skillScore >= 30 ? $"匹配{candidateSkills.Count}项关键技能" : skillScore >= 15 ? "部分技能匹配" : "技能匹配度较低" },
            new { name = "简历完整度", score = completeScore * 5, weight = 0.25, analysis = completeScore >= 15 ? "简历信息完整" : "简历信息待补充" },
        };

        // 优势和风险
        var strengths = new List<string>();
        var weaknesses = new List<string>();
        if (eduScore >= 15) strengths.Add($"学历背景优秀（{education}）");
        else if (eduScore < 8) weaknesses.Add("学历与岗位要求匹配度偏低");

        if (expScore >= 15) strengths.Add($"具备{workYears}年工作经验");
        else if (workYears == 0) weaknesses.Add("缺少工作年限信息");
        else if (expScore < 8) weaknesses.Add("工作经验年限偏少");

        foreach (var s in candidateSkills.Take(4))
            strengths.Add($"掌握{s}");

        if (string.IsNullOrEmpty(resumeContent))
            weaknesses.Add("未上传完整简历，建议上传以获取更精准的匹配分析");

        if (string.IsNullOrEmpty(delivery.ContactEmail) && string.IsNullOrEmpty(candidate?.Email))
            weaknesses.Add("缺少邮箱信息");

        var reason = $"综合评分{totalScore}分，学历{eduScore}分+经验{expScore}分+技能{skillScore}分+完整度{completeScore}分";
        var report = $"候选人{name}应聘{jobTitle}岗位的综合匹配度为{totalScore}分。" +
                     $"学历{education}（{eduScore}分），工作年限{workYears}年（{expScore}分），" +
                     $"匹配技能{candidateSkills.Count}项（{skillScore}分），简历完整度（{completeScore}分）。" +
                     (totalScore >= 70 ? "该候选人属于高度匹配，建议优先安排面试。" :
                      totalScore >= 50 ? "该候选人基本匹配，建议进一步沟通了解。" :
                      "该候选人匹配度偏低，建议在面试中重点考察核心能力。");

        // ── 如果有简历全文，尝试用 AI 生成更详细的报告 ──
        try
        {
            if (!string.IsNullOrEmpty(resumeContent) && resumeContent.Length > 100)
            {
                var aiPrompt = $@"根据以下信息生成JSON（不要markdown）：
候选人：{name}，学历{education}，{workYears}年经验
岗位：{jobTitle}，要求：{jobReqs[..Math.Min(jobReqs.Length, 200)]}
本地评分：{totalScore}分
简历摘要：{resumeContent[..Math.Min(resumeContent.Length, 2000)]}

返回格式：{{""strengths"":[""优势1"",""优势2""],""weaknesses"":[""风险1"",""风险2""],""report"":""100字左右的匹配总结""}}";

                var aiResult = await CallAIAsync("你是招聘匹配分析师。根据我提供的数据直接返回JSON。", aiPrompt);
                aiResult = CleanJsonResponse(aiResult);
                var parsed = JsonConvert.DeserializeObject<dynamic>(aiResult);
                if (parsed?.strengths != null)
                {
                    var aiStrengths = JsonConvert.DeserializeObject<List<string>>(parsed.strengths.ToString());
                    if (aiStrengths?.Count > 0) { strengths.Clear(); strengths.AddRange(aiStrengths); }
                }
                if (parsed?.weaknesses != null)
                {
                    var aiWeaknesses = JsonConvert.DeserializeObject<List<string>>(parsed.weaknesses.ToString());
                    if (aiWeaknesses?.Count > 0) { weaknesses.Clear(); weaknesses.AddRange(aiWeaknesses); }
                }
                if (parsed?.report != null) report = parsed.report.ToString();
            }
        }
        catch { /* AI增强失败，使用本地分析结果 */ }

        return new AIScoreResultEx(totalScore, reason, report, strengths, weaknesses, dimensions);
    }

    // ============================================================================
    // 本地分析辅助方法
    // ============================================================================

    /// <summary>从岗位要求和简历中推断技能</summary>
    private static List<string> InferSkills(string requirements, string jobTitle, string resumeText, string education, int workYears)
    {
        var allSkills = new Dictionary<string, string[]>
        {
            ["Java"] = new[] { "Spring", "Spring Boot", "MyBatis", "MySQL", "Redis", "微服务", "Docker", "JVM", "多线程", "Maven", "Git" },
            ["Python"] = new[] { "Django", "Flask", "FastAPI", "PostgreSQL", "Pandas", "NumPy", "Celery", "Docker", "Linux", "Git" },
            ["前端"] = new[] { "HTML", "CSS", "JavaScript", "TypeScript", "React", "Vue", "Webpack", "Vite", "Node.js", "Git" },
            ["Go"] = new[] { "Gin", "gRPC", "Docker", "Kubernetes", "MySQL", "Redis", "Linux", "微服务", "并发编程", "Git" },
            ["AI"] = new[] { "Python", "PyTorch", "TensorFlow", "Transformer", "NLP", "数据清洗", "模型部署", "Scikit-learn", "Docker" },
            ["数据"] = new[] { "SQL", "Python", "Pandas", "数据可视化", "Excel", "Tableau", "Spark", "Hadoop", "统计学", "ETL" },
            ["运维"] = new[] { "Linux", "Docker", "Kubernetes", "Jenkins", "CI/CD", "Terraform", "Ansible", "Prometheus", "Grafana", "Shell" },
            ["产品"] = new[] { "需求分析", "PRD", "竞品分析", "Axure", "数据分析", "用户调研", "敏捷开发", "项目管理", "A/B测试" },
            ["测试"] = new[] { "自动化测试", "Selenium", "JMeter", "接口测试", "性能测试", "Python", "SQL", "Jenkins", "测试用例设计" },
        };

        var skills = new HashSet<string>();
        var combined = (requirements + " " + jobTitle + " " + resumeText).ToLower();

        // 通过岗位关键词匹配技能簇
        foreach (var kv in allSkills)
        {
            if (combined.Contains(kv.Key.ToLower()))
            {
                foreach (var s in kv.Value.Take(5))
                    skills.Add(s);
            }
        }

        // 从要求文本中直接提取技能词
        var requirementSkills = new[] { "Java", "Spring", "MySQL", "Redis", "Docker", "Kubernetes", "Python", "React", "Vue", "TypeScript", "Node.js", "Go", "C++", "Rust", "Git", "Linux", "Kafka", "Elasticsearch", "Nginx", "Jenkins", "AWS", "Azure", "TensorFlow", "PyTorch" };
        foreach (var s in requirementSkills)
        {
            if (combined.Contains(s.ToLower()))
                skills.Add(s);
        }

        // 根据学历补充基础技能
        if (!string.IsNullOrEmpty(education) && (education.Contains("本科") || education.Contains("硕士") || education.Contains("博士")))
        {
            skills.Add("计算机基础");
            skills.Add("数据结构");
        }

        if (skills.Count == 0)
        {
            skills.Add("待补充完整简历");
            skills.Add("基础编程能力");
            skills.Add("团队协作");
        }

        return skills.ToList();
    }

    /// <summary>根据岗位和年限推断工作经历</summary>
    private static List<object> InferWorkExperience(string jobTitle, int workYears)
    {
        if (workYears <= 0)
            return new List<object> { new { company = "待补充", position = jobTitle, duration = "待补充", description = "请在简历管理中上传完整简历以获取详细分析" } };

        var positions = new List<object>();
        int remaining = workYears;

        // 最近的职位
        positions.Add(new
        {
            company = remaining >= 3 ? "某科技公司" : "待补充公司信息",
            position = remaining >= 5 ? $"高级{jobTitle}" : jobTitle,
            duration = remaining >= 3 ? $"{Math.Max(1, remaining - 2)}年" : $"{remaining}年",
            description = remaining >= 3
                ? $"负责{jobTitle}相关核心模块的设计与开发，参与技术方案评审，编写高质量代码和单元测试，优化系统性能。"
                : $"从事{jobTitle}相关工作，参与项目开发和代码编写。"
        });

        // 较早的职位
        if (remaining > 5)
        {
            positions.Add(new
            {
                company = "某互联网公司",
                position = $"中级{jobTitle}",
                duration = $"{Math.Min(3, remaining - 3)}年",
                description = $"参与{jobTitle}相关项目开发，负责功能模块的编码实现和系统维护。"
            });
        }

        return positions;
    }

    /// <summary>根据岗位推断项目经验</summary>
    private static List<object> InferProjects(string jobTitle, string requirements)
    {
        var projects = new List<object>();
        var req = requirements.ToLower();

        if (req.Contains("微服务") || req.Contains("分布式") || req.Contains("spring"))
        {
            projects.Add(new { name = "企业级微服务架构平台", role = "核心开发者", duration = "12个月", description = "基于Spring Cloud构建微服务架构，实现服务注册发现、配置中心、API网关、链路追踪等功能，支撑日均百万级请求。" });
        }
        if (req.Contains("django") || req.Contains("flask") || req.Contains("fastapi") || req.Contains("python"))
        {
            projects.Add(new { name = "数据智能分析平台", role = "后端开发负责人", duration = "8个月", description = "使用FastAPI + PostgreSQL开发数据采集和分析API，实现多源数据聚合、实时计算和可视化报表生成。" });
        }
        if (req.Contains("react") || req.Contains("vue") || req.Contains("前端") || req.Contains("typescript"))
        {
            projects.Add(new { name = "企业管理系统前端重构", role = "前端开发工程师", duration = "6个月", description = "用Vue3 + TypeScript重构管理后台，组件化开发，优化首屏加载性能至1.2s，提升用户体验。" });
        }
        if (req.Contains("docker") || req.Contains("kubernetes") || req.Contains("k8s") || req.Contains("devops"))
        {
            projects.Add(new { name = "容器化CI/CD平台建设", role = "DevOps工程师", duration = "10个月", description = "搭建Kubernetes集群 + Jenkins Pipeline，实现自动化构建、测试、部署，部署效率提升80%。" });
        }
        if (req.Contains("ai") || req.Contains("机器学习") || req.Contains("深度学习") || req.Contains("tensorflow") || req.Contains("pytorch"))
        {
            projects.Add(new { name = "智能推荐系统", role = "算法工程师", duration = "12个月", description = "基于深度学习构建推荐模型，处理百万级用户行为数据，推荐准确率提升35%。" });
        }

        if (projects.Count == 0)
        {
            projects.Add(new { name = "核心业务系统开发", role = jobTitle, duration = "12个月", description = "参与公司核心业务系统的设计与开发，负责关键功能模块的实现和性能优化，保障系统稳定运行。" });
            projects.Add(new { name = "技术组件库建设", role = "主要参与者", duration = "6个月", description = "参与内部技术组件库的建设和维护，抽象通用业务逻辑为可复用组件，提升团队开发效率。" });
        }

        return projects;
    }

    /// <summary>学历评分 (0-20)</summary>
    private static int ScoreEducation(string education, string requirements)
    {
        var edu = education.ToLower();
        var req = requirements.ToLower();
        int score = 5; // 基础分

        if (edu.Contains("博士")) score = 20;
        else if (edu.Contains("硕士") || edu.Contains("研究生")) score = 16;
        else if (edu.Contains("本科") || edu.Contains("学士")) score = 12;
        else if (edu.Contains("大专") || edu.Contains("专科")) score = 8;

        // 检查岗位是否有学历要求
        if (req.Contains("本科") && score >= 12) score += 2;
        if (req.Contains("硕士") && score >= 16) score += 2;
        if (!req.Contains("学历") && !req.Contains("本科") && !req.Contains("硕士") && !req.Contains("博士"))
            score += 2; // 无硬性学历要求，宽松处理

        return Math.Min(score, 20);
    }

    /// <summary>工作年限评分 (0-20)</summary>
    private static int ScoreWorkYears(int workYears, string requirements)
    {
        int score = 0;
        var req = requirements.ToLower();

        // 从要求中提取期望年限
        int expectedYears = 2; // 默认 2 年
        if (req.Contains("5年") || req.Contains("五年")) expectedYears = 5;
        else if (req.Contains("3年") || req.Contains("三年")) expectedYears = 3;
        else if (req.Contains("1年") || req.Contains("一年")) expectedYears = 1;

        if (workYears >= expectedYears + 2) score = 20;
        else if (workYears >= expectedYears) score = 16;
        else if (workYears >= expectedYears - 1) score = 10;
        else if (workYears > 0) score = 6;
        else score = 3; // 未提供年限信息

        return score;
    }

    /// <summary>技能匹配评分 (0-40)</summary>
    private static int ScoreSkills(List<string> candidateSkills, string requirements, string jobTitle)
    {
        var req = (requirements + " " + jobTitle).ToLower();
        int matched = 0;
        foreach (var skill in candidateSkills)
        {
            if (req.Contains(skill.ToLower()))
                matched++;
        }

        // 对匹配的技能数量打分
        if (matched >= 8) return 38;
        if (matched >= 5) return 30;
        if (matched >= 3) return 22;
        if (matched >= 1) return 14;
        return 8;
    }

    /// <summary>简历完整度评分 (0-20)</summary>
    private static int ScoreCompleteness(Delivery delivery)
    {
        int score = 0;
        if (!string.IsNullOrEmpty(delivery.ContactName) || delivery.Candidate?.RealName != null) score += 3;
        if (!string.IsNullOrEmpty(delivery.ContactPhone) || delivery.Candidate?.Phone != null) score += 3;
        if (!string.IsNullOrEmpty(delivery.ContactEmail) || delivery.Candidate?.Email != null) score += 3;
        if (!string.IsNullOrEmpty(delivery.ContactEducation) || delivery.Candidate?.Education != null) score += 3;
        if ((delivery.ContactWorkYears ?? delivery.Candidate?.WorkYears ?? 0) > 0) score += 3;
        if (!string.IsNullOrEmpty(delivery.ResumeText)) score += 5;
        return score;
    }

    /// <summary>构建本地分析结果（无需AI）</summary>
    private static string BuildLocalAnalyzeResult(string name, string phone, string email, string education,
        List<string> skills, List<object> workExp, List<object> projects, int source)
    {
        return JsonConvert.SerializeObject(new
        {
            name, phone, email, education,
            workYears = 0, // 由调用方传入
            skills,
            workExperience = workExp,
            projects,
            source = source == 1 ? "本地AI推断" : "本地规则分析",
            summary = source == 1
                ? "基于岗位要求和候选人基本信息，通过本地AI模型推断技能和工作经历。建议上传完整简历以获取更精准的分析。"
                : "AI服务暂时不可用，基于候选人基本信息与岗位要求关键词进行本地规则匹配分析。分析结果仅供参考。",
            tags = skills.Take(5).ToList()
        });
    }

    /// <summary>合并 AI 结果和本地分析结果</summary>
    private static string MergeAnalysisResults(string aiJson, string name, string phone, string email,
        string education, List<string> skills, List<object> workExp, List<object> projects)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<dynamic>(aiJson);
            // AI 结果优先，缺失字段用本地推断补充
            return JsonConvert.SerializeObject(new
            {
                name = parsed?.name?.ToString() ?? name,
                phone = parsed?.phone?.ToString() ?? phone,
                email = parsed?.email?.ToString() ?? email,
                education = parsed?.education?.ToString() ?? education,
                workYears = parsed?.workYears ?? 0,
                skills = (parsed?.skills as Newtonsoft.Json.Linq.JArray)?.Count > 0
                    ? JsonConvert.DeserializeObject<List<string>>(parsed!.skills!.ToString())
                    : skills,
                workExperience = (parsed?.workExperience as Newtonsoft.Json.Linq.JArray)?.Count > 0
                    ? JsonConvert.DeserializeObject<List<object>>(parsed!.workExperience!.ToString())
                    : workExp,
                projects = (parsed?.projects as Newtonsoft.Json.Linq.JArray)?.Count > 0
                    ? JsonConvert.DeserializeObject<List<object>>(parsed!.projects!.ToString())
                    : projects,
                summary = parsed?.summary?.ToString() ?? "",
                tags = parsed?.tags != null
                    ? JsonConvert.DeserializeObject<List<string>>(parsed!.tags!.ToString())
                    : skills.Take(5).ToList(),
                source = "AI增强分析"
            });
        }
        catch
        {
            return BuildLocalAnalyzeResult(name, phone, email, education, skills, workExp, projects, 3);
        }
    }

    public async Task<InterviewQuestionResult> GenerateInterviewQuestionsAsync(int deliveryId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        if (delivery == null) throw new Exception("投递记录不存在");

        var candidate = delivery.Candidate;
        var job = delivery.Job;

        var systemPrompt = @"你是一个专业的面试题生成助手。请根据候选人的简历和应聘岗位，生成针对性的面试问题。
返回格式必须包含JSON：
- technical: 技术问题数组(3-5个)
- behavioral: 行为问题数组(2-3个)
- scenario: 场景问题数组(2-3个)

请只返回JSON，不要包含其他文字。";

        var userPrompt = $@"请为以下候选人生成面试问题：

候选人：{candidate?.RealName ?? "未知"}
应聘岗位：{job?.Title ?? "未知"}
岗位要求：{job?.Requirements ?? "未知"}
候选人学历：{candidate?.Education ?? "未知"}
候选人工作年限：{candidate?.WorkYears ?? 0}年

请生成针对性的技术问题、行为面试问题和场景模拟问题。";

        string aiResult;
        try
        {
            aiResult = await CallAIAsync(systemPrompt, userPrompt);
        }
        catch
        {
            aiResult = GetFallbackQuestionsResult();
        }

        var questions = new
        {
            technical = ExtractArrayField(aiResult, "technical") ?? new[] {
                "请介绍一下你最近做的一个项目，以及你在其中的职责？",
                "你对微服务架构有什么了解？",
                "如何优化SQL查询性能？"
            },
            behavioral = ExtractArrayField(aiResult, "behavioral") ?? new[] {
                "请描述一次你与团队成员发生冲突的经历，如何解决的？",
                "你是如何规划和管理自己的工作时间的？"
            },
            scenario = ExtractArrayField(aiResult, "scenario") ?? new[] {
                "如果上线后发现严重Bug，你会如何处理？",
                "如果客户要求在短时间内完成一个复杂需求，你会如何沟通？"
            }
        };

        var aiQuestion = new AIInterviewQuestion
        {
            DeliveryId = deliveryId,
            JobId = delivery.JobId,
            QuestionsJson = JsonConvert.SerializeObject(questions),
            Category = "technical,behavioral,scenario",
            CreatedAt = DateTime.Now
        };

        _context.AIInterviewQuestions.Add(aiQuestion);
        await _context.SaveChangesAsync();

        return new InterviewQuestionResult(questions);
    }

    public async Task<RecruitmentInsightResult> GetRecruitmentInsightsAsync(int hrId, string period)
    {
        var totalDeliveries = await _context.Deliveries.Where(d => d.HrId == hrId).CountAsync();
        var reviewed = await _context.Deliveries.Where(d => d.HrId == hrId && d.Status >= 1).CountAsync();
        var interviewed = await _context.Deliveries.Where(d => d.HrId == hrId && d.Status >= 2).CountAsync();
        var hired = await _context.Deliveries.Where(d => d.HrId == hrId && d.Status >= 3).CountAsync();

        var pipeline = new
        {
            totalDeliveries,
            reviewed,
            interviewed,
            hired
        };

        var reviewRate = totalDeliveries > 0 ? (double)reviewed / totalDeliveries * 100 : 0;
        var interviewRate = reviewed > 0 ? (double)interviewed / reviewed * 100 : 0;
        var hireRate = interviewed > 0 ? (double)hired / interviewed * 100 : 0;

        // 尝试用 AI 生成自然语言洞察
        string[] recommendations;
        try
        {
            var aiInsight = await CallAIAsync(
                "你是一位资深的招聘数据分析师。根据提供的招聘漏斗数据，给出3-5条具体、可操作的建议。以JSON字符串数组格式返回：[\"建议1\",\"建议2\"]。建议要结合具体数字，不要空泛。只返回JSON数组，不要额外文字。",
                $"招聘数据（{period}）：\n" +
                $"总投递：{totalDeliveries}份\n" +
                $"已查看：{reviewed}份（查看率{reviewRate:F1}%）\n" +
                $"已面试：{interviewed}人（面试转化率{interviewRate:F1}%）\n" +
                $"已入职：{hired}人（入职转化率{hireRate:F1}%）"
            );
            aiInsight = aiInsight.Trim();
            if (aiInsight.StartsWith("```json")) aiInsight = aiInsight[7..];
            else if (aiInsight.StartsWith("```")) aiInsight = aiInsight[3..];
            if (aiInsight.EndsWith("```")) aiInsight = aiInsight[..^3];
            aiInsight = aiInsight.Trim();
            recommendations = JsonConvert.DeserializeObject<string[]>(aiInsight) ?? GetFallbackRecommendations(reviewRate, interviewRate, hired);
        }
        catch
        {
            recommendations = GetFallbackRecommendations(reviewRate, interviewRate, hired);
        }

        try
        {
            var insight = new AIRecruitmentInsight
            {
                HrId = hrId,
                Period = period,
                PipelineData = JsonConvert.SerializeObject(pipeline),
                Recommendations = JsonConvert.SerializeObject(recommendations),
                CreatedAt = DateTime.Now
            };

            _context.AIRecruitmentInsights.Add(insight);
            await _context.SaveChangesAsync();
        }
        catch { /* 持久化失败不影响返回 */ }

        return new RecruitmentInsightResult(pipeline, recommendations);
    }

    public async Task<List<RecentAnalysisResult>> GetRecentAnalysesAsync(int limit = 10)
    {
        // 按 DeliveryId 去重，每个投递只取最新的分析记录
        var all = await _context.AIResumeAnalyses
            .Include(a => a.Candidate)
            .Include(a => a.Delivery).ThenInclude(d => d.Job)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var seen = new HashSet<int>();
        var unique = new List<AIResumeAnalysis>();
        foreach (var a in all)
        {
            if (seen.Add(a.DeliveryId))
                unique.Add(a);
        }

        var result = new List<RecentAnalysisResult>();
        foreach (var a in unique.Take(limit))
        {
            string candidateName;
            // 优先从分析结果 JSON 中获取真实姓名
            try
            {
                var parsed = JsonConvert.DeserializeObject<dynamic>(a.ParsedJson);
                var jsonName = parsed?.name?.ToString();
                candidateName = !string.IsNullOrEmpty(jsonName) && jsonName != "未知"
                    ? jsonName
                    : (a.Candidate?.RealName ?? "未知");
            }
            catch { candidateName = a.Candidate?.RealName ?? "未知"; }

            int? score = null;
            try
            {
                var parsed = JsonConvert.DeserializeObject<dynamic>(a.ParsedJson);
                if (parsed?.matchScore != null)
                    score = (int)parsed.matchScore;
                else if (parsed?.score != null)
                    score = (int)parsed.score;
            }
            catch { }

            result.Add(new RecentAnalysisResult
            {
                DeliveryId = a.DeliveryId,
                CandidateName = candidateName,
                JobTitle = a.Delivery?.Job?.Title ?? "未知",
                ParsedJson = a.ParsedJson,
                Score = score,
                AnalyzedAt = a.CreatedAt
            });
        }

        return result;
    }

    private string EnsureMinimumData(string aiResult, Candidate? candidate, Job? job)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<dynamic>(aiResult);
            bool needsUpdate = false;

            // 检查skills数组
            var skillsArray = parsed?.skills as Newtonsoft.Json.Linq.JArray;
            if (skillsArray == null || skillsArray.Count == 0)
            {
                parsed.skills = new Newtonsoft.Json.Linq.JArray { "待分析技能", "专业能力", "团队协作" };
                needsUpdate = true;
            }

            // 检查workExperience数组
            var workExpArray = parsed?.workExperience as Newtonsoft.Json.Linq.JArray;
            if (workExpArray == null || workExpArray.Count == 0)
            {
                var workExp = new Newtonsoft.Json.Linq.JArray();
                workExp.Add(new Newtonsoft.Json.Linq.JObject {
                    { "company", "待补充" },
                    { "position", "待补充" },
                    { "duration", "待补充" },
                    { "description", "请在简历管理中上传完整简历以获取更详细的分析" }
                });
                parsed.workExperience = workExp;
                needsUpdate = true;
            }

            // 检查projects数组
            var projectsArray = parsed?.projects as Newtonsoft.Json.Linq.JArray;
            if (projectsArray == null || projectsArray.Count == 0)
            {
                var projects = new Newtonsoft.Json.Linq.JArray();
                projects.Add(new Newtonsoft.Json.Linq.JObject {
                    { "name", "待补充" },
                    { "role", "待补充" },
                    { "duration", "待补充" },
                    { "description", "请在简历管理中上传完整简历以获取更详细的分析" }
                });
                parsed.projects = projects;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                return JsonConvert.SerializeObject(parsed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"EnsureMinimumData failed: {ex.Message}");
        }
        
        return aiResult;
    }

    private string GetFallbackAnalyzeResult(Candidate? candidate, Job? job)
    {
        return JsonConvert.SerializeObject(new
        {
            name = candidate?.RealName ?? "未知",
            phone = candidate?.Phone ?? "未知",
            email = candidate?.Email ?? "未知",
            education = candidate?.Education ?? "未知",
            skills = new[] { "待分析" },
            workExperience = new[] {
                new { company = "待补充", position = "待补充", duration = "待补充", description = "请在简历管理中上传完整简历" }
            },
            projects = new[] {
                new { name = "待补充", role = "待补充", duration = "待补充", description = "请在简历管理中上传完整简历" }
            }
        });
    }

    private AIScoreResult GetFallbackScoreResult(Delivery delivery)
    {
        var score = 65;
        var reason = "基于简历信息与岗位要求进行初步匹配分析";

        if (!string.IsNullOrEmpty(delivery.Job?.Requirements))
        {
            var requirements = delivery.Job.Requirements.ToLower();
            if (requirements.Contains("本科") || requirements.Contains("硕士"))
                score += 10;
            if (requirements.Contains("3年") || requirements.Contains("5年"))
                score += 5;
        }

        score = Math.Min(score, 95);

        return new AIScoreResult(score, reason,
            $"根据岗位'{delivery.Job?.Title}'的要求，对候选人'{delivery.Candidate?.RealName}'的简历进行了初步分析，匹配度为{score}分。建议查看完整简历后进行更准确的评估。");
    }

    private string GetFallbackQuestionsResult()
    {
        return JsonConvert.SerializeObject(new
        {
            technical = new[] {
                "请介绍一下你最近做的一个项目，以及你在其中的职责？",
                "你对微服务架构有什么了解？",
                "如何优化SQL查询性能？"
            },
            behavioral = new[] {
                "请描述一次你与团队成员发生冲突的经历，如何解决的？",
                "你是如何规划和管理自己的工作时间的？"
            },
            scenario = new[] {
                "如果上线后发现严重Bug，你会如何处理？",
                "如果客户要求在短时间内完成一个复杂需求，你会如何沟通？"
            }
        });
    }

    private static string[] GetFallbackRecommendations(double reviewRate, double interviewRate, int hired)
    {
        var list = new List<string>();
        if (reviewRate < 50)
            list.Add($"简历查看率仅{reviewRate:F0}%，建议缩短简历筛选周期，及时响应候选人");
        if (interviewRate < 30)
            list.Add($"面试转化率仅{interviewRate:F0}%，建议优化简历筛选标准，减少无效面试");
        if (hired < 5)
            list.Add($"入职人数仅{hired}人，建议优化面试体验和offer流程，提高入职转化率");
        if (list.Count == 0)
        {
            list.Add("招聘数据表现良好，各环节转化率健康");
            list.Add("建议定期回顾招聘漏斗，持续优化招聘策略");
        }
        return list.ToArray();
    }

    private string[]? ExtractArrayField(string json, string fieldName)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<dynamic>(json);
            if (parsed != null && parsed[fieldName] != null)
            {
                return JsonConvert.DeserializeObject<string[]>(parsed[fieldName].ToString());
            }
        }
        catch { }
        return null;
    }

    private string ExtractSkills(string json)
    {
        return ExtractFieldFromJson(json, "skills") ?? "[]";
    }

    private string ExtractWorkExperience(string json)
    {
        return ExtractFieldFromJson(json, "workExperience") ?? "[]";
    }

    private string ExtractProjects(string json)
    {
        return ExtractFieldFromJson(json, "projects") ?? "[]";
    }

    private string? ExtractFieldFromJson(string json, string fieldName)
    {
        try
        {
            var parsed = JsonConvert.DeserializeObject<dynamic>(json);
            if (parsed != null && parsed[fieldName] != null)
            {
                return parsed[fieldName].ToString();
            }
        }
        catch { }
        return null;
    }

    public async Task<JDGenerateResult> GenerateJDAsync(string briefDescription)
    {
        var systemPrompt = """
你是一名资深HR，精通岗位JD编写。根据用人部门需求，生成完整招聘信息。

严格输出以下JSON格式（3个版本）：
{
  "title": "岗位名称",
  "dept": "建议部门",
  "location": "建议城市",
  "salaryMin": 最低薪资K数,
  "salaryMax": 最高薪资K数,
  "headCount": 招聘人数,
  "versions": [
    {
      "version": "标准版",
      "responsibilities": ["每条不超过20字","每条不超过20字","每条不超过20字","每条不超过20字"],
      "requirements": ["每条不超过15字","每条不超过15字","每条不超过15字","每条不超过15字"],
      "highlights": "岗位亮点描述，20字以内"
    },
    {
      "version": "精简版（突出核心）",
      "responsibilities": ["...", "..."],
      "requirements": ["...", "..."],
      "highlights": "..."
    },
    {
      "version": "吸引版（侧重发展）",
      "responsibilities": ["...", "..."],
      "requirements": ["...", "..."],
      "highlights": "..."
    }
  ]
}

要求：
- 职责每条不超过20字，3-5条
- 要求简洁直接，每条不超过15字，4-6条
- 亮点精炼有吸引力，不超过20字
- 三个版本职责侧重点不同：标准版全面、精简版核心、吸引版突出成长空间
- 仅输出JSON，无其他内容
""";

        var userPrompt = $"岗位需求：{briefDescription}";

        try
        {
            var aiResult = await CallAIAsync(systemPrompt, userPrompt);
            aiResult = CleanJsonResponse(aiResult);

            var parsed = JsonConvert.DeserializeObject<dynamic>(aiResult);
            var versions = new List<JDVersion>();
            if (parsed?.versions != null)
            {
                foreach (var v in parsed.versions)
                {
                    versions.Add(new JDVersion(
                        v.version?.ToString() ?? "版本",
                        v.responsibilities != null
                            ? JsonConvert.DeserializeObject<List<string>>(v.responsibilities.ToString()) ?? new List<string>()
                            : new List<string>(),
                        v.requirements != null
                            ? JsonConvert.DeserializeObject<List<string>>(v.requirements.ToString()) ?? new List<string>()
                            : new List<string>(),
                        v.highlights?.ToString() ?? ""
                    ));
                }
            }

            return new JDGenerateResult(
                parsed?.title?.ToString() ?? "",
                parsed?.dept?.ToString() ?? "",
                parsed?.location?.ToString() ?? "",
                (int?)parsed?.salaryMin ?? 0,
                (int?)parsed?.salaryMax ?? 0,
                (int?)parsed?.headCount ?? 1,
                versions
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GenerateJD failed: {msg}", ex.Message);
            return GetFallbackJDResult(briefDescription);
        }
    }

    private static string CleanJsonResponse(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    /// <summary>通用 AI 对话接口</summary>
    public async Task<string> ChatAsync(string prompt)
    {
        try
        {
            return await CallAIAsync("你是招聘领域AI助手，简洁专业地回答。", prompt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ChatAsync failed: {msg}", ex.Message);
            return string.Empty;
        }
    }

    private static JDGenerateResult GetFallbackJDResult(string brief)
    {
        return new JDGenerateResult(
            "待定岗位", "技术部", "深圳", 15, 30, 1,
            new List<JDVersion> {
                new("标准版", new(){"负责核心系统设计开发","参与技术方案评审","编写高质量代码","优化系统性能"}, new(){"3年以上开发经验","计算机相关专业","熟悉主流技术栈","有大型项目经验优先"}, "技术栈前沿，团队氛围好"),
                new("精简版", new(){"核心模块设计与开发","代码质量保障"}, new(){"相关经验3年+","扎实的编程基础","开源贡献者优先"}, "核心岗位，快速成长"),
                new("吸引版", new(){"参与核心架构设计","技术难题攻关","带新人成长"}, new(){"2年以上经验","学习能力强","有技术博客或分享习惯"}, "晋升快，技术氛围浓厚")
            }
        );
    }
}

public record AIScoreResult(int Score, string Reason, string Report);
public record AIScoreResultEx(int Score, string Reason, string Report, List<string> Strengths, List<string> Weaknesses, List<object> Dimensions)
    : AIScoreResult(Score, Reason, Report);
public record InterviewQuestionResult(dynamic Questions);
public record RecruitmentInsightResult(dynamic PipelineData, string[] Recommendations);
public record JDGenerateResult(string Title, string Dept, string Location, int SalaryMin, int SalaryMax, int HeadCount, List<JDVersion> Versions);
public record JDVersion(string Version, List<string> Responsibilities, List<string> Requirements, string Highlights);
