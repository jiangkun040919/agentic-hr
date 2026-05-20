using System.Diagnostics;
using System.Text.Json;

namespace AIRecruitment.Api.Services;

public interface IPdfExtractService
{
    Task<string> ExtractTextAsync(string filePath);
    Task<string> ExtractBase64Async(string base64, string fileName);
}

public class PdfExtractService : IPdfExtractService
{
    private readonly string _pythonPath;
    private readonly string _tempDir;

    public PdfExtractService()
    {
        _pythonPath = @"C:\Users\Lenovo\AppData\Local\Python\bin\python.exe";
        _tempDir = Path.Combine(Path.GetTempPath(), "ResumePDFs");
        Directory.CreateDirectory(_tempDir);
    }

    private static string BuildPythonScript(string filePath, string outputFile)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var escapedPath = filePath.Replace("\\", "\\\\");
        var escapedOutput = outputFile.Replace("\\", "\\\\");

        if (ext == ".docx" || ext == ".doc")
        {
            return $@"
import json, sys
try:
    from docx import Document
    doc = Document(sys.argv[1])
    text = []
    for para in doc.paragraphs:
        if para.text.strip():
            text.append(para.text.strip())
    for table in doc.tables:
        for row in table.rows:
            row_text = ' | '.join(cell.text.strip() for cell in row.cells)
            if row_text.strip():
                text.append(row_text)
    full = '\n'.join(text)
    with open(r'{escapedOutput}', 'w', encoding='utf-8') as f:
        json.dump({{'text': full}}, f, ensure_ascii=False)
    print('OK')
except Exception as e:
    with open(r'{escapedOutput}', 'w', encoding='utf-8') as f:
        json.dump({{'error': str(e)}}, f)
    print(f'ERR:{{e}}')";
        }

        // PDF (default)
        return $@"
import pdfplumber, json, sys
try:
    with pdfplumber.open(sys.argv[1]) as pdf:
        text = []
        for page in pdf.pages:
            t = page.extract_text()
            if t: text.append(t)
        full = '\n'.join(text)
    with open(r'{escapedOutput}', 'w', encoding='utf-8') as f:
        json.dump({{'text': full}}, f, ensure_ascii=False)
    print('OK')
except Exception as e:
    with open(r'{escapedOutput}', 'w', encoding='utf-8') as f:
        json.dump({{'error': str(e)}}, f)
    print(f'ERR:{{e}}')";
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "";

        var outputFile = Path.Combine(_tempDir, $"out_{Guid.NewGuid()}.json");
        var script = BuildPythonScript(filePath, outputFile);

        var scriptPath = Path.Combine(_tempDir, $"extract_{Guid.NewGuid()}.py");
        await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{scriptPath}\" \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return "";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(cts.Token);

        if (File.Exists(outputFile))
        {
            var json = await File.ReadAllTextAsync(outputFile, System.Text.Encoding.UTF8);
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(json);
                return result.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
            }
            catch { return ""; }
        }
        return "";
    }

    public async Task<string> ExtractBase64Async(string base64, string fileName)
    {
        if (string.IsNullOrEmpty(base64)) return "";

        var pure = base64.Contains(',') ? base64.Split(',')[1] : base64;
        var bytes = Convert.FromBase64String(pure);
        var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid()}_{fileName}");
        await File.WriteAllBytesAsync(filePath, bytes);

        return await ExtractTextAsync(filePath);
    }
}
