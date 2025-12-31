#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Epic Games 数据获取脚本 - FastAPI 版本
通过 Legendary CLI 和 Epic 官方 Web API，提供游戏管理、详情查询、成就查询和个人档案功能
"""

import json
import subprocess
import os
import sys
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import requests
from curl_cffi import requests as cffi_requests

# 设置UTF-8编码，避免Windows下的编码问题
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

app = FastAPI(title="Epic Games Manager (Clean Version)")

# 配置跨域请求，允许前端从任意端口访问
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


class AuthRequest(BaseModel):
    code: str


# ================= 基础工具 =================

def run_legendary_command(args):
    """
    执行本地 Legendary CLI 命令
    """
    try:
        command = ["legendary"] + args
        # capture_output=True 用于捕获命令行输出，encoding='utf-8' 防止中文乱码
        result = subprocess.run(
            command, capture_output=True, text=True, encoding='utf-8', shell=True
        )
        return result
    except Exception as e:
        print(f"Command Error: {e}", file=sys.stderr)
        return None


def get_legendary_credentials():
    """
    从本地 Legendary 配置文件中读取 Token 和 Account ID
    用于后续需要身份验证的 API 请求
    """
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


# ================= 核心 API 请求函数 =================
# 注意：以下函数主要使用 curl_cffi 库来模拟 Chrome 浏览器指纹，
# 以绕过 Epic Games 的反爬虫（TLS 指纹检测）机制。

def fetch_definitions_and_product_id(namespace):
    """
    通过 GraphQL 获取 ProductID
    作用: 将 Legendary 提供的 Namespace 转换为商店 API 需要的 ProductID
    """
    url = "https://store.epicgames.com/graphql"
    # Epic 的 GraphQL 查询哈希值，可能会随时间变化，目前有效
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
    except:
        pass
    return None


def fetch_player_global_profile(token, account_id):
    """
    获取玩家全局基础信息（头像、昵称 ID）
    """
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
        print(f"Profile Info Error: {e}", file=sys.stderr)
    return None


def fetch_store_offer_details(product_id, offer_id):
    """
    Store Offer API
    获取价格、退款政策、详细标签等信息
    """
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
    except:
        pass
    return None


def fetch_store_product_core(product_id):
    """
    Store Product Core API
    备用接口：当 Offer ID 失效时，仅通过 Product ID 获取基础信息（无价格）
    """
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
        print(f"Product Core Error: {e}", file=sys.stderr)
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
    except:
        pass
    return None


def fetch_profile_summary(token, account_id):
    """获取个人档案概览（最近玩过的游戏及成就统计）"""
    url = "https://store.epicgames.com/graphql"
    QUERY_HASH = "47d0391fa5ec42d829e4a03f399cb586a29cf3cebd940cc4747aed0192c61114"
    variables = {"epicAccountId": account_id, "accountId": account_id, "locale": "zh-CN", "page": 1}
    params = {
        "operationName": "playerProfilePrivate",
        "variables": json.dumps(variables),
        "extensions": json.dumps({"persistedQuery": {"version": 1, "sha256Hash": QUERY_HASH}})
    }
    headers = {"Authorization": f"Bearer {token}", "Referer": "https://store.epicgames.com/zh-CN/achievements"}
    try:
        resp = cffi_requests.get(url, params=params, headers=headers, impersonate="chrome110", timeout=10)
        if resp.status_code == 200:
            return resp.json()
    except:
        pass
    return None


# ================= 路由逻辑 =================

@app.get("/")
def home():
    """根路径，用于检查服务是否运行"""
    return {"message": "Epic Games Manager (Clean Version)"}


@app.post("/api/auth/login")
async def login(request: AuthRequest):
    """
    登录接口
    接收 Epic 网页授权的 Authorization Code，调用 Legendary 完成登录
    """
    if not request.code or not request.code.strip():
        raise HTTPException(status_code=400, detail="授权码不能为空")
    
    result = run_legendary_command(["auth", "--code", request.code])
    if result and result.returncode == 0:
        return {"status": "success", "success": True}
    
    # 获取详细的错误信息
    error_msg = "Login failed"
    if result:
        if result.stderr:
            error_msg = result.stderr.strip()
        elif result.stdout:
            error_msg = result.stdout.strip()
        else:
            error_msg = f"Legendary命令执行失败，退出码: {result.returncode}"
    else:
        error_msg = "无法执行legendary命令，请确保已安装legendary-gl并在PATH中"
    
    print(f"Legendary认证失败: {error_msg}", file=sys.stderr)
    raise HTTPException(status_code=400, detail=error_msg)


@app.get("/api/profile/info")
async def get_profile_info():
    """获取当前登录用户的个人信息（头像、昵称）"""
    token, account_id = get_legendary_credentials()
    if not token:
        raise HTTPException(status_code=401, detail="未登录")

    data = fetch_player_global_profile(token, account_id)

    if data and 'data' in data:
        try:
            profile = data['data']['PlayerProfile']['playerProfile']
            return {
                "success": True,
                "data": {
                    "account_id": profile.get('epicAccountId'),
                    "display_name": profile.get('displayName'),
                    "avatar": {
                        "small": profile.get('avatar', {}).get('small'),
                        "medium": profile.get('avatar', {}).get('medium'),
                        "large": profile.get('avatar', {}).get('large')
                    }
                }
            }
        except Exception as e:
            print(f"Parsing Profile Error: {e}", file=sys.stderr)
            pass

    return {"success": False, "message": "无法获取个人信息"}


@app.get("/api/games")
async def get_games():
    """
    获取游戏库列表 (快速版)
    直接解析 Legendary 的本地 JSON 缓存，不请求网络，确保稳定性。
    """
    # 调用 legendary list-games --json 获取所有游戏数据
    result = run_legendary_command(["list-games", "--json"])
    if not result or result.returncode != 0:
        raise HTTPException(status_code=401, detail="请先登录")

    try:
        # 解析命令行输出的 JSON
        output = result.stdout.strip()
        start, end = output.find('['), output.rfind(']')
        if start == -1 or end == -1:
            return {"success": False, "data": {"count": 0, "games": []}}
        
        games_data = json.loads(output[start:end + 1])

        clean_games = []
        for game in games_data:
            app_name = game.get('app_name')
            md = game.get('metadata', {})
            # 尝试获取 namespace (不同游戏存储位置可能不同)
            namespace = md.get('namespace') or md.get('mainGameItem', {}).get('namespace') or md.get(
                'catalogItemId') or app_name
            # 尝试获取 offer_id (用于后续查询价格)
            offer_id = md.get('mainGameItem', {}).get('id') or md.get('id')

            clean_games.append({
                "title": game.get('app_title'),
                "id": app_name,
                "namespace": namespace,
                "offer_id": offer_id,
                "is_installed": False
                # 已移除 playtime 相关字段
            })

        return {"success": True, "data": {"count": len(clean_games), "games": clean_games}}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/game/details")
async def get_game_details(namespace: str, offer_id: str = None):
    """
    获取游戏详情
    根据 namespace 和 offer_id 查询商店 API，获取图片、简介、价格等
    """
    final_data = {}
    source = "unknown"
    product_id = None

    # 1. 必须先通过 Namespace 获取 ProductID (Epic 内部 ID 映射)
    def_resp = fetch_definitions_and_product_id(namespace)
    if def_resp and 'data' in def_resp:
        try:
            product_id = def_resp['data']['Achievement']['productAchievementsRecordBySandbox'].get('productId')
        except:
            pass
    if not product_id:
        product_id = namespace

    # 2. 方案 A：尝试 Offer API (信息最全)
    if offer_id and offer_id != "None":
        store_data = fetch_store_offer_details(product_id, offer_id)
        if store_data:
            source = "offer_api (Full)"
            purchases = store_data.get('purchase', [])
            price = purchases[0].get('priceDisplay') if purchases else "免费/未知"
            final_data = {
                "title": store_data.get('title'),
                "description": store_data.get('shortDescription'),
                "developer": ", ".join(store_data.get('developers', [])),
                "publisher": ", ".join(store_data.get('publishers', [])),
                "release_date": store_data.get('releaseDate', {}).get('timestamp'),
                "price_display": price,
                "tags": [t.get('name') for t in store_data.get('tags', {}).get('epicFeatures', [])],
                "refund_type": store_data.get('refundType'),
                "image": store_data.get('media', {}).get('card16x9', {}).get('imageSrc')
            }

    # 3. 方案 B：Product Core API (兜底方案，无价格)
    if not final_data and product_id:
        prod_data = fetch_store_product_core(product_id)
        if prod_data:
            source = "product_core (Details Only)"
            media = prod_data.get('media', {})
            img_src = media.get('card16x9', {}).get('imageSrc') or media.get('logo', {}).get('imageSrc')

            final_data = {
                "title": prod_data.get('title'),
                "description": prod_data.get('shortDescription'),
                "developer": ", ".join(prod_data.get('developers', [])),
                "publisher": ", ".join(prod_data.get('publishers', [])),
                "release_date": None,
                "price_display": "查看商店",
                "tags": [],
                "refund_type": "未知",
                "image": img_src
            }

    if not final_data:
        return {"success": False, "message": "无法获取详情 (Product ID 或 Offer ID 无效)"}

    final_data["success"] = True
    final_data["source"] = source
    # 返回格式：{"success": true, "data": {...}}
    return {"success": True, "data": final_data}


@app.get("/api/achievements/{namespace}")
async def get_game_achievements(namespace: str):
    """
    获取游戏成就列表
    包含：成就图标、描述、经验值、用户是否解锁、解锁时间
    """
    try:
        token, account_id = get_legendary_credentials()
        if not token:
            return {"success": False, "supported": False, "achievements": [], "message": "未登录"}

        # 获取成就定义
        def_response = fetch_definitions_and_product_id(namespace)
        if not def_response or 'data' not in def_response:
            return {"success": False, "supported": False, "achievements": [], "message": "无法获取成就定义"}
        
        try:
            base_record = def_response['data']['Achievement']['productAchievementsRecordBySandbox']
            if not base_record:
                return {"success": False, "supported": False, "achievements": [], "message": "该游戏不支持成就"}
            product_id = base_record.get('productId') or namespace
        except Exception as e:
            print(f"解析成就定义失败: {e}", file=sys.stderr)
            return {"success": False, "supported": False, "achievements": [], "message": f"解析成就定义失败: {str(e)}"}

        # 获取用户进度
        progress_map = {}
        try:
            prog_response = fetch_player_profile_achievements(token, account_id, product_id)
            if prog_response and 'data' in prog_response:
                try:
                    raw_prog_list = prog_response.get('data', {}).get('PlayerProfile', {}).get('playerProfile', {}) \
                        .get('productAchievements', {}).get('data', {}).get('playerAchievements', [])
                    for item in raw_prog_list:
                        p = item.get('playerAchievement', {})
                        if p.get('achievementName'):
                            progress_map[p['achievementName']] = {"unlocked": p.get('unlocked', False),
                                                                  "unlock_date": p.get('unlockDate'),
                                                                  "progress": p.get('progress')}
                except Exception as e:
                    print(f"解析用户进度失败: {e}", file=sys.stderr)
        except Exception as e:
            print(f"获取用户进度失败: {e}", file=sys.stderr)
            # 继续处理，即使获取用户进度失败

        # 合并数据
        final_list = []
        try:
            achievements = base_record.get('achievements', [])
            for item in achievements:
                try:
                    ach = item.get('achievement', {})
                    ach_id = ach.get('name')
                    if not ach_id:
                        continue
                    p = progress_map.get(ach_id, {})
                    final_list.append({
                        "id": ach_id,
                        "name": ach.get('unlockedDisplayName'),
                        "description": ach.get('unlockedDescription'),
                        "icon": ach.get('unlockedIconLink'),
                        "xp": ach.get('XP', 0),
                        "is_completed": p.get("unlocked", False),
                        "unlocked_at": p.get("unlock_date"),
                        "progress_val": p.get("progress")
                    })
                except Exception as e:
                    print(f"处理单个成就失败: {e}", file=sys.stderr)
                    continue
        except Exception as e:
            print(f"合并成就数据失败: {e}", file=sys.stderr)
            return {"success": False, "supported": False, "achievements": [], "message": f"合并成就数据失败: {str(e)}"}

        # 返回格式：{"success": true, "data": {"supported": true, "total": ..., "unlocked_count": ..., "achievements": [...]}}
        return {
            "success": True,
            "data": {
                "supported": True,
                "total": base_record.get('totalAchievements', 0),
                "unlocked_count": len(progress_map),
                "achievements": final_list
            }
        }
    except Exception as e:
        print(f"获取游戏成就时发生未处理的错误: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"获取游戏成就失败: {str(e)}")


@app.get("/api/profile/summary")
async def get_profile_summary():
    """获取用户成就概览"""
    token, account_id = get_legendary_credentials()
    if not token:
        raise HTTPException(status_code=401, detail="未登录")

    data = fetch_profile_summary(token, account_id)
    if not data or 'data' not in data:
        return {"success": False, "data": {"games": []}}

    try:
        ach_summaries = data.get('data', {}).get('PlayerProfile', {}).get('playerProfile', {}).get(
            'achievementsSummaries', {}).get('data', [])
        clean_summary = []
        for item in ach_summaries:
            product = item.get('product', {})
            images = item.get('baseOfferForSandbox', {}).get('keyImages', [])
            img_url = next((img['url'] for img in images if img['type'] == 'OfferImageWide'),
                           images[0]['url'] if images else "")
            clean_summary.append({
                "game_name": product.get('name'),
                "namespace": item.get('sandboxId'),
                "total_achievements": item.get('productAchievements', {}).get('totalAchievements', 0),
                "unlocked_count": item.get('totalUnlocked', 0),
                "image": img_url
            })
        return {"success": True, "data": {"games": clean_summary}}
    except:
        return {"success": False, "data": {"games": []}}


if __name__ == "__main__":
    import uvicorn
    print("确保你的默认浏览器已经登录了 Epic Games 官网。复制以下链接并在浏览器中打开 https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code ", file=sys.stderr)
    print("启动后，浏览器访问 http://127.0.0.1:8000/docs 可查看自动生成的 API 文档。", file=sys.stderr)
    # 允许局域网访问，方便测试
    uvicorn.run(app, host="0.0.0.0", port=8000)
