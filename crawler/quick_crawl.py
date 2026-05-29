"""轻量 51job 爬虫 — httpx + 正则，不依赖 Scrapy"""
import re, json, time, random, sys
import httpx

API_URL = "http://localhost:5000/api/job/batch-import"
API_KEY = "a3f8b2c1-d4e5-4f6g-7h8i-9j0k1l2m3n4o"

CITIES = {"北京":"010000","上海":"020000","广州":"030200","深圳":"040000","杭州":"080200","成都":"090200","武汉":"180200","南京":"070200"}
KEYWORDS = ["Java","Python","AI","机器学习","前端","数据分析","产品经理","Go","DevOps","大数据","测试","大模型"]

HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36"}

DEPT_MAP = [
    (["AI","机器学习","NLP","算法","深度学习","大模型","人工智能"], "AI部"),
    (["数据","大数据","ETL","数据仓库","数据分析"], "数据部"),
    (["产品","产品经理","PO"], "产品部"),
    (["Java","Python","前端","Go","C++","DevOps","测试","后端","全栈","运维","安全","架构","嵌入式"], "技术部"),
    (["运营"], "运营部"),
    (["市场","销售","商务"], "市场部"),
]

def infer_dept(title):
    for kw_list, dept in DEPT_MAP:
        for kw in kw_list:
            if kw.lower() in title.lower():
                return dept
    return "技术部"

def parse_salary(text):
    if not text: return None, None
    text = text.replace("万/月","").replace("万/年","").replace("千/月","").replace("K/月","")
    parts = re.split(r'[-~]', text)
    if len(parts) == 2:
        try: return int(float(parts[0].strip())), int(float(parts[1].strip()))
        except: pass
    return None, None

client = httpx.Client(timeout=15, headers=HEADERS, follow_redirects=True)
all_jobs = []

for city_name, city_code in CITIES.items():
    for kw in KEYWORDS:
        url = f"https://search.51job.com/list/{city_code},000000,0000,00,9,99,{kw},2,1.html"
        try:
            resp = client.get(url)
            match = re.search(r'window\.__SEARCH_RESULT__\s*=\s*(\{.+?\})\s*</script>', resp.text, re.DOTALL)
            if not match: continue
            data = json.loads(match.group(1))
            items = data.get("engine_search_result", [])
        except Exception as e:
            print(f"  [skip] {city_name}/{kw}: {e}")
            continue

        for item in items:
            title = item.get("job_name", "")
            if not title: continue
            lo, hi = parse_salary(item.get("providesalary_text", ""))
            job = {
                "title": title,
                "dept": infer_dept(title),
                "location": item.get("workarea_text", "") or city_name,
                "jd": item.get("attribute_text", "")[:2000],
                "requirements": "",
                "salaryMin": lo,
                "salaryMax": hi,
                "headCount": 1,
                "status": 1,
                "sourceUrl": item.get("job_href", ""),
            }
            all_jobs.append(job)

        print(f"  {city_name}/{kw}: {len(items)} jobs")
        time.sleep(random.uniform(1.5, 2.5))

client.close()
print(f"\n总采集: {len(all_jobs)} 条岗位")

# 推送到后端
if all_jobs:
    try:
        r = httpx.post(API_URL, json=all_jobs, headers={"X-Api-Key": API_KEY, "Content-Type": "application/json"}, timeout=60)
        print(f"推送到后端: {r.status_code} - {r.text[:200]}")
    except Exception as e:
        print(f"推送失败: {e}")
        json.dump(all_jobs, open("jobs_export.json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print(f"已保存到 jobs_export.json")
