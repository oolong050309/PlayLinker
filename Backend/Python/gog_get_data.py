#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GOG数据获取脚本
获取GOG用户信息、游戏库、成就等数据
"""

import sys
import json
import argparse
import requests
from typing import Dict, Any, Optional, List

# GOG API配置
CLIENT_ID = "46899977096215655"
CLIENT_SECRET = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9"
TOKEN_URL = "https://auth.gog.com/token"
EMBED_HOST = "https://embed.gog.com"
GAMEPLAY_HOST = "https://gameplay.gog.com"


def print_info(message):
    """打印信息"""
    print(f"INFO: {message}", file=sys.stderr, flush=True)


def print_error(message):
    """打印错误"""
    print(f"ERROR: {message}", file=sys.stderr, flush=True)


def load_tokens(tokens_path: str) -> Optional[Dict[str, Any]]:
    """
    从文件加载令牌
    
    Args:
        tokens_path: 令牌文件路径
        
    Returns:
        令牌数据字典,如果文件不存在或加载失败则返回None
    """
    import os
    
    if not os.path.exists(tokens_path):
        print_error("令牌文件不存在")
        return None
    
    try:
        with open(tokens_path, 'r', encoding='utf-8') as f:
            token_data = json.load(f)
        print_info("成功加载令牌")
        return token_data
    except Exception as e:
        print_error(f"加载令牌失败: {e}")
        return None


def save_tokens(token_data: Dict[str, Any], tokens_path: str):
    """
    保存令牌到文件
    
    Args:
        token_data: 令牌数据字典
        tokens_path: 令牌文件路径
    """
    try:
        with open(tokens_path, 'w', encoding='utf-8') as f:
            json.dump(token_data, f, ensure_ascii=False, indent=2)
        print_info("令牌已更新保存")
    except Exception as e:
        print_error(f"保存令牌失败: {e}")


def refresh_access_token(refresh_token: str) -> Optional[Dict[str, Any]]:
    """
    使用刷新令牌获取新的访问令牌
    
    Args:
        refresh_token: 刷新令牌
        
    Returns:
        包含新令牌信息的字典,失败返回None
    """
    print_info("刷新访问令牌...")
    
    params = {
        "client_id": CLIENT_ID,
        "client_secret": CLIENT_SECRET,
        "grant_type": "refresh_token",
        "refresh_token": refresh_token
    }
    
    try:
        response = requests.get(TOKEN_URL, params=params, timeout=30)
        response.raise_for_status()
        
        token_data = response.json()
        
        if "access_token" not in token_data:
            raise ValueError("响应中缺少access_token")
        
        print_info("成功刷新访问令牌")
        return token_data
        
    except Exception as e:
        print_error(f"刷新令牌失败: {e}")
        return None


def make_request(endpoint: str, access_token: str, host: str = EMBED_HOST, 
                 params: Optional[Dict] = None) -> Optional[Dict[str, Any]]:
    """
    发送HTTP请求到GOG API
    
    Args:
        endpoint: API端点
        access_token: 访问令牌
        host: API主机地址
        params: 查询参数
        
    Returns:
        响应JSON数据,失败返回None
    """
    url = f"{host}{endpoint}"
    headers = {
        "Authorization": f"Bearer {access_token}",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
    }
    
    try:
        response = requests.get(url, headers=headers, params=params, timeout=30)
        
        # 404表示数据不存在(例如游戏没有游玩记录),这是正常情况
        if response.status_code == 404:
            return {"error": "not_found", "data": None}
        
        response.raise_for_status()
        
        # 有些API返回空内容
        if not response.content:
            return {}
        
        return response.json()
        
    except requests.exceptions.RequestException as e:
        print_error(f"请求 {endpoint} 失败: {e}")
        return None


def get_user_data(access_token: str) -> Optional[Dict[str, Any]]:
    """获取用户基本信息"""
    print_info("获取用户数据...")
    return make_request("/userData.json", access_token)


def get_owned_products(access_token: str) -> Optional[Dict[str, Any]]:
    """获取用户拥有的游戏列表"""
    print_info("获取游戏列表...")
    return make_request("/user/data/games", access_token)


def get_game_details(access_token: str, game_id: str) -> Optional[Dict[str, Any]]:
    """获取游戏详细信息"""
    return make_request(f"/account/gameDetails/{game_id}.json", access_token)


def get_achievements(access_token: str, product_id: str, user_id: str) -> Optional[Dict[str, Any]]:
    """获取游戏成就信息"""
    url = f"/clients/{product_id}/users/{user_id}/achievements"
    return make_request(url, access_token, host=GAMEPLAY_HOST)


def get_game_sessions(access_token: str, product_id: str, user_id: str) -> Optional[Dict[str, Any]]:
    """获取游戏会话记录(游玩时长)"""
    url = f"/clients/{product_id}/users/{user_id}/sessions"
    result = make_request(url, access_token, host=GAMEPLAY_HOST)
    
    # 404表示没有游玩记录,这是正常情况
    if result and result.get("error") == "not_found":
        return {"sessions": []}
    
    return result


def calculate_total_play_time(sessions: List[Dict]) -> int:
    """
    计算总游玩时长(分钟)
    
    Args:
        sessions: 游戏会话列表
        
    Returns:
        总游玩时长(分钟)
    """
    total_minutes = 0
    
    for session in sessions:
        # sessions数据结构: 每个session包含开始和结束时间
        if "duration" in session:
            # 如果直接提供了时长(秒)
            total_minutes += session["duration"] // 60
        elif "dateStarted" in session and "dateFinished" in session:
            # 如果提供了开始和结束时间,计算时长
            try:
                from datetime import datetime
                start = datetime.fromisoformat(session["dateStarted"].replace("Z", "+00:00"))
                end = datetime.fromisoformat(session["dateFinished"].replace("Z", "+00:00"))
                duration_seconds = (end - start).total_seconds()
                total_minutes += int(duration_seconds // 60)
            except Exception as e:
                print_error(f"解析会话时间失败: {e}")
    
    return total_minutes


def get_all_data(tokens_path: str) -> Dict[str, Any]:
    """
    获取所有GOG数据
    
    Args:
        tokens_path: 令牌文件路径
        
    Returns:
        包含所有数据的字典
    """
    # 加载令牌
    token_data = load_tokens(tokens_path)
    if not token_data:
        return {
            "success": False,
            "error": "token_not_found",
            "message": "令牌文件不存在或无效"
        }
    
    access_token = token_data.get("access_token")
    refresh_token = token_data.get("refresh_token")
    user_id = token_data.get("user_id")
    
    if not access_token or not refresh_token:
        return {
            "success": False,
            "error": "invalid_token",
            "message": "令牌数据不完整"
        }
    
    # 获取用户数据
    user_data = get_user_data(access_token)
    
    # 如果请求失败(可能令牌过期),尝试刷新令牌
    if not user_data:
        print_info("访问令牌可能已过期,尝试刷新...")
        new_token_data = refresh_access_token(refresh_token)
        
        if not new_token_data:
            return {
                "success": False,
                "error": "token_expired",
                "message": "令牌已过期且无法刷新,请重新认证"
            }
        
        # 更新令牌
        if "refresh_token" not in new_token_data:
            new_token_data["refresh_token"] = refresh_token
        if "user_id" not in new_token_data and user_id:
            new_token_data["user_id"] = user_id
        
        save_tokens(new_token_data, tokens_path)
        
        # 使用新令牌重新请求
        access_token = new_token_data["access_token"]
        user_data = get_user_data(access_token)
        
        if not user_data:
            return {
                "success": False,
                "error": "api_error",
                "message": "无法获取用户数据"
            }
    
    # 获取用户ID(用于后续API调用)
    if not user_id:
        user_id = user_data.get("galaxyUserId") or user_data.get("userId")
    
    # 获取游戏列表
    owned_games = get_owned_products(access_token)
    
    result = {
        "success": True,
        "userId": user_id,
        "userData": user_data,
        "ownedGames": owned_games,
        "games": []
    }
    
    # 获取每个游戏的详细信息、成就和游玩时长
    if owned_games and "owned" in owned_games:
        game_ids = owned_games["owned"]
        print_info(f"找到 {len(game_ids)} 个游戏")
        
        for i, game_id in enumerate(game_ids, 1):
            print_info(f"获取游戏 {i}/{len(game_ids)}: {game_id}")
            
            game_info = {
                "gameId": str(game_id),
                "details": None,
                "achievements": None,
                "playTimeMinutes": 0
            }
            
            # 获取游戏详情
            game_details = get_game_details(access_token, str(game_id))
            if game_details:
                game_info["details"] = game_details
            
            # 获取成就
            achievements = get_achievements(access_token, str(game_id), str(user_id))
            if achievements:
                game_info["achievements"] = achievements
            
            # 获取游玩时长
            sessions = get_game_sessions(access_token, str(game_id), str(user_id))
            if sessions and "sessions" in sessions and sessions["sessions"]:
                play_time = calculate_total_play_time(sessions["sessions"])
                game_info["playTimeMinutes"] = play_time
                game_info["sessions"] = sessions
            
            result["games"].append(game_info)
    
    print_info(f"数据获取完成: {len(result['games'])} 个游戏")
    return result


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='GOG数据获取脚本')
    parser.add_argument('--tokens', required=True, help='令牌文件路径')
    
    args = parser.parse_args()
    
    try:
        # 获取所有数据
        result = get_all_data(args.tokens)
        
        # 输出JSON结果
        print(json.dumps(result, ensure_ascii=False, indent=2))
        
        # 返回状态码
        if result.get("success", False):
            sys.exit(0)
        else:
            sys.exit(1)
        
    except Exception as e:
        print_error(f"数据获取失败: {e}")
        result = {
            "success": False,
            "error": "unexpected_error",
            "message": f"数据获取失败: {str(e)}"
        }
        print(json.dumps(result, ensure_ascii=False))
        sys.exit(1)


if __name__ == "__main__":
    main()


