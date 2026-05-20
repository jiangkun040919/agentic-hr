using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

public class DataSeederService
{
    private readonly AppDbContext _context;
    private readonly KnowledgeGraphService? _graph;

    public DataSeederService(AppDbContext context, KnowledgeGraphService? graph = null)
    {
        _context = context;
        _graph = graph;
    }

    public async Task SeedAsync()
    {
        if (_context.Jobs.Any(j => j.Title == "Java开发工程师")) return;

        var hr = new SysUser { Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), Role = "admin", RealName = "系统管理员", Status = 1, CreatedAt = DateTime.Now };
        var hr2 = new SysUser { Username = "hr_zhang", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Role = "hr", RealName = "张HR", Status = 1, CreatedAt = DateTime.Now };
        _context.SysUsers.AddRange(hr, hr2);
        await _context.SaveChangesAsync();

        var jobs = new List<Job>
        {
            new() { Title = "Java开发工程师", Dept = "技术部", Location = "北京", SalaryMin = 18, SalaryMax = 35, HeadCount = 3, Status = 1, HrId = hr2.UserId, JD = "负责后端服务开发和维护\n参与系统架构设计\n编写高质量代码和单元测试\n参与代码评审", Requirements = "3年以上Java开发经验\n精通Spring Boot/Spring Cloud\n熟悉MySQL、Redis\n了解微服务架构\n有分布式系统经验优先", CreatedAt = DateTime.Now.AddDays(-30) },
            new() { Title = "Python开发工程师", Dept = "技术部", Location = "上海", SalaryMin = 20, SalaryMax = 40, HeadCount = 2, Status = 1, HrId = hr2.UserId, JD = "负责数据处理和分析平台开发\n构建AI应用后端服务\n参与API设计\n编写技术文档", Requirements = "Python开发经验3年+\n熟悉Django/Flask/FastAPI\n掌握PostgreSQL\n了解Docker容器化\n有数据分析经验优先", CreatedAt = DateTime.Now.AddDays(-25) },
            new() { Title = "前端开发工程师", Dept = "技术部", Location = "深圳", SalaryMin = 15, SalaryMax = 30, HeadCount = 2, Status = 1, HrId = hr2.UserId, JD = "负责Web前端页面开发\n优化前端性能和用户体验\n参与组件库建设\n与后端协作完成功能开发", Requirements = "精通React或Vue\n熟练TypeScript\n了解Webpack/Vite构建工具\n有移动端适配经验\n注重代码质量", CreatedAt = DateTime.Now.AddDays(-20) },
            new() { Title = "DevOps工程师", Dept = "技术部", Location = "北京", SalaryMin = 25, SalaryMax = 45, HeadCount = 1, Status = 1, HrId = hr2.UserId, JD = "负责CI/CD流水线建设\n管理Kubernetes集群\n监控系统搭建和维护\n自动化运维脚本开发", Requirements = "精通Docker和Kubernetes\n熟悉Jenkins/GitLab CI\n了解AWS或阿里云\nLinux系统管理经验\n有Terraform经验优先", CreatedAt = DateTime.Now.AddDays(-15) },
            new() { Title = "数据分析师", Dept = "数据部", Location = "杭州", SalaryMin = 12, SalaryMax = 25, HeadCount = 2, Status = 1, HrId = hr2.UserId, JD = "负责业务数据分析\n构建数据看板和报表\n数据质量监控\n为业务决策提供数据支持", Requirements = "精通SQL\n熟练使用Python/Pandas\n了解数据可视化工具\n有统计学基础\n沟通能力强", CreatedAt = DateTime.Now.AddDays(-10) },
            new() { Title = "机器学习工程师", Dept = "AI部", Location = "北京", SalaryMin = 30, SalaryMax = 60, HeadCount = 2, Status = 1, HrId = hr2.UserId, JD = "负责ML模型开发与优化\n特征工程和模型评估\n模型部署和监控\n跟踪前沿技术", Requirements = "精通Python和PyTorch/TensorFlow\n深入理解ML算法\n有模型部署经验\n了解分布式训练\n发表过顶会论文优先", CreatedAt = DateTime.Now.AddDays(-8) },
            new() { Title = "产品经理", Dept = "产品部", Location = "北京", SalaryMin = 20, SalaryMax = 40, HeadCount = 1, Status = 1, HrId = hr2.UserId, JD = "负责产品规划和需求分析\n撰写PRD文档\n协调开发、设计资源\n跟踪产品数据和用户反馈", Requirements = "3年以上产品经验\n有B端SaaS产品经验\n优秀的数据分析能力\n沟通和推动能力强\n技术背景优先", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { Title = "AI应用工程师", Dept = "AI部", Location = "深圳", SalaryMin = 25, SalaryMax = 50, HeadCount = 3, Status = 1, HrId = hr2.UserId, JD = "负责AI应用开发和落地\n大模型应用集成\nPrompt优化\n构建RAG系统", Requirements = "熟悉大模型应用开发\n了解LangChain/LlamaIndex\n掌握Prompt Engineering\nPython开发熟练\n有向量检索经验优先", CreatedAt = DateTime.Now.AddDays(-3) },
        };
        _context.Jobs.AddRange(jobs);
        await _context.SaveChangesAsync();

        var candidates = new (string, string, string, int)[]
        {
            ("张小明", "13800001111", "本科", 3), ("李小红", "13800002222", "硕士", 5),
            ("王大伟", "13800003333", "本科", 2), ("赵小芳", "13800004444", "硕士", 4),
            ("陈建国", "13800005555", "博士", 1), ("林美丽", "13800006666", "本科", 6),
            ("周杰", "13800007777", "硕士", 3), ("吴志强", "13800008888", "本科", 7),
        };
        var statuses = new[] { 0, 0, 1, 2, 2, 3, 4, 0 };
        var rng = new Random(42);

        for (int i = 0; i < candidates.Length; i++)
        {
            var job = jobs[rng.Next(jobs.Count)];
            var (name, phone, edu, yrs) = candidates[i];
            var c = new Candidate { RealName = name, Phone = phone, Email = $"{name}@example.com", Education = edu, WorkYears = yrs, CreatedAt = DateTime.Now.AddDays(-rng.Next(1, 30)) };
            _context.Candidates.Add(c);
            await _context.SaveChangesAsync();
            _context.Deliveries.Add(new Delivery { JobId = job.JobId, CandidateId = c.CandidateId, HrId = hr2.UserId, ContactName = name, ContactPhone = phone, ContactEmail = c.Email, ContactEducation = edu, ContactWorkYears = yrs, Status = statuses[i], DeliverTime = DateTime.Now.AddDays(-rng.Next(1, 20)) });
        }
        await _context.SaveChangesAsync();

        if (_graph != null)
            foreach (var j in jobs)
                await _graph.UpsertJobSkillsAsync(j.JobId, j.Title, j.Requirements, j.JD);
    }
}
