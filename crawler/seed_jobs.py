"""
Generate 100+ realistic IT job listings and push to backend API.
Run: python seed_jobs.py
"""
import random
import httpx

API_URL = 'http://localhost:5000/api/job/batch-import'
API_KEY = 'a3f8b2c1-d4e5-4f6g-7h8i-9j0k1l2m3n4o'
BATCH_SIZE = 20

CITIES = ['北京', '上海', '广州', '深圳', '杭州', '成都', '武汉', '南京', '西安', '苏州']

# Rich job templates based on real IT job market
JOB_TEMPLATES = [
    # (title, dept, salary_min, salary_max, jd, requirements)
    ('Java高级开发工程师', '技术部', 25, 45,
     '负责核心业务系统架构设计与开发；参与微服务架构演进，提升系统可扩展性；编写高质量代码，进行Code Review；优化系统性能，解决高并发场景下的技术难题。',
     '5年以上Java开发经验，精通Spring Boot/Spring Cloud；熟悉MySQL、Redis、Kafka等中间件；有分布式系统设计经验；良好的编码习惯和团队协作能力。'),

    ('Python后端开发工程师', '技术部', 20, 40,
     '负责AI平台后端服务开发与维护；设计RESTful API接口；参与数据处理流水线建设；编写单元测试与集成测试。',
     '3年以上Python开发经验，熟悉Django/FastAPI；熟练使用PostgreSQL、MongoDB；了解Docker、K8s部署；有AI/ML项目经验者优先。'),

    ('前端开发工程师（React）', '技术部', 18, 35,
     '负责WEB端核心页面开发；组件库设计与维护；前端性能优化；与后端协作完成产品迭代。',
     '3年以上前端开发经验，精通React/TypeScript；熟悉Webpack/Vite构建工具；有组件库开发经验；了解Node.js服务端渲染。'),

    ('前端开发工程师（Vue）', '技术部', 15, 32,
     '负责公司中后台管理系统开发；参与前端基础设施建设；编写可复用组件和业务模块。',
     '2年以上Vue开发经验，熟悉Element Plus/Ant Design Vue；掌握TypeScript；了解前端工程化；有小程序开发经验优先。'),

    ('AI算法工程师（NLP）', 'AI部', 35, 70,
     '负责自然语言处理算法研发；大模型微调与部署；对话系统与文本理解优化；跟踪前沿技术并落地。',
     '硕士及以上学历，NLP/CV方向；熟练使用PyTorch/TensorFlow；有大模型训练或微调经验；在ACL/EMNLP等发表论文者优先。'),

    ('AI算法工程师（CV）', 'AI部', 35, 65,
     '负责计算机视觉算法研发；目标检测、图像分割模型优化；模型轻量化与部署；参与AI产品落地。',
     '硕士及以上学历，计算机视觉方向；精通PyTorch、OpenCV；有模型部署经验（TensorRT/ONNX）；3年以上相关经验。'),

    ('大模型应用开发工程师', 'AI部', 30, 60,
     '负责大语言模型应用层开发；Prompt Engineering优化；RAG系统构建与优化；Agent框架设计与实现。',
     '熟悉LangChain/LlamaIndex等框架；有向量数据库使用经验（Milvus/Pinecone等）；了解Prompt优化技巧；Python编程能力强。'),

    ('机器学习工程师', 'AI部', 28, 55,
     '负责推荐系统算法优化；用户画像构建；AB实验设计与分析；模型效果评估与迭代。',
     '3年以上ML工程经验；熟悉推荐系统常用算法；掌握Spark/Flink大数据处理；有电商/内容推荐经验者优先。'),

    ('深度学习框架研发工程师', 'AI部', 40, 80,
     '负责自研深度学习框架核心模块开发；算子优化与编译器开发；分布式训练系统设计。',
     '精通C++/CUDA编程；深入理解深度学习框架原理；有TVM/XLA等编译器经验；博士学历优先。'),

    ('数据分析师', '数据部', 12, 25,
     '负责业务数据分析与洞察；搭建数据看板和报表体系；参与数据仓库建设；为产品和运营提供数据决策支持。',
     '2年以上数据分析经验；熟练使用SQL、Python；熟悉Tableau/FineBI等BI工具；统计学基础扎实。'),

    ('大数据开发工程师', '数据部', 25, 50,
     '负责大数据平台建设与维护；ETL流程开发与优化；数据仓库建模；实时数据处理系统开发。',
     '3年以上大数据开发经验；精通Hadoop/Spark/Flink；熟悉Hive/HBase/Kafka；有数据治理经验优先。'),

    ('数据仓库工程师', '数据部', 22, 45,
     '负责企业级数据仓库设计与建设；数据模型设计；ETL任务调度与监控；数据质量保障。',
     '熟悉维度建模方法论；有阿里云MaxCompute/DataWorks使用经验；SQL能力优秀；有数据治理项目经验。'),

    ('数据科学家', '数据部', 30, 60,
     '负责利用统计学和机器学习方法解决业务问题；用户行为建模；预测模型开发；与业务团队紧密合作推动数据驱动决策。',
     '统计/数学/计算机相关硕士以上学历；精通Python/R；有完整的数据科学项目经验；优秀的沟通能力。'),

    ('产品经理（SaaS）', '产品部', 22, 45,
     '负责企业级SaaS产品规划与设计；用户需求调研与分析；产品路线图制定；协调研发、设计推动产品迭代。',
     '3年以上B端产品经验；有SaaS产品0-1经验优先；具备数据驱动决策意识；优秀的沟通协调能力。'),

    ('产品经理（AI方向）', '产品部', 25, 50,
     '负责AI产品的需求分析、功能设计与落地；调研AI行业趋势和竞品；制定AI产品Roadmap；推动算法与工程团队协作。',
     '了解AI/ML技术原理；有AI产品落地经验；数据敏感度高；有技术背景者优先。'),

    ('高级产品经理', '产品部', 28, 55,
     '负责核心产品线整体规划；带领产品团队完成产品迭代；竞品分析与市场洞察；推动跨部门协作。',
     '5年以上产品经验；有团队管理经验；有成功的产品案例；互联网大厂经验优先。'),

    ('DevOps工程师', '技术部', 25, 50,
     '负责CI/CD流水线建设与维护；容器化平台管理；监控告警体系搭建；基础设施即代码（IaC）实践。',
     '3年以上DevOps经验；精通Kubernetes/Docker；熟悉Jenkins/GitLab CI；掌握Terraform/Ansible；有AWS/阿里云运维经验。'),

    ('SRE运维工程师', '技术部', 28, 55,
     '负责线上服务稳定性保障；故障排查与根因分析；容量规划与性能优化；自动化运维工具开发。',
     '5年以上运维经验；深入理解Linux系统；熟悉Prometheus/Grafana监控体系；有Go/Python脚本开发能力。'),

    ('云平台架构师', '技术部', 40, 75,
     '负责公司云原生架构设计；多活容灾方案规划；技术选型与POC验证；指导团队技术成长。',
     '8年以上架构经验；精通AWS/阿里云/腾讯云至少一种；有大规模分布式系统设计经验；有跨云迁移项目经验。'),

    ('测试开发工程师', '技术部', 18, 35,
     '负责自动化测试框架搭建；编写接口自动化测试用例；性能测试与压力测试；参与CI/CD质量卡点建设。',
     '3年以上测试开发经验；精通Python/Java至少一种；熟悉Selenium/Pytest/JMeter；有持续集成测试经验。'),

    ('安全工程师', '技术部', 25, 50,
     '负责应用安全评估与渗透测试；安全体系建设；安全漏洞跟踪与修复；安全编码规范制定。',
     '3年以上安全领域经验；熟悉Web安全/移动安全；持有CISSP/CISP等证书优先；有CTF经验者加分。'),

    ('Golang后端开发工程师', '技术部', 22, 45,
     '负责高并发基础服务开发；API网关设计与实现；微服务中间件开发；性能优化与问题排查。',
     '3年以上Go开发经验；熟悉Gin/Echo等框架；深入理解并发编程；有中间件开发经验优先。'),

    ('C++系统开发工程师', '技术部', 30, 60,
     '负责高性能计算引擎开发；底层存储系统优化；网络协议栈开发；系统级性能调优。',
     '5年以上C++开发经验；熟悉Linux系统编程；对数据结构和算法有深入理解；有数据库/存储系统开发经验优先。'),

    ('Unity/UE游戏开发工程师', '技术部', 20, 40,
     '负责游戏客户端功能开发；游戏引擎工具链开发；渲染效果优化；与美术、策划团队协作。',
     '2年以上游戏开发经验；精通Unity或Unreal Engine；熟悉C#/C++；了解计算机图形学基础。'),

    ('iOS开发工程师', '技术部', 22, 45,
     '负责iOS客户端架构设计与开发；性能优化与内存管理；新技术预研与落地；App Store发布管理。',
     '3年以上iOS开发经验；精通Swift/Objective-C；熟悉iOS系统框架；有知名App开发经验优先。'),

    ('Android开发工程师', '技术部', 22, 45,
     '负责Android客户端功能迭代；组件化架构设计；性能与稳定性优化；Google Play/国内应用商店发布。',
     '3年以上Android开发经验；精通Kotlin/Java；熟悉Jetpack组件库；有大型App开发经验优先。'),

    ('Flutter跨平台开发工程师', '技术部', 20, 42,
     '负责Flutter跨平台应用开发；组件库设计与维护；原生插件开发；应用性能调优。',
     '2年以上Flutter开发经验；熟悉Dart语言；有原生Android或iOS开发经验；有已上线的Flutter项目。'),

    ('区块链开发工程师', '技术部', 30, 60,
     '负责区块链核心协议开发；智能合约编写与审计；DeFi应用研发；链上数据分析。',
     '3年以上区块链开发经验；精通Solidity/Rust；熟悉EVM原理；有知名DeFi项目经验优先。'),

    ('技术总监/CTO', '技术部', 50, 100,
     '负责公司整体技术战略规划；技术团队建设与管理；技术架构决策；推动技术创新与工程文化。',
     '10年以上技术经验，3年以上技术管理经验；有过成功的大规模系统设计经历；优秀的领导力和沟通力。'),

    ('运营经理', '运营部', 15, 30,
     '负责产品运营策略制定与执行；用户增长与活跃度提升；活动策划与数据分析；社群运营管理。',
     '3年以上互联网运营经验；数据驱动思维；优秀的活动策划能力；有B端运营经验优先。'),

    ('用户增长运营', '运营部', 12, 25,
     '负责用户增长策略制定；渠道投放优化；用户生命周期管理；增长实验设计与分析。',
     '2年以上增长运营经验；熟悉主流投放渠道；有数据分析能力；有成功增长案例优先。'),

    ('内容运营专员', '运营部', 10, 20,
     '负责产品内容策划与编辑；公众号/视频号内容运营；SEO优化；用户教育和引导内容产出。',
     '1年以上内容运营经验；优秀的文字功底；了解SEO基础知识；有短视频制作能力加分。'),

    ('市场营销经理', '市场部', 18, 35,
     '负责品牌营销策略制定；线上线下活动策划执行；媒体关系维护；市场预算管理。',
     '5年以上市场营销经验；有B2B营销经验优先；优秀的项目管理能力；有供应商管理经验。'),

    ('商务拓展经理（BD）', '市场部', 15, 35,
     '负责企业客户开拓与维护；商务谈判与合同签署；合作方案制定；客户关系管理。',
     '3年以上商务拓展经验；有企业服务销售经验优先；优秀的沟通谈判能力；能独立完成销售闭环。'),

    ('财务分析师', '财务部', 12, 25,
     '负责公司财务数据分析与报告；预算编制与执行跟踪；业务线财务模型搭建；为管理层提供决策支持。',
     '2年以上财务分析经验；持有CPA/CMA等证书优先；精通Excel/财务软件；有互联网行业经验优先。'),

    ('HRBP', '人力资源部', 12, 25,
     '负责业务部门人力资源支持；招聘、培训、绩效管理；员工关系维护；组织文化建设。',
     '3年以上HR经验；有HRBP经验优先；熟悉互联网行业人才市场；优秀的沟通协调能力。'),

    ('React前端架构师', '技术部', 30, 55,
     '负责大型前端项目架构设计；微前端方案落地；前端工程化体系建设；性能优化与监控方案制定。',
     '5年以上前端经验；精通React生态；有微前端/模块联邦实践经验；有团队技术管理经验。'),

    ('全栈开发工程师', '技术部', 20, 42,
     '负责全栈应用开发，前后端均可独立完成；快速原型开发；技术方案设计；数据库设计与优化。',
     '3年以上全栈开发经验；熟悉React/Vue + Node.js/Python技术栈；有独立项目开发能力；了解CI/CD流程。'),

    ('知识图谱工程师', 'AI部', 30, 60,
     '负责领域知识图谱构建；实体识别与关系抽取；图算法研发与优化；知识推理与问答系统开发。',
     '熟悉知识图谱构建流程；精通Neo4j/JanusGraph等图数据库；有NLP基础；硕士及以上学历优先。'),

    ('AIGC应用开发工程师', 'AI部', 28, 55,
     '负责AIGC产品研发，包括文生图、文生视频等应用开发；Diffusion模型应用与调优；多模态AI系统搭建。',
     '了解Stable Diffusion/Midjourney等主流模型；有AIGC产品落地经验；Python编程能力强；对新技术有热情。'),

    ('算法研究员', 'AI部', 45, 90,
     '负责前沿AI算法研究；发表高水平学术论文；将研究成果转化为产品能力；与工程团队协作推动落地。',
     '博士学历，AI/ML方向；在顶会/顶刊有论文发表；有独立研究能力；有工业界研究经验优先。'),

    ('ETL数据开发工程师', '数据部', 18, 38,
     '负责企业数据集成与ETL流程开发；数据清洗与质量监控；数据仓库模型设计与维护；BI报表数据支撑。',
     '2年以上ETL开发经验；精通SQL和数据建模；有Kettle/DataX等工具使用经验；有阿里云/华为云数据服务使用经验。'),

    ('UI/UX设计师', '产品部', 15, 30,
     '负责B端产品界面设计；设计系统组件库维护；用户交互流程优化；设计规范制定与推广。',
     '3年以上UI设计经验；精通Figma/Sketch；有B端产品设计经验；有设计系统搭建经验优先。'),

    ('技术项目经理（TPM）', '技术部', 28, 55,
     '负责跨团队技术项目管理和交付；项目规划、进度跟踪与风险管理；敏捷开发流程优化；协调多团队资源。',
     '5年以上技术项目管理经验；PMP/Scrum认证优先；有大型项目交付经验；有技术背景。'),

    ('招聘专员', '人力资源部', 8, 18,
     '负责技术岗位招聘全流程；招聘渠道拓展与维护；人才Mapping与储备；招聘数据分析与优化。',
     '2年以上招聘经验；有IT/互联网招聘经验优先；熟悉主流招聘渠道；优秀的沟通和判断能力。'),
]

# Generate additional variants by combining city, salary tweaks
def generate_jobs():
    jobs = []
    seen = set()  # (title, location) dedup

    for tmpl in JOB_TEMPLATES:
        title, dept, smin, smax, jd, req = tmpl
        # Each template appears in 2-3 cities with salary variations
        num_cities = random.randint(2, 3)
        chosen_cities = random.sample(CITIES, num_cities)

        for city in chosen_cities:
            # Vary salary by city tier
            if city in ('北京', '上海', '深圳'):
                mul = random.uniform(1.0, 1.3)
            elif city in ('广州', '杭州'):
                mul = random.uniform(0.9, 1.15)
            else:
                mul = random.uniform(0.75, 1.0)

            s_min = int(smin * mul)
            s_max = int(smax * mul)

            # Round to nice numbers
            s_min = max(5, (s_min // 5) * 5)
            s_max = max(10, (s_max // 5) * 5)

            key = (title, city)
            if key in seen:
                continue
            seen.add(key)

            jobs.append({
                'title': title,
                'dept': dept,
                'location': city,
                'salaryMin': s_min,
                'salaryMax': s_max,
                'headCount': random.randint(1, 5),
                'jd': jd,
                'requirements': req,
                'source': 'seed_generator',
                'sourceUrl': '',
            })

    return jobs


def push_batch(jobs_batch):
    try:
        r = httpx.post(
            API_URL,
            json=jobs_batch,
            headers={
                'Content-Type': 'application/json',
                'X-Api-Key': API_KEY,
            },
            timeout=30,
        )
        if r.status_code == 200:
            data = r.json()
            print(f'  Imported {data.get("data",{}).get("imported",0)} jobs')
            return data.get('data', {}).get('imported', 0)
        else:
            print(f'  Error: {r.status_code} {r.text[:200]}')
            return 0
    except Exception as e:
        print(f'  Exception: {e}')
        return 0


if __name__ == '__main__':
    print('Generating job listings...')
    jobs = generate_jobs()
    print(f'Generated {len(jobs)} unique jobs')

    total_imported = 0
    for i in range(0, len(jobs), BATCH_SIZE):
        batch = jobs[i:i + BATCH_SIZE]
        print(f'Pushing batch {i//BATCH_SIZE + 1} ({len(batch)} jobs)...')
        imported = push_batch(batch)
        total_imported += imported

    print(f'\nDone! Total imported: {total_imported} jobs')
    print(f'Duplicates skipped: {len(jobs) - total_imported}')
