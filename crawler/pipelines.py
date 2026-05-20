import json
import logging
from typing import Optional
import httpx

logger = logging.getLogger(__name__)


class PushToAPIPipeline:
    """Push scraped jobs to the .NET backend batch-import API."""

    def __init__(self, api_url: str, auth_token: str, api_key: str):
        self.api_url = api_url
        self.auth_token = auth_token
        self.api_key = api_key
        self.buffer: list = []
        self.batch_size = 10

    @classmethod
    def from_crawler(cls, crawler):
        return cls(
            api_url=crawler.settings.get('API_PUSH_URL', 'http://localhost:5000/api/job/batch-import'),
            auth_token=crawler.settings.get('API_AUTH_TOKEN', ''),
            api_key=crawler.settings.get('API_KEY', ''),
        )

    def process_item(self, item, spider):
        self.buffer.append(item.to_dict())
        if len(self.buffer) >= self.batch_size:
            self._flush()
        return item

    def close_spider(self, spider):
        if self.buffer:
            self._flush()

    def _flush(self):
        try:
            headers = {'Content-Type': 'application/json'}
            if self.auth_token:
                headers['Authorization'] = f'Bearer {self.auth_token}'
            if self.api_key:
                headers['X-Api-Key'] = self.api_key
            resp = httpx.post(self.api_url, json=self.buffer, headers=headers, timeout=30)
            if resp.status_code == 200:
                logger.info(f'Pushed {len(self.buffer)} jobs to API')
            else:
                logger.warning(f'API returned {resp.status_code}: {resp.text[:200]}')
        except Exception as e:
            logger.error(f'Failed to push to API: {e}')
        finally:
            self.buffer.clear()


class JsonExportPipeline:
    """Fallback: export to JSON file."""

    def __init__(self):
        self.items: list = []

    def process_item(self, item, spider):
        self.items.append(item.to_dict())
        return item

    def close_spider(self, spider):
        path = 'jobs_export.json'
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(self.items, f, ensure_ascii=False, indent=2)
        logger.info(f'Exported {len(self.items)} jobs to {path}')
