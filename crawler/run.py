"""Standalone runner for 51job crawler. Run with: python run.py"""
import sys
from scrapy.crawler import CrawlerProcess
from scrapy.utils.project import get_project_settings
from spiders.job51 import Job51Spider


def main():
    settings = get_project_settings()
    settings.set('ITEM_PIPELINES', {
        'pipelines.JsonExportPipeline': 300,
        'pipelines.PushToAPIPipeline': 500,
    })

    process = CrawlerProcess(settings)
    process.crawl(Job51Spider)
    process.start()
    print('Crawl finished. Export saved to jobs_export.json')


if __name__ == '__main__':
    main()
