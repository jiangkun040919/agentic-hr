using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

/// <summary>Excel 导出服务，使用 ClosedXML 生成 .xlsx</summary>
public class ExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>导出投递记录（含候选人姓名、岗位、状态、时间）</summary>
    public async Task<byte[]> ExportDeliveriesAsync()
    {
        var deliveries = await _db.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .OrderByDescending(d => d.DeliverTime)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("投递记录");

        // 表头
        var headers = new[] { "投递ID", "候选人姓名", "应聘岗位", "部门", "状态", "投递时间", "更新时间", "HR备注" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // 表头样式
        var headerRow = ws.Range(1, 1, 1, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // 数据行
        for (int i = 0; i < deliveries.Count; i++)
        {
            var d = deliveries[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = d.DeliveryId;
            ws.Cell(row, 2).Value = d.ContactName ?? d.Candidate?.RealName ?? "";
            ws.Cell(row, 3).Value = d.Job?.Title ?? "";
            ws.Cell(row, 4).Value = d.Job?.Dept ?? "";
            ws.Cell(row, 5).Value = d.Status switch
            {
                0 => "待处理",
                1 => "通过初筛",
                2 => "面试中",
                3 => "已录用",
                4 => "已拒绝",
                _ => "未知"
            };
            ws.Cell(row, 6).Value = d.DeliverTime.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 7).Value = d.UpdateTime?.ToString("yyyy-MM-dd HH:mm") ?? "";
            ws.Cell(row, 8).Value = d.Remark ?? "";
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>导出候选人数据</summary>
    public async Task<byte[]> ExportCandidatesAsync()
    {
        var candidates = await _db.Candidates
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("候选人数据");

        var headers = new[] { "候选人ID", "姓名", "手机号", "邮箱", "学历", "工作年限(年)", "注册时间" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var headerRow = ws.Range(1, 1, 1, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = c.CandidateId;
            ws.Cell(row, 2).Value = c.RealName;
            ws.Cell(row, 3).Value = c.Phone;
            ws.Cell(row, 4).Value = c.Email ?? "";
            ws.Cell(row, 5).Value = c.Education ?? "";
            ws.Cell(row, 6).Value = c.WorkYears ?? 0;
            ws.Cell(row, 7).Value = c.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>导出准确率评测报告</summary>
    public async Task<byte[]> ExportBenchmarkReportAsync()
    {
        // 从 BenchmarkDataService 获取报告数据
        var benchmarkService = new BenchmarkDataService(_db, null!, null!, null!);
        var report = await benchmarkService.RunAccuracyBenchmarkAsync();

        using var workbook = new XLWorkbook();

        // Sheet 1: 概览
        var ws1 = workbook.Worksheets.Add("评测概览");
        ws1.Cell(1, 1).Value = "准确率评测报告";
        ws1.Cell(1, 1).Style.Font.Bold = true;
        ws1.Cell(1, 1).Style.Font.FontSize = 16;
        ws1.Cell(2, 1).Value = $"开始时间: {report.StartedAt:yyyy-MM-dd HH:mm:ss}";
        ws1.Cell(3, 1).Value = $"完成时间: {report.CompletedAt:yyyy-MM-dd HH:mm:ss}";
        ws1.Cell(5, 1).Value = "指标";
        ws1.Cell(5, 2).Value = "准确率";
        ws1.Cell(6, 1).Value = "JD解析准确率";
        ws1.Cell(6, 2).Value = $"{report.AvgJDParseAccuracy:F1}%";
        ws1.Cell(7, 1).Value = "简历提取准确率";
        ws1.Cell(7, 2).Value = $"{report.AvgResumeAccuracy:F1}%";
        ws1.Cell(8, 1).Value = "人岗匹配准确率";
        ws1.Cell(8, 2).Value = $"{report.AvgMatchingAccuracy:F1}%";
        ws1.Cell(5, 1).Style.Font.Bold = true;
        ws1.Cell(5, 2).Style.Font.Bold = true;

        // Sheet 2: JD解析详情
        var ws2 = workbook.Worksheets.Add("JD解析详情");
        ws2.Cell(1, 1).Value = "测试项";
        ws2.Cell(1, 2).Value = "准确率";
        ws2.Cell(1, 3).Value = "详情";
        ws2.Range(1, 1, 1, 3).Style.Font.Bold = true;
        for (int i = 0; i < report.JDParseResults.Count; i++)
        {
            ws2.Cell(i + 2, 1).Value = report.JDParseResults[i].Name;
            ws2.Cell(i + 2, 2).Value = $"{report.JDParseResults[i].Accuracy:F1}%";
            ws2.Cell(i + 2, 3).Value = report.JDParseResults[i].Details;
        }

        // Sheet 3: 简历提取详情
        var ws3 = workbook.Worksheets.Add("简历提取详情");
        ws3.Cell(1, 1).Value = "测试项";
        ws3.Cell(1, 2).Value = "准确率";
        ws3.Cell(1, 3).Value = "详情";
        ws3.Range(1, 1, 1, 3).Style.Font.Bold = true;
        for (int i = 0; i < report.ResumeExtractionResults.Count; i++)
        {
            ws3.Cell(i + 2, 1).Value = report.ResumeExtractionResults[i].Name;
            ws3.Cell(i + 2, 2).Value = $"{report.ResumeExtractionResults[i].Accuracy:F1}%";
            ws3.Cell(i + 2, 3).Value = report.ResumeExtractionResults[i].Details;
        }

        // Sheet 4: 匹配详情
        var ws4 = workbook.Worksheets.Add("匹配详情");
        ws4.Cell(1, 1).Value = "测试项";
        ws4.Cell(1, 2).Value = "准确率";
        ws4.Cell(1, 3).Value = "详情";
        ws4.Range(1, 1, 1, 3).Style.Font.Bold = true;
        for (int i = 0; i < report.MatchingResults.Count; i++)
        {
            ws4.Cell(i + 2, 1).Value = report.MatchingResults[i].Name;
            ws4.Cell(i + 2, 2).Value = $"{report.MatchingResults[i].Accuracy:F1}%";
            ws4.Cell(i + 2, 3).Value = report.MatchingResults[i].Details;
        }

        foreach (var ws in workbook.Worksheets)
            ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
