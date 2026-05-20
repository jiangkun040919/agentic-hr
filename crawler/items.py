from dataclasses import dataclass, field, asdict
from typing import Optional


@dataclass
class JobItem:
    title: str = ''
    dept: str = '技术部'
    location: str = ''
    salary_min: Optional[int] = None
    salary_max: Optional[int] = None
    head_count: Optional[int] = None
    jd: str = ''
    requirements: str = ''
    source: str = '51job'
    source_url: str = ''

    def to_dict(self) -> dict:
        # camelCase keys to match C# backend (SalaryMin/HeadCount etc.)
        result = {
            'title': self.title,
            'dept': self.dept,
            'location': self.location,
            'salaryMin': self.salary_min,
            'salaryMax': self.salary_max,
            'headCount': self.head_count,
            'jd': self.jd,
            'requirements': self.requirements,
            'source': self.source,
            'sourceUrl': self.source_url,
        }
        return {k: v for k, v in result.items() if v is not None}
