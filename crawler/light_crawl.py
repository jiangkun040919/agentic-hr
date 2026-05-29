"""
轻量招聘爬虫 - 从招聘信息聚合页抓取数据
数据源：CSDN招聘频道、开源中国招聘等（防护较弱的站点）
"""
import json
import re
import sys
from curl_cffi import requests


def crawl_oschina_jobs(keyword: str = "", max_results: int = 5) -> list:
    """爬取开源中国招聘信息"""
    results = []
    try:
        url = "https://www.oschina.net/project/career"
        r = requests.get(url, impersonate="chrome120", timeout=15)
        if r.status_code != 200:
            return results

        # 提取职位卡片
        cards = re.findall(r'<div[^>]*class="[^"]*item[^"]*"[^>]*>.*?</div>\s*</div>\s*</div>', r.text, re.DOTALL)
        for card in cards[:max_results]:
            title_match = re.search(r'<a[^>]*class="[^"]*title[^"]*"[^>]*>(.*?)</a>', card, re.DOTALL)
            if not title_match:
                continue
            title = re.sub(r'<[^>]+>', '', title_match.group(1)).strip()
            if not title:
                continue

            # 提取其他信息
            text = re.sub(r'<[^>]+>', ' ', card)
            text = re.sub(r'\s+', ' ', text).strip()

            salary_min = salary_max = None
            sal_match = re.search(r'(\d+)k\s*[-~]\s*(\d+)k', text, re.IGNORECASE)
            if sal_match:
                salary_min = int(sal_match.group(1))
                salary_max = int(sal_match.group(2))

            location = "北京"
            for city in ["北京", "上海", "广州", "深圳", "杭州", "成都"]:
                if city in text:
                    location = city
                    break

            results.append({
                "title": title,
                "location": location,
                "salaryMin": salary_min,
                "salaryMax": salary_max,
                "jd": text[:500],
                "sourceUrl": url,
                "sourcePlatform": "开源中国",
            })
    except Exception:
        pass
    return results


def crawl_csdn_jobs(keyword: str = "", max_results: int = 5) -> list:
    """爬取CSDN招聘频道"""
    results = []
    try:
        url = "https://job.csdn.net/"
        r = requests.get(url, impersonate="chrome120", timeout=15)
        if r.status_code != 200:
            return results

        # CSDN 招聘可能有 JSON 数据
        json_match = re.search(r'window\.__INITIAL_STATE__\s*=\s*({.*?});', r.text, re.DOTALL)
        if json_match:
            data = json.loads(json_match.group(1))
            # 尝试多种可能的数据路径
            jobs_data = (
                data.get("jobList") or
                data.get("list") or
                data.get("data", {}).get("list") or
                []
            )
            for item in jobs_data[:max_results]:
                if isinstance(item, dict):
                    results.append({
                        "title": item.get("title") or item.get("name", ""),
                        "location": item.get("city") or item.get("location", "北京"),
                        "salaryMin": item.get("salaryMin"),
                        "salaryMax": item.get("salaryMax"),
                        "jd": (item.get("description") or item.get("jd", ""))[:500],
                        "sourceUrl": item.get("url", url),
                        "sourcePlatform": "CSDN招聘",
                    })
    except Exception:
        pass
    return results


def crawl_aggregate(keyword: str = "Java", city: str = "北京", max_results: int = 5) -> list:
    """聚合多个来源"""
    all_results = []

    # 尝试各个来源
    for crawler in [crawl_oschina_jobs, crawl_csdn_jobs]:
        try:
            jobs = crawler(keyword, max_results)
            all_results.extend(jobs)
            if len(all_results) >= max_results:
                break
        except Exception:
            pass

    # 过滤：只保留标题含关键词的
    if keyword:
        all_results = [
            j for j in all_results
            if keyword.lower() in j.get("title", "").lower()
        ]

    return all_results[:max_results]


def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--keyword", default="Java", help="关键词")
    parser.add_argument("--city", default="北京", help="城市")
    parser.add_argument("--max", type=int, default=5, dest="max_results")
    args = parser.parse_args()

    jobs = crawl_aggregate(args.keyword, args.city, args.max_results)
    print(json.dumps(jobs, ensure_ascii=False))


if __name__ == "__main__":
    main()
