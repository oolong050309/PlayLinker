"""
Xbox Web API 数据获取脚本（服务器部署版本）
从Xbox Live获取用户数据并输出为JSON
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


class XboxDataCollector:
    """Xbox 数据收集器类"""

    def __init__(self, tokens_file):
        self.tokens_file = tokens_file
        self.client_id = CLIENT_ID
        self.client_secret = CLIENT_SECRET
        self.auth_mgr = None
        self.xbl_client = None
        self.session = None

    async def authenticate(self, session):
        """进行身份认证"""
        self.session = session
        self.auth_mgr = AuthenticationManager(
            self.session, self.client_id, self.client_secret, ""
        )

        # 加载令牌
        try:
            if os.path.exists(self.tokens_file):
                with open(self.tokens_file, "r", encoding="utf-8") as f:
                    tokens = f.read()
                self.auth_mgr.oauth = OAuth2TokenResponse.model_validate_json(tokens)
            else:
                return {
                    "success": False,
                    "error": "token_not_found",
                    "message": f"令牌文件不存在: {self.tokens_file}"
                }
        except Exception as e:
            return {
                "success": False,
                "error": "token_load_failed",
                "message": f"加载令牌失败: {str(e)}"
            }

        # 刷新令牌
        try:
            await self.auth_mgr.refresh_tokens()
        except Exception as e:
            return {
                "success": False,
                "error": "token_refresh_failed",
                "message": f"令牌刷新失败: {str(e)}"
            }

        # 保存刷新后的令牌
        with open(self.tokens_file, mode="w", encoding="utf-8") as f:
            f.write(self.auth_mgr.oauth.json())

        # 创建 Xbox API 客户端
        self.xbl_client = XboxLiveClient(self.auth_mgr)
        return {"success": True, "xuid": self.xbl_client.xuid}

    async def get_own_profile(self):
        """获取用户资料"""
        try:
            profile = await self.xbl_client.profile.get_profile_by_xuid(
                self.xbl_client.xuid
            )

            profile_data = {
                "xuid": self.xbl_client.xuid,
                "gamertag": None,
                "modern_gamertag": None,
                "gamer_score": None,
                "display_pic": None,
                "bio": None,
                "location": None,
                "tenure_level": None,
                "account_tier": None,
            }

            if profile.profile_users and len(profile.profile_users) > 0:
                user = profile.profile_users[0]
                for setting in user.settings:
                    if setting.id == "Gamertag":
                        profile_data["gamertag"] = setting.value
                    elif setting.id == "ModernGamertag":
                        profile_data["modern_gamertag"] = setting.value
                    elif setting.id == "Gamerscore":
                        profile_data["gamer_score"] = setting.value
                    elif setting.id == "GameDisplayPicRaw":
                        profile_data["display_pic"] = setting.value
                    elif setting.id == "Bio":
                        profile_data["bio"] = setting.value
                    elif setting.id == "Location":
                        profile_data["location"] = setting.value
                    elif setting.id == "TenureLevel":
                        profile_data["tenure_level"] = setting.value
                    elif setting.id == "AccountTier":
                        profile_data["account_tier"] = setting.value

            return profile_data
        except Exception as e:
            return {"error": str(e)}

    async def get_own_presence(self):
        """获取在线状态"""
        try:
            from xbox.webapi.api.provider.presence.models import PresenceLevel

            presence = await self.xbl_client.presence.get_presence_own(
                presence_level=PresenceLevel.ALL
            )

            presence_data = {
                "xuid": presence.xuid,
                "state": presence.state,
                "last_seen": None,
                "devices": [],
            }

            if presence.last_seen:
                presence_data["last_seen"] = {
                    "device_type": presence.last_seen.device_type,
                    "title_id": presence.last_seen.title_id,
                    "title_name": presence.last_seen.title_name,
                    "timestamp": presence.last_seen.timestamp,
                }

            if presence.devices:
                for device in presence.devices:
                    device_info = {
                        "type": device.type,
                        "titles": [],
                    }
                    if device.titles:
                        for title in device.titles:
                            title_info = {
                                "id": title.id,
                                "name": title.name,
                                "state": title.state,
                                "placement": title.placement,
                                "last_modified": title.lastModified,
                                "activity": [],
                            }
                            if title.activity:
                                for activity in title.activity:
                                    title_info["activity"].append(
                                        {
                                            "rich_presence": activity.richPresence,
                                            "media": activity.media,
                                        }
                                    )
                            device_info["titles"].append(title_info)
                    presence_data["devices"].append(device_info)

            return presence_data
        except Exception as e:
            return {"error": str(e)}

    async def get_title_history(self, xuid=None, max_items=50):
        """获取游戏活动历史"""
        try:
            from xbox.webapi.api.provider.titlehub.models import TitleFields

            target_xuid = xuid or self.xbl_client.xuid

            fields = [
                TitleFields.ACHIEVEMENT,
                TitleFields.IMAGE,
                TitleFields.SERVICE_CONFIG_ID,
                TitleFields.DETAIL,
                TitleFields.STATS,
                TitleFields.GAME_PASS,
            ]

            title_history = await self.xbl_client.titlehub.get_title_history(
                target_xuid, max_items=max_items, fields=fields
            )

            titles_data = {
                "xuid": target_xuid,
                "titles": [],
            }

            if title_history.titles:
                for title in title_history.titles:
                    title_info = {
                        "title_id": title.title_id,
                        "name": title.name,
                        "type": title.type,
                        "devices": title.devices,
                        "display_image": title.display_image,
                        "service_config_id": title.service_config_id,
                        "modern_title_id": title.modern_title_id,
                        "pfn": title.pfn,
                        "is_bundle": title.is_bundle,
                        "achievement": None,
                        "title_history": None,
                        "detail": None,
                        "game_pass": None,
                        "stats": None,
                        "images": None,
                        "game_time_minutes": None,
                    }

                    if title.title_history:
                        title_info["title_history"] = {
                            "last_time_played": (
                                title.title_history.last_time_played.isoformat()
                                if hasattr(title.title_history.last_time_played, "isoformat")
                                else str(title.title_history.last_time_played)
                            ),
                            "visible": title.title_history.visible,
                            "can_hide": title.title_history.can_hide,
                        }

                    if title.achievement:
                        title_info["achievement"] = {
                            "current_achievements": title.achievement.current_achievements,
                            "total_achievements": title.achievement.total_achievements,
                            "current_gamerscore": title.achievement.current_gamerscore,
                            "total_gamerscore": title.achievement.total_gamerscore,
                            "progress_percentage": title.achievement.progress_percentage,
                            "source_version": title.achievement.source_version,
                        }

                    if title.detail:
                        title_info["detail"] = {
                            "description": title.detail.description,
                            "short_description": title.detail.short_description,
                            "developer_name": title.detail.developer_name,
                            "publisher_name": title.detail.publisher_name,
                            "release_date": (
                                title.detail.release_date.isoformat()
                                if title.detail.release_date
                                and hasattr(title.detail.release_date, "isoformat")
                                else str(title.detail.release_date) if title.detail.release_date else None
                            ),
                            "min_age": title.detail.min_age,
                            "genres": title.detail.genres,
                            "xbox_live_gold_required": title.detail.xbox_live_gold_required,
                            "capabilities": title.detail.capabilities,
                        }

                    if title.game_pass:
                        title_info["game_pass"] = {
                            "is_game_pass": title.game_pass.is_game_pass,
                        }

                    if title.images:
                        title_info["images"] = [
                            {"url": img.url, "type": img.type} for img in title.images
                        ]

                    # 获取游戏时间
                    if title.service_config_id:
                        try:
                            from xbox.webapi.api.provider.userstats.models import GeneralStatsField

                            stats = await self.xbl_client.userstats.get_stats(
                                target_xuid,
                                title.service_config_id,
                                stats_fields=[GeneralStatsField.MINUTES_PLAYED],
                            )
                            if stats.statlistscollection:
                                for statlist in stats.statlistscollection:
                                    for stat in statlist.stats:
                                        if (
                                            stat.scid == title.service_config_id
                                            and stat.name == "MinutesPlayed"
                                        ):
                                            try:
                                                title_info["game_time_minutes"] = int(stat.value)
                                            except (ValueError, TypeError):
                                                title_info["game_time_minutes"] = stat.value
                                            break
                                    if title_info.get("game_time_minutes") is not None:
                                        break
                        except Exception:
                            pass

                    titles_data["titles"].append(title_info)

            return titles_data
        except Exception as e:
            return {"error": str(e)}

    async def collect_all_data(self):
        """收集所有数据"""
        all_data = {
            "collection_time": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "xuid": self.xbl_client.xuid,
            "profile": await self.get_own_profile(),
            "presence": await self.get_own_presence(),
            "title_history": await self.get_title_history(),
        }
        return all_data


async def main():
    """主函数"""
    import argparse

    parser = argparse.ArgumentParser(description="获取 Xbox 用户数据")
    parser.add_argument(
        "--tokens",
        "-t",
        required=True,
        help="令牌文件路径"
    )
    parser.add_argument(
        "--output",
        "-o",
        help="输出文件路径（可选，不指定则输出到stdout）"
    )
    args = parser.parse_args()

    async with SignedSession() as session:
        collector = XboxDataCollector(tokens_file=args.tokens)

        # 认证
        auth_result = await collector.authenticate(session)
        if not auth_result.get("success"):
            print(json.dumps(auth_result), flush=True)
            sys.exit(1)

        # 收集数据
        data = await collector.collect_all_data()
        data["success"] = True

        # 输出数据
        output_json = json.dumps(data, ensure_ascii=False, indent=2)
        
        if args.output:
            with open(args.output, "w", encoding="utf-8") as f:
                f.write(output_json)
        else:
            print(output_json, flush=True)


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

