using System.Diagnostics;
using System.Text.Json;

namespace AIRecruitment.Api.Services;

public interface IPdfExtractService
{
    Task<string> ExtractTextAsync(string filePath);
    Task<(string text, string? filePath)> ExtractBase64Async(string base64, string fileName, int deliveryId);
}

public class PdfExtractService : IPdfExtractService
{
    private readonly string _pythonPath;
    private readonly string _tempDir;
    private readonly string _uploadsDir;

    public PdfExtractService()
    {
        _pythonPath = @"C:\Users\Lenovo\AppData\Local\Python\pythoncore-3.14-64\python.exe";
        // 备选路径
        if (!File.Exists(_pythonPath))
            _pythonPath = @"C:\Users\Lenovo\AppData\Local\Python\bin\python.exe";
        _tempDir = Path.Combine(Path.GetTempPath(), "ResumePDFs");
        _uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "resumes");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_uploadsDir);
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

        // PDF — 三层降级：pdfplumber → PyMuPDF → OCR
        return $@"
import json, sys, re

def is_garbled(text):
    '''判断文本是否乱码：有效内容<20字符或中文占比异常低'''
    if not text or len(text.strip()) < 20:
        return True
    stripped = text.strip()
    cjk = len(re.findall(r'[\u4e00-\u9fff]', stripped))
    total = len(re.sub(r'\s', '', stripped))
    # 纯中文简历应 > 10% CJK；全英文简历不判乱码
    if cjk == 0 and any(ord(c) > 127 for c in stripped):
        return False  # 非中文内容，不判乱码
    if cjk > 0 and total > 0 and cjk / total < 0.05:
        return True
    return False

def extract_pdfplumber(path):
    try:
        import pdfplumber
        with pdfplumber.open(path) as pdf:
            parts = []
            for page in pdf.pages:
                t = page.extract_text()
                if t: parts.append(t)
            return '\n'.join(parts)
    except: return ''

def extract_fitz(path):
    try:
        import fitz
        doc = fitz.open(path)
        parts = []
        for page in doc:
            t = page.get_text()
            if t: parts.append(t)
        doc.close()
        return '\n'.join(parts)
    except: return ''

def extract_ocr(path):
    try:
        import os as _os
        # 设置中文语言包路径
        _os.environ.setdefault('TESSDATA_PREFIX',
            r'C:\Users\Lenovo\AppData\Local\Tesseract-OCR\tessdata')
        from pdf2image import convert_from_path
        import pytesseract
        # 指定 tesseract 路径
        pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'
        images = convert_from_path(path, first_page=3, last_page=3)
        parts = []
        for img in images:
            t = pytesseract.image_to_string(img, lang='chi_sim+eng')
            if t: parts.append(t)
        return '\n'.join(parts)
    except: return ''

try:
    full = extract_pdfplumber(sys.argv[1])
    engine = 'pdfplumber'
    if is_garbled(full):
        full = extract_fitz(sys.argv[1])
        engine = 'fitz'
    if is_garbled(full):
        full = extract_ocr(sys.argv[1])
        engine = 'ocr'
    with open(r'{escapedOutput}', 'w', encoding='utf-8') as f:
        json.dump({{'text': full, 'engine': engine}}, f, ensure_ascii=False)
    print(f'OK:{{engine}}')
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

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 超时：杀掉进程，返回空文本
            try { process.Kill(entireProcessTree: true); } catch { }
            return "";
        }

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

    /// <summary>Word 转 PDF（使用 Python docx2pdf，需要本机安装 Microsoft Word）</summary>
    public async Task<string?> ConvertWordToPdfAsync(string docxPath, int deliveryId)
    {
        var ext = Path.GetExtension(docxPath).ToLowerInvariant();
        if (ext != ".docx" && ext != ".doc") return null;

        var pdfPath = Path.Combine(_uploadsDir, $"{deliveryId}_{DateTime.UtcNow:yyyyMMddHHmmss}_converted.pdf");
        var escapedInput = docxPath.Replace("\\", "\\\\");
        var escapedOutput = pdfPath.Replace("\\", "\\\\");

        var script = $@"
import sys
try:
    from docx2pdf import convert
    convert(r'{escapedInput}', r'{escapedOutput}')
    print('OK')
except Exception as e:
    print(f'ERR:{{e}}')";

        var scriptPath = Path.Combine(_tempDir, $"convert_{Guid.NewGuid()}.py");
        await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(cts.Token);

        var output = await process.StandardOutput.ReadToEndAsync();
        if (output.Contains("OK") && File.Exists(pdfPath))
            return pdfPath;

        Console.WriteLine($"[Word2PDF] Failed: {output}");
        return null;
    }

    public async Task<(string text, string? filePath)> ExtractBase64Async(string base64, string fileName, int deliveryId)
    {
        if (string.IsNullOrEmpty(base64)) return ("", null);

        var pure = base64.Contains(',') ? base64.Split(',')[1] : base64;
        var bytes = Convert.FromBase64String(pure);

        // 保存到临时目录用于提取文本
        var tempPath = Path.Combine(_tempDir, $"{Guid.NewGuid()}_{fileName}");
        await File.WriteAllBytesAsync(tempPath, bytes);

        // 保存到永久目录
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var permFileName = $"{deliveryId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var permPath = Path.Combine(_uploadsDir, permFileName);
        await File.WriteAllBytesAsync(permPath, bytes);

        // Word 文件自动转 PDF 以便浏览器预览
        string? finalPath = permPath;
        if (ext == ".docx" || ext == ".doc")
        {
            var pdfPath = await ConvertWordToPdfAsync(permPath, deliveryId);
            if (pdfPath != null)
                finalPath = pdfPath; // 预览/下载时优先用 PDF
        }

        var text = await ExtractTextAsync(tempPath);
        return (text, finalPath);
    }
}
