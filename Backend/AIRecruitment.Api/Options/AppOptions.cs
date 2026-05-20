namespace AIRecruitment.Api.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "AIRecruitment";
    public int ExpiryDays { get; set; } = 7;
}

public class AIOptions
{
    public const string Section = "AI";
    public string Provider { get; set; } = "minimax";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "MiniMax-M2.7";
    public string BaseUrl { get; set; } = "https://api.minimax.chat/v1";
    public bool UseMock { get; set; }
}

public class MinIOOptions
{
    public const string Section = "MinIO";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "recruitment";
}

public class TencentCloudOptions
{
    public const string Section = "TencentCloud";
    public string SecretId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string FaceRegion { get; set; } = "ap-guangzhou";
}

public class Neo4jOptions
{
    public const string Section = "Neo4j";
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string User { get; set; } = "neo4j";
    public string Password { get; set; } = "password";
}
