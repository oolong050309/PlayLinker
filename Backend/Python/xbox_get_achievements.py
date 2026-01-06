"""
Xbox Web API 游戏成就获取脚本
获取指定游戏的详细成就列表和玩家解锁状态
"""
import asyncio
import json
import os
import sys
from datetime import datetime

try:
    from xbox.webapi.api.client import XboxLiveClient
    from xbox.webapi.authentication.manager import AuthenticationManager
    from xbox.webapi.authentication.models import OAuth2TokenResponse
    from xbox.webapi.common.signed_session import SignedSession
    from xbox.webapi.scripts import CLIENT_ID, CLIENT_SECRET
except ImportError:
    print(json.dumps({
        "success": False,
        "error": "xbox-webapi-python 未安装",
        "message": "请安装: pip install xbox-webapi-python"
    }, ensure_ascii=False))
    sys.exit(1)


async def get_game_achievements(tokens_file, xuid, title_id):
    """获取指定游戏的详细成就信息"""
    try:
        async with SignedSession() as session:
            auth_mgr = AuthenticationManager(
                session, CLIENT_ID, CLIENT_SECRET, ""
            )

            # 加载令牌
            if not os.path.exists(tokens_file):
                print(json.dumps({
                    "success": False,
                    "error": "token_not_found",
                    "message": f"令牌文件不存在: {tokens_file}"
                }, ensure_ascii=False), flush=True)
                sys.exit(1)

            with open(tokens_file, "r", encoding="utf-8") as f:
                tokens = f.read()
            auth_mgr.oauth = OAuth2TokenResponse.model_validate_json(tokens)

            # 刷新令牌
            await auth_mgr.refresh_tokens()

            # 保存刷新后的令牌
            with open(tokens_file, mode="w", encoding="utf-8") as f:
                f.write(auth_mgr.oauth.model_dump_json())

            # 创建 Xbox API 客户端
            xbl_client = XboxLiveClient(auth_mgr)

            achievements_data = {
                "success": True,
                "title_id": title_id,
                "xuid": xuid,
                "achievements": []
            }

            # 循环获取所有页面的成就数据
            continuation_token = None
            page_count = 0
            total_achievements = 0
            
            while True:
                page_count += 1
                print(f"INFO: 获取第 {page_count} 页成就数据...", flush=True)
                
                # 获取游戏成就进度（支持分页）
                if continuation_token:
                    # 如果有 continuation_token，需要使用已签名的 session 发送请求
                    from xbox.webapi.api.provider.achievements.models import AchievementResponse
                    url = f"https://achievements.xboxlive.com/users/xuid({xuid})/achievements"
                    params = {"titleId": title_id, "continuationToken": continuation_token}
                    
                    # 使用 xbl_client 的 session（已签名）
                    resp = await xbl_client.achievements.client.session.get(
                        url,
                        params=params,
                        headers={"x-xbl-contract-version": "2"},
                        rate_limits=xbl_client.achievements.rate_limit_read
                    )
                    resp.raise_for_status()
                    achievements = AchievementResponse(**resp.json())
                else:
                    # 第一页，使用标准方法
                    achievements = await xbl_client.achievements.get_achievements_xboxone_gameprogress(
                        xuid, title_id
                    )

                # 记录总数（第一页时）
                if page_count == 1 and achievements.paging_info:
                    total_achievements = achievements.paging_info.total_records
                    print(f"INFO: 总共有 {total_achievements} 个成就", flush=True)

                # 处理当前页的成就
                if achievements.achievements:
                    print(f"INFO: 第 {page_count} 页获取到 {len(achievements.achievements)} 个成就", flush=True)
                    for ach in achievements.achievements:
                        # 获取图标URL
                        icon_unlocked = ""
                        icon_locked = ""
                        if ach.media_assets:
                            for media in ach.media_assets:
                                if media.name == "Icon" or media.name == "UnlockedIcon":
                                    icon_unlocked = media.url
                                elif media.name == "LockedIcon":
                                    icon_locked = media.url
                        
                        # 如果没有找到解锁图标，尝试使用第一个媒体资源
                        if not icon_unlocked and ach.media_assets:
                            icon_unlocked = ach.media_assets[0].url
                        
                        # 如果没有锁定图标，使用解锁图标
                        if not icon_locked:
                            icon_locked = icon_unlocked
                        
                        # 获取Gamerscore（rewards 在 Achievement 对象中，不在 Progression 中）
                        gamerscore = 0
                        if ach.rewards:
                            for reward in ach.rewards:
                                # 检查 reward.type 是否为 "Gamerscore"
                                if reward.type == "Gamerscore":
                                    try:
                                        gamerscore = int(reward.value)
                                    except (ValueError, TypeError):
                                        pass
                                    break
                        
                        # 判断是否已解锁
                        is_unlocked = ach.progress_state == "Achieved"
                        
                        # 获取解锁时间（time_unlocked 在 Progression 对象中，可能为 None）
                        unlock_time = None
                        if ach.progression and hasattr(ach.progression, 'time_unlocked') and ach.progression.time_unlocked:
                            try:
                                unlock_time = ach.progression.time_unlocked.isoformat() if hasattr(ach.progression.time_unlocked, "isoformat") else str(ach.progression.time_unlocked)
                            except (AttributeError, ValueError):
                                pass
                        
                        ach_info = {
                            "id": ach.id,
                            "name": ach.name,
                            "description": ach.description or "",
                            "locked_description": ach.locked_description or "",
                            "progress_state": ach.progress_state,
                            "is_secret": ach.is_secret,
                            "is_unlocked": is_unlocked,
                            "unlock_time": unlock_time,
                            "gamerscore": gamerscore,
                            "icon_unlocked": icon_unlocked,
                            "icon_locked": icon_locked,
                        }
                        achievements_data["achievements"].append(ach_info)
                else:
                    print(f"WARNING: 第 {page_count} 页没有成就数据", flush=True)

                # 检查是否有下一页
                if achievements.paging_info and achievements.paging_info.continuation_token:
                    continuation_token = achievements.paging_info.continuation_token
                    print(f"INFO: 还有更多成就，continuation_token={continuation_token}", flush=True)
                else:
                    # 没有更多页面，退出循环
                    print(f"INFO: 已获取所有成就，共 {len(achievements_data['achievements'])} 个（总计 {total_achievements} 个）", flush=True)
                    break

            print(json.dumps(achievements_data, ensure_ascii=False, indent=2), flush=True)
    except Exception as e:
        import traceback
        error_msg = f"获取游戏成就失败: {str(e)}\n{traceback.format_exc()}"
        print(json.dumps({
            "success": False,
            "error": "exception",
            "message": error_msg,
            "title_id": title_id,
            "xuid": xuid
        }, ensure_ascii=False), flush=True)
        sys.exit(1)


async def main():
    """主函数"""
    import argparse

    parser = argparse.ArgumentParser(description="获取 Xbox 游戏成就")
    parser.add_argument(
        "--tokens",
        "-t",
        required=True,
        help="令牌文件路径"
    )
    parser.add_argument(
        "--xuid",
        "-x",
        required=True,
        help="Xbox用户ID (XUID)"
    )
    parser.add_argument(
        "--title-id",
        "-tid",
        required=True,
        help="游戏标题ID"
    )
    args = parser.parse_args()

    await get_game_achievements(args.tokens, args.xuid, args.title_id)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as e:
        print(json.dumps({
            "success": False,
            "error": "exception",
            "message": str(e)
        }, ensure_ascii=False), flush=True)
        sys.exit(1)

