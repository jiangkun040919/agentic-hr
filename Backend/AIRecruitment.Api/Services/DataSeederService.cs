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
        return; // 演示账号已删除，不再播种
    }
}
