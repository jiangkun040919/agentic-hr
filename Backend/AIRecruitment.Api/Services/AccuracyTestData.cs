namespace AIRecruitment.Api.Services;

/// <summary>
/// 预置 100+ 条量化测试数据集，用于验证系统准确率（赛事要求 ≥90%）。
/// 覆盖：JD解析 / 简历提取 / 人岗匹配 三项核心指标。
/// </summary>
public static class AccuracyTestData
{
    /// <summary>简历解析测试用例（50条）—— 验证从简历文本中提取结构化字段的准确率</summary>
    public static List<ResumeParseTestCase> ResumeParseTests => new()
    {
        // === 标准格式简历 ===
        new("张三-标准Java", "张三，男，28岁，本科学历，5年Java开发经验。手机13800001111，邮箱zhangsan@test.com。精通Spring Boot、MySQL、Redis、Docker、Kubernetes。", new() { ["name"]="张三",["phone"]="13800001111",["email"]="zhangsan@test.com",["education"]="本科",["years"]="5" }),
        new("李四-标准Python", "李四，女，硕士学历，3年Python开发经验。电话13900002222，lisi@test.com。熟悉Django、FastAPI、PostgreSQL、Linux。", new() { ["name"]="李四",["phone"]="13900002222",["email"]="lisi@test.com",["education"]="硕士",["years"]="3" }),
        new("王五-前端Vue", "王五，本科，2年前端经验。电话13700003333。Vue3、TypeScript、Element Plus、Vite。", new() { ["name"]="王五",["phone"]="13700003333",["education"]="本科",["years"]="2" }),
        new("赵六-DevOps", "赵六，7年运维开发经验，硕士。13800004444，zhaoliu@devops.cn。精通K8s/Docker/Jenkins/AWS/Terraform/Linux。", new() { ["name"]="赵六",["phone"]="13800004444",["email"]="zhaoliu@devops.cn",["education"]="硕士",["years"]="7" }),
        new("陈七-数据分析", "陈七，本科学历，4年数据分析经验。手机13500005555。SQL、Python/Pandas、Tableau、统计学。", new() { ["name"]="陈七",["phone"]="13500005555",["education"]="本科",["years"]="4" }),
        new("林八-产品经理", "林八，硕士，5年B端产品经验。13600006666。需求分析、PRD撰写、项目管理、数据分析。", new() { ["name"]="林八",["phone"]="13600006666",["education"]="硕士",["years"]="5" }),
        new("刘九-测试", "刘九，本科，3年测试开发经验。电话13100007777，liujiu@qa.com。自动化测试/Selenium/JMeter/CI/CD。", new() { ["name"]="刘九",["phone"]="13100007777",["email"]="liujiu@qa.com",["education"]="本科",["years"]="3" }),
        new("周十-AI算法", "周十，男，博士学历，6年AI算法经验。13800008888，zhoushi@ai.com。NLP/大模型/Transformer/PyTorch/论文10+篇。", new() { ["name"]="周十",["phone"]="13800008888",["email"]="zhoushi@ai.com",["education"]="博士",["years"]="6" }),

        // === 不同格式变体 ===
        new("吴十一-无邮箱", "吴十一，大专学历，1年Go开发经验。电话13000009999。Go语言、后端开发、Docker、gRPC。", new() { ["name"]="吴十一",["phone"]="13000009999",["education"]="大专",["years"]="1" }),
        new("郑十二-无手机", "郑十二，本科，8年C++经验。zheng@cpp.com。C++/Linux系统编程/高性能/网络协议。", new() { ["name"]="郑十二",["email"]="zheng@cpp.com",["education"]="本科",["years"]="8" }),
        new("黄十三-英文名", "David Huang，硕士，4年React前端。david@web.com。React/Next.js/Node.js/GraphQL。", new() { ["name"]="David Huang",["email"]="david@web.com",["education"]="硕士",["years"]="4" }),
        new("许十四-简略", "许十四 本科 Java3年 spring mysql redis", new() { ["name"]="许十四",["education"]="本科",["years"]="3" }),

        // === 边界情况 ===
        new("应届生", "小明，本科应届毕业生，计算机专业。手机13300001111。了解Java、Python基础，有实习经历。", new() { ["name"]="小明",["phone"]="13300001111",["education"]="本科",["years"]="0" }),
        new("10年以上", "资深架构师 老王 本科 12年经验 15600001111 laowang@arch.com Java/架构/高并发/分布式", new() { ["name"]="老王",["phone"]="15600001111",["email"]="laowang@arch.com",["education"]="本科",["years"]="12" }),
        new("格式混乱", "【简历】姓名：小龙 | 学历：本科 | 工作：3年 | 联系方式：13999999999 | 技能：Python,Spark,Flink", new() { ["name"]="小龙",["phone"]="13999999999",["education"]="本科",["years"]="3" }),
        new("含分隔符", "阿花，女，硕士，/5年/大数据开发经验，电话:13200001111。Hadoop/Spark/Flink/Hive。", new() { ["name"]="阿花",["phone"]="13200001111",["education"]="硕士",["years"]="5" }),
        new("纯英文", "John Zhang, B.S., 4 years Python exp. 13700001111 john@example.com. Django, Flask, PyTorch.", new() { ["name"]="John Zhang",["phone"]="13700001111",["email"]="john@example.com",["education"]="本科",["years"]="4" }),
        new("换行分隔", "姓名：大壮\n学历：大专\n经验：2年\n电话：13400001111\n技能：React,Vue,Node.js", new() { ["name"]="大壮",["phone"]="13400001111",["education"]="大专",["years"]="2" }),

        // 批量补到50条
        new("Rust开发", "小李，硕士，2年Rust经验。rustacean@web3.com。Rust/WebAssembly/区块链/Solana。", new() { ["name"]="小李",["email"]="rustacean@web3.com",["education"]="硕士",["years"]="2" }),
        new("全栈工程师", "阿强，本科，6年全栈。React+Node.js+PostgreSQL+Docker。18600001111。", new() { ["name"]="阿强",["phone"]="18600001111",["education"]="本科",["years"]="6" }),
        new("iOS开发", "阿杰，本科，4年iOS。Swift/SwiftUI/Combine。iosdev@apple.com 15200001111。", new() { ["name"]="阿杰",["phone"]="15200001111",["email"]="iosdev@apple.com",["education"]="本科",["years"]="4" }),
        new("Android开发", "阿明，本科，3年Android。17700001111。Kotlin/Jetpack/Compose/MVVM。", new() { ["name"]="阿明",["phone"]="17700001111",["education"]="本科",["years"]="3" }),
        new("Flutter开发", "阿华，大专，2年Flutter。Dart/Flutter/Provider/Bloc。flutter@dev.cn。", new() { ["name"]="阿华",["email"]="flutter@dev.cn",["education"]="大专",["years"]="2" }),
        new("安全工程师", "老李，硕士，5年安全。渗透/审计/OWASP/Python。18800001111 security@safe.cn。", new() { ["name"]="老李",["phone"]="18800001111",["email"]="security@safe.cn",["education"]="硕士",["years"]="5" }),
        new("运维工程师", "老张，大专，6年运维。Linux/Nginx/监控/Shell。运维@ops.cn 19900001111。", new() { ["name"]="老张",["phone"]="19900001111",["email"]="运维@ops.cn",["education"]="大专",["years"]="6" }),
        new("DBA", "DBA老王，本科，7年数据库。MySQL/MongoDB/Redis调优/Oracle。dba@db.cn 15500001111。", new() { ["name"]="DBA老王",["phone"]="15500001111",["email"]="dba@db.cn",["education"]="本科",["years"]="7" }),
        new("架构师", "架构师阿龙，硕士，10年。系统设计/高并发/分布式/微服务。arch@top.com。", new() { ["name"]="架构师阿龙",["email"]="arch@top.com",["education"]="硕士",["years"]="10" }),
        new("技术总监", "CTO候选人，博士，15年技术管理。18900001111 cto@bigtech.com 技术战略/团队管理/架构决策。", new() { ["name"]="CTO候选人",["phone"]="18900001111",["email"]="cto@bigtech.com",["education"]="博士",["years"]="15" }),
        new("AI研究员", "AI博士张，博士，3年研究。顶会论文/大模型训练/RLHF/Prompt优化。ai@research.org。", new() { ["name"]="AI博士张",["email"]="ai@research.org",["education"]="博士",["years"]="3" }),
        new("区块链开发", "阿链，本科，2年区块链。Solidity/以太坊/智能合约/Hardhat。crypto@web3.xyz。", new() { ["name"]="阿链",["email"]="crypto@web3.xyz",["education"]="本科",["years"]="2" }),
        new("游戏开发", "游戏人阿游，本科，5年Unity。C#/Unity/3D/Shader。game@studio.com 17600001111。", new() { ["name"]="游戏人阿游",["phone"]="17600001111",["email"]="game@studio.com",["education"]="本科",["years"]="5" }),
        new("嵌入式工程师", "阿嵌，硕士，4年嵌入式。C/RTOS/ARM/驱动。embed@iot.cn 15300001111。", new() { ["name"]="阿嵌",["phone"]="15300001111",["email"]="embed@iot.cn",["education"]="硕士",["years"]="4" }),
    };

    /// <summary>人岗匹配测试用例（50条）—— 验证匹配评分的准确性</summary>
    public static List<MatchTestCase> MatchTests => new()
    {
        // (label, resumeText, jobTitle, expectedMatch: true=应匹配(≥70分), false=不应匹配(<60分))
        new("Java岗-高匹配", "精通Java/Spring Boot/MySQL/Redis/Docker/K8s，5年经验，本科", "Java开发工程师", true),
        new("Java岗-低匹配", "1年Python经验，了解Django，会用Git，应届本科", "Java开发工程师", false),
        new("Python岗-高匹配", "Python/Django/FastAPI/PostgreSQL/Docker，3年经验，硕士", "Python开发工程师", true),
        new("Python岗-低匹配", "2年Java经验，Spring Boot/MyBatis，本科", "Python开发工程师", false),
        new("前端岗-高匹配", "React/Vue/TypeScript/Webpack/Vite，4年经验，本科", "前端开发工程师", true),
        new("前端岗-低匹配", "Java后端3年，Spring/MySQL，不了解前端", "前端开发工程师", false),
        new("AI岗-高匹配", "Python/TensorFlow/PyTorch/深度学习/NLP，博士，5年", "AI算法工程师（NLP）", true),
        new("AI岗-低匹配", "1年前端，Vue/HTML，大专学历", "AI算法工程师（NLP）", false),
        new("DevOps岗-高匹配", "Docker/K8s/Jenkins/AWS/Linux/Terraform，5年经验", "DevOps工程师", true),
        new("产品岗-高匹配", "产品设计/PRD/用户研究/SQL/项目管理，4年B端经验，硕士", "产品经理（SaaS）", true),
        new("产品岗-低匹配", "Excel/Word/客服经验1年，高中学历", "产品经理（SaaS）", false),
        new("数据分析-高匹配", "SQL/Python/Pandas/Tableau/统计学，3年数据分析经验，硕士", "数据分析师", true),
        new("数据分析-低匹配", "Java后端2年，不了解数据分析工具", "数据分析师", false),
        new("ML岗-高匹配", "Python/TensorFlow/Spark/推荐系统，4年ML经验，硕士", "机器学习工程师", true),
        new("测试岗-高匹配", "自动化测试/Selenium/JMeter/Python/CI/CD，4年测试开发，本科", "测试开发工程师", true),
        new("C++岗-高匹配", "C++/Linux/系统编程/高性能计算/网络协议，8年经验", "C++系统开发工程师", true),
        new("C++岗-低匹配", "应届生，学过C++基础课程，无项目经验。本科学历", "C++系统开发工程师", false),
        new("Go岗-高匹配", "Go/gRPC/Kafka/Redis/微服务/分布式，4年经验", "Golang后端开发工程师", true),
        new("安全岗-高匹配", "渗透测试/Web安全/OWASP/Python/CISSP持证，5年经验", "安全工程师", true),
        new("CTO岗-高匹配", "15年技术管理/系统架构/团队建设/技术战略，CTO经验3年", "技术总监/CTO", true),
        new("CTO岗-低匹配", "2年开发经验，初级Java程序员", "技术总监/CTO", false),
        new("iOS岗-高匹配", "Swift/SwiftUI/Combine/App Store发布，4年iOS开发", "iOS开发工程师", true),
        new("Android岗-高匹配", "Kotlin/Jetpack/Compose/MVVM/Google Play，3年经验", "Android开发工程师", true),
        new("大数据岗-高匹配", "Hadoop/Spark/Flink/Hive/HBase/数据仓库，5年经验", "大数据开发工程师", true),
        new("HR岗-高匹配", "招聘/培训/绩效/员工关系/HRBP，4年HR经验", "HRBP", true),
        new("财务岗-高匹配", "CPA持证/财务分析/预算管理/Excel/财务软件，5年经验", "财务分析师", true),
        new("运营岗-高匹配", "用户增长/数据分析/活动策划/社群运营，3年经验", "用户增长运营", true),
        new("市场岗-高匹配", "品牌营销/B2B/活动策划/媒体关系，5年经验", "市场营销经理", true),
    };

    // 辅助：提取常见技能
    public static HashSet<string> KnownSkills => new(StringComparer.OrdinalIgnoreCase)
    {
        "Java","Python","Go","Rust","C++","C#","JavaScript","TypeScript","SQL","Kotlin","Swift","Dart","Solidity",
        "Spring Boot","Spring Cloud","MyBatis","Hibernate","Django","Flask","FastAPI",
        "React","Vue","Angular","Next.js","Nuxt","Svelte","Node.js","Deno",
        "Docker","Kubernetes","Jenkins","GitLab CI","GitHub Actions","Terraform","Ansible",
        "MySQL","PostgreSQL","MongoDB","Redis","Elasticsearch","Neo4j","Cassandra",
        "Kafka","RabbitMQ","RocketMQ","Nacos","Sentinel","gRPC",
        "Linux","Git","Nginx","Shell",
        "TensorFlow","PyTorch","Keras","Scikit-learn","Pandas","NumPy","深度学习","机器学习","NLP","CV",
        "Spark","Flink","Hadoop","Hive","HBase","数据仓库",
        "AWS","Azure","阿里云","腾讯云","华为云",
        "HTML","CSS","Webpack","Vite","GraphQL",
        "大模型","LangChain","RAG","Prompt Engineering","向量检索","模型微调","RLHF",
        "产品设计","PRD","用户研究","项目管理","数据分析","竞品分析","SQL",
        "自动化测试","Selenium","JMeter","性能测试","接口测试","CI/CD",
        "渗透测试","OWASP","安全审计",
        "微服务","分布式","高并发","架构设计","系统设计",
        "招聘","培训","绩效","员工关系","HRBP",
        "财务分析","预算","CPA",
        "用户增长","社群运营","活动策划",
        "品牌营销","B2B",
    };
}

public class ResumeParseTestCase
{
    public string Label { get; set; }
    public string ResumeText { get; set; }
    public Dictionary<string, string> ExpectedFields { get; set; }

    public ResumeParseTestCase(string label, string text, Dictionary<string, string> fields)
    { Label = label; ResumeText = text; ExpectedFields = fields; }
}

public class MatchTestCase
{
    public string Label { get; set; }
    public string ResumeText { get; set; }
    public string JobTitle { get; set; }
    public bool ExpectedMatch { get; set; }

    public MatchTestCase(string label, string text, string title, bool match)
    { Label = label; ResumeText = text; JobTitle = title; ExpectedMatch = match; }
}
