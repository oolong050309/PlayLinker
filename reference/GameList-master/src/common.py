__all__ = [
    'eprint',
    'jprint',
    'lines',
    'fetch_url',
    'format_time',
    'format_datetime',
    'headers',
]

import sys
import time
import requests
import json
from datetime import datetime

headers = {
    'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:120.0) Gecko/20100101 Firefox/120.0'}


def eprint(*args, **kwargs):
    print(*args, file=sys.stderr, **kwargs)


def jprint(data):
    # 处理Windows系统的编码问题
    try:
        json_str = json.dumps(data, ensure_ascii=False, indent=4)
        # 尝试直接打印
        print(json_str)
        # 同时考虑到可能需要直接写入文件的情况，提供写入文件的能力
        if len(sys.argv) > 2 and sys.argv[2] == '--output':
            # 从命令行参数获取输出文件名
            output_file = sys.argv[3] if len(sys.argv) > 3 else 'output.json'
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(json_str)
                f.write('\n')
    except UnicodeEncodeError:
        # 如果直接打印失败，尝试替换无法编码的字符
        import re
        json_str = json.dumps(data, ensure_ascii=False, indent=4)
        # 移除或替换无法编码的特殊字符
        json_str = re.sub(r'[\u007f-\u009f\u00ad\u2000-\u200f\u2010-\u201f\u202a-\u202f\u2060-\u206f\u2122]', '', json_str)
        print(json_str)
        # 同样写入文件
        if len(sys.argv) > 2 and sys.argv[2] == '--output':
            output_file = sys.argv[3] if len(sys.argv) > 3 else 'output.json'
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(json_str)
                f.write('\n')


def lines(path):
    try:
        return list(filter(lambda s: s, open(path, 'r', encoding='utf-8').read().split('\n')))
    except Exception:
        return []


def fetch_url(url, headers=headers, try_times=5, sleep_interval=1):
    if try_times == 0:
        return None
    try:
        return requests.get(url, headers=headers)
    except Exception:
        time.sleep(sleep_interval)
        return fetch_url(url, headers=headers, try_times=try_times - 1)


def format_time(timestamp):
    return datetime.fromtimestamp(timestamp).strftime('%Y-%m-%d %H:%M:%S')

def format_datetime(time):
    return time.strftime('%Y-%m-%d %H:%M:%S')
