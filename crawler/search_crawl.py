"""
搜索引擎聚合爬虫 - 搜索招聘信息，提取岗位数据
用法: python search_crawl.py --keyword "Java开发" --city "北京" --max 5
"""
import argparse
import json
import re
import sys
from curl_cffi import requests


def search_jobs_aggregate(keyword: str, city: str = "北京", max_results: int = 5) -> list:
    """通过搜索引擎聚合招聘信息"""
    results = []
    query = f"{keyword} 招聘 {city}"

    try:
        # 尝试用 Bing 搜索
        url = f"https://www.bing.com/search?q={requests.utils.quote(query)}&count=20"
        r = requests.get(url, impersonate="chrome120", timeout=15)

        if r.status_code != 200 or len(r.text) < 500:
            return results

        # 提取搜索结果块
        # Bing 使用 <li class="b_algo"> 包裹每个结果
        blocks = re.findall(
            r'<li class="b_algo".*?</li>', r.text, re.DOTALL
        )

        for block in blocks[:max_results * 2]:
            # 提取标题和URL
            title_match = re.search(r'<h2[^>]*>.*?<a[^>]*href="([^"]+)"[^>]*>(.*?)</a>', block, re.DOTALL)
            if not title_match:
                continue

            url = title_match.group(1)
            title = re.sub(r'<[^>]+>', '', title_match.group(2)).strip()

            # 过滤非招聘结果
            if not any(kw in title for kw in ["招聘", "工程师", "开发", "经理", "设计师", "专员", "实习"]):
                continue

            # 提取摘要
            desc_match = re.search(r'<p[^>]*class="b_lineclamp[^"]*"[^>]*>(.*?)</p>', block, re.DOTALL)
            desc = ""
            if desc_match:
                desc = re.sub(r'<[^>]+>', ' ', desc_match.group(1)).strip()
                desc = re.sub(r'\s+', ' ', desc)

            # 提取可能的薪资和公司信息
            salary_min = salary_max = None
            salary_match = re.search(r'(\d+)[kK千]-(\d+)[kK千]', desc)
            if salary_match:
                salary_min = int(salary_match.group(1))
                salary_max = int(salary_match.group(2))
            else:
                # 尝试匹配 万/月 格式
                salary_match = re.search(r'(\d+\.?\d*)\s*万.*?(\d+\.?\d*)\s*万', desc)
                if salary_match:
                    salary_min = int(float(salary_match.group(1)) * 10)
                    salary_max = int(float(salary_match.group(2)) * 10)

            # 提取城市
            location = city
            city_match = re.search(r'(北京|上海|广州|深圳|杭州|成都|武汉|南京|西安|苏州)', desc)
            if city_match:
                location = city_match.group(1)

            results.append({
                "title": title,
                "location": location,
                "salaryMin": salary_min,
                "salaryMax": salary_max,
                "jd": desc[:1000],
                "sourceUrl": url,
                "sourcePlatform": "聚合搜索",
            })

            if len(results) >= max_results:
                break

    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)

    return results


def main():
    parser = argparse.ArgumentParser(description="搜索引擎聚合招聘爬虫")
    parser.add_argument("--keyword", required=True, help="搜索关键词")
    parser.add_argument("--city", default="北京", help="城市")
    parser.add_argument("--max", type=int, default=5, dest="max_results")
    args = parser.parse_args()

    jobs = search_jobs_aggregate(args.keyword, args.city, args.max_results)
    print(json.dumps(jobs, ensure_ascii=False))


if __name__ == "__main__":
    main()
