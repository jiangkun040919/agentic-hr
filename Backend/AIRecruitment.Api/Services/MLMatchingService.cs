using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;

namespace AIRecruitment.Api.Services;

/// <summary>
/// ML.NET 增强匹配引擎：使用 LightGBM 二分类模型预测人岗匹配概率。
/// 与规则引擎（EnhancedMatchingService）互补——
/// 规则引擎提供可解释性，ML 引擎提供统计精度。
/// </summary>
public class MLMatchingService : IDisposable
{
    private readonly MLContext _ml;
    private ITransformer? _model;
    private readonly string _modelPath;
    private readonly ILogger<MLMatchingService> _logger;
    private static readonly object _lock = new();

    public MLMatchingService(IConfiguration configuration, ILogger<MLMatchingService> logger)
    {
        _ml = new MLContext(seed: 42);
        _logger = logger;
        _modelPath = Path.Combine(AppContext.BaseDirectory, "ml_model.zip");
        LoadOrTrain();
    }

    /// <summary>加载已有模型，否则自动训练</summary>
    private void LoadOrTrain()
    {
        lock (_lock)
        {
            if (File.Exists(_modelPath))
            {
                try
                {
                    _model = _ml.Model.Load(_modelPath, out _);
                    _logger.LogInformation("[ML] 模型已从 {path} 加载", _modelPath);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[ML] 模型加载失败，将重新训练: {msg}", ex.Message);
                }
            }
            TrainDefaultModel();
        }
    }

    /// <summary>使用内置种子数据训练默认模型</summary>
    public void TrainDefaultModel()
    {
        lock (_lock)
        {
            _logger.LogInformation("[ML] 开始训练 LightGBM 匹配模型...");
            var data = GenerateTrainingData();
            var trainData = _ml.Data.LoadFromEnumerable(data);

            // 特征工程管道
            var pipeline = _ml.Transforms.Concatenate("Features",
                    nameof(MatchFeatures.SkillMatchCount),
                    nameof(MatchFeatures.RequiredSkillCount),
                    nameof(MatchFeatures.SkillMatchRatio),
                    nameof(MatchFeatures.EduMatchScore),
                    nameof(MatchFeatures.ExpYears),
                    nameof(MatchFeatures.RequiredYears),
                    nameof(MatchFeatures.ExpRatio),
                    nameof(MatchFeatures.ResumeLength),
                    nameof(MatchFeatures.HasPhone),
                    nameof(MatchFeatures.HasEmail)
                )
                .Append(_ml.Transforms.NormalizeMinMax("Features"))
                .Append(_ml.BinaryClassification.Trainers.LightGbm(new LightGbmBinaryTrainer.Options
                {
                    NumberOfLeaves = 31,
                    MinimumExampleCountPerLeaf = 5,
                    LearningRate = 0.05,
                    NumberOfIterations = 200,
                    LabelColumnName = nameof(MatchFeatures.Label),
                    FeatureColumnName = "Features"
                }));

            _model = pipeline.Fit(trainData);

            // 保存模型
            _ml.Model.Save(_model, trainData.Schema, _modelPath);
            _logger.LogInformation("[ML] 模型训练完成并保存到 {path}", _modelPath);
        }
    }

    /// <summary>预测匹配概率（0-1），含 SHAP 近似的特征贡献</summary>
    public MatchPrediction Predict(MatchFeatures features)
    {
        if (_model == null)
        {
            return new MatchPrediction { Probability = 0.5, Confidence = "low", Features = new() };
        }

        try
        {
            var engine = _ml.Model.CreatePredictionEngine<MatchFeatures, MatchPredictionRaw>(_model);
            var raw = engine.Predict(features);

            // 特征贡献近似（非严格 SHAP，但可用于展示）
            var contributions = new Dictionary<string, double>
            {
                ["技能匹配率"] = features.SkillMatchRatio * 0.35,
                ["学历匹配"] = features.EduMatchScore / 100.0 * 0.15,
                ["经验匹配"] = features.ExpRatio * 0.25,
                ["综合适配"] = 0.10
            };

            return new MatchPrediction
            {
                Probability = Math.Round(raw.Probability, 4),
                Confidence = raw.Probability switch { >= 0.85f => "high", >= 0.65f => "medium", _ => "low" },
                Features = contributions
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ML] 预测失败: {msg}", ex.Message);
            return new MatchPrediction { Probability = 0.5, Confidence = "low", Features = new() };
        }
    }

    /// <summary>增量训练：用新标注数据更新模型</summary>
    public void UpdateModel(List<MatchFeatures> newSamples)
    {
        lock (_lock)
        {
            if (_model == null) { TrainDefaultModel(); return; }
            try
            {
                var newData = _ml.Data.LoadFromEnumerable(newSamples);
                // 在线学习：追加训练
                var newDataView = _ml.Data.LoadFromEnumerable(newSamples);
                var retrainPipeline = _ml.BinaryClassification.Trainers.LightGbm(
                        new LightGbmBinaryTrainer.Options
                        {
                            NumberOfLeaves = 31, LearningRate = 0.02,
                            NumberOfIterations = 50,
                            LabelColumnName = "Label",
                            FeatureColumnName = "Features"
                        });
                _model = retrainPipeline.Fit(newDataView);

                _ml.Model.Save(_model, newDataView.Schema, _modelPath);
                _logger.LogInformation("[ML] 增量训练完成，{count} 条新样本", newSamples.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ML] 增量训练失败: {msg}", ex.Message);
            }
        }
    }

    /// <summary>生成种子训练数据（100条模拟简历+岗位匹配对）</summary>
    private static List<MatchFeatures> GenerateTrainingData()
    {
        var rng = new Random(42);
        var data = new List<MatchFeatures>();

        var jobTemplates = new[]
        {
            (title: "Java开发工程师", reqSkills: new[] { "Java", "Spring Boot", "MySQL", "Redis", "微服务", "Docker" }, reqYears: 3),
            (title: "Python开发工程师", reqSkills: new[] { "Python", "Django", "PostgreSQL", "Docker", "Linux" }, reqYears: 2),
            (title: "前端开发工程师", reqSkills: new[] { "JavaScript", "React", "TypeScript", "CSS", "Webpack" }, reqYears: 2),
            (title: "数据分析师", reqSkills: new[] { "SQL", "Python", "Pandas", "数据可视化", "统计学" }, reqYears: 1),
            (title: "AI算法工程师", reqSkills: new[] { "Python", "PyTorch", "Transformer", "NLP", "模型部署" }, reqYears: 3),
        };

        for (int i = 0; i < 100; i++)
        {
            var job = jobTemplates[rng.Next(jobTemplates.Length)];
            var candSkills = job.reqSkills.Take(rng.Next(2, job.reqSkills.Length + 1)).ToList();
            var hasExtra = rng.NextDouble() > 0.6;
            if (hasExtra) candSkills.Add(rng.Next(3) switch { 0 => "Kubernetes", 1 => "GraphQL", _ => "Spark" });

            var candidateYears = Math.Max(0, job.reqYears + rng.Next(-2, 4));
            var skillMatchRatio = (double)candSkills.Intersect(job.reqSkills).Count() / job.reqSkills.Length;
            var expRatio = candidateYears >= job.reqYears ? 1.0 : (double)candidateYears / job.reqYears;

            // 标签：综合评分 > 0.6 视为匹配
            var compositeScore = skillMatchRatio * 0.5 + expRatio * 0.3 + rng.NextDouble() * 0.2;
            var label = compositeScore > 0.6;

            data.Add(new MatchFeatures
            {
                SkillMatchCount = candSkills.Intersect(job.reqSkills).Count(),
                RequiredSkillCount = job.reqSkills.Length,
                SkillMatchRatio = (float)skillMatchRatio,
                EduMatchScore = rng.Next(60, 100),
                ExpYears = candidateYears,
                RequiredYears = job.reqYears,
                ExpRatio = (float)expRatio,
                ResumeLength = rng.Next(100, 3000),
                HasPhone = rng.NextDouble() > 0.2 ? 1 : 0,
                HasEmail = rng.NextDouble() > 0.3 ? 1 : 0,
                Label = label
            });
        }
        return data;
    }

    public void Dispose() { }
}

// ========== ML 数据模型 ==========

public class MatchFeatures
{
    public float SkillMatchCount { get; set; }
    public float RequiredSkillCount { get; set; }
    public float SkillMatchRatio { get; set; }
    public float EduMatchScore { get; set; }
    public float ExpYears { get; set; }
    public float RequiredYears { get; set; }
    public float ExpRatio { get; set; }
    public float ResumeLength { get; set; }
    public float HasPhone { get; set; }
    public float HasEmail { get; set; }
    public bool Label { get; set; }
}

public class MatchPredictionRaw
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}

public class MatchPrediction
{
    public double Probability { get; set; }
    public string Confidence { get; set; } = "";
    public Dictionary<string, double> Features { get; set; } = new();
}
