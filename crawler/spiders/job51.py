import re
import json as json_lib
import scrapy
from items import JobItem

# 51job city area codes
CITIES = {
    '北京': '010000', '上海': '020000', '广州': '030200', '深圳': '040000',
    '杭州': '080200', '成都': '090200', '武汉': '180200', '南京': '070200',
    '西安': '200200', '苏州': '060000',
}

# IT keywords to search
KEYWORDS = [
    'Java', 'Python', '前端', 'AI', '机器学习', 'DevOps',
    '数据分析', '产品经理', '测试', 'Go', '大数据',
]

# Department inference from title keywords
DEPT_KEYWORD_MAP = [
    (['AI', '机器学习', 'NLP', '算法', '深度学习', '大模型', '人工智能', 'CV', '自然语言'], 'AI部'),
    (['数据', '大数据', 'ETL', '数据仓库', '数据分析', '数据科学', '数仓'], '数据部'),
    (['产品', '产品经理', 'PO'], '产品部'),
    (['Java', 'Python', '前端', 'Go', 'C++', 'DevOps', '测试', 'PHP', '.NET', 'Node', 'React', 'Vue', 'Angular',
      'iOS', 'Android', 'Flutter', 'K8s', 'Docker', '后端', '全栈', '运维', '安全', '架构', '嵌入式', '游戏'], '技术部'),
    (['运营'], '运营部'),
    (['市场', '销售', '商务'], '市场部'),
    (['财务', '会计', '出纳'], '财务部'),
    (['人力', 'HR', '招聘', '人事', '行政'], '人力资源部'),
]

seen_urls = set()


def infer_dept(title: str) -> str:
    for keywords, dept in DEPT_KEYWORD_MAP:
        for kw in keywords:
            if kw.lower() in title.lower():
                return dept
    return '技术部'


class Job51Spider(scrapy.Spider):
    name = '51job'
    allowed_domains = ['51job.com', 'jobs.51job.com']

    def start_requests(self):
        for city_name, city_code in CITIES.items():
            for keyword in KEYWORDS:
                url = (
                    f'https://search.51job.com/list/{city_code},000000,0000,00,9,99,'
                    f'{keyword},2,1.html?lang=c&postchannel=0000&workyear=99&cotype=99'
                    f'&degreefrom=99&jobterm=99&companysize=99&ord_field=0&dibiaoid=0'
                )
                yield scrapy.Request(url, meta={'city': city_name, 'keyword': keyword, 'page': 1})

    def parse(self, response):
        city = response.meta['city']
        keyword = response.meta['keyword']
        current_page = response.meta['page']

        script_texts = response.css('script::text').getall()
        for text in script_texts:
            if 'window.__SEARCH_RESULT__' not in text:
                continue
            match = re.search(r'window\.__SEARCH_RESULT__\s*=\s*(\{.+?\})\s*</script>', text, re.DOTALL)
            if not match:
                continue
            try:
                data = json_lib.loads(match.group(1))
            except json_lib.JSONDecodeError:
                continue

            engine_search_result = data.get('engine_search_result', [])
            for item in engine_search_result:
                detail_url = item.get('job_href', '')
                if detail_url in seen_urls:
                    continue
                seen_urls.add(detail_url)

                job = JobItem()
                job.title = item.get('job_name', '')
                job.location = item.get('workarea_text', '') or city
                job.salary_min, job.salary_max = self._parse_salary(item.get('providesalary_text', ''))
                job.source_url = detail_url
                job.requirements = item.get('attribute_text', '')
                job.dept = infer_dept(job.title)

                if detail_url:
                    yield scrapy.Request(
                        detail_url,
                        callback=self.parse_detail,
                        meta={'job': job},
                        dont_filter=True,
                    )

            total_page = data.get('total_page', 1)
            if current_page < total_page and current_page < 10:
                next_url = response.url.replace(
                    f',{current_page}.html', f',{current_page + 1}.html'
                )
                yield scrapy.Request(next_url, meta={'city': city, 'keyword': keyword, 'page': current_page + 1})

    def parse_detail(self, response):
        job = response.meta['job']
        job_msg = response.css('div.job_msg, div.bmsg.job_msg.inbox::text, div.tCompany_main div::text').getall()
        full_text = ' '.join(t.strip() for t in job_msg if t.strip() and len(t.strip()) > 10)
        job.jd = full_text[:3000] if full_text else ''
        yield job

    def _parse_salary(self, text: str):
        if not text:
            return None, None
        orig = text
        text = text.replace('万/月', '').replace('万/年', '').replace('千/月', '').replace('K/月', '').replace('元/天', '')
        parts = re.split(r'[-~]', text)
        if len(parts) == 2:
            try:
                lo, hi = float(parts[0].strip()), float(parts[1].strip())
                if '万/月' in orig:
                    lo, hi = lo * 10, hi * 10
                elif '万/年' in orig:
                    lo, hi = lo * 10 / 12, hi * 10 / 12
                return int(lo), int(hi)
            except ValueError:
                pass
        return None, None
