#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Epic Games 数据获取脚本
通过 Legendary CLI 和 Epic Web API 获取游戏库、详情、成就等数据
"""

import sys
import json
import subprocess
import os
import argparse
from typing import Dict, Any, Optional, List

try:
    from curl_cffi import requests as cffi_requests
except ImportError:
    print(json.dumps({
        "success": False,
        "error": "curl_cffi 未安装",
        "message": "请安装: pip install curl_cffi"
    }, ensure_ascii=False))
    sys.exit(1)


def print_info(message):
    """打印信息到stderr"""
    print(f"INFO: {message}", file=sys.stderr, flush=True)


def print_error(message):
    """打印错误到stderr"""
    print(f"ERROR: {message}", file=sys.stderr, flush=True)


def run_legendary_command(args):
    """执行 Legendary CLI 命令"""
    try:
        command = ["legendary"] + args
        result = subprocess.run(
            command, capture_output=True, text=True, encoding='utf-8', shell=True
        )
        return result
    except Exception as e:
        print_error(f"Command Error: {e}")
        return None


def get_legendary_credentials():
    """从本地 Legendary 配置文件中读取 Token 和 Account ID"""
    possible_paths = [
        os.path.join(os.environ.get('APPDATA', ''), 'legendary', 'user.json'),
        os.path.join(os.environ.get('USERPROFILE', ''), '.config', 'legendary', 'user.json'),
        os.path.join(os.getcwd(), 'legendary', 'user.json'),
        os.path.join(os.getcwd(), 'user.json')
    ]
    for path in possible_paths:
        if os.path.exists(path):
            try:
                with open(path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    token = data.get('access_token') or data.get('userdata', {}).get('access_token')
                    aid = data.get('account_id')
                    if token:
                        return token, aid
            except:
                pass
    return None, None


def fetch_definitions_and_product_id(namespace):
    """通过 GraphQL 获取 ProductID"""
    url = "https://store.epicgames.com/graphql"
    QUERY_HASH = "9284d2fe200e351d1496feda728db23bb52bfd379b236fc3ceca746c1f1b33f2"

    params = {
        "operationName": "Achievement",
        "variables": json.dumps({"sandboxId": namespace, "locale": "zh-CN"}),
        "extensions": json.dumps({"persistedQuery": {"version": 1, "sha256Hash": QUERY_HASH}})
    }
    headers = {"Referer": f"https://store.epicgames.com/zh-CN/p/{namespace}"}

    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=10)
        if resp.status_code == 200:
            return resp.json()
    except Exception as e:
        print_error(f"GraphQL Error: {e}")
    return None


def fetch_player_global_profile(token, account_id):
    """获取玩家全局基础信息"""
    url = "https://store.epicgames.com/graphql"
    QUERY_HASH = "ff954147a23d38a0e5b050962d442099487da001a0ab4b10ccbec8ac49755b3c"

    params = {
        "operationName": "playerProfile",
        "variables": json.dumps({"epicAccountId": account_id}),
        "extensions": json.dumps({"persistedQuery": {"version": 1, "sha256Hash": QUERY_HASH}})
    }

    headers = {
        "Authorization": f"Bearer {token}",
        "Referer": "https://store.epicgames.com/zh-CN/"
    }

    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=10)
        if resp.status_code == 200:
            return resp.json()
    except Exception as e:
        print_error(f"Profile Info Error: {e}")
    return None


def fetch_store_offer_details(product_id, offer_id):
    """Store Offer API - 获取价格、退款政策、详细标签等信息"""
    if not product_id or not offer_id:
        return None
    url = f"https://egs-platform-service.store.epicgames.com/api/v1/egs/products/{product_id}/offers/{offer_id}"
    params = {"country": "CN", "locale": "zh-CN", "store": "EGS"}
    headers = {"Origin": "https://store.epicgames.com", "Referer": "https://store.epicgames.com/"}

    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=5)
        if resp.status_code == 200:
            data = resp.json()
            if not data.get('title'):
                return None
            return data
    except Exception as e:
        print_error(f"Offer API Error: {e}")
    return None


def fetch_store_product_core(product_id):
    """Store Product Core API - 备用接口"""
    if not product_id:
        return None
    url = f"https://egs-platform-service.store.epicgames.com/api/v1/egs/products/{product_id}"
    params = {"country": "CN", "locale": "zh-CN", "store": "EGS"}
    headers = {"Origin": "https://store.epicgames.com", "Referer": "https://store.epicgames.com/"}

    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=5)
        if resp.status_code == 200:
            return resp.json()
    except Exception as e:
        print_error(f"Product Core Error: {e}")
    return None


def fetch_player_profile_achievements(token, account_id, product_id):
    """获取成就进度"""
    url = "https://store.epicgames.com/graphql"
    QUERY_HASH = "70ff714976f88a85aafa3cb5abb9909d52e12a3ff585d7b49550d2493a528fb0"
    params = {
        "operationName": "playerProfileAchievementsByProductId",
        "variables": json.dumps({"epicAccountId": account_id, "productId": product_id}),
        "extensions": json.dumps({"persistedQuery": {"version": 1, "sha256Hash": QUERY_HASH}})
    }
    headers = {"Authorization": f"Bearer {token}", "Referer": "https://store.epicgames.com/zh-CN/achievements"}
    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=10)
        if resp.status_code == 200:
            return resp.json()
    except Exception as e:
        print_error(f"Achievements Error: {e}")
    return None


def get_games_list():
    """获取游戏库列表"""
    result = run_legendary_command(["list-games", "--json"])
    if not result or result.returncode != 0:
        return None

    try:
        output = result.stdout.strip()
        start, end = output.find('['), output.rfind(']')
        if start == -1 or end == -1:
            return None
        games_data = json.loads(output[start:end + 1])
        return games_data
    except Exception as e:
        print_error(f"Parse games error: {e}")
        return None


def get_game_details(namespace, offer_id=None):
    """获取游戏详情"""
    product_id = None

    # 通过 Namespace 获取 ProductID
    def_resp = fetch_definitions_and_product_id(namespace)
    if def_resp and 'data' in def_resp:
        try:
            product_id = def_resp['data']['Achievement']['productAchievementsRecordBySandbox'].get('productId')
        except:
            pass
    if not product_id:
        product_id = namespace

    # 尝试 Offer API
    if offer_id and offer_id != "None":
        store_data = fetch_store_offer_details(product_id, offer_id)
        if store_data:
            return {
                "product_id": product_id,
                "title": store_data.get('title'),
                "description": store_data.get('shortDescription'),
                "developers": store_data.get('developers', []),
                "publishers": store_data.get('publishers', []),
                "release_date": store_data.get('releaseDate', {}).get('timestamp'),
                "price_display": store_data.get('purchase', [{}])[0].get('priceDisplay') if store_data.get('purchase') else "免费/未知",
                "tags": [t.get('name') for t in store_data.get('tags', {}).get('epicFeatures', [])],
                "image": store_data.get('media', {}).get('card16x9', {}).get('imageSrc')
            }

    # 备用：Product Core API
    if product_id:
        prod_data = fetch_store_product_core(product_id)
        if prod_data:
            media = prod_data.get('media', {})
            img_src = media.get('card16x9', {}).get('imageSrc') or media.get('logo', {}).get('imageSrc')
            return {
                "product_id": product_id,
                "title": prod_data.get('title'),
                "description": prod_data.get('shortDescription'),
                "developers": prod_data.get('developers', []),
                "publishers": prod_data.get('publishers', []),
                "release_date": None,
                "price_display": "查看商店",
                "tags": [],
                "image": img_src
            }

    return None


def get_game_achievements(namespace, token, account_id):
    """获取游戏成就列表"""
    def_response = fetch_definitions_and_product_id(namespace)
    try:
        base_record = def_response['data']['Achievement']['productAchievementsRecordBySandbox']
        if not base_record:
            return None
        product_id = base_record.get('productId') or namespace
    except:
        return None

    # 获取用户进度
    prog_response = fetch_player_profile_achievements(token, account_id, product_id)
    progress_map = {}
    if prog_response and 'data' in prog_response:
        try:
            raw_prog_list = prog_response.get('data', {}).get('PlayerProfile', {}).get('playerProfile', {}) \
                .get('productAchievements', {}).get('data', {}).get('playerAchievements', [])
            for item in raw_prog_list:
                p = item.get('playerAchievement', {})
                if p.get('achievementName'):
                    progress_map[p['achievementName']] = {
                        "unlocked": p.get('unlocked', False),
                        "unlock_date": p.get('unlockDate'),
                        "progress": p.get('progress')
                    }
        except:
            pass

    # 合并数据
    achievements = []
    for item in base_record.get('achievements', []):
        ach = item.get('achievement', {})
        ach_id = ach.get('name')
        p = progress_map.get(ach_id, {})
        achievements.append({
            "id": ach_id,
            "name": ach.get('unlockedDisplayName'),
            "description": ach.get('unlockedDescription'),
            "icon": ach.get('unlockedIconLink'),
            "xp": ach.get('XP', 0),
            "is_completed": p.get("unlocked", False),
            "unlocked_at": p.get("unlock_date"),
            "progress_val": p.get("progress")
        })

    return {
        "total": base_record.get('totalAchievements', 0),
        "unlocked_count": len([p for p in progress_map.values() if p.get("unlocked")]),
        "achievements": achievements
    }


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='Epic Games 数据获取脚本')
    parser.add_argument('--action', required=True, choices=['games', 'profile', 'game-details', 'achievements'],
                        help='操作类型')
    parser.add_argument('--namespace', help='游戏 Namespace (用于详情和成就)')
    parser.add_argument('--offer-id', help='游戏 Offer ID (用于详情)')
    parser.add_argument('--game-id', help='游戏 ID (用于成就)')

    args = parser.parse_args()

    result = {
        "success": False,
        "data": None
    }

    try:
        if args.action == 'games':
            # 获取游戏列表
            games_data = get_games_list()
            if games_data:
                clean_games = []
                for game in games_data:
                    app_name = game.get('app_name')
                    md = game.get('metadata', {})
                    namespace = md.get('namespace') or md.get('mainGameItem', {}).get('namespace') or md.get('catalogItemId') or app_name
                    offer_id = md.get('mainGameItem', {}).get('id') or md.get('id')

                    clean_games.append({
                        "title": game.get('app_title'),
                        "id": app_name,
                        "namespace": namespace,
                        "offer_id": offer_id,
                        "is_installed": False
                    })

                result["success"] = True
                result["data"] = {
                    "count": len(clean_games),
                    "games": clean_games
                }
            else:
                result["error"] = "无法获取游戏列表，请确保已登录 Legendary"

        elif args.action == 'profile':
            # 获取用户信息
            token, account_id = get_legendary_credentials()
            if not token:
                result["error"] = "未登录，请先运行 legendary auth"
            else:
                data = fetch_player_global_profile(token, account_id)
                if data and 'data' in data:
                    try:
                        profile = data['data']['PlayerProfile']['playerProfile']
                        result["success"] = True
                        result["data"] = {
                            "account_id": profile.get('epicAccountId'),
                            "display_name": profile.get('displayName'),
                            "avatar": {
                                "small": profile.get('avatar', {}).get('small'),
                                "medium": profile.get('avatar', {}).get('medium'),
                                "large": profile.get('avatar', {}).get('large')
                            }
                        }
                    except Exception as e:
                        result["error"] = f"解析个人信息失败: {e}"
                else:
                    result["error"] = "无法获取个人信息"

        elif args.action == 'game-details':
            # 获取游戏详情
            if not args.namespace:
                result["error"] = "缺少 --namespace 参数"
            else:
                details = get_game_details(args.namespace, args.offer_id)
                if details:
                    result["success"] = True
                    result["data"] = details
                else:
                    result["error"] = "无法获取游戏详情"

        elif args.action == 'achievements':
            # 获取成就
            if not args.namespace:
                result["error"] = "缺少 --namespace 参数"
            else:
                token, account_id = get_legendary_credentials()
                if not token:
                    result["error"] = "未登录，请先运行 legendary auth"
                else:
                    achievements = get_game_achievements(args.namespace, token, account_id)
                    if achievements:
                        result["success"] = True
                        result["data"] = achievements
                    else:
                        result["error"] = "无法获取成就信息或该游戏不支持成就"

    except Exception as e:
        result["error"] = str(e)
        print_error(f"Unexpected error: {e}")

    # 输出JSON结果
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()

