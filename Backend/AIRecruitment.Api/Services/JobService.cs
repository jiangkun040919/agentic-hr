using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public interface IJobService
{
    Task<PagedResponse<JobResponse>> GetJobListAsync(JobListParams p);
    Task<JobResponse> GetJobDetailAsync(int id);
    Task<JobResponse> CreateJobAsync(int hrId, JobFormData data);
    Task<JobResponse> UpdateJobAsync(int id, int hrId, JobFormData data, bool isAdmin = false);
    Task DeleteJobAsync(int id);
    Task UpdateJobStatusAsync(int id, int status);
    Task<PagedResponse<JobResponse>> GetMyJobsAsync(int hrId, JobListParams p);
    Task<int> BatchImportAsync(List<JobImportItem> items);
}

public record JobImportItem(
    string Title, string? Dept, string? Location,
    int? SalaryMin, int? SalaryMax, int? HeadCount,
    string? JD, string? Requirements, string? Source, string? SourceUrl);

public class JobService : IJobService
{
    private readonly AppDbContext _context;
    private readonly ICacheService _cache;
    private readonly KnowledgeGraphService? _graph;

    public JobService(AppDbContext context, ICacheService cache, KnowledgeGraphService? graph = null)
    {
        _context = context;
        _cache = cache;
        _graph = graph;
    }

    private static readonly string[] KnownSkills = { "Java", "Spring", "MySQL", "Redis", "Docker", "Kubernetes", "Python", "FastAPI", "PostgreSQL", "MongoDB", "React", "Vue", "TypeScript", "Node.js", "Go", "C++", "Git", "CI/CD", "Kafka", "Elasticsearch", "AWS", "Azure", "TensorFlow", "PyTorch", "Spark", "Flink", "Hadoop", "Linux", "Nginx", "RabbitMQ", "SpringBoot", "MyBatis", "Dubbo", "Zookeeper", "Netty", "HTML", "CSS", "JavaScript", "Webpack", "Vite", "GraphQL", "gRPC", "Jenkins", "Ansible", "Terraform", "Prometheus", "Grafana" };

    private List<string> ExtractSkills(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new();
        return KnownSkills.Where(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<PagedResponse<JobResponse>> GetJobListAsync(JobListParams p)
    {
        // 无筛选条件的首页列表走缓存（5分钟）
        var isDefaultQuery = string.IsNullOrEmpty(p.Keyword)
            && string.IsNullOrEmpty(p.Dept) && string.IsNullOrEmpty(p.Location)
            && p.Page == 1;
        if (isDefaultQuery)
        {
            var cacheKey = $"jobs:list:default:p{p.PageSize}";
            var cached = await _cache.GetAsync<PagedResponse<JobResponse>>(cacheKey);
            if (cached != null) return cached;
        }

        var query = _context.Jobs
            .Where(j => j.Status == 1)
            .AsQueryable();

        if (!string.IsNullOrEmpty(p.Keyword))
            query = query.Where(j => j.Title.Contains(p.Keyword));
        if (!string.IsNullOrEmpty(p.Dept))
            query = query.Where(j => j.Dept == p.Dept);
        if (!string.IsNullOrEmpty(p.Location))
            query = query.Where(j => j.Location.Contains(p.Location));

        query = p.SortBy switch
        {
            "created_at" => p.SortOrder == "asc" ? query.OrderBy(j => j.CreatedAt) : query.OrderByDescending(j => j.CreatedAt),
            "salary" => p.SortOrder == "asc" ? query.OrderBy(j => j.SalaryMin) : query.OrderByDescending(j => j.SalaryMax),
            _ => query.OrderByDescending(j => j.CreatedAt)
        };

        var total = await query.CountAsync();
        var items = (await query
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(j => new {
                j.JobId, j.Title, j.Dept, j.Location, j.JD, j.Requirements,
                j.SalaryMin, j.SalaryMax, j.HeadCount, j.Status, j.HrId,
                j.CreatedAt, j.UpdatedAt, j.ExpiredAt,
                DeliveryCount = j.Deliveries.Count,
                InterviewCount = j.Deliveries.SelectMany(d => d.Interviews).Count()
            })
            .ToListAsync())
            .Select(j => new JobResponse(
                j.JobId, j.Title, j.Dept, j.Location, j.JD, j.Requirements,
                j.SalaryMin, j.SalaryMax, j.HeadCount, j.Status, j.HrId,
                j.CreatedAt, j.UpdatedAt, j.ExpiredAt) with {
                    Skills = ExtractSkills(j.Requirements),
                    DeliveryCount = j.DeliveryCount,
                    InterviewCount = j.InterviewCount
                })
            .ToList();

        var result = new PagedResponse<JobResponse>(items, total, p.Page, p.PageSize);

        if (isDefaultQuery)
            await _cache.SetAsync($"jobs:list:default:p{p.PageSize}", result, TimeSpan.FromMinutes(5));

        return result;
    }

    public async Task<JobResponse> GetJobDetailAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job == null) throw new Exception("岗位不存在");

        return new JobResponse(
            job.JobId, job.Title, job.Dept, job.Location, job.JD, job.Requirements,
            job.SalaryMin, job.SalaryMax, job.HeadCount, job.Status, job.HrId,
            job.CreatedAt, job.UpdatedAt, job.ExpiredAt) with {
                Skills = ExtractSkills(job.Requirements)
            };
    }

    public async Task<JobResponse> CreateJobAsync(int hrId, JobFormData data)
    {
        var job = new Job
        {
            Title = data.Title,
            Dept = data.Dept,
            Location = data.Location,
            JD = data.JD,
            Requirements = data.Requirements,
            SalaryMin = data.SalaryMin,
            SalaryMax = data.SalaryMax,
            HeadCount = data.HeadCount,
            Status = data.Status,
            HrId = hrId,
            ExpiredAt = data.ExpiredAt,
            CreatedAt = DateTime.Now
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("jobs:list:default");

        // 同步到知识图谱
        if (_graph != null)
        {
            try { await _graph.UpsertJobSkillsAsync(job.JobId, job.Title, job.Requirements, job.JD); }
            catch { /* 图谱不可用时静默跳过 */ }
        }

        return new JobResponse(
            job.JobId, job.Title, job.Dept, job.Location, job.JD, job.Requirements,
            job.SalaryMin, job.SalaryMax, job.HeadCount, job.Status, job.HrId,
            job.CreatedAt, job.UpdatedAt, job.ExpiredAt);
    }

    public async Task<JobResponse> UpdateJobAsync(int id, int hrId, JobFormData data, bool isAdmin = false)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job == null) throw new Exception("岗位不存在");
        if (!isAdmin && job.HrId != hrId) throw new Exception("无权限修改");

        job.Title = data.Title;
        job.Dept = data.Dept;
        job.Location = data.Location;
        job.JD = data.JD;
        job.Requirements = data.Requirements;
        job.SalaryMin = data.SalaryMin;
        job.SalaryMax = data.SalaryMax;
        job.HeadCount = data.HeadCount;
        job.Status = data.Status;
        job.ExpiredAt = data.ExpiredAt;
        job.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("jobs:list:default");

        // 同步到知识图谱
        if (_graph != null)
        {
            try { await _graph.UpsertJobSkillsAsync(job.JobId, job.Title, job.Requirements, job.JD); }
            catch { /* 图谱不可用时静默跳过 */ }
        }

        return new JobResponse(
            job.JobId, job.Title, job.Dept, job.Location, job.JD, job.Requirements,
            job.SalaryMin, job.SalaryMax, job.HeadCount, job.Status, job.HrId,
            job.CreatedAt, job.UpdatedAt, job.ExpiredAt);
    }

    public async Task DeleteJobAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job == null) throw new Exception("岗位不存在");

        _context.Jobs.Remove(job);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("jobs:list:default");
    }

    public async Task UpdateJobStatusAsync(int id, int status)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job == null) throw new Exception("岗位不存在");

        job.Status = status;
        job.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("jobs:list:default");
    }

    public async Task<PagedResponse<JobResponse>> GetMyJobsAsync(int hrId, JobListParams p)
    {
        var query = _context.Jobs.Where(j => j.HrId == hrId);

        if (!string.IsNullOrEmpty(p.Keyword))
            query = query.Where(j => j.Title.Contains(p.Keyword));
        if (p.Status.HasValue)
            query = query.Where(j => j.Status == p.Status);

        var total = await query.CountAsync();
        var items = (await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(j => new {
                j.JobId, j.Title, j.Dept, j.Location, j.JD, j.Requirements,
                j.SalaryMin, j.SalaryMax, j.HeadCount, j.Status, j.HrId,
                j.CreatedAt, j.UpdatedAt, j.ExpiredAt,
                DeliveryCount = j.Deliveries.Count,
                InterviewCount = j.Deliveries.SelectMany(d => d.Interviews).Count()
            })
            .ToListAsync())
            .Select(j => new JobResponse(
                j.JobId, j.Title, j.Dept, j.Location, j.JD, j.Requirements,
                j.SalaryMin, j.SalaryMax, j.HeadCount, j.Status, j.HrId,
                j.CreatedAt, j.UpdatedAt, j.ExpiredAt) with {
                    Skills = ExtractSkills(j.Requirements),
                    DeliveryCount = j.DeliveryCount,
                    InterviewCount = j.InterviewCount
                })
            .ToList();

        return new PagedResponse<JobResponse>(items, total, p.Page, p.PageSize);
    }

    public async Task<int> BatchImportAsync(List<JobImportItem> items)
    {
        var count = 0;
        var rng = new Random();
        foreach (var item in items)
        {
            var exists = await _context.Jobs.AnyAsync(j =>
                j.Title == item.Title && j.Location == item.Location);
            if (exists) continue;

            var job = new Job
            {
                Title = item.Title,
                Dept = item.Dept ?? "技术部",
                Location = item.Location ?? "深圳",
                JD = item.JD ?? "",
                Requirements = item.Requirements ?? "",
                SalaryMin = item.SalaryMin,
                SalaryMax = item.SalaryMax,
                HeadCount = item.HeadCount ?? 1,
                Status = 1,
                HrId = 1,
                CreatedAt = DateTime.Now.AddDays(-rng.Next(0, 30))
            };

            _context.Jobs.Add(job);
            count++;
        }
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("jobs:list:default");
        return count;
    }
}