"""
51job 同步爬虫 - 根据关键词和城市直接爬取
用法: python direct_crawl.py --keyword "Java" --city "北京" --max 5
输出: JSON 格式的岗位列表
"""
import re
import json
import sys
import argparse
import requests
from typing import Optional

CITY_CODES = {
    '北京': '010000', '上海': '020000', '广州': '030200', '深圳': '040000',
    '杭州': '080200', '成都': '090200', '武汉': '180200', '南京': '070200',
    '西安': '200200', '苏州': '060000', '全国': '000000',
}

HEADERS = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36',
    'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
    'Accept-Language': 'zh-CN,zh;q=0.9',
}

session = requests.Session()
session.headers.update(HEADERS)


def parse_salary(text: str) -> tuple:
    """解析薪资文本 -> (min, max) K/月"""
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
            elif '千/月' in orig or 'K/月' in orig:
                pass  # already in K
            return int(lo), int(hi)
        except ValueError:
            pass
    return None, None


def fetch_job_detail(detail_url: str) -> str:
    """获取职位详情页的 JD 文本"""
    try:
        resp = session.get(detail_url, timeout=10)
        resp.encoding = 'gbk'
        # 提取 bmsg.job_msg 中的文本
        matches = re.findall(r'<div class="bmsg job_msg inbox">(.*?)</div>', resp.text, re.DOTALL)
        if matches:
            text = re.sub(r'<[^>]+>', '\n', matches[0])
            text = re.sub(r'\n{3,}', '\n\n', text).strip()
            return text[:5000] if text else ''
        # fallback: 提取所有职位描述区域
        job_area = re.search(r'class="job_msg"[^>]*>(.*?)(?=<div class="mt10|$)', resp.text, re.DOTALL)
        if job_area:
            text = re.sub(r'<[^>]+>', '\n', job_area.group(1))
            text = re.sub(r'\n{3,}', '\n\n', text).strip()
            return text[:5000] if text else ''
    except Exception:
        pass
    return ''


def search_jobs(keyword: str, city: str = '全国', max_results: int = 10) -> list:
    """搜索51job岗位"""
    city_code = CITY_CODES.get(city, '000000')
    results = []

    url = (
        f'https://search.51job.com/list/{city_code},000000,0000,00,9,99,'
        f'{keyword},2,1.html?lang=c&postchannel=0000&workyear=99&cotype=99'
        f'&degreefrom=99&jobterm=99&companysize=99&ord_field=0&dibiaoid=0'
    )

    try:
        resp = session.get(url, timeout=15)
        resp.encoding = 'gbk'
        html = resp.text

        # 提取 __SEARCH_RESULT__ JSON
        match = re.search(r'window\.__SEARCH_RESULT__\s*=\s*(\{.+?\})\s*</script>', html, re.DOTALL)
        if not match:
            return results

        data = json.loads(match.group(1))
        engine_result = data.get('engine_search_result', [])

        for item in engine_result[:max_results]:
            title = item.get('job_name', '')
            if not title:
                continue

            location = item.get('workarea_text', '') or city
            salary_min, salary_max = parse_salary(item.get('providesalary_text', ''))
            detail_url = item.get('job_href', '')
            company = item.get('company_name', '')
            attr = item.get('attribute_text', '')  # 如 "3-4年 | 本科"

            # 获取详情
            jd = ''
            if detail_url:
                jd = fetch_job_detail(detail_url)

            results.append({
                'title': title.strip(),
                'location': location.strip(),
                'salaryMin': salary_min,
                'salaryMax': salary_max,
                'company': company.strip(),
                'jd': jd,
                'requirements': attr.strip(),
                'sourceUrl': detail_url,
                'sourcePlatform': '51job',
            })

            if len(results) >= max_results:
                break

    except Exception as e:
        print(json.dumps({'error': str(e)}), file=sys.stderr)
        return results

    return results


def main():
    parser = argparse.ArgumentParser(description='51job 岗位爬虫')
    parser.add_argument('--keyword', required=True, help='搜索关键词')
    parser.add_argument('--city', default='全国', help='城市名称')
    parser.add_argument('--max', type=int, default=5, dest='max_results', help='最大结果数')
    args = parser.parse_args()

    jobs = search_jobs(args.keyword, args.city, args.max_results)
    print(json.dumps(jobs, ensure_ascii=False))


if __name__ == '__main__':
    main()
