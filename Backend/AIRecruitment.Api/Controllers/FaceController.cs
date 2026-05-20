using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/face")]
public class FaceController : ControllerBase
{
    private readonly ILogger<FaceController> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly string[] Expressions = { "angry", "disgust", "fear", "happy", "sad", "surprise", "neutral" };
    private static readonly string[] ExpressionLabels = { "愤怒", "厌恶", "恐惧", "高兴", "悲伤", "惊讶", "平静" };

    public FaceController(
        ILogger<FaceController> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 表情分析 — 接收前端截图 base64，调用腾讯云 DetectFace API
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeExpression([FromBody] FaceAnalyzeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ImageBase64))
                return Ok(new { code = 400, message = "图片数据不能为空" });

            // 确保 base64 不含前缀
            var pureBase64 = request.ImageBase64;
            if (pureBase64.Contains(','))
                pureBase64 = pureBase64.Split(',')[1];

            var secretId = _config["TencentCloud:SecretId"];
            var secretKey = _config["TencentCloud:SecretKey"];
            var region = _config["TencentCloud:FaceRegion"] ?? "ap-guangzhou";

            if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                return Ok(new { code = 500, message = "腾讯云密钥未配置" });

            var payload = new
            {
                Image = pureBase64,
                NeedFaceAttributes = 1,
                NeedQualityDetection = 0
            };

            var host = $"iai.tencentcloudapi.com";
            var service = "iai";
            var action = "DetectFace";
            var version = "2020-03-03";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var dateStr = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

            var bodyJson = JsonSerializer.Serialize(payload);

            // TC3-HMAC-SHA256 签名
            var credentialScope = $"{dateStr}/{service}/tc3_request";
            var hashedPayload = Sha256Hex(bodyJson);
            var hashedParams = Sha256Hex("");

            var canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{host}\nx-tc-action:{action.ToLowerInvariant()}\n";
            var signedHeaders = "content-type;host;x-tc-action";
            var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{hashedParams}\n{hashedPayload}";

            var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

            var secretDate = HmacSha256Raw($"TC3{secretKey}", dateStr);
            var secretService = HmacSha256Raw(secretDate, service);
            var secretSigning = HmacSha256Raw(secretService, "tc3_request");
            var signature = HexEncode(HmacSha256Raw(secretSigning, stringToSign));

            var authorization = $"TC3-HMAC-SHA256 Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

            using var client = _httpClientFactory.CreateClient();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{host}")
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Authorization", authorization);
            httpRequest.Headers.Add("X-TC-Action", action);
            httpRequest.Headers.Add("X-TC-Version", version);
            httpRequest.Headers.Add("X-TC-Timestamp", timestamp.ToString());
            httpRequest.Headers.Add("X-TC-Region", region);

            var response = await client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("腾讯云 DetectFace 响应(前300字): {body}",
                responseBody.Length > 300 ? responseBody[..300] : responseBody);

            var result = JsonDocument.Parse(responseBody);

            if (result.RootElement.TryGetProperty("Response", out var resp)
                && resp.TryGetProperty("Error", out var error))
            {
                var errMsg = error.GetProperty("Message").GetString() ?? "未知错误";
                _logger.LogWarning("腾讯云人脸检测失败: {msg}", errMsg);
                return Ok(new { code = 500, message = $"表情分析失败: {errMsg}" });
            }

            // 解析表情结果
            if (resp.TryGetProperty("FaceInfos", out var faceInfos)
                && faceInfos.GetArrayLength() > 0)
            {
                var face = faceInfos[0];
                var expression = new Dictionary<string, object>();

                if (face.TryGetProperty("FaceAttributesInfo", out var attrs))
                {
                    // 表情
                    if (attrs.TryGetProperty("Expression", out var exprEl))
                    {
                        var exprType = exprEl.GetInt32(); // 0=angry, 1=disgust, 2=fear, 3=happy, 4=sad, 5=surprise, 6=neutral, 7=...
                        var exprLabel = exprType < ExpressionLabels.Length ? ExpressionLabels[exprType] : "未知";
                        expression["expression"] = exprLabel;
                        expression["expressionType"] = exprType;
                    }

                    // 眼睛
                    if (attrs.TryGetProperty("EyeOpen", out var eyeEl))
                    {
                        expression["eyeOpen"] = eyeEl.GetInt32() == 1;
                    }

                    // 头部姿态
                    if (attrs.TryGetProperty("HeadPose", out var poseEl))
                    {
                        expression["headYaw"] = poseEl.GetProperty("Yaw").GetDouble();
                        expression["headPitch"] = poseEl.GetProperty("Pitch").GetDouble();
                        expression["headRoll"] = poseEl.GetProperty("Roll").GetDouble();
                    }

                    // 性别年龄
                    if (attrs.TryGetProperty("Gender", out var genderEl))
                        expression["gender"] = genderEl.GetInt32() == 0 ? "男" : "女";
                    if (attrs.TryGetProperty("Age", out var ageEl))
                        expression["age"] = ageEl.GetInt32();

                    // 眼镜
                    if (attrs.TryGetProperty("Glass", out var glassEl))
                        expression["glass"] = glassEl.GetInt32() == 1;
                }

                return Ok(new
                {
                    code = 200,
                    data = expression
                });
            }

            return Ok(new { code = 200, data = new { expression = "未检测到人脸", expressionType = -1 } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "表情分析异常");
            return Ok(new { code = 500, message = "表情分析服务异常" });
        }
    }

    // ── 签名工具 ─────────────────────────────────────────
    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return HexEncode(bytes);
    }

    private static byte[] HmacSha256Raw(string key, string data)
    {
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
    }

    private static byte[] HmacSha256Raw(byte[] key, string data)
    {
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
    }

    private static string HmacSha256Hex(string key, string data)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        return HexEncode(hash);
    }

    private static string HexEncode(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record FaceAnalyzeRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("imageBase64")] string ImageBase64);
